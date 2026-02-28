using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Actions;
using TaleWorlds.Core;

namespace BLTAdoptAHero
{
    public enum PartyOrderType { Siege, Defend, Patrol }

    public class PartyOrderBehavior : CampaignBehaviorBase
    {
        public static PartyOrderBehavior Current { get; private set; }

        private List<string> _ordersJson = new();

        /// <summary>
        /// StringIds of kingdoms whose king has issued '!party army allowai off' and '!party army allowblt off'.
        /// Presence == Armies blocked; absence == allowed (default).
        /// </summary>
        private List<string> _aiArmiesBlockedKingdoms = new();
        private List<string> _bltArmiesBlockedKingdoms = new();

        // Runtime list — deserialized from _ordersJson on load
        [NonSerialized]
        private List<PartyOrderData> _orders = new();

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
                        var party = MobileParty.All.FirstOrDefault(p => p.StringId == order.PartyId);
                        if (party == null || !party.IsActive) { order.IsActive = false; continue; }
                        party.Ai.SetDoNotMakeNewDecisions(true);
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

        /// <summary>
        /// Register a new order. Cancels any existing active order for the same party first.
        /// Caller is responsible for issuing the SetPartyAiAction before calling this.
        /// </summary>
        public void RegisterOrder(
            Hero hero,
            MobileParty party,
            PartyOrderType type,
            Settlement targetSettlement,
            int maxReissueAttempts,
            int expiryHours = 0)
        {
            if (hero == null || party == null) return;

            // Cancel existing order silently — new order supersedes it
            CancelOrdersForParty(party.StringId, null, false);

            _orders.Add(new PartyOrderData
            {
                HeroId = hero.StringId,
                PartyId = party.StringId,
                Type = type,
                TargetSettlementId = targetSettlement?.StringId,
                IssuedAtDays = CampaignTime.Now.ToDays,
                ExpiresAtDays = expiryHours > 0
                                      ? CampaignTime.Now.ToDays + expiryHours / 24.0
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

        public bool IsAIArmiesBlocked(Kingdom kingdom)
            => kingdom != null && _aiArmiesBlockedKingdoms.Contains(kingdom.StringId);

        public bool IsBLTArmiesBlocked(Kingdom kingdom)
            => kingdom != null && _bltArmiesBlockedKingdoms.Contains(kingdom.StringId);


        /// <summary>
        /// Sets the AI army block state for <paramref name="kingdom"/>.
        /// <paramref name="blocked"/> = true  → '!party army allowai off'
        /// <paramref name="blocked"/> = false → '!party army allowai on'  (default)
        /// </summary>
        public void SetAIArmiesBlocked(Kingdom kingdom, bool blocked)
        {
            if (kingdom == null) return;
            if (blocked)
            {
                if (!_aiArmiesBlockedKingdoms.Contains(kingdom.StringId))
                    _aiArmiesBlockedKingdoms.Add(kingdom.StringId);
            }
            else
            {
                if (_aiArmiesBlockedKingdoms.Contains(kingdom.StringId))
                    _aiArmiesBlockedKingdoms.Remove(kingdom.StringId);
            }
        }
        public void SetBLTArmiesBlocked(Kingdom kingdom, bool blocked)
        {
            if (kingdom == null) return;
            if (blocked)
            {
                if (!_bltArmiesBlockedKingdoms.Contains(kingdom.StringId))
                    _bltArmiesBlockedKingdoms.Add(kingdom.StringId);
            }
            else
            {
                if (_bltArmiesBlockedKingdoms.Contains(kingdom.StringId))
                    _bltArmiesBlockedKingdoms.Remove(kingdom.StringId);
            }
        }

        public PartyOrderData GetActiveOrder(string partyId) =>
            _orders.FirstOrDefault(o => o.IsActive && o.PartyId == partyId);

        // ─────────────────────────────────────────────
        //  HOURLY MONITORING
        // ─────────────────────────────────────────────

        private void OnHourlyTickParty(MobileParty party)
        {
            try
            {
                if (party == null || !party.IsActive) return;

                if (party.LeaderHero != null && party.LeaderHero.IsAdopted()
                    && party.Army != null && party.Army.LeaderParty == party)
                {
                    party.Army.Cohesion = 100f;
                }

                var order = _orders.FirstOrDefault(o => o.IsActive && o.PartyId == party?.StringId);
                if (order == null) return;

                if (!party.IsActive)
                {
                    ExpireOrder(order, null, false);
                    return;
                }

                // Expiry check
                if (order.ExpiresAtDays > 0 && CampaignTime.Now.ToDays >= order.ExpiresAtDays)
                {
                    NotifyHero(order, $"Army order expired.");
                    ExpireOrder(order, "Expired", false);
                    return;
                }

                // Don't interfere while party is in combat
                if (party.MapEvent != null) return;

                var expectedBehavior = OrderTypeToAiBehavior(order.Type);

                // Order is holding — verify both behavior type AND the actual target settlement
                var expectedTarget = order.TargetSettlementId != null
                    ? Settlement.Find(order.TargetSettlementId)
                    : null;

                bool behaviorMatches = party.DefaultBehavior == expectedBehavior;

                // For settlement-targeted orders, also confirm the party is heading to the right place. TargetSettlement can be null mid-path (approaching) so we
                // only flag a mismatch when it is explicitly set to something different.
                bool targetMatches = expectedTarget == null
                    || party.TargetSettlement == null
                    || party.TargetSettlement == expectedTarget;

                if (behaviorMatches && targetMatches)
                {
                    order.ReissueAttempts = 0;
                    return;
                }

                // Behavior drifted — attempt re-issue
                if (order.ReissueAttempts >= order.MaxReissueAttempts)
                {
                    NotifyHero(order, $"Army order could not be maintained and has been released.");
                    ExpireOrder(order, "Max reissues reached", false);
                    return;
                }

                var target = order.TargetSettlementId != null
                    ? Settlement.Find(order.TargetSettlementId) : null;

                if (!ValidateOrder(party, order.Type, target))
                {
                    NotifyHero(order, $"Army order cancelled — conditions no longer valid.");
                    ExpireOrder(order, "Validation failed", false);
                    return;
                }

                // Silent re-issue
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
        //  EVENT HANDLERS
        // ─────────────────────────────────────────────

        private void OnMakePeace(IFaction f1, IFaction f2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                foreach (var order in _orders.Where(o => o.IsActive).ToList())
                {
                    if (order.Type != PartyOrderType.Siege) continue;

                    var target = order.TargetSettlementId != null
                        ? Settlement.Find(order.TargetSettlementId) : null;
                    if (target == null) continue;

                    var hero = Hero.FindFirst(h => h.StringId == order.HeroId);
                    if (hero?.Clan?.Kingdom == null) continue;

                    var targetFaction = target.OwnerClan?.Kingdom ?? target.OwnerClan?.MapFaction;
                    if (targetFaction == null) continue;

                    bool peace = (f1 == hero.Clan.Kingdom && f2 == targetFaction)
                              || (f2 == hero.Clan.Kingdom && f1 == targetFaction);

                    if (peace)
                    {
                        NotifyHero(order, $"Peace declared with {targetFaction.Name} — siege order cancelled.");
                        ExpireOrder(order, "Peace", false);
                    }
                }
            }
            catch (Exception ex) { Log.Error($"[BLT] PartyOrderBehavior.OnMakePeace error: {ex}"); }
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
            catch (Exception ex) { Log.Error($"[BLT] PartyOrderBehavior.OnHeroKilled error: {ex}"); }
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            try
            {
                if (prisoner == null) return;
                foreach (var o in _orders.Where(x => x.IsActive && x.HeroId == prisoner.StringId).ToList())
                {
                    NotifyHero(o, $"You were captured — army order released.");
                    ExpireOrder(o, "Hero captured", false);
                }
            }
            catch (Exception ex) { Log.Error($"[BLT] PartyOrderBehavior.OnHeroPrisonerTaken error: {ex}"); }
        }

        private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            try
            {
                if (party == null) return;
                foreach (var o in _orders.Where(x => x.IsActive && x.PartyId == party.StringId).ToList())
                    ExpireOrder(o, "Party destroyed", false);
            }
            catch (Exception ex) { Log.Error($"[BLT] PartyOrderBehavior.OnMobilePartyDestroyed error: {ex}"); }
        }

        private void OnSettlementOwnerChanged(
            Settlement settlement, bool openToClaim,
            Hero newOwner, Hero oldOwner, Hero capturer,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (settlement == null) return;
                foreach (var o in _orders
                    .Where(x => x.IsActive
                             && x.Type == PartyOrderType.Siege
                             && x.TargetSettlementId == settlement.StringId).ToList())
                {
                    var hero = Hero.FindFirst(h => h.StringId == o.HeroId);
                    bool ours = hero?.Clan?.Kingdom != null
                                && newOwner?.Clan?.Kingdom == hero.Clan.Kingdom;

                    NotifyHero(o, ours
                        ? $"Your army captured {settlement.Name}!"
                        : $"{settlement.Name} was taken by {newOwner?.Name.ToString() ?? "another faction"} — siege order released.");
                    ExpireOrder(o, "Settlement owner changed", false);
                }
            }
            catch (Exception ex) { Log.Error($"[BLT] PartyOrderBehavior.OnSettlementOwnerChanged error: {ex}"); }
        }

        private void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayerArmy)
        {
            try
            {
                if (army?.LeaderParty == null) return;
                // If the army was disbanded externally, release our order so we don't try to re-issue
                foreach (var o in _orders
                    .Where(x => x.IsActive && x.PartyId == army.LeaderParty.StringId).ToList())
                    ExpireOrder(o, $"Army disbanded ({reason})", false);
            }
            catch (Exception ex) { Log.Error($"[BLT] PartyOrderBehavior.OnArmyDispersed error: {ex}"); }
        }

        private void OnMissionEnded(IMission mission)
        {
            try
            {
                foreach (var order in _orders.Where(o => o.IsActive).ToList())
                {
                    var party = MobileParty.All.FirstOrDefault(p => p.StringId == order.PartyId);
                    if (party == null || !party.IsActive) continue;

                    var target = order.TargetSettlementId != null
                        ? Settlement.Find(order.TargetSettlementId) : null;

                    if (!ValidateOrder(party, order.Type, target)) continue;

                    IssueOrder(party, order.Type, target);
                    party.Ai.SetDoNotMakeNewDecisions(true);
                }
            }
            catch (Exception ex) { Log.Error($"[BLT] PartyOrderBehavior.OnMissionEnded error: {ex}"); }
        }

        /// <summary>
        /// Returns false if the settlement cannot be reached by the party's
        /// navigation capability (e.g. island with no naval access).
        /// MapDistanceModel returns float.MaxValue for unreachable paths.
        /// </summary>
        public static bool IsSettlementReachable(MobileParty party, Settlement target)
        {
            try
            {
                // Notes from Claude:
                // NavigationType.Default gives estimatedLandRatio hardcoded to 1f always —
                // it never reflects the actual path. The ratio is only genuinely computed
                // by GetLandRatioOfPathBetweenSettlements when using NavigationType.All,
                // so we use that here for land parties.
                if (party.IsCurrentlyAtSea)
                {
                    float navalDist = Campaign.Current.Models.MapDistanceModel.GetDistance(
                        party, target, true, MobileParty.NavigationType.Naval, out _);
                    return navalDist < float.MaxValue - 1f;
                }

                float dist = Campaign.Current.Models.MapDistanceModel.GetDistance(
                    party, target, false, MobileParty.NavigationType.All, out float landRatio);

                if (dist >= float.MaxValue - 1f)
                    return false;

                // Notes from Claude:
                // landRatio is genuinely path-analyzed here (via GetLandRatioOfPathBetweenSettlements).
                // -1 means it couldn't be computed at all (same-face shortcut with unknown nav type).
                // Below 0.5 means more than half the route is water — a land party will stall.
                if (landRatio >= 0f && landRatio < 0.5f)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] IsSettlementReachable error: {ex}");
                return false;
            }
        }




        // ─────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────

        private void ExpireOrder(PartyOrderData order, string reason, bool notify)
        {
            if (!order.IsActive) return;
            order.IsActive = false;

            var party = MobileParty.All.FirstOrDefault(p => p.StringId == order.PartyId);
            party?.Ai.SetDoNotMakeNewDecisions(false);

            if (notify && reason != null)
                NotifyHero(order, reason);

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
            catch (Exception ex) { Log.Error($"[BLT] PartyOrderBehavior.NotifyHero error: {ex}"); }
        }

        /// <summary>
        /// Issue the appropriate SetPartyAiAction for a given order type.
        /// Called both at order creation and on silent re-issue.
        /// </summary>
        public static void IssueOrder(MobileParty party, PartyOrderType type, Settlement target)
        {
            bool atSea = party.IsCurrentlyAtSea;

            // isFromPort must be true not only when the party is at sea, but also when
            // a land party's target requires a water crossing — otherwise NavigationType.All
            // will attempt to walk/sail without first routing through a port, causing
            // the party to break pathing entirely when exiting a town toward an island target.
            bool needsWaterCrossing = !atSea && target != null && LandPartyNeedsWaterCrossing(party, target);
            bool isFromPort = atSea || needsWaterCrossing;

            MobileParty.NavigationType nav = (atSea || needsWaterCrossing)
                ? MobileParty.NavigationType.Naval
                : MobileParty.NavigationType.All;

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
                            pm.FindBestSettlementToDefend(party, party.LeaderHero.Clan.Kingdom),
                            nav, isFromPort, false);
                    break;
            }
        }

        /// <summary>
        /// Returns true if a land party cannot reach <paramref name="target"/> by land alone
        /// (landRatio below threshold), meaning it needs to embark from a port.
        /// Reuses the same distance model logic as IsSettlementReachable.
        /// </summary>
        private static bool LandPartyNeedsWaterCrossing(MobileParty party, Settlement target)
        {
            try
            {
                float dist = Campaign.Current.Models.MapDistanceModel.GetDistance(
                    party, target, false, MobileParty.NavigationType.All, out float landRatio);

                // Unreachable entirely, or more than half the route is water
                if (dist >= float.MaxValue - 1f) return true;
                if (landRatio >= 0f && landRatio < 0.5f) return true;

                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] LandPartyNeedsWaterCrossing error: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Validate that the prerequisites for re-issuing an order are still met.
        /// </summary>
        public static bool ValidateOrder(MobileParty party, PartyOrderType type, Settlement target)
        {
            if (party.MapEvent != null) return false;

            switch (type)
            {
                case PartyOrderType.Siege:
                    {
                        if (target == null || !target.IsFortification) return false;
                        if (!party.MapFaction.IsAtWarWith(target.MapFaction)) return false;

                        // ── already actively besieging this target → always valid ──────
                        if (party.BesiegedSettlement == target) return true;

                        // ── target is under siege: valid only if WE are the besieger ──
                        if (target.IsUnderSiege)
                            return target.SiegeEvent?.BesiegerCamp?.LeaderParty?.MapFaction
                                   == party.MapFaction;

                        // ── not yet under siege; valid if we're not besieging something else ──
                        return party.BesiegedSettlement == null;
                    }

                case PartyOrderType.Defend:
                    return target != null && target.IsFortification;

                case PartyOrderType.Patrol:
                    return true;

                default:
                    return false;
            }
        }

        private static AiBehavior OrderTypeToAiBehavior(PartyOrderType type) => type switch
        {
            PartyOrderType.Siege => AiBehavior.BesiegeSettlement,
            PartyOrderType.Defend => AiBehavior.DefendSettlement,
            PartyOrderType.Patrol => AiBehavior.PatrolAroundPoint,
            _ => AiBehavior.None
        };

    }
}

// Top-level class — NOT nested inside PartyOrderBehavior.
// Bannerlord's save system scans registered behavior types at save time;
// nested classes produce mangled names that cause save failure even when
// SyncData does nothing.
[Serializable]
public class PartyOrderData
{
    public string HeroId { get; set; }
    public string PartyId { get; set; }
    public int TypeRaw { get; set; } // PartyOrderType as int — custom enums are safer this way
    public string TargetSettlementId { get; set; }
    public double IssuedAtDays { get; set; }
    public double ExpiresAtDays { get; set; }
    public int MaxReissueAttempts { get; set; }
    public int ReissueAttempts { get; set; }
    public bool IsActive { get; set; }

    // Convenience accessor — not serialized, just casts TypeRaw
    public PartyOrderType Type
    {
        get => (PartyOrderType)TypeRaw;
        set => TypeRaw = (int)value;
    }
}