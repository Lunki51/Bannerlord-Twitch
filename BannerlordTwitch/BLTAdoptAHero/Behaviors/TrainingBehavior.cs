using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace BLTAdoptAHero.Actions
{
    public class TrainingBehavior : CampaignBehaviorBase
    {
        public static TrainingBehavior Current { get; private set; }

        public class TrainingEntry
        {
            public int Fund;
            /// <summary>
            /// 0 = no cap.  When > 0 the leader's party is upgraded only up to this tier,
            /// and once it is satisfied the daily budget spills over to a random free clan party.
            /// </summary>
            public int MaxTier;
        }

        private Dictionary<string, TrainingEntry> _funds = new();

        public TrainingBehavior() { Current = this; }

        // ─────────────────────────────────────────────
        //  LIFECYCLE
        // ─────────────────────────────────────────────

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, OnDailyTickHero);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                var keys = _funds?.Keys.ToList() ?? new List<string>();
                var values = _funds?.Values.Select(e => e.Fund).ToList() ?? new List<int>();
                var tiers = _funds?.Values.Select(e => e.MaxTier).ToList() ?? new List<int>();
                dataStore.SyncData("BLT_TrainingFunds_Keys", ref keys);
                dataStore.SyncData("BLT_TrainingFunds_Values", ref values);
                dataStore.SyncData("BLT_TrainingFunds_Tiers", ref tiers);
            }
            else
            {
                List<string> keys = null;
                List<int> values = null;
                List<int> tiers = null;
                dataStore.SyncData("BLT_TrainingFunds_Keys", ref keys);
                dataStore.SyncData("BLT_TrainingFunds_Values", ref values);
                dataStore.SyncData("BLT_TrainingFunds_Tiers", ref tiers);

                if (keys != null && values != null && keys.Count == values.Count)
                {
                    _funds = new Dictionary<string, TrainingEntry>();
                    for (int i = 0; i < keys.Count; i++)
                    {
                        _funds[keys[i]] = new TrainingEntry
                        {
                            Fund = values[i],
                            // Tiers list may be absent in saves from before this change
                            MaxTier = (tiers != null && i < tiers.Count) ? tiers[i] : 0
                        };
                    }
                }
                else
                    _funds = new Dictionary<string, TrainingEntry>();
            }

            Current = this;
        }

        // ─────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────

        public TrainingEntry GetEntry(Hero h)
        {
            if (h == null) return null;
            return _funds.TryGetValue(h.StringId, out var e) ? e : null;
        }

        /// <summary>
        /// Add gold to the training fund, optionally setting (or updating) the tier cap.
        /// Calling with maxTier = 0 leaves any existing cap unchanged.
        /// </summary>
        public void AddFund(Hero h, int gold, int maxTier = 0)
        {
            if (h == null || gold <= 0) return;

            if (!_funds.TryGetValue(h.StringId, out var entry))
            {
                entry = new TrainingEntry();
                _funds[h.StringId] = entry;
            }

            entry.Fund += gold;

            // Always update the tier cap when a non-zero value is supplied so
            // the streamer can change it simply by investing more gold.
            if (maxTier > 0)
                entry.MaxTier = maxTier;
        }

        public int CancelFund(Hero h)
        {
            if (h == null) return 0;
            if (!_funds.TryGetValue(h.StringId, out var entry)) return 0;
            int refund = entry.Fund;
            _funds.Remove(h.StringId);
            return refund;
        }

        public static int ComputeDailyBudget(TrainingEntry entry)
        {
            if (entry == null || entry.Fund <= 0) return 0;

            int cap = GlobalCommonConfig.Get().TrainMaxDailySpend > 0
                ? GlobalCommonConfig.Get().TrainMaxDailySpend
                : entry.Fund;

            return Math.Min(entry.Fund, cap);
        }

        // ─────────────────────────────────────────────
        //  DAILY TICK
        // ─────────────────────────────────────────────

        private void OnDailyTickHero(Hero h)
        {
            if (h == null) return;
            if (!_funds.TryGetValue(h.StringId, out var entry)) return;

            if (entry.Fund <= 0)
            {
                _funds.Remove(h.StringId);
                return;
            }

            var leaderParty = h.PartyBelongedTo;
            if (leaderParty == null ||
                leaderParty.LeaderHero != h ||
                leaderParty.MapEvent != null ||
                leaderParty.IsDisbanding)
                return;

            int budget = ComputeDailyBudget(entry);
            if (budget <= 0) return;

            int spent;

            if (entry.MaxTier <= 0)
            {
                // ── Original behaviour: no cap, only the leader's party ──────────
                spent = ProcessUpgrades(leaderParty, budget, maxTier: 0);
            }
            else
            {
                // ── MaxTier mode ─────────────────────────────────────────────────
                // Step 1: bring the leader's own party up to MaxTier first.
                if (!PartyIsAtOrAboveTier(leaderParty, entry.MaxTier))
                {
                    spent = ProcessUpgrades(leaderParty, budget, entry.MaxTier);
                }
                else
                {
                    // Step 2: leader's party is satisfied — spill over to a random
                    // free clan party that still has troops below MaxTier.
                    var spillTarget = PickSpilloverParty(h, leaderParty, entry.MaxTier);
                    if (spillTarget != null)
                        spent = ProcessUpgrades(spillTarget, budget, entry.MaxTier);
                    else
                        spent = 0; // Everyone is capped; let the fund sit until it's cancelled.
                }
            }

            if (spent > 0)
            {
                entry.Fund -= spent;
                if (entry.Fund <= 0)
                    _funds.Remove(h.StringId);
            }
        }

        // ─────────────────────────────────────────────
        //  SPILLOVER PARTY SELECTION
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns a random free clan party (not the leader's own party) that still
        /// has at least one troop below <paramref name="maxTier"/>.
        /// "Free" means not in a map event, not disbanding, and not in someone else's army
        /// (armies led by the hero himself are fine — the hero's own party is already
        /// excluded by the caller).
        /// Returns null if every eligible party is already at or above the tier cap.
        /// </summary>
        private static MobileParty PickSpilloverParty(Hero h, MobileParty leaderParty, int maxTier)
        {
            var eligible = h.Clan.WarPartyComponents
                .Select(wpc => wpc?.MobileParty)
                .Where(mp => mp != null
                    && mp != leaderParty
                    && mp.LeaderHero != null
                    && mp.IsLordParty
                    && mp.MapEvent == null
                    && !mp.IsDisbanding
                    && mp.MemberRoster.TotalHealthyCount > 0
                    && !PartyIsAtOrAboveTier(mp, maxTier))
                .ToList();

            if (eligible.Count == 0) return null;

            // Pick one at random each day so over time all clan parties get upgraded.
            return eligible[MBRandom.RandomInt(eligible.Count)];
        }

        // ─────────────────────────────────────────────
        //  TIER SATISFACTION CHECK
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns true when every non-hero troop in the party's healthy roster has
        /// a tier >= <paramref name="minTier"/>, OR when the party has no non-hero troops.
        /// Wounded troops are excluded — they don't count toward the cap threshold so
        /// the fund isn't wasted waiting for casualties to heal.
        /// </summary>
        private static bool PartyIsAtOrAboveTier(MobileParty party, int minTier)
        {
            foreach (var slot in party.MemberRoster.GetTroopRoster())
            {
                if (slot.Character.IsHero) continue;
                int healthy = slot.Number - slot.WoundedNumber;
                if (healthy <= 0) continue;
                if (slot.Character.Tier < minTier) return false;
            }
            return true;
        }

        // ─────────────────────────────────────────────
        //  UPGRADE PROCESSING
        // ─────────────────────────────────────────────

        /// <summary>
        /// Spends up to <paramref name="budget"/> gold upgrading troops in <paramref name="party"/>.
        /// When <paramref name="maxTier"/> &gt; 0, troops already at or above that tier are skipped.
        /// Returns the total gold spent.
        /// </summary>
        private static int ProcessUpgrades(MobileParty party, int budget, int maxTier)
        {
            var model = Campaign.Current.Models.PartyTroopUpgradeModel;
            if (model == null) return 0;

            int spent = 0;

            var candidates = party.MemberRoster.GetTroopRoster()
                .Where(slot =>
                    !slot.Character.IsHero &&
                    slot.Number > slot.WoundedNumber &&
                    slot.Character.UpgradeTargets != null &&
                    slot.Character.UpgradeTargets.Length > 0 &&
                    // Skip troops already at or above the tier cap (0 = no cap)
                    (maxTier <= 0 || slot.Character.Tier < maxTier))
                .OrderBy(slot => slot.Character.Tier)
                .ToList();

            foreach (var slot in candidates)
            {
                if (spent >= budget) break;

                var troop = slot.Character;

                // If upgrading would push past the cap, skip this troop.
                // e.g. a tier-4 troop whose only upgrade target is tier 5 is fine at cap=5,
                // but a tier-5 troop at cap=5 would already be filtered above.
                if (maxTier > 0)
                {
                    var bestTarget = PickBestUpgradeTarget(party, troop, model);
                    if (bestTarget == null || bestTarget.Tier > maxTier) continue;
                }

                var target = PickBestUpgradeTarget(party, troop, model);
                if (target == null) continue;

                float multiplier = GlobalCommonConfig.Get().TrainGoldCostMultiplier;
                int baseCost = Math.Max(1, (int)model.GetGoldCostForUpgrade(party.Party, troop, target).ResultNumber);
                int goldPer = Math.Max(1, (int)Math.Ceiling(baseCost * multiplier));

                int healthy = slot.Number - slot.WoundedNumber;
                int affordable = (budget - spent) / goldPer;
                int amount = Math.Min(healthy, affordable);

                if (amount <= 0) continue;

                party.MemberRoster.RemoveTroop(troop, amount);
                party.MemberRoster.AddToCounts(target, amount);

                spent += amount * goldPer;
            }

            return spent;
        }

        private static CharacterObject PickBestUpgradeTarget(
            MobileParty party, CharacterObject troop, PartyTroopUpgradeModel model)
        {
            var targets = troop.UpgradeTargets;
            if (targets == null || targets.Length == 0) return null;
            if (targets.Length == 1) return targets[0];

            CharacterObject best = null;
            float bestWeight = -1f;
            for (int i = 0; i < targets.Length; i++)
            {
                float weight = model.GetUpgradeChanceForTroopUpgrade(party.Party, troop, i);
                if (weight > bestWeight) { bestWeight = weight; best = targets[i]; }
            }
            return best;
        }
    }
}