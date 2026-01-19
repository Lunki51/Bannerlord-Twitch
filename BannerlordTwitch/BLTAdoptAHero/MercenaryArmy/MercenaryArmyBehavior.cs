using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using BannerlordTwitch.Util;
using Helpers;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Manages mercenary armies hired by viewers to capture specific settlements.
    /// Provides comprehensive lifecycle management with proper validation and cleanup.
    /// </summary>
    public class MercenaryArmyBehavior : CampaignBehaviorBase
    {
        public static MercenaryArmyBehavior Current { get; private set; }

        // Main data storage - synchronized with save/load
        private List<MercenaryArmyData> _mercenaryArmies = new();

        public MercenaryArmyBehavior()
        {
            Current = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
            CampaignEvents.OnClanDestroyedEvent.AddNonSerializedListener(this, OnClanDestroyed);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("BLT_MercenaryArmies", ref _mercenaryArmies);
                _mercenaryArmies ??= new List<MercenaryArmyData>();

                // Comprehensive post-load validation and cleanup
                if (dataStore.IsLoading)
                {
                    PerformPostLoadValidation();
                    RebuildPatchSystem();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] MercenaryArmyBehavior.SyncData error: {ex}");
                _mercenaryArmies = new List<MercenaryArmyData>();
            }
        }

        private void PerformPostLoadValidation()
        {
            var invalidArmies = new List<MercenaryArmyData>();

            foreach (var army in _mercenaryArmies)
            {
                // Check for null or missing IDs
                if (army == null || string.IsNullOrEmpty(army.CommanderHeroId) ||
                    string.IsNullOrEmpty(army.TargetSettlementId))
                {
                    invalidArmies.Add(army);
                    continue;
                }

                // Verify commander hero still exists
                var commander = Hero.FindFirst(h => h.StringId == army.CommanderHeroId);
                if (commander == null)
                {
                    Log.Info($"[BLT] Mercenary commander {army.CommanderHeroId} not found, removing army");
                    invalidArmies.Add(army);
                    continue;
                }

                // Verify party still exists
                var party = MobileParty.All.FirstOrDefault(p => p.StringId == army.PartyId);
                if (party == null)
                {
                    Log.Info($"[BLT] Mercenary party {army.PartyId} not found, removing army");
                    invalidArmies.Add(army);
                    continue;
                }

                // Verify target settlement still exists
                var settlement = Settlement.Find(army.TargetSettlementId);
                if (settlement == null)
                {
                    Log.Info($"[BLT] Target settlement {army.TargetSettlementId} not found, removing army");
                    invalidArmies.Add(army);
                    continue;
                }

                // Verify original hero still exists (for refunds)
                var originalHero = Hero.FindFirst(h => h.StringId == army.OriginalHeroId);
                if (originalHero == null)
                {
                    Log.Info($"[BLT] Original hero {army.OriginalHeroId} not found, army will remain but cannot refund");
                }
            }

            // Remove all invalid armies
            foreach (var army in invalidArmies)
            {
                _mercenaryArmies.Remove(army);
            }

            if (invalidArmies.Count > 0)
            {
                Log.Info($"[BLT] Removed {invalidArmies.Count} invalid mercenary armies during load validation");
            }
        }

        private void RebuildPatchSystem()
        {
            // Rebuild the patch system's HashSet after load
            MercenaryArmyPatches.ClearAllRegistrations();

            foreach (var army in _mercenaryArmies)
            {
                var party = MobileParty.All.FirstOrDefault(p => p.StringId == army.PartyId);
                if (party != null)
                {
                    MercenaryArmyPatches.RegisterMercenaryParty(party, army);
                }
            }

            Log.Info($"[BLT] Rebuilt patch system for {_mercenaryArmies.Count} mercenary armies");
        }

        public class ArmyCreationResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
            public MercenaryArmyData ArmyData { get; set; }
        }

        public ArmyCreationResult CreateMercenaryArmy(
            Hero originalHero,
            Settlement targetSettlement,
            int troopCount,
            int elitePercentage,
            int minTroopThreshold,
            int totalCost,
            int refundAmount,
            int maxLifetimeDays)
        {
            Hero mercCommander = null;
            MobileParty party = null;

            try
            {
                // === VALIDATION ===
                if (originalHero?.Clan == null)
                    return new ArmyCreationResult { Success = false, ErrorMessage = "Invalid hero or clan" };

                if (targetSettlement == null)
                    return new ArmyCreationResult { Success = false, ErrorMessage = "Invalid target settlement" };

                if (troopCount <= 0)
                    return new ArmyCreationResult { Success = false, ErrorMessage = "Invalid troop count" };

                // === PHASE 1: CREATE COMMANDER ===
                mercCommander = CreateMercenaryCommander(originalHero);
                if (mercCommander == null)
                {
                    return new ArmyCreationResult { Success = false, ErrorMessage = "Failed to create mercenary commander" };
                }

                // === PHASE 2: FIND SPAWN LOCATION ===
                var spawnSettlement = FindBestSpawnSettlement(originalHero.Clan.Kingdom, targetSettlement);
                if (spawnSettlement == null)
                {
                    CleanupFailedCommander(mercCommander);
                    return new ArmyCreationResult { Success = false, ErrorMessage = "No friendly settlements available for spawning" };
                }

                // === PHASE 3: CREATE PARTY ===
                party = CreateMercenaryParty(mercCommander, spawnSettlement, troopCount, elitePercentage);
                if (party == null)
                {
                    CleanupFailedCommander(mercCommander);
                    return new ArmyCreationResult { Success = false, ErrorMessage = "Failed to create mercenary party" };
                }

                // Verify troops were actually added
                if (party.MemberRoster.TotalManCount < troopCount / 2)
                {
                    CleanupFailedParty(party);
                    CleanupFailedCommander(mercCommander);
                    return new ArmyCreationResult { Success = false, ErrorMessage = $"Failed to recruit sufficient troops (culture may lack required troop types)" };
                }

                // === PHASE 4: CREATE ARMY DATA ===
                var armyData = new MercenaryArmyData
                {
                    CommanderHeroId = mercCommander.StringId,
                    OriginalHeroId = originalHero.StringId,
                    PartyId = party.StringId,
                    TargetSettlementId = targetSettlement.StringId,
                    TargetFactionId = (targetSettlement.OwnerClan?.Kingdom ?? targetSettlement.OwnerClan?.MapFaction)?.StringId,
                    InitialTroopCount = party.MemberRoster.TotalManCount,
                    MinimumTroopThreshold = minTroopThreshold,
                    TotalCost = totalCost,
                    RefundAmount = refundAmount,
                    CreationTime = CampaignTime.Now,
                    MaxLifetimeDays = maxLifetimeDays,
                    IsActive = true
                };

                // === PHASE 5: REGISTER ARMY (ATOMIC OPERATION) ===
                _mercenaryArmies.Add(armyData);
                MercenaryArmyPatches.RegisterMercenaryParty(party, armyData);

                // === PHASE 6: INITIALIZE AI ===
                InitializeMercenaryBehavior(party, targetSettlement);

                Log.Info($"[BLT] Successfully created mercenary army: {party.Name} targeting {targetSettlement.Name}");

                return new ArmyCreationResult
                {
                    Success = true,
                    ArmyData = armyData
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] CreateMercenaryArmy exception: {ex}");

                // Comprehensive cleanup on exception
                if (party != null)
                    CleanupFailedParty(party);
                if (mercCommander != null)
                    CleanupFailedCommander(mercCommander);

                return new ArmyCreationResult { Success = false, ErrorMessage = $"Unexpected error: {ex.Message}" };
            }
        }

        private Hero CreateMercenaryCommander(Hero originalHero)
        {
            try
            {
                var clan = originalHero.Clan;
                var culture = originalHero.Culture;

                // Find appropriate template
                var template = culture.NotableTemplates
                    .FirstOrDefault(t => t.Occupation == Occupation.Mercenary)
                    ?? culture.NotableTemplates.FirstOrDefault(t => t.Occupation == Occupation.Soldier)
                    ?? culture.NotableTemplates.FirstOrDefault();

                if (template == null)
                {
                    Log.Error($"[BLT] No character templates available for culture {culture.Name}");
                    return null;
                }

                // Use original hero's settlement or find a safe fallback
                var homeSettlement = originalHero.HomeSettlement
                    ?? Settlement.All.FirstOrDefault(s => s.OwnerClan?.Kingdom == clan.Kingdom && s.IsTown)
                    ?? Settlement.All.FirstOrDefault(s => s.IsTown);

                if (homeSettlement == null)
                {
                    Log.Error($"[BLT] No valid home settlement found for mercenary commander");
                    return null;
                }

                // Create the hero
                var mercCommander = HeroCreator.CreateSpecialHero(
                    template,
                    homeSettlement,
                    clan,
                    null,
                    MBRandom.RandomInt(25, 40)
                );

                if (mercCommander == null)
                {
                    Log.Error($"[BLT] HeroCreator.CreateSpecialHero returned null");
                    return null;
                }

                // Configure the hero
                mercCommander.SetName(
                    new TextObject($"{originalHero.FirstName}'s Mercenary"),
                    new TextObject($"Mercenary Captain")
                );

                mercCommander.Clan = clan;
                mercCommander.SetNewOccupation(Occupation.Lord);

                // Set skills for effective combat and command
                mercCommander.HeroDeveloper.SetInitialSkillLevel(DefaultSkills.Leadership, 150);
                mercCommander.HeroDeveloper.SetInitialSkillLevel(DefaultSkills.Tactics, 120);
                mercCommander.HeroDeveloper.SetInitialSkillLevel(DefaultSkills.OneHanded, 100);
                mercCommander.HeroDeveloper.SetInitialSkillLevel(DefaultSkills.TwoHanded, 100);
                mercCommander.HeroDeveloper.SetInitialSkillLevel(DefaultSkills.Polearm, 100);

                return mercCommander;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] CreateMercenaryCommander error: {ex}");
                return null;
            }
        }

        private MobileParty CreateMercenaryParty(Hero commander, Settlement spawnLocation, int troopCount, int elitePercentage)
        {
            try
            {
                // Spawn the party at the settlement
                var party = MobilePartyHelper.SpawnLordParty(
                    commander,
                    spawnLocation.GatePosition,
                    0.5f
                );

                if (party == null)
                {
                    Log.Error($"[BLT] SpawnLordParty returned null");
                    return null;
                }

                // Clear any default troops
                party.MemberRoster?.Clear();

                // Calculate troop distribution
                int eliteCount = (int)(troopCount * (elitePercentage / 100f));
                int regularCount = troopCount - eliteCount;

                var culture = commander.Culture;

                // Add regular troops with fallback logic
                if (regularCount > 0)
                {
                    AddTroopsWithFallback(party, culture, regularCount, false);
                }

                // Add elite troops with fallback logic
                if (eliteCount > 0)
                {
                    AddTroopsWithFallback(party, culture, eliteCount, true);
                }

                // Add starting food - quarter of troop count (like reinforcements)
                int startingFood = party.MemberRoster.TotalManCount / 4;
                if (startingFood > 0)
                {
                    party.ItemRoster?.AddToCounts(DefaultItems.Grain, startingFood);
                }

                // Initialize party at position
                party.InitializeMobilePartyAtPosition(spawnLocation.GatePosition);

                // Convert to custom party component for better control
                CustomPartyComponent.ConvertPartyToCustomParty(
                    party,
                    spawnLocation,
                    new TextObject("{=MercParty}Mercenary Army"),
                    commander
                );

                return party;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] CreateMercenaryParty error: {ex}");
                return null;
            }
        }

        private void AddTroopsWithFallback(MobileParty party, CultureObject culture, int troopCount, bool elite)
        {
            int meleeCount = troopCount / 2 + (troopCount % 2);
            int rangedCount = troopCount - meleeCount;

            // Define troop types with priority fallbacks
            CharacterObject meleeType = null;
            CharacterObject rangedType = null;

            if (elite)
            {
                meleeType = culture.MeleeEliteMilitiaTroop ?? culture.EliteBasicTroop;
                rangedType = culture.RangedEliteMilitiaTroop ?? culture.EliteBasicTroop;
            }
            else
            {
                meleeType = culture.MeleeMilitiaTroop ?? culture.BasicTroop;
                rangedType = culture.RangedMilitiaTroop ?? culture.BasicTroop;
            }

            // Ultimate fallback: find any soldier from this culture
            meleeType ??= CharacterObject.All.FirstOrDefault(c =>
                    c.Culture == culture &&
                    c.Occupation == Occupation.Soldier &&
                    !c.IsRanged);

            rangedType ??= CharacterObject.All.FirstOrDefault(c =>
                    c.Culture == culture &&
                    c.Occupation == Occupation.Soldier &&
                    c.IsRanged);

            // Final fallback: use melee type for both if ranged not available
            rangedType ??= meleeType;

            // Add troops
            if (meleeType != null && meleeCount > 0)
            {
                party.MemberRoster.AddToCounts(meleeType, meleeCount);
            }

            if (rangedType != null && rangedCount > 0)
            {
                party.MemberRoster.AddToCounts(rangedType, rangedCount);
            }

            if (meleeType == null && rangedType == null)
            {
                Log.Error($"[BLT] Could not find any valid troops for culture {culture.Name}");
            }
        }

        private Settlement FindBestSpawnSettlement(Kingdom kingdom, Settlement targetSettlement)
        {
            if (kingdom == null || targetSettlement == null)
                return null;

            var friendlySettlements = Settlement.All
                .Where(s => s.OwnerClan?.Kingdom == kingdom && (s.IsTown || s.IsCastle))
                .ToList();

            if (friendlySettlements.Count == 0)
            {
                Log.Info($"[BLT] Kingdom {kingdom.Name} has no friendly settlements for mercenary spawn");
                return null;
            }

            // Find closest settlement to target
            Settlement closest = null;
            float minDistanceSquared = float.MaxValue;

            foreach (var settlement in friendlySettlements)
            {
                float distanceSquared = settlement.GetPosition2D.DistanceSquared(targetSettlement.GetPosition2D);
                if (distanceSquared < minDistanceSquared)
                {
                    minDistanceSquared = distanceSquared;
                    closest = settlement;
                }
            }

            return closest;
        }

        private void InitializeMercenaryBehavior(MobileParty party, Settlement targetSettlement)
        {
            try
            {
                if (party == null || targetSettlement == null)
                    return;

                // Disable AI decision-making - we'll control this party manually
                party.Ai.SetDoNotMakeNewDecisions(true);

                // Set the party to besiege the target settlement
                var nav = party.IsCurrentlyAtSea ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.Default;
                party.SetMoveBesiegeSettlement(targetSettlement, nav);

                Log.Info($"[BLT] Initialized mercenary party {party.Name} to besiege {targetSettlement.Name}");
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] InitializeMercenaryBehavior error: {ex}");
            }
        }

        private void CleanupFailedCommander(Hero commander)
        {
            try
            {
                if (commander != null)
                {
                    KillCharacterAction.ApplyByRemove(commander);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] CleanupFailedCommander error: {ex}");
            }
        }

        private void CleanupFailedParty(MobileParty party)
        {
            try
            {
                if (party != null)
                {
                    party.MemberRoster?.Clear();
                    DestroyPartyAction.Apply(null, party);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] CleanupFailedParty error: {ex}");

                // Fallback cleanup
                try
                {
                    if (party != null)
                    {
                        party.IsActive = false;
                    }
                }
                catch { }
            }
        }

        // ===== EVENT HANDLERS =====

        private void OnHourlyTick()
        {
            try
            {
                // Create defensive copy to allow modification during iteration
                foreach (var armyData in _mercenaryArmies.ToList())
                {
                    if (!armyData.IsActive)
                        continue;

                    UpdateMercenaryArmy(armyData);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnHourlyTick error: {ex}");
            }
        }

        private void OnDailyTick()
        {
            try
            {
                foreach (var armyData in _mercenaryArmies.ToList())
                {
                    if (!armyData.IsActive)
                        continue;

                    MaintainMercenaryArmy(armyData);
                    CheckArmyLifetime(armyData);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnDailyTick error: {ex}");
            }
        }

        private void UpdateMercenaryArmy(MercenaryArmyData armyData)
        {
            try
            {
                var party = MobileParty.All.FirstOrDefault(p => p.StringId == armyData.PartyId);
                var targetSettlement = Settlement.Find(armyData.TargetSettlementId);

                // Validate party and target still exist
                if (party == null || targetSettlement == null)
                {
                    DisbandMercenaryArmy(armyData, DisbandReason.InvalidData, true);
                    return;
                }

                // Check if target is still hostile
                if (!IsTargetStillValid(armyData, targetSettlement))
                {
                    DisbandMercenaryArmy(armyData, DisbandReason.PeaceDeclared, true);
                    return;
                }

                // Ensure AI is disabled and behavior is set correctly
                if (!party.Ai.DoNotMakeNewDecisions)
                {
                    party.Ai.SetDoNotMakeNewDecisions(true);
                }

                // If not currently sieging, ensure we're moving to besiege
                if (party.BesiegedSettlement != targetSettlement && party.DefaultBehavior != AiBehavior.BesiegeSettlement)
                {
                    var nav = party.IsCurrentlyAtSea ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.Default;
                    party.SetMoveBesiegeSettlement(targetSettlement, nav);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] UpdateMercenaryArmy error for {armyData.PartyId}: {ex}");
            }
        }

        private void MaintainMercenaryArmy(MercenaryArmyData armyData)
        {
            try
            {
                var party = MobileParty.All.FirstOrDefault(p => p.StringId == armyData.PartyId);

                if (party == null)
                {
                    DisbandMercenaryArmy(armyData, DisbandReason.PartyDestroyed, false);
                    return;
                }

                int troopCount = party.MemberRoster?.TotalManCount ?? 0;

                // Simple food management - quarter of troop count (like reinforcements)
                int desiredFood = troopCount / 4;
                int currentFood = party.TotalFoodAtInventory;

                if (currentFood < desiredFood)
                {
                    int toAdd = desiredFood - currentFood;
                    party.ItemRoster?.AddToCounts(DefaultItems.Grain, toAdd);
                }

                // Check troop threshold (only when not actively sieging)
                bool isEngaged = party.BesiegedSettlement != null || party.SiegeEvent != null || party.MapEvent != null;

                if (!isEngaged && troopCount < armyData.MinimumTroopThreshold)
                {
                    DisbandMercenaryArmy(armyData, DisbandReason.LowTroops, false);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] MaintainMercenaryArmy error for {armyData.PartyId}: {ex}");
            }
        }

        private void CheckArmyLifetime(MercenaryArmyData armyData)
        {
            try
            {
                if (armyData.MaxLifetimeDays <= 0)
                    return; // Unlimited lifetime

                float daysAlive = (float)(CampaignTime.Now - armyData.CreationTime).ToDays;

                if (daysAlive > armyData.MaxLifetimeDays)
                {
                    DisbandMercenaryArmy(armyData, DisbandReason.ContractExpired, true);

                    var originalHero = Hero.FindFirst(h => h.StringId == armyData.OriginalHeroId);
                    if (originalHero != null)
                    {
                        Log.ShowInformation(
                            $"Your mercenary army contract has expired and been refunded",
                            originalHero.CharacterObject,
                            Log.Sound.Notification1
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] CheckArmyLifetime error: {ex}");
            }
        }

        private bool IsTargetStillValid(MercenaryArmyData armyData, Settlement targetSettlement)
        {
            try
            {
                // Check if we stored the original target faction
                if (string.IsNullOrEmpty(armyData.TargetFactionId))
                    return true; // Can't validate, assume valid

                var originalHero = Hero.FindFirst(h => h.StringId == armyData.OriginalHeroId);
                if (originalHero?.Clan?.Kingdom == null)
                    return false;

                var targetFaction = targetSettlement.OwnerClan?.Kingdom ?? targetSettlement.OwnerClan?.MapFaction;
                if (targetFaction == null)
                    return false;

                // Check if still at war
                return FactionManager.IsAtWarAgainstFaction(originalHero.Clan.Kingdom, targetFaction);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] IsTargetStillValid error: {ex}");
                return true; // Assume valid on error
            }
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                foreach (var armyData in _mercenaryArmies.Where(a => a.TargetSettlementId == settlement.StringId).ToList())
                {
                    var originalHero = Hero.FindFirst(h => h.StringId == armyData.OriginalHeroId);

                    // Check if captured by hero's kingdom
                    if (newOwner?.Clan?.Kingdom != null && originalHero?.Clan?.Kingdom == newOwner.Clan.Kingdom)
                    {
                        // Success!
                        DisbandMercenaryArmy(armyData, DisbandReason.MissionSuccess, false);

                        if (originalHero != null)
                        {
                            Log.ShowInformation(
                                $"Your mercenary army successfully captured {settlement.Name}!",
                                originalHero.CharacterObject,
                                Log.Sound.Notification1
                            );
                        }
                    }
                    else
                    {
                        // Captured by someone else
                        DisbandMercenaryArmy(armyData, DisbandReason.TargetCapturedByOthers, true);

                        if (originalHero != null)
                        {
                            Log.ShowInformation(
                                $"{settlement.Name} was captured by {newOwner?.Clan?.Name?.ToString() ?? "another faction"}. Your mercenary contract has been cancelled.",
                                originalHero.CharacterObject,
                                Log.Sound.Notification1
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnSettlementOwnerChanged error: {ex}");
            }
        }

        private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            try
            {
                var armyData = _mercenaryArmies.FirstOrDefault(a => a.PartyId == party?.StringId);
                if (armyData != null)
                {
                    DisbandMercenaryArmy(armyData, DisbandReason.PartyDestroyed, false);

                    var originalHero = Hero.FindFirst(h => h.StringId == armyData.OriginalHeroId);
                    var targetSettlement = Settlement.Find(armyData.TargetSettlementId);

                    if (originalHero != null)
                    {
                        Log.ShowInformation(
                            $"Your mercenary army targeting {targetSettlement?.Name?.ToString() ?? "unknown"} was destroyed in battle",
                            originalHero.CharacterObject,
                            Log.Sound.Notification1
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnMobilePartyDestroyed error: {ex}");
            }
        }

        private void OnClanDestroyed(Clan clan)
        {
            try
            {
                foreach (var armyData in _mercenaryArmies.Where(a =>
                {
                    var hero = Hero.FindFirst(h => h.StringId == a.OriginalHeroId);
                    return hero?.Clan == clan;
                }).ToList())
                {
                    DisbandMercenaryArmy(armyData, DisbandReason.ClanDestroyed, true);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnClanDestroyed error: {ex}");
            }
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            try
            {
                foreach (var armyData in _mercenaryArmies.Where(a => a.OriginalHeroId == victim?.StringId).ToList())
                {
                    DisbandMercenaryArmy(armyData, DisbandReason.HeroKilled, true);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnHeroKilled error: {ex}");
            }
        }

        private void OnMakePeace(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                // Peace made - disband armies targeting now-peaceful factions
                foreach (var armyData in _mercenaryArmies.ToList())
                {
                    var targetSettlement = Settlement.Find(armyData.TargetSettlementId);
                    if (targetSettlement == null)
                        continue;

                    var originalHero = Hero.FindFirst(h => h.StringId == armyData.OriginalHeroId);
                    if (originalHero?.Clan?.Kingdom == null)
                        continue;

                    var targetFaction = targetSettlement.OwnerClan?.Kingdom ?? targetSettlement.OwnerClan?.MapFaction;
                    if (targetFaction == null)
                        continue;

                    // Check if this peace affects this army
                    bool isPeaceBetweenRelevantFactions =
                        (faction1 == originalHero.Clan.Kingdom && faction2 == targetFaction) ||
                        (faction2 == originalHero.Clan.Kingdom && faction1 == targetFaction);

                    if (isPeaceBetweenRelevantFactions)
                    {
                        DisbandMercenaryArmy(armyData, DisbandReason.PeaceDeclared, true);

                        if (originalHero != null)
                        {
                            Log.ShowInformation(
                                $"Peace was declared with {targetFaction.Name}. Your mercenary army has been disbanded.",
                                originalHero.CharacterObject,
                                Log.Sound.Notification1
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnMakePeace error: {ex}");
            }
        }

        // ===== DISBANDMENT =====

        private enum DisbandReason
        {
            MissionSuccess,
            PartyDestroyed,
            LowTroops,
            ContractExpired,
            PeaceDeclared,
            TargetCapturedByOthers,
            ClanDestroyed,
            HeroKilled,
            InvalidData
        }

        private void DisbandMercenaryArmy(MercenaryArmyData armyData, DisbandReason reason, bool shouldRefund)
        {
            try
            {
                if (!armyData.IsActive)
                    return; // Already disbanded

                armyData.IsActive = false;

                Log.Info($"[BLT] Disbanding mercenary army {armyData.PartyId}: {reason} (Refund: {shouldRefund})");

                // Unregister from patch system
                var party = MobileParty.All.FirstOrDefault(p => p.StringId == armyData.PartyId);
                if (party != null)
                {
                    MercenaryArmyPatches.UnregisterMercenaryParty(party);
                }

                // Process refund
                if (shouldRefund && armyData.RefundAmount > 0)
                {
                    var originalHero = Hero.FindFirst(h => h.StringId == armyData.OriginalHeroId);
                    if (originalHero != null)
                    {
                        BLTAdoptAHeroCampaignBehavior.Current?.ChangeHeroGold(originalHero, armyData.RefundAmount, true);
                        Log.Info($"[BLT] Refunded {armyData.RefundAmount} gold to {originalHero.Name}");
                    }
                }

                // Destroy party
                if (party != null)
                {
                    try
                    {
                        party.MemberRoster?.Clear();
                        DestroyPartyAction.Apply(null, party);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception($"[BLT] Failed to destroy party normally", ex, false);
                        try
                        {
                            party.IsActive = false;
                        }
                        catch { }
                    }
                }

                // Remove commander hero
                var commander = Hero.FindFirst(h => h.StringId == armyData.CommanderHeroId);
                if (commander != null)
                {
                    try
                    {
                        KillCharacterAction.ApplyByRemove(commander);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception($"[BLT] Failed to remove commander hero", ex, false);
                    }
                }

                // Remove from tracking
                _mercenaryArmies.Remove(armyData);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] DisbandMercenaryArmy error: {ex}");
            }
        }

        // ===== PUBLIC QUERY METHODS =====

        public int GetActiveArmiesForHero(Hero hero)
        {
            if (hero == null)
                return 0;

            return _mercenaryArmies.Count(a => a.IsActive && a.OriginalHeroId == hero.StringId);
        }

        public int GetActiveArmiesForClan(Clan clan)
        {
            if (clan == null)
                return 0;

            return _mercenaryArmies.Count(a =>
            {
                if (!a.IsActive)
                    return false;

                var hero = Hero.FindFirst(h => h.StringId == a.OriginalHeroId);
                return hero?.Clan == clan;
            });
        }

        public List<MercenaryArmyData> GetArmiesForHero(Hero hero)
        {
            if (hero == null)
                return new List<MercenaryArmyData>();

            return _mercenaryArmies.Where(a => a.IsActive && a.OriginalHeroId == hero.StringId).ToList();
        }

        // ===== DATA STRUCTURES =====

        [Serializable]
        public class MercenaryArmyData
        {
            // Identity
            public string CommanderHeroId { get; set; }
            public string OriginalHeroId { get; set; }
            public string PartyId { get; set; }
            public string TargetSettlementId { get; set; }
            public string TargetFactionId { get; set; }

            // Configuration
            public int InitialTroopCount { get; set; }
            public int MinimumTroopThreshold { get; set; }
            public int TotalCost { get; set; }
            public int RefundAmount { get; set; }

            // Lifetime
            public CampaignTime CreationTime { get; set; }
            public int MaxLifetimeDays { get; set; }

            // State
            public bool IsActive { get; set; }
        }
    }
}