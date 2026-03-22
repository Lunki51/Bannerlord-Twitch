using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Actions;
using TaleWorlds.Core;
using Helpers;

namespace BLTAdoptAHero
{
    // ── Order types ──────────────────────────────────────────────────────────
    public enum PartyOrderType
    {
        Siege = 0,  // Besiege an enemy fortification
        Defend = 1,  // Defend a settlement (raw AI action)
        Patrol = 2,  // Patrol around a settlement
        Garrison = 3,  // Travel to and stay inside a friendly fortification
        Raid = 4,  // Raid an enemy village
        SmartGuard = 5,  // Dynamic: defend > village-patrol > patrol based on live conditions
    }

    public class PartyOrderBehavior : CampaignBehaviorBase
    {
        public static PartyOrderBehavior Current { get; private set; }

        private List<string> _ordersJson = new();
        private List<string> _aiArmiesBlockedKingdoms = new();
        private List<string> _bltArmiesBlockedKingdoms = new();

        [NonSerialized] private List<PartyOrderData> _orders = new();

        public PartyOrderBehavior() { Current = this; }

        // ─────────────────────────────────────────────
        //  LIFECYCLE
        // ─────────────────────────────────────────────

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, OnArmyDispersed);
            CampaignEvents.OnMissionEndedEvent.AddNonSerializedListener(this, OnMissionEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("BLT_PartyOrders", ref _ordersJson);
                dataStore.SyncData("BLT_AIArmiesBlockedKingdoms", ref _aiArmiesBlockedKingdoms);
                dataStore.SyncData("BLT_BLTArmiesBlockedKingdoms", ref _bltArmiesBlockedKingdoms);
                _ordersJson ??= new List<string>();
                _aiArmiesBlockedKingdoms ??= new List<string>();
                _bltArmiesBlockedKingdoms ??= new List<string>();

                if (dataStore.IsLoading)
                {
                    _orders = _ordersJson
                        .Select(json =>
                        {
                            try { return Newtonsoft.Json.JsonConvert.DeserializeObject<PartyOrderData>(json); }
                            catch { return null; }
                        })
                        .Where(o => o != null && !string.IsNullOrEmpty(o.PartyId))
                        .ToList();

                    foreach (var order in _orders.Where(o => o.IsActive))
                    {
                        var p = MobileParty.All.FirstOrDefault(x => x.StringId == order.PartyId);
                        if (p == null || !p.IsActive) { order.IsActive = false; continue; }
                        p.Ai.SetDoNotMakeNewDecisions(true);
                    }
                }
                else
                {
                    _ordersJson = _orders
                        .Select(o =>
                        {
                            try { return Newtonsoft.Json.JsonConvert.SerializeObject(o); }
                            catch { return null; }
                        })
                        .Where(s => s != null)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] PartyOrderBehavior.SyncData error: {ex}");
                _orders = new List<PartyOrderData>();
                _ordersJson = new List<string>();
            }
        }

        // ─────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────

        public void RegisterOrder(Hero hero, MobileParty party, PartyOrderType type,
            Settlement targetSettlement, int maxReissueAttempts, float expiryDays = 0f)
        {
            if (hero == null || party == null) return;
            CancelOrdersForParty(party.StringId, null, false);

            _orders.Add(new PartyOrderData
            {
                HeroId = hero.StringId,
                PartyId = party.StringId,
                Type = type,
                TargetSettlementId = targetSettlement?.StringId,
                IssuedAtDays = CampaignTime.Now.ToDays,
                // expiryDays == 0 means no expiry; otherwise store the absolute day.
                ExpiresAtDays = expiryDays > 0f
                                         ? CampaignTime.Now.ToDays + (double)expiryDays
                                         : 0,
                MaxReissueAttempts = maxReissueAttempts,
                ReissueAttempts = 0,
                IsActive = true
            });
        }

        public void CancelOrdersForParty(string partyId, string reason, bool notify)
        {
            foreach (var o in _orders.Where(x => x.IsActive && x.PartyId == partyId).ToList())
                ExpireOrder(o, reason, notify);
        }

        public bool HasActiveOrder(string partyId) =>
            _orders.Any(o => o.IsActive && o.PartyId == partyId);

        public PartyOrderData GetActiveOrder(string partyId) =>
            _orders.FirstOrDefault(o => o.IsActive && o.PartyId == partyId);

        public bool IsAIArmiesBlocked(Kingdom k) => k != null && _aiArmiesBlockedKingdoms.Contains(k.StringId);
        public bool IsBLTArmiesBlocked(Kingdom k) => k != null && _bltArmiesBlockedKingdoms.Contains(k.StringId);

        public void SetAIArmiesBlocked(Kingdom k, bool blocked)
        {
            if (k == null) return;
            if (blocked) { if (!_aiArmiesBlockedKingdoms.Contains(k.StringId)) _aiArmiesBlockedKingdoms.Add(k.StringId); }
            else { _aiArmiesBlockedKingdoms.Remove(k.StringId); }
        }
        public void SetBLTArmiesBlocked(Kingdom k, bool blocked)
        {
            if (k == null) return;
            if (blocked) { if (!_bltArmiesBlockedKingdoms.Contains(k.StringId)) _bltArmiesBlockedKingdoms.Add(k.StringId); }
            else { _bltArmiesBlockedKingdoms.Remove(k.StringId); }
        }

        // ─────────────────────────────────────────────
        //  HOURLY MONITORING
        // ─────────────────────────────────────────────

        private void OnHourlyTickParty(MobileParty party)
        {
            try
            {
                if (party == null || !party.IsActive) return;

                // Keep BLT army cohesion topped up
                if (party.LeaderHero?.IsAdopted() == true
                    && party.Army != null && party.Army.LeaderParty == party)
                {
                    party.Army.Cohesion = 100f;
                }

                var order = _orders.FirstOrDefault(o => o.IsActive && o.PartyId == party.StringId);
                if (order == null) return;

                if (!party.IsActive) { ExpireOrder(order, null, false); return; }

                // ── Settlement handling ─────────────────────────────────────────────────
                if (party.CurrentSettlement != null)
                {
                    // Garrison at the correct fort: satisfied — stay locked
                    if (order.Type == PartyOrderType.Garrison &&
                        party.CurrentSettlement.StringId == order.TargetSettlementId)
                    {
                        order.ReissueAttempts = 0;
                        party.Ai.SetDoNotMakeNewDecisions(true);
                        return;
                    }
                    // Every other order type (including SmartGuard): unlock so the
                    // party can leave. The order will be re-applied next tick.
                    try { party.Ai.SetDoNotMakeNewDecisions(false); }
                    catch (Exception ex) { Log.Error($"[BLT] AI unlock in settlement error: {ex}"); }
                    return;
                }

                // Expiry
                if (order.ExpiresAtDays > 0 && CampaignTime.Now.ToDays >= order.ExpiresAtDays)
                {
                    NotifyHero(order, "Army order expired.");
                    ExpireOrder(order, "Expired", false);
                    return;
                }

                if (party.MapEvent != null) return;

                // ── SmartGuard: re-evaluate conditions; only re-issue on state change ──
                if (order.Type == PartyOrderType.SmartGuard)
                {
                    var sgTarget = order.TargetSettlementId != null
                        ? Settlement.Find(order.TargetSettlementId) : null;
                    if (sgTarget == null || !sgTarget.IsFortification)
                    {
                        NotifyHero(order, "Smart guard target is no longer valid.");
                        ExpireOrder(order, "Invalid target", false);
                        return;
                    }

                    // Determine desired state from current world conditions
                    var besiegerFaction = sgTarget.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction;
                    bool enemySieging = sgTarget.IsUnderSiege
                        && besiegerFaction != null
                        && party.MapFaction.IsAtWarWith(besiegerFaction);

                    Settlement raidedVillage = null;
                    if (!enemySieging && sgTarget.BoundVillages != null)
                    {
                        foreach (var v in sgTarget.BoundVillages)
                        {
                            var vs = v?.Settlement;
                            if (vs?.IsUnderRaid != true) continue;
                            var rf = vs.LastAttackerParty?.MapFaction;
                            if (rf != null && party.MapFaction.IsAtWarWith(rf))
                            { raidedVillage = vs; break; }
                        }
                    }

                    AiBehavior wantedBehavior = enemySieging
                        ? AiBehavior.DefendSettlement
                        : AiBehavior.PatrolAroundPoint;
                    Settlement wantedTarget = enemySieging ? sgTarget
                        : (raidedVillage ?? sgTarget);

                    bool behaviorOk = party.DefaultBehavior == wantedBehavior;
                    bool targetOk = party.TargetSettlement != null
                                      && party.TargetSettlement.StringId == wantedTarget.StringId;

                    // Only call IssueSmartGuardOrder when the desired state actually changed
                    if (!behaviorOk || !targetOk)
                        IssueSmartGuardOrder(party, sgTarget);

                    party.Ai.SetDoNotMakeNewDecisions(true);
                    order.ReissueAttempts = 0;
                    return;
                }

                // ── Garrison: satisfied if party is inside the settlement ──
                if (order.Type == PartyOrderType.Garrison)
                {
                    bool inside = party.CurrentSettlement?.StringId == order.TargetSettlementId;
                    bool enRoute = party.DefaultBehavior == AiBehavior.GoToSettlement;
                    if (inside)
                    {
                        order.ReissueAttempts = 0;
                        party.Ai.SetDoNotMakeNewDecisions(true);
                        return;
                    }
                    if (enRoute)
                    {
                        order.ReissueAttempts = 0;
                        return;
                    }
                    // Fell through → treat as drift below
                }

                // If the party is already in the act of besieging the correct settlement,
                // do NOT re-issue — the game engine handles the assault transition itself.
                // Just ensure the AI is unlocked and get out of the way.
                if (order.Type == PartyOrderType.Siege
                    && party.BesiegedSettlement != null
                    && party.BesiegedSettlement.StringId == order.TargetSettlementId)
                {
                    order.ReissueAttempts = 0;
                    party.Ai.SetDoNotMakeNewDecisions(false);
                    return;
                }

                // ── Standard drift detection ──────────────────────────────────
                var expectedBehavior = OrderTypeToAiBehavior(order.Type);
                var expectedTarget = order.TargetSettlementId != null
                    ? Settlement.Find(order.TargetSettlementId) : null;

                bool behaviorMatches = party.DefaultBehavior == expectedBehavior;
                bool targetMatches = expectedTarget == null
                    || party.TargetSettlement == null
                    || party.TargetSettlement == expectedTarget;

                if (behaviorMatches && targetMatches)
                {
                    order.ReissueAttempts = 0;
                    if (order.Type == PartyOrderType.Siege && party.BesiegedSettlement != null)
                        party.Ai.SetDoNotMakeNewDecisions(false);
                    return;
                }

                if (order.ReissueAttempts >= order.MaxReissueAttempts)
                {
                    NotifyHero(order, "Army order could not be maintained and has been released.");
                    ExpireOrder(order, "Max reissues reached", false);
                    return;
                }

                var target = order.TargetSettlementId != null
                    ? Settlement.Find(order.TargetSettlementId) : null;

                if (!ValidateOrder(party, order.Type, target))
                {
                    NotifyHero(order, "Army order cancelled — conditions no longer valid.");
                    ExpireOrder(order, "Validation failed", false);
                    return;
                }

                IssueOrder(party, order.Type, target);
                party.Ai.SetDoNotMakeNewDecisions(true);
                order.ReissueAttempts++;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] PartyOrderBehavior.OnHourlyTickParty error: {ex}");
            }
        }

        // ─────────────────────────────────────────────
        //  EVENT HANDLERS  (unchanged from original)
        // ─────────────────────────────────────────────

        private void OnMakePeace(IFaction f1, IFaction f2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                foreach (var order in _orders.Where(o => o.IsActive && o.Type == PartyOrderType.Siege).ToList())
                {
                    var target = order.TargetSettlementId != null ? Settlement.Find(order.TargetSettlementId) : null;
                    if (target == null) continue;
                    var hero = Hero.FindFirst(h => h.StringId == order.HeroId);
                    if (hero?.Clan?.Kingdom == null) continue;
                    var tgtFaction = target.OwnerClan?.Kingdom ?? target.OwnerClan?.MapFaction;
                    if (tgtFaction == null) continue;
                    bool peace = (f1 == hero.Clan.Kingdom && f2 == tgtFaction)
                              || (f2 == hero.Clan.Kingdom && f1 == tgtFaction);
                    if (peace)
                    {
                        NotifyHero(order, $"Peace declared with {tgtFaction.Name} — siege order cancelled.");
                        ExpireOrder(order, "Peace", false);
                    }
                }
            }
            catch (Exception ex) { Log.Error($"[BLT] OnMakePeace error: {ex}"); }
        }

        private void OnHeroKilled(Hero victim, Hero killer,
            KillCharacterAction.KillCharacterActionDetail detail, bool show)
        {
            try
            {
                if (victim == null) return;
                foreach (var o in _orders.Where(x => x.IsActive && x.HeroId == victim.StringId).ToList())
                    ExpireOrder(o, "Hero killed", false);
            }
            catch (Exception ex) { Log.Error($"[BLT] OnHeroKilled error: {ex}"); }
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            try
            {
                if (prisoner == null) return;
                foreach (var o in _orders.Where(x => x.IsActive && x.HeroId == prisoner.StringId).ToList())
                {
                    NotifyHero(o, "You were captured — order released.");
                    ExpireOrder(o, "Hero captured", false);
                }
            }
            catch (Exception ex) { Log.Error($"[BLT] OnHeroPrisonerTaken error: {ex}"); }
        }

        private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            try
            {
                if (party == null) return;
                foreach (var o in _orders.Where(x => x.IsActive && x.PartyId == party.StringId).ToList())
                    ExpireOrder(o, "Party destroyed", false);
            }
            catch (Exception ex) { Log.Error($"[BLT] OnMobilePartyDestroyed error: {ex}"); }
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim,
            Hero newOwner, Hero oldOwner, Hero capturer,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (settlement == null) return;
                foreach (var o in _orders
                    .Where(x => x.IsActive && x.Type == PartyOrderType.Siege
                             && x.TargetSettlementId == settlement.StringId).ToList())
                {
                    var hero = Hero.FindFirst(h => h.StringId == o.HeroId);
                    bool ours = hero?.Clan?.Kingdom != null && newOwner?.Clan?.Kingdom == hero.Clan.Kingdom;
                    NotifyHero(o, ours
                        ? $"Your forces captured {settlement.Name}!"
                        : $"{settlement.Name} was taken by {newOwner?.Name.ToString() ?? "another faction"} — siege order released.");
                    ExpireOrder(o, "Settlement owner changed", false);
                }
            }
            catch (Exception ex) { Log.Error($"[BLT] OnSettlementOwnerChanged error: {ex}"); }
        }

        private void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayerArmy)
        {
            try
            {
                if (army?.LeaderParty == null) return;
                foreach (var o in _orders
                    .Where(x => x.IsActive && x.PartyId == army.LeaderParty.StringId).ToList())
                    ExpireOrder(o, $"Army disbanded ({reason})", false);
            }
            catch (Exception ex) { Log.Error($"[BLT] OnArmyDispersed error: {ex}"); }
        }

        private void OnMissionEnded(IMission mission)
        {
            try
            {
                foreach (var order in _orders.Where(o => o.IsActive).ToList())
                {
                    var p = MobileParty.All.FirstOrDefault(x => x.StringId == order.PartyId);
                    if (p == null || !p.IsActive) continue;
                    var target = order.TargetSettlementId != null ? Settlement.Find(order.TargetSettlementId) : null;
                    if (!ValidateOrder(p, order.Type, target)) continue;
                    IssueOrder(p, order.Type, target);
                    p.Ai.SetDoNotMakeNewDecisions(true);
                }
            }
            catch (Exception ex) { Log.Error($"[BLT] OnMissionEnded error: {ex}"); }
        }

        // ─────────────────────────────────────────────
        //  ORDER ISSUANCE
        // ─────────────────────────────────────────────

        /// <summary>
        /// Issue the appropriate SetPartyAiAction for an order type.
        /// Called both at order creation and on silent re-issue.
        /// SmartGuard delegates to IssueSmartGuardOrder.
        /// </summary>
        public static void IssueOrder(MobileParty party, PartyOrderType type, Settlement target)
        {
            bool atSea = party.IsCurrentlyAtSea;
            bool needsWaterCrossing = !atSea && target != null && LandPartyNeedsWaterCrossing(party, target);
            bool isFromPort = atSea || needsWaterCrossing;
            var nav = isFromPort ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.All;

            var pm = new PartyManagement();

            switch (type)
            {
                case PartyOrderType.Siege:
                    if (target != null)
                        SetPartyAiAction.GetActionForBesiegingSettlement(party, target, nav, isFromPort);
                    break;

                case PartyOrderType.Defend:
                    if (target != null)
                        SetPartyAiAction.GetActionForDefendingSettlement(party, target, nav, isFromPort, false);
                    break;

                case PartyOrderType.Patrol:
                    if (target != null)
                        SetPartyAiAction.GetActionForPatrollingAroundSettlement(party, target, nav, isFromPort, false);
                    else
                        SetPartyAiAction.GetActionForPatrollingAroundSettlement(
                            party,
                            pm.FindBestSettlementToDefend(party, party.LeaderHero?.Clan?.Kingdom),
                            nav, isFromPort, false);
                    break;

                case PartyOrderType.Garrison:
                    // GoToSettlement causes the party to travel to and enter the settlement
                    if (target != null)
                        SetPartyAiAction.GetActionForVisitingSettlement(party, target, nav, isFromPort, isFromPort);
                    break;

                case PartyOrderType.Raid:
                    if (target != null)
                        SetPartyAiAction.GetActionForRaidingSettlement(party, target, nav, isFromPort);
                    break;

                case PartyOrderType.SmartGuard:
                    if (target != null)
                        IssueSmartGuardOrder(party, target);
                    else
                        SetPartyAiAction.GetActionForPatrollingAroundSettlement(
                            party,
                            pm.FindBestSettlementToDefend(party, party.LeaderHero?.Clan?.Kingdom),
                            nav, isFromPort, false);
                    break;
            }
        }

        /// <summary>
        /// Dynamic defend/patrol logic for a fortification.
        /// Priority: (1) defend if enemy is besieging; (2) patrol to village under enemy raid;
        /// (3) patrol the fortification.
        /// Does NOT count as a re-issue attempt — called every tick for SmartGuard orders.
        /// </summary>
        public static void IssueSmartGuardOrder(MobileParty party, Settlement fortification)
        {
            if (party == null || fortification == null || !fortification.IsFortification) return;

            bool atSea = party.IsCurrentlyAtSea;

            // ── 1. Defend if fortification is under attack by an enemy ───────
            if (fortification.IsUnderSiege && fortification.SiegeEvent?.BesiegerCamp != null)
            {
                var besiegerFaction = fortification.SiegeEvent.BesiegerCamp.LeaderParty?.MapFaction;
                if (besiegerFaction != null && party.MapFaction.IsAtWarWith(besiegerFaction))
                {
                    bool w = !atSea && LandPartyNeedsWaterCrossing(party, fortification);
                    bool fp = atSea || w;
                    var nav = fp ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.All;
                    SetPartyAiAction.GetActionForDefendingSettlement(party, fortification, nav, fp, false);
                    return;
                }
                // Besieger is non-hostile (e.g. allied siege or civil war) → fall through to patrol
            }

            // ── 2. Patrol to defend a bound village under enemy raid ─────────
            if (fortification.BoundVillages != null)
            {
                foreach (var village in fortification.BoundVillages.OrderByDescending(v => v.Hearth))
                {
                    if (village.Settlement?.IsUnderRaid != true || village.Settlement == null) continue;
                    var raider = village.Settlement.LastAttackerParty;
                    if (raider == null || !party.MapFaction.IsAtWarWith(raider.MapFaction)) continue;

                    bool w = !atSea && LandPartyNeedsWaterCrossing(party, village.Settlement);
                    bool fp = atSea || w;
                    var nav = fp ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.All;
                    SetPartyAiAction.GetActionForPatrollingAroundSettlement(party, village.Settlement, nav, fp, false);
                    return;
                }
            }

            // ── 3. Default: patrol the fortification ─────────────────────────
            {
                bool w = !atSea && LandPartyNeedsWaterCrossing(party, fortification);
                bool fp = atSea || w;
                var nav = fp ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.All;
                SetPartyAiAction.GetActionForPatrollingAroundSettlement(party, fortification, nav, fp, false);
            }
        }

        // ─────────────────────────────────────────────
        //  REACHABILITY HELPERS  (unchanged)
        // ─────────────────────────────────────────────

        public static bool IsSettlementReachable(MobileParty party, Settlement target)
        {
            try
            {
                if (party.IsCurrentlyAtSea)
                {
                    float navalDist = Campaign.Current.Models.MapDistanceModel.GetDistance(
                        party, target, true, MobileParty.NavigationType.Naval, out _);
                    return navalDist < float.MaxValue - 1f;
                }
                float dist = Campaign.Current.Models.MapDistanceModel.GetDistance(
                    party, target, false, MobileParty.NavigationType.All, out float landRatio);
                if (dist >= float.MaxValue - 1f) return false;
                if (landRatio >= 0f && landRatio < 0.5f) return false;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] IsSettlementReachable error: {ex}");
                return false;
            }
        }

        private static bool LandPartyNeedsWaterCrossing(MobileParty party, Settlement target)
        {
            try
            {
                // Already at sea — handled by the caller via IsCurrentlyAtSea
                if (party.IsCurrentlyAtSea) return false;

                // Distance calculation is unreliable from inside a settlement gate;
                // returning false here prevents the naval-stamp bug entirely.
                if (party.CurrentSettlement != null) return false;

                float dist = Campaign.Current.Models.MapDistanceModel.GetDistance(
                    party, target, false, MobileParty.NavigationType.All, out float landRatio);

                // Unreachable by land nav: do NOT assume naval — default to land.
                // The old "return true" here was the root cause of the sailing bug.
                if (dist >= float.MaxValue - 1f) return false;

                // landRatio < 0.5 means the majority of the route crosses water
                if (landRatio >= 0f && landRatio < 0.5f) return true;

                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] LandPartyNeedsWaterCrossing error: {ex}");
                return false;   // safe default: land routing
            }
        }

        // ─────────────────────────────────────────────
        //  ORDER VALIDATION
        // ─────────────────────────────────────────────

        /// <summary>
        /// Validate that the prerequisites for (re-)issuing an order are still met.
        /// Allied siege joining is supported via BLTTreatyManager.
        /// </summary>
        public static bool ValidateOrder(MobileParty party, PartyOrderType type, Settlement target)
        {
            if (party.MapEvent != null && party.SiegeEvent == null) return false;

            switch (type)
            {
                case PartyOrderType.Siege:
                    {
                        if (target == null || !target.IsFortification) return false;
                        if (!party.MapFaction.IsAtWarWith(target.MapFaction)) return false;
                        if (party.BesiegedSettlement == target) return true;
                        if (target.IsUnderSiege)
                        {
                            var besiegerFaction = target.SiegeEvent?.BesiegerCamp?.MapFaction;
                            Log.Info($"[BLT-VALIDATE] {party.StringId}: SIEGE — target is under siege by {besiegerFaction?.Name}");

                            if (besiegerFaction == null)
                            {
                                Log.Info($"[BLT-VALIDATE] {party.StringId}: SIEGE FALSE — besieger faction null");
                                return false;
                            }

                            if (besiegerFaction == party.MapFaction)
                            {
                                Log.Info($"[BLT-VALIDATE] {party.StringId}: SIEGE TRUE — same faction as besieger");
                                return true;
                            }

                            var besiegerK = besiegerFaction as Kingdom;
                            var partyK = party.MapFaction as Kingdom;
                            if (besiegerK != null && partyK != null)
                            {
                                var alliance = BLTTreatyManager.Current?.GetAlliance(partyK, besiegerK);
                                bool besiegerAtWar = besiegerK.IsAtWarWith(target.MapFaction);
                                Log.Info($"[BLT-VALIDATE] {party.StringId}: SIEGE kingdom check — " +
                                         $"alliance={alliance != null} besiegerAtWar={besiegerAtWar} " +
                                         $"partyK={partyK.Name} besiegerK={besiegerK.Name}");
                                bool result = alliance != null && besiegerAtWar;
                                Log.Info($"[BLT-VALIDATE] {party.StringId}: SIEGE {result} — kingdom alliance result");
                                return result;
                            }

                            var besiegerC = besiegerFaction as Clan;
                            var clan = party.ActualClan;
                            if (besiegerC != null && clan != null && BLTClanDiplomacyBehavior.Current != null)
                            {
                                bool hasAlliance = BLTClanDiplomacyBehavior.Current.HasAlliance(clan, besiegerC);
                                Log.Info($"[BLT-VALIDATE] {party.StringId}: SIEGE {hasAlliance} — clan alliance check " +
                                         $"clan={clan.Name} besiegerClan={besiegerC.Name}");
                                return hasAlliance;
                            }

                            Log.Info($"[BLT-VALIDATE] {party.StringId}: SIEGE FALSE — under siege but no alliance path matched. " +
                                     $"besiegerFactionType={besiegerFaction.GetType().Name} " +
                                     $"partyFactionType={party.MapFaction?.GetType().Name} " +
                                     $"besiegerC={besiegerC?.Name} clan={clan?.Name} " +
                                     $"diplomacyBehavior={BLTClanDiplomacyBehavior.Current != null}");
                            return false;
                        }

                        bool noOtherSiege = party.BesiegedSettlement == null;
                        Log.Info($"[BLT-VALIDATE] {party.StringId}: SIEGE {noOtherSiege} — no active siege on target, " +
                                 $"partyBesiegedSettlement={party.BesiegedSettlement?.StringId}");
                        return noOtherSiege;
                    }

                case PartyOrderType.Defend:
                    return target != null && target.IsFortification;

                case PartyOrderType.Patrol:
                    return true;

                case PartyOrderType.Garrison:
                    // Valid as long as the fortification exists and we're not at war with its owners
                    return target != null && target.IsFortification
                        && (party.MapFaction == null || !party.MapFaction.IsAtWarWith(target.MapFaction));

                case PartyOrderType.Raid:
                    // Valid while village exists, we're at war, and it isn't already raided by others
                    return target != null && target.IsVillage
                        && party.MapFaction.IsAtWarWith(target.MapFaction)
                        && (target.Village?.Settlement.IsUnderRaid == false && target.Village?.Settlement.IsRaided == false
                            || target.Village.Settlement?.LastAttackerParty?.MapFaction == party.MapFaction);

                case PartyOrderType.SmartGuard:
                    // Valid as long as the target fortification exists (conditions handled dynamically)
                    return target != null && target.IsFortification;

                default:
                    return false;
            }
        }

        // ─────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────

        private static AiBehavior OrderTypeToAiBehavior(PartyOrderType type) => type switch
        {
            PartyOrderType.Siege => AiBehavior.BesiegeSettlement,
            PartyOrderType.Defend => AiBehavior.DefendSettlement,
            PartyOrderType.Patrol => AiBehavior.PatrolAroundPoint,
            PartyOrderType.Garrison => AiBehavior.GoToSettlement,
            PartyOrderType.Raid => AiBehavior.RaidSettlement,
            PartyOrderType.SmartGuard => AiBehavior.PatrolAroundPoint, // base; actual varies
            _ => AiBehavior.None
        };

        private void ExpireOrder(PartyOrderData order, string reason, bool notify)
        {
            if (!order.IsActive) return;
            order.IsActive = false;

            // MobileParty.All may not contain a party that is currently being
            // destroyed by DisbandArmyAction (it is removed from the list before
            // the ArmyDispersed event fires). Search the broader collection and
            // guard the AI call so a partially-destroyed party doesn't throw.
            MobileParty p = null;
            try
            {
                p = Campaign.Current.MobileParties
                        .FirstOrDefault(x => x?.StringId == order.PartyId && x.IsActive);
                if (p != null)
                    p.Ai.SetDoNotMakeNewDecisions(false);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] ExpireOrder: could not release AI lock for {order.PartyId}: {ex}");
            }

            if (notify && reason != null) NotifyHero(order, reason);
            _orders.Remove(order);
        }


        private static void NotifyHero(PartyOrderData order, string message)
        {
            try
            {
                var hero = Hero.FindFirst(h => h.StringId == order.HeroId);
                if (hero != null)
                    Log.ShowInformation(message, hero.CharacterObject, Log.Sound.Notification1);
            }
            catch (Exception ex) { Log.Error($"[BLT] NotifyHero error: {ex}"); }
        }
    }
}

// ── Persisted order data (top-level class for save-system compatibility) ──────
[Serializable]
public class PartyOrderData
{
    public string HeroId { get; set; }
    public string PartyId { get; set; }
    public int TypeRaw { get; set; } // PartyOrderType as int
    public string TargetSettlementId { get; set; }
    public double IssuedAtDays { get; set; }
    public double ExpiresAtDays { get; set; }
    public int MaxReissueAttempts { get; set; }
    public int ReissueAttempts { get; set; }
    public bool IsActive { get; set; }

    public PartyOrderType Type
    {
        get => (PartyOrderType)TypeRaw;
        set => TypeRaw = (int)value;
    }
}