using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero
{
    public enum PartyOrderType { Siege, Defend, Patrol }

    public class PartyOrderBehavior : CampaignBehaviorBase
    {
        public static PartyOrderBehavior Current { get; private set; }

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
        }

        public override void SyncData(IDataStore dataStore)
        {
            //try
            //{
            //    dataStore.SyncData("BLT_PartyOrders", ref _orders);
            //    _orders ??= new List<PartyOrderData>();
            //
            //    if (!dataStore.IsLoading) return;
            //
            //    _orders.RemoveAll(o => o == null || string.IsNullOrEmpty(o.PartyId));
            //
            //    // Re-apply AI locks on surviving active orders
            //    foreach (var order in _orders.Where(o => o.IsActive))
            //    {
            //        var party = MobileParty.All.FirstOrDefault(p => p.StringId == order.PartyId);
            //        if (party == null || !party.IsActive)
            //        {
            //            order.IsActive = false;
            //            continue;
            //        }
            //        party.Ai.SetDoNotMakeNewDecisions(true);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Log.Error($"[BLT] PartyOrderBehavior.SyncData error: {ex}");
            //    _orders = new List<PartyOrderData>();
            //}
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

        public PartyOrderData GetActiveOrder(string partyId) =>
            _orders.FirstOrDefault(o => o.IsActive && o.PartyId == partyId);

        // ─────────────────────────────────────────────
        //  HOURLY MONITORING
        // ─────────────────────────────────────────────

        private void OnHourlyTickParty(MobileParty party)
        {
            try
            {
                // Skip mercenary parties — MercenaryArmyBehavior owns those
                //if (MercenaryArmyPatches.IsMercenaryParty(party)) return;

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

                // Order is holding — reset reissue counter
                if (party.DefaultBehavior == expectedBehavior)
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
            var nav = party.IsCurrentlyAtSea
                ? MobileParty.NavigationType.Naval
                : MobileParty.NavigationType.Default;
            bool atSea = party.IsCurrentlyAtSea;

            switch (type)
            {
                case PartyOrderType.Siege:
                    if (target != null)
                        SetPartyAiAction.GetActionForBesiegingSettlement(party, target, nav, atSea);
                    break;
                case PartyOrderType.Defend:
                    if (target != null)
                        SetPartyAiAction.GetActionForDefendingSettlement(party, target, nav, atSea, false);
                    break;
                case PartyOrderType.Patrol:
                    if (target != null)
                        SetPartyAiAction.GetActionForPatrollingAroundSettlement(party, target, nav, atSea, false);
                    else
                        SetPartyAiAction.GetActionForPatrollingAroundPoint(party, new CampaignVec2(party.GetPosition2D, !atSea), nav, atSea);
                    break;
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
                    return target != null
                        && target.IsFortification
                        && !target.IsUnderSiege
                        && party.MapFaction.IsAtWarWith(target.MapFaction)
                        && party.BesiegedSettlement == null;
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

        // ─────────────────────────────────────────────
        //  DATA
        // ─────────────────────────────────────────────

        [Serializable]
        public class PartyOrderData
        {
            public string HeroId { get; set; }
            public string PartyId { get; set; }
            public PartyOrderType Type { get; set; }
            public string TargetSettlementId { get; set; }
            public double IssuedAtDays { get; set; }
            public double ExpiresAtDays { get; set; }
            public int MaxReissueAttempts { get; set; }
            public int ReissueAttempts { get; set; }
            public bool IsActive { get; set; }
        }
    }
}