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

        private List<MercenaryArmyData> _armies = new();

        public MercenaryArmyBehavior() { Current = this; }

        // ─────────────────────────────────────────────
        //  LIFECYCLE
        // ─────────────────────────────────────────────

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("BLT_MercenaryArmies", ref _armies);
                _armies ??= new List<MercenaryArmyData>();

                if (!dataStore.IsLoading) return;

                // Strip invalid entries
                _armies.RemoveAll(a => a == null
                    || string.IsNullOrEmpty(a.PartyId)
                    || string.IsNullOrEmpty(a.KingdomId));

                // Rebuild patch registrations; re-apply locks on surviving entries
                MercenaryArmyPatches.ClearAllRegistrations();
                foreach (var d in _armies)
                {
                    var p = MobileParty.All.FirstOrDefault(x => x.StringId == d.PartyId);
                    if (p == null || !p.IsActive)
                    {
                        d.IsActive = false;
                        continue;
                    }

                    if (p.Army != null)
                        MercenaryArmyPatches.RegisterMercenaryArmy(p, p.Army);

                    // Re-apply AI lock so load doesn't give the AI a free re-evaluation
                    if (d.IsActive)
                        p.Ai.SetDoNotMakeNewDecisions(true);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] MercenaryArmyBehavior.SyncData error: {ex}");
                _armies = new List<MercenaryArmyData>();
            }
        }

        // ─────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────

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
            int maxReissueAttempts,
            int totalCost,
            int refundAmount,
            int maxLifetimeDays)
        {
            Hero commander = null;
            MobileParty party = null;

            try
            {
                if (originalHero?.Clan?.Kingdom == null)
                    return Fail("Invalid hero or clan");
                if (targetSettlement == null)
                    return Fail("Invalid target settlement");

                // ── Siege prerequisites (guide: Validation Requirements) ──
                if (!targetSettlement.IsFortification)
                    return Fail($"{targetSettlement.Name} is not a fortification");
                if (targetSettlement.IsUnderSiege)
                    return Fail($"{targetSettlement.Name} is already under siege");
                if (!originalHero.Clan.Kingdom.IsAtWarWith(
                        targetSettlement.OwnerClan?.Kingdom
                        ?? targetSettlement.OwnerClan?.MapFaction))
                    return Fail($"Not at war with {targetSettlement.Name}'s owner");

                // ── Create commander ──
                commander = CreateCommander(originalHero);
                if (commander == null)
                    return Fail("Failed to create commander");

                // ── Find spawn point (closest friendly settlement to target) ──
                var spawnSettlement = FindClosestFriendlySettlement(originalHero.Clan.Kingdom, targetSettlement);
                if (spawnSettlement == null)
                {
                    Cleanup(commander, null, null);
                    return Fail("No friendly settlements to spawn from");
                }

                // ── Spawn party ──
                party = MobilePartyHelper.SpawnLordParty(commander, spawnSettlement.GatePosition, 0.5f);
                if (party == null || !party.IsActive)
                {
                    Cleanup(commander, null, null);
                    return Fail("Failed to create party");
                }

                // ── Populate troops ──
                party.MemberRoster?.Clear();
                AddTroops(party, commander.Culture, troopCount, elitePercentage);
                if (party.MemberRoster.TotalManCount < troopCount / 2)
                {
                    Cleanup(commander, party, null);
                    return Fail("Failed to recruit sufficient troops");
                }

                // ── Add food ──
                int food = Math.Max(1, party.MemberRoster.TotalManCount / 4);
                party.ItemRoster?.AddToCounts(DefaultItems.Grain, food);

                // ── Create army (leader-only; no gather phase allies needed) ──
                //    Kingdom.CreateArmy handles proper engine-side registration.
                //    We immediately override the gather-phase move with our siege order.
                originalHero.Clan.Kingdom.CreateArmy(commander, targetSettlement, Army.ArmyTypes.Besieger, null);

                var army = party.Army;
                if (army == null)
                {
                    Cleanup(commander, party, null);
                    return Fail("Army object not created");
                }

                // ── Issue siege order ──
                var nav = party.IsCurrentlyAtSea
                    ? MobileParty.NavigationType.Naval
                    : MobileParty.NavigationType.Default;
                SetPartyAiAction.GetActionForBesiegingSettlement(party, targetSettlement, nav, party.IsCurrentlyAtSea);

                // ── Lock Tier 3 AI (siege order survives hourly re-evaluation) ──
                party.Ai.SetDoNotMakeNewDecisions(true);

                // ── Register ──
                var data = new MercenaryArmyData
                {
                    CommanderHeroId = commander.StringId,
                    OriginalHeroId = originalHero.StringId,
                    PartyId = party.StringId,
                    KingdomId = originalHero.Clan.Kingdom.StringId,
                    TargetSettlementId = targetSettlement.StringId,
                    TargetFactionId = (targetSettlement.OwnerClan?.Kingdom ?? targetSettlement.OwnerClan?.MapFaction)?.StringId,
                    InitialTroopCount = party.MemberRoster.TotalManCount,
                    MinimumTroopThreshold = minTroopThreshold,
                    MaxReissueAttempts = maxReissueAttempts,
                    TotalCost = totalCost,
                    RefundAmount = refundAmount,
                    CreationTimeDays = CampaignTime.Now.ToDays,
                    MaxLifetimeDays = maxLifetimeDays,
                    Status = "Marching",
                    IsActive = true
                };

                _armies.Add(data);
                MercenaryArmyPatches.RegisterMercenaryArmy(party, army);

                Log.Info($"[BLT] Mercenary army created: {party.Name} → {targetSettlement.Name}");
                return new ArmyCreationResult { Success = true, ArmyData = data };
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] CreateMercenaryArmy error: {ex}");
                Cleanup(commander, party, null);
                return Fail(ex.Message);
            }

            static ArmyCreationResult Fail(string msg) =>
                new() { Success = false, ErrorMessage = msg };
        }

        public int GetActiveArmiesForHero(Hero hero)
        {
            if (hero == null) return 0;
            return _armies.Count(a => a.IsActive && a.OriginalHeroId == hero.StringId);
        }

        // ─────────────────────────────────────────────
        //  EVENT HANDLERS
        // ─────────────────────────────────────────────

        /// <summary>
        /// Core monitoring loop. Runs every in-game hour for every mobile party.
        /// Only processes parties we are tracking; everything else returns immediately.
        /// </summary>
        private void OnHourlyTickParty(MobileParty party)
        {
            try
            {
                var data = _armies.FirstOrDefault(a => a.IsActive && a.PartyId == party?.StringId);
                if (data == null) return;

                // ── Party no longer alive ──
                if (!party.IsActive)
                {
                    DisbandArmy(data, "Party lost", false);
                    return;
                }

                // ── Troop threshold (skip if currently fighting) ──
                if (party.MapEvent == null
                    && party.MemberRoster.TotalHealthyCount < data.MinimumTroopThreshold)
                {
                    DisbandArmy(data, "Troops depleted", false);
                    return;
                }

                // ── Verify target still exists ──
                var target = Settlement.Find(data.TargetSettlementId);
                if (target == null)
                {
                    DisbandArmy(data, "Target settlement missing", true);
                    return;
                }

                // ── Siege underway — milestone notification (once only) ──
                if (party.BesiegedSettlement == target && data.Status != "Besieging")
                {
                    data.Status = "Besieging";
                    data.ReissueAttempts = 0;
                    NotifyHero(data, $"Your mercenaries are besieging {target.Name}!", Log.Sound.Notification1);
                    return;
                }

                // ── Behavior drift detection (not in combat, not already besieging) ──
                if (party.MapEvent != null || party.DefaultBehavior == AiBehavior.BesiegeSettlement)
                {
                    // All good or actively fighting — reset counter if besieging
                    if (party.DefaultBehavior == AiBehavior.BesiegeSettlement)
                        data.ReissueAttempts = 0;
                    return;
                }

                // DefaultBehavior has drifted away from siege and we're not in combat
                if (data.ReissueAttempts >= data.MaxReissueAttempts)
                {
                    DisbandArmy(data, "Siege order could not be maintained", true);
                    return;
                }

                // Re-validate before re-issuing
                bool canSiege = target.IsFortification
                    && !target.IsUnderSiege
                    && party.MapFaction.IsAtWarWith(target.MapFaction)
                    && party.BesiegedSettlement == null
                    && party.MapEvent == null;

                if (!canSiege)
                {
                    DisbandArmy(data, "Siege no longer possible", true);
                    return;
                }

                // Silent re-issue
                var nav = party.IsCurrentlyAtSea
                    ? MobileParty.NavigationType.Naval
                    : MobileParty.NavigationType.Default;
                SetPartyAiAction.GetActionForBesiegingSettlement(party, target, nav, party.IsCurrentlyAtSea);
                party.Ai.SetDoNotMakeNewDecisions(true);
                data.ReissueAttempts++;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnHourlyTickParty error: {ex}");
            }
        }

        private void OnDailyTick()
        {
            try
            {
                foreach (var data in _armies.ToList())
                {
                    if (!data.IsActive) continue;

                    var party = MobileParty.All.FirstOrDefault(p => p.StringId == data.PartyId);
                    if (party == null) continue;

                    // ── Food top-up ──
                    int desired = Math.Max(1, party.MemberRoster.TotalManCount / 4);
                    int current = party.TotalFoodAtInventory;
                    if (current < desired)
                        party.ItemRoster?.AddToCounts(DefaultItems.Grain, desired - current);

                    // ── Lifetime expiry ──
                    if (data.MaxLifetimeDays > 0
                        && CampaignTime.Now.ToDays >= data.CreationTimeDays + data.MaxLifetimeDays)
                    {
                        DisbandArmy(data, "Contract expired", true);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnDailyTick error: {ex}");
            }
        }

        private void OnSettlementOwnerChanged(
            Settlement settlement, bool openToClaim,
            Hero newOwner, Hero oldOwner, Hero capturer,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (settlement == null) return;

                foreach (var data in _armies.Where(a => a.IsActive && a.TargetSettlementId == settlement.StringId).ToList())
                {
                    var originalHero = Hero.FindFirst(h => h.StringId == data.OriginalHeroId);
                    bool ours = newOwner?.Clan?.Kingdom != null
                                && originalHero?.Clan?.Kingdom == newOwner.Clan.Kingdom;

                    if (ours)
                    {
                        NotifyHero(data, $"Your mercenaries captured {settlement.Name}!", Log.Sound.Notification1);
                        DisbandArmy(data, "Target captured", false); // success — no refund needed
                    }
                    else
                    {
                        NotifyHero(data, $"{settlement.Name} was captured by {newOwner?.Name.ToString() ?? "someone else"}", Log.Sound.Notification1);
                        DisbandArmy(data, "Target lost to others", true);
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
                var data = _armies.FirstOrDefault(a => a.IsActive && a.PartyId == party.StringId);
                if (data != null)
                    DisbandArmy(data, "Army destroyed in battle", false);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnMobilePartyDestroyed error: {ex}");
            }
        }

        private void OnHeroKilled(
            Hero victim, Hero killer,
            KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            try
            {
                if (victim == null) return;
                foreach (var data in _armies.Where(a => a.IsActive && a.OriginalHeroId == victim.StringId).ToList())
                    DisbandArmy(data, "Commissioning hero died", true);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnHeroKilled error: {ex}");
            }
        }

        private void OnMakePeace(IFaction f1, IFaction f2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                if (f1 == null || f2 == null) return;

                foreach (var data in _armies.Where(a => a.IsActive).ToList())
                {
                    var target = Settlement.Find(data.TargetSettlementId);
                    if (target == null) continue;

                    var hero = Hero.FindFirst(h => h.StringId == data.OriginalHeroId);
                    if (hero?.Clan?.Kingdom == null) continue;

                    var targetFaction = target.OwnerClan?.Kingdom ?? target.OwnerClan?.MapFaction;
                    if (targetFaction == null) continue;

                    bool peace = (f1 == hero.Clan.Kingdom && f2 == targetFaction)
                              || (f2 == hero.Clan.Kingdom && f1 == targetFaction);

                    if (peace)
                    {
                        NotifyHero(data,
                            $"Peace with {targetFaction.Name} — mercenary contract cancelled. {data.RefundAmount}g refunded.",
                            Log.Sound.Notification1);
                        DisbandArmy(data, "Peace declared", true);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnMakePeace error: {ex}");
            }
        }

        // ─────────────────────────────────────────────
        //  INTERNALS
        // ─────────────────────────────────────────────

        private void DisbandArmy(MercenaryArmyData data, string reason, bool refund)
        {
            try
            {
                if (data == null || !data.IsActive) return;
                data.IsActive = false;

                Log.Info($"[BLT] Disbanding mercenary army {data.PartyId}: {reason} (refund={refund})");

                var party = MobileParty.All.FirstOrDefault(p => p.StringId == data.PartyId);
                var army = party?.Army;
                var commander = Hero.FindFirst(h => h.StringId == data.CommanderHeroId);

                // Release lock before cleanup — prevents engine fighting the destroy action
                if (party != null)
                    party.Ai.SetDoNotMakeNewDecisions(false);

                MercenaryArmyPatches.UnregisterMercenaryArmy(data.PartyId);

                if (refund && data.RefundAmount > 0)
                {
                    var hero = Hero.FindFirst(h => h.StringId == data.OriginalHeroId);
                    BLTAdoptAHeroCampaignBehavior.Current?.ChangeHeroGold(hero, data.RefundAmount, true);
                }

                Cleanup(commander, party, army);
                _armies.Remove(data);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] DisbandArmy error: {ex}");
            }
        }

        private static void Cleanup(Hero commander, MobileParty party, Army army)
        {
            try { if (army != null) DisbandArmyAction.ApplyByUnknownReason(army); }
            catch (Exception ex) { Log.Error($"[BLT] Cleanup(army) error: {ex}"); }

            try { if (party != null && party.IsActive) DestroyPartyAction.Apply(null, party); }
            catch (Exception ex) { Log.Error($"[BLT] Cleanup(party) error: {ex}"); }

            try { if (commander != null) KillCharacterAction.ApplyByRemove(commander); }
            catch (Exception ex) { Log.Error($"[BLT] Cleanup(commander) error: {ex}"); }
        }

        private static void NotifyHero(MercenaryArmyData data, string message, Log.Sound sound)
        {
            try
            {
                var hero = Hero.FindFirst(h => h.StringId == data.OriginalHeroId);
                if (hero != null)
                    Log.ShowInformation(message, hero.CharacterObject, sound);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] NotifyHero error: {ex}");
            }
        }

        private static Hero CreateCommander(Hero originalHero)
        {
            try
            {
                var template = originalHero.Culture.NotableTemplates?.FirstOrDefault();
                if (template == null) return null;

                var home = originalHero.HomeSettlement
                    ?? Settlement.All.FirstOrDefault(s =>
                        s.OwnerClan?.Kingdom == originalHero.Clan.Kingdom && s.IsTown);
                if (home == null) return null;

                var c = HeroCreator.CreateSpecialHero(template, home, originalHero.Clan, null, 30);
                if (c == null) return null;

                c.SetName(
                    new TextObject($"{originalHero.FirstName}'s Mercenary"),
                    new TextObject("Mercenary Captain"));
                c.SetNewOccupation(Occupation.Lord);
                c.HeroDeveloper.SetInitialSkillLevel(DefaultSkills.Leadership, 150);
                c.HeroDeveloper.SetInitialSkillLevel(DefaultSkills.Tactics, 120);
                return c;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] CreateCommander error: {ex}");
                return null;
            }
        }

        private static void AddTroops(MobileParty party, CultureObject culture, int total, int elitePct)
        {
            int elite = (int)(total * (elitePct / 100f));
            int regular = total - elite;

            var reg = culture.MeleeMilitiaTroop ?? culture.BasicTroop;
            var eli = culture.MeleeEliteMilitiaTroop ?? culture.EliteBasicTroop ?? reg;

            if (reg != null && regular > 0) party.MemberRoster.AddToCounts(reg, regular);
            if (eli != null && elite > 0) party.MemberRoster.AddToCounts(eli, elite);
        }

        private static Settlement FindClosestFriendlySettlement(Kingdom kingdom, Settlement target) =>
            Settlement.All
                .Where(s => s.OwnerClan?.Kingdom == kingdom && (s.IsTown || s.IsCastle))
                .OrderBy(s => s.GetPosition2D.DistanceSquared(target.GetPosition2D))
                .FirstOrDefault();

    }

    // Top-level class — NOT nested inside MercenaryArmyBehavior.
    // Bannerlord's XML serializer resolves types by name; nested classes
    // produce "MercenaryArmyBehavior+MercenaryArmyData" which it cannot
    // reliably round-trip, causing save failure even with an empty list.
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
        public int MaxReissueAttempts { get; set; }
        public int ReissueAttempts { get; set; }
        public int TotalCost { get; set; }
        public int RefundAmount { get; set; }
        public double CreationTimeDays { get; set; }
        public int MaxLifetimeDays { get; set; }
        public string Status { get; set; }
        public bool IsActive { get; set; }
    }
}