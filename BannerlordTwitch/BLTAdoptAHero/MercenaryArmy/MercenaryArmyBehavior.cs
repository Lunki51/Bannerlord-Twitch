using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using BannerlordTwitch.Util;
using Helpers;

namespace BLTAdoptAHero
{
    public class MercenaryArmyBehavior : CampaignBehaviorBase
    {
        public static MercenaryArmyBehavior Current { get; private set; }
        private List<MercenaryArmyData> _mercenaryArmies = new();

        public MercenaryArmyBehavior()
        {
            Current = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
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

                if (dataStore.IsLoading)
                {
                    // Remove invalid armies
                    _mercenaryArmies.RemoveAll(a => a == null ||
                        string.IsNullOrEmpty(a.PartyId) ||
                        string.IsNullOrEmpty(a.KingdomId));

                    // Rebuild patch registrations
                    MercenaryArmyPatches.ClearAllRegistrations();
                    foreach (var armyData in _mercenaryArmies)
                    {
                        var party = MobileParty.All.FirstOrDefault(p => p.StringId == armyData.PartyId);
                        if (party?.Army != null)
                        {
                            MercenaryArmyPatches.RegisterMercenaryArmy(party, party.Army);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] MercenaryArmyBehavior.SyncData error: {ex}");
                _mercenaryArmies = new List<MercenaryArmyData>();
            }
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
            Army army = null;

            try
            {
                // Validate
                if (originalHero?.Clan?.Kingdom == null)
                    return new ArmyCreationResult { Success = false, ErrorMessage = "Invalid hero or clan" };
                if (targetSettlement == null)
                    return new ArmyCreationResult { Success = false, ErrorMessage = "Invalid target settlement" };

                // Create commander
                mercCommander = CreateCommander(originalHero);
                if (mercCommander == null)
                {
                    return new ArmyCreationResult { Success = false, ErrorMessage = "Failed to create commander" };
                }

                // Find spawn location
                var spawnSettlement = FindClosestFriendlySettlement(originalHero.Clan.Kingdom, targetSettlement);
                if (spawnSettlement == null)
                {
                    Cleanup(mercCommander, null, null);
                    return new ArmyCreationResult { Success = false, ErrorMessage = "No friendly settlements" };
                }

                // Create party
                party = MobilePartyHelper.SpawnLordParty(mercCommander, spawnSettlement.GatePosition, 0.5f);
                if (party == null || !party.IsActive)
                {
                    Cleanup(mercCommander, null, null);
                    return new ArmyCreationResult { Success = false, ErrorMessage = "Failed to create party" };
                }

                // Add troops
                party.MemberRoster?.Clear();
                AddTroops(party, mercCommander.Culture, troopCount, elitePercentage);
                if (party.MemberRoster.TotalManCount < troopCount / 2)
                {
                    Cleanup(mercCommander, party, null);
                    return new ArmyCreationResult { Success = false, ErrorMessage = "Failed to recruit troops" };
                }

                // Add food
                int food = party.MemberRoster.TotalManCount / 4;
                if (food > 0)
                    party.ItemRoster?.AddToCounts(DefaultItems.Grain, food);

                // Create army (void method, sets party.Army)
                originalHero.Clan.Kingdom.CreateArmy(mercCommander, targetSettlement, Army.ArmyTypes.Besieger, null);

                // Get the created army from the party
                army = party.Army;
                if (army == null)
                {
                    Cleanup(mercCommander, party, null);
                    return new ArmyCreationResult { Success = false, ErrorMessage = "Failed to create army" };
                }

                // Create data
                var armyData = new MercenaryArmyData
                {
                    CommanderHeroId = mercCommander.StringId,
                    OriginalHeroId = originalHero.StringId,
                    PartyId = party.StringId,
                    KingdomId = originalHero.Clan.Kingdom.StringId,
                    TargetSettlementId = targetSettlement.StringId,
                    TargetFactionId = (targetSettlement.OwnerClan?.Kingdom ?? targetSettlement.OwnerClan?.MapFaction)?.StringId,
                    InitialTroopCount = party.MemberRoster.TotalManCount,
                    MinimumTroopThreshold = minTroopThreshold,
                    TotalCost = totalCost,
                    RefundAmount = refundAmount,
                    CreationTimeDays = CampaignTime.Now.ToDays,
                    MaxLifetimeDays = maxLifetimeDays,
                    IsActive = true
                };

                // Register
                _mercenaryArmies.Add(armyData);
                MercenaryArmyPatches.RegisterMercenaryArmy(party, army);

                Log.Info($"[BLT] Created mercenary army: {party.Name} -> {targetSettlement.Name}");
                return new ArmyCreationResult { Success = true, ArmyData = armyData };
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] CreateMercenaryArmy error: {ex}");
                Cleanup(mercCommander, party, army);
                return new ArmyCreationResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private Hero CreateCommander(Hero originalHero)
        {
            try
            {
                var template = originalHero.Culture.NotableTemplates.FirstOrDefault();
                if (template == null) return null;

                var homeSettlement = originalHero.HomeSettlement
                    ?? Settlement.All.FirstOrDefault(s => s.OwnerClan?.Kingdom == originalHero.Clan.Kingdom && s.IsTown);
                if (homeSettlement == null) return null;

                var commander = HeroCreator.CreateSpecialHero(template, homeSettlement, originalHero.Clan, null, 30);
                if (commander == null) return null;

                commander.SetName(new TextObject($"{originalHero.FirstName}'s Mercenary"), new TextObject("Mercenary Captain"));
                commander.SetNewOccupation(Occupation.Lord);

                // Set skills
                commander.HeroDeveloper.SetInitialSkillLevel(DefaultSkills.Leadership, 150);
                commander.HeroDeveloper.SetInitialSkillLevel(DefaultSkills.Tactics, 120);

                return commander;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] CreateCommander error: {ex}");
                return null;
            }
        }

        private void AddTroops(MobileParty party, CultureObject culture, int troopCount, int elitePercentage)
        {
            int eliteCount = (int)(troopCount * (elitePercentage / 100f));
            int regularCount = troopCount - eliteCount;

            // Get troop types
            var regular = culture.MeleeMilitiaTroop ?? culture.BasicTroop;
            var elite = culture.MeleeEliteMilitiaTroop ?? culture.EliteBasicTroop ?? regular;

            if (regular != null && regularCount > 0)
                party.MemberRoster.AddToCounts(regular, regularCount);
            if (elite != null && eliteCount > 0)
                party.MemberRoster.AddToCounts(elite, eliteCount);
        }

        private Settlement FindClosestFriendlySettlement(Kingdom kingdom, Settlement target)
        {
            return Settlement.All
                .Where(s => s.OwnerClan?.Kingdom == kingdom && (s.IsTown || s.IsCastle))
                .OrderBy(s => s.GetPosition2D.DistanceSquared(target.GetPosition2D))
                .FirstOrDefault();
        }

        private void Cleanup(Hero commander, MobileParty party, Army army)
        {
            try
            {
                if (army != null)
                    DisbandArmyAction.ApplyByUnknownReason(army);
                if (party != null)
                    DestroyPartyAction.Apply(null, party);
                if (commander != null)
                    KillCharacterAction.ApplyByRemove(commander);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Cleanup error: {ex}");
            }
        }

        // ===== EVENT HANDLERS =====

        private void OnDailyTick()
        {
            try
            {
                foreach (var armyData in _mercenaryArmies.ToList())
                {
                    if (!armyData.IsActive) continue;

                    // Add food
                    var party = MobileParty.All.FirstOrDefault(p => p.StringId == armyData.PartyId);
                    if (party != null)
                    {
                        int desired = party.MemberRoster.TotalManCount / 4;
                        int current = party.TotalFoodAtInventory;
                        if (current < desired)
                            party.ItemRoster?.AddToCounts(DefaultItems.Grain, desired - current);
                    }

                    // Check lifetime
                    if (armyData.MaxLifetimeDays > 0)
                    {
                        double expiryDay =
                            armyData.CreationTimeDays + armyData.MaxLifetimeDays;

                        if (CampaignTime.Now.ToDays >= expiryDay)
                        {
                            DisbandArmy(armyData, "Contract expired", true);
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnDailyTick error: {ex}");
            }
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (settlement == null) return;

                foreach (var armyData in _mercenaryArmies.Where(a => a.TargetSettlementId == settlement.StringId).ToList())
                {
                    var originalHero = Hero.FindFirst(h => h.StringId == armyData.OriginalHeroId);
                    bool success = newOwner?.Clan?.Kingdom != null && originalHero?.Clan?.Kingdom == newOwner.Clan.Kingdom;

                    DisbandArmy(armyData, success ? "Target captured!" : "Target lost to others", !success);

                    if (originalHero != null)
                    {
                        Log.ShowInformation(
                            success ? $"Your mercenaries captured {settlement.Name}!" : $"{settlement.Name} was captured by {newOwner?.Name}",
                            originalHero.CharacterObject,
                            Log.Sound.Notification1
                        );
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
                if (party == null) return;

                var armyData = _mercenaryArmies.FirstOrDefault(a => a.PartyId == party.StringId);
                if (armyData != null)
                {
                    DisbandArmy(armyData, "Army destroyed in battle", false);
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
                if (clan == null) return;
                foreach (var armyData in _mercenaryArmies.Where(a =>
                {
                    var hero = Hero.FindFirst(h => h.StringId == a.OriginalHeroId);
                    return hero?.Clan == clan;
                }).ToList())
                {
                    DisbandArmy(armyData, "Clan destroyed", true);
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
                if (victim == null) return;
                foreach (var armyData in _mercenaryArmies.Where(a => a.OriginalHeroId == victim.StringId).ToList())
                {
                    DisbandArmy(armyData, "Hero killed", true);
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
                if (faction1 == null || faction2 == null) return;

                foreach (var armyData in _mercenaryArmies.ToList())
                {
                    var target = Settlement.Find(armyData.TargetSettlementId);
                    if (target == null) continue;

                    var originalHero = Hero.FindFirst(h => h.StringId == armyData.OriginalHeroId);
                    if (originalHero?.Clan?.Kingdom == null) continue;

                    var targetFaction = target.OwnerClan?.Kingdom ?? target.OwnerClan?.MapFaction;
                    if (targetFaction == null) continue;

                    bool peaceMade = (faction1 == originalHero.Clan.Kingdom && faction2 == targetFaction) ||
                                    (faction2 == originalHero.Clan.Kingdom && faction1 == targetFaction);

                    if (peaceMade)
                    {
                        DisbandArmy(armyData, "Peace declared", true);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnMakePeace error: {ex}");
            }
        }

        private void DisbandArmy(MercenaryArmyData armyData, string reason, bool refund)
        {
            try
            {
                if (armyData == null || !armyData.IsActive) return;

                armyData.IsActive = false;
                Log.Info($"[BLT] Disbanding army {armyData.PartyId}: {reason} (refund={refund})");

                // Get party and army
                var party = MobileParty.All.FirstOrDefault(p => p.StringId == armyData.PartyId);
                var army = party?.Army;
                var commander = Hero.FindFirst(h => h.StringId == armyData.CommanderHeroId);

                // Unregister
                MercenaryArmyPatches.UnregisterMercenaryArmy(armyData.PartyId);

                // Refund
                if (refund && armyData.RefundAmount > 0)
                {
                    var hero = Hero.FindFirst(h => h.StringId == armyData.OriginalHeroId);
                    if (hero != null)
                    {
                        BLTAdoptAHeroCampaignBehavior.Current?.ChangeHeroGold(hero, armyData.RefundAmount, true);
                    }
                }

                // Cleanup
                Cleanup(commander, party, army);

                _mercenaryArmies.Remove(armyData);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] DisbandArmy error: {ex}");
            }
        }

        public int GetActiveArmiesForHero(Hero hero)
        {
            if (hero == null) return 0;
            return _mercenaryArmies.Count(a => a.IsActive && a.OriginalHeroId == hero.StringId);
        }

        public int GetActiveArmiesForClan(Clan clan)
        {
            if (clan == null) return 0;
            return _mercenaryArmies.Count(a =>
            {
                if (!a.IsActive) return false;
                var hero = Hero.FindFirst(h => h.StringId == a.OriginalHeroId);
                return hero?.Clan == clan;
            });
        }

        [Serializable]
        public class MercenaryArmyData
        {
            public string CommanderHeroId { get; set; }
            public string OriginalHeroId { get; set; }
            public string PartyId { get; set; }
            public string KingdomId { get; set; }
            public string TargetSettlementId { get; set; }
            public string TargetFactionId { get; set; }
            public int InitialTroopCount { get; set; }
            public int MinimumTroopThreshold { get; set; }
            public int TotalCost { get; set; }
            public int RefundAmount { get; set; }
            public double CreationTimeDays { get; set; }
            public int MaxLifetimeDays { get; set; }
            public bool IsActive { get; set; }
        }
    }
}