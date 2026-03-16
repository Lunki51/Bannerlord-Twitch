using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using BannerlordTwitch.Util;
using TaleWorlds.Library;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Manages armies created by independent clans (Army.Kingdom = null).
    /// Because these armies are not registered on any Kingdom, we track them here.
    /// The Army objects themselves are saved/loaded by the game's own save system;
    /// we only need to rebuild our tracking list on load by scanning MobileParty.All.
    /// </summary>
    public class BLTClanArmyBehavior : CampaignBehaviorBase
    {
        public static BLTClanArmyBehavior Current { get; private set; }

        // Runtime-only — rebuilt each load from the live world state.
        [NonSerialized] private readonly List<Army> _clanArmies = new();

        public BLTClanArmyBehavior() { Current = this; }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public override void RegisterEvents()
        {
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, OnArmyDispersed);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No fields to persist — Army saves itself.  On load, rebuild from world.
            if (dataStore.IsLoading)
                RebuildFromWorld();
        }

        private void RebuildFromWorld()
        {
            _clanArmies.Clear();
            var seen = new HashSet<Army>();
            foreach (var mp in MobileParty.All)
            {
                if (mp?.Army == null || seen.Contains(mp.Army)) continue;
                // A clan army: no kingdom, this party IS the leader
                if (mp.Army.Kingdom == null && mp.Army.LeaderParty == mp)
                {
                    _clanArmies.Add(mp.Army);
                    seen.Add(mp.Army);
                }
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Creates an Army for an independent clan (no kingdom required).
        /// Passes null as the kingdom to the Army constructor — the setter is
        /// null-safe so this is safe; the Army simply won't appear in any
        /// Kingdom.Armies list and is tracked here instead.
        /// </summary>
        public static Army CreateClanArmy(Hero armyLeader, Settlement gatherPoint,
            Army.ArmyTypes armyType, MBReadOnlyList<MobileParty> partiesToCall = null)
        {
            try
            {
                if (armyLeader?.PartyBelongedTo?.LeaderHero != armyLeader)
                {
                    Log.Error("[BLT] CreateClanArmy: armyLeader is not leading their party.");
                    return null;
                }

                // Army(null, ...) is safe: the Kingdom setter short-circuits when value == _kingdom (both null).
                var army = new Army(null, armyLeader.PartyBelongedTo, armyType);

                // Gather calls FindBestGatheringSettlementAndMoveTheLeader internally;
                // our Harmony patch (BLT_ClanArmyFindGatheringPatch) intercepts that for null-kingdom armies.
                army.Gather(gatherPoint, partiesToCall);

                Current?._clanArmies.Add(army);
                CampaignEventDispatcher.Instance.OnArmyCreated(army);
                return army;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] CreateClanArmy error: {ex}");
                return null;
            }
        }

        /// <summary>All clan armies led by parties in the given clan.</summary>
        public List<Army> GetClanArmies(Clan clan) =>
            _clanArmies.Where(a => a?.LeaderParty?.ActualClan == clan).ToList();

        public bool HasClanArmy(Clan clan) =>
            _clanArmies.Any(a => a?.LeaderParty?.ActualClan == clan);

        public Army GetClanArmy(Clan clan) =>
            _clanArmies.FirstOrDefault(a => a?.LeaderParty?.ActualClan == clan);

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayerArmy)
        {
            _clanArmies.Remove(army);
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            try
            {
                if (clan == null || newKingdom == null) return;

                // Disband the clan's own independent army — it is now part of a kingdom
                // and should use the kingdom army system instead.
                var ownArmy = GetClanArmy(clan);
                if (ownArmy != null)
                    DisbandArmyAction.ApplyByUnknownReason(ownArmy);
                // OnArmyDispersed handles list cleanup.

                // Remove this clan's parties from any other clan armies they were attached to.
                foreach (var army in _clanArmies.ToList())
                {
                    if (army?.LeaderParty == null) continue;
                    foreach (var mp in army.Parties
                                 .Where(p => p.ActualClan == clan && p != army.LeaderParty)
                                 .ToList())
                    {
                        mp.Army = null;
                        mp.AttachedTo = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] BLTClanArmyBehavior.OnClanChangedKingdom error: {ex}");
            }
        }

        // ── Gathering helper (consumed by BLT_ClanArmyFindGatheringPatch) ─────

        /// <summary>
        /// Picks the best settlement for a clan army to gather at when it has no kingdom.
        /// Priority:
        ///   1. Own clan fortifications (not under siege)
        ///   2. Allied clan fortifications via BLTClanDiplomacyBehavior (not yet online — null-guarded)
        ///   3. Nearest non-hostile fortification to the leader party's current position
        /// Returns null only when the world contains no suitable settlement at all.
        /// </summary>
        public static Settlement FindClanGatherSettlement(Army army)
        {
            var leader = army.LeaderParty;
            var clan = leader?.LeaderHero?.Clan;
            if (clan == null) return null;

            Settlement best = null;
            float bestScore = -1f;

            void Consider(Settlement s, float bonus)
            {
                if (s == null || !s.IsFortification || s.IsUnderSiege) return;
                float dist = Campaign.Current.Models.MapDistanceModel
                    .GetDistance(leader, s, false, leader.NavigationCapability, out _);
                if (dist >= Campaign.MapDiagonalSquared) return;
                float score = bonus + 10000f / (dist + 1f);
                if (score > bestScore) { bestScore = score; best = s; }
            }

            // 1. Own fiefs
            foreach (var fief in clan.Fiefs)
                Consider(fief.Settlement, 200f);

            // 2. Allied clan fiefs — BLTClanDiplomacyBehavior is added in a later step;
            //    the null-check keeps this compile-safe until then.
            if (BLTClanDiplomacyBehavior.Current != null)
                foreach (var allied in BLTClanDiplomacyBehavior.Current.GetAlliedClans(clan))
                    foreach (var fief in allied.Fiefs)
                        Consider(fief.Settlement, 100f);

            if (best != null) return best;

            // 3. Nearest non-hostile fortification
            var pos = leader.GetPosition2D;
            return Settlement.All
                .Where(s => s.IsFortification && !s.IsUnderSiege
                            && !s.MapFaction.IsAtWarWith(clan))
                .OrderBy(s => s.GetPosition2D.Distance(pos))
                .FirstOrDefault();
        }
    }
}