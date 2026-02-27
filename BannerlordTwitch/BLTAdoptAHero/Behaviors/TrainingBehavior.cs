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
        }

        private Dictionary<string, TrainingEntry> _funds = new();

        public TrainingBehavior()
        {
            Current = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, OnDailyTickHero);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("BLT_TrainingFunds", ref _funds);
            _funds ??= new Dictionary<string, TrainingEntry>();
            Current = this;
        }

        // ─────────────────────────────────────────────

        public TrainingEntry GetEntry(Hero h)
        {
            if (h == null) return null;
            return _funds.TryGetValue(h.StringId, out var e) ? e : null;
        }

        public void AddFund(Hero h, int gold)
        {
            if (h == null || gold <= 0) return;

            if (!_funds.TryGetValue(h.StringId, out var entry))
            {
                entry = new TrainingEntry();
                _funds[h.StringId] = entry;
            }

            entry.Fund += gold;
        }

        public int CancelFund(Hero h)
        {
            if (h == null) return 0;

            if (!_funds.TryGetValue(h.StringId, out var entry))
                return 0;

            int refund = entry.Fund;
            _funds.Remove(h.StringId);
            return refund;
        }

        public static int ComputeDailyBudget(TrainingEntry entry)
        {
            if (entry == null || entry.Fund <= 0)
                return 0;

            int cap = GlobalCommonConfig.Get().TrainMaxDailySpend > 0
                ? GlobalCommonConfig.Get().TrainMaxDailySpend
                : entry.Fund;

            return Math.Min(entry.Fund, cap);
        }

        // ─────────────────────────────────────────────

        private void OnDailyTickHero(Hero h)
        {
            if (h == null) return;

            if (!_funds.TryGetValue(h.StringId, out var entry))
                return;

            if (entry.Fund <= 0)
            {
                _funds.Remove(h.StringId);
                return;
            }

            var party = h.PartyBelongedTo;

            if (party == null ||
                party.LeaderHero != h ||
                party.MapEvent != null ||
                party.IsDisbanding)
                return;

            int budget = ComputeDailyBudget(entry);
            if (budget <= 0) return;

            int spent = ProcessUpgrades(party, budget);

            if (spent > 0)
            {
                entry.Fund -= spent;

                if (entry.Fund <= 0)
                    _funds.Remove(h.StringId);
            }
        }

        // ─────────────────────────────────────────────

        private static int ProcessUpgrades(MobileParty party, int budget)
        {
            var model = Campaign.Current.Models.PartyTroopUpgradeModel;
            if (model == null) return 0;

            int spent = 0;

            var candidates = party.MemberRoster.GetTroopRoster()
                .Where(slot =>
                    !slot.Character.IsHero &&
                    slot.Number > slot.WoundedNumber &&
                    slot.Character.UpgradeTargets.ToList().Count > 0)
                .OrderBy(slot => slot.Character.Tier)
                .ToList();

            foreach (var slot in candidates)
            {
                if (spent >= budget)
                    break;

                var troop = slot.Character;

                var target = PickBestUpgradeTarget(party, troop, model);
                if (target == null)
                    continue;

                float multiplier = GlobalCommonConfig.Get().TrainGoldCostMultiplier;

                int baseCost = Math.Max(1,
                    (int)model.GetGoldCostForUpgrade(party.Party, troop, target).ResultNumber);

                int goldPer = Math.Max(1, (int)Math.Ceiling(baseCost * multiplier));

                int healthy = slot.Number - slot.WoundedNumber;
                int affordable = (budget - spent) / goldPer;
                int amount = Math.Min(healthy, affordable);

                if (amount <= 0)
                    continue;

                party.MemberRoster.RemoveTroop(troop, amount);
                party.MemberRoster.AddToCounts(target, amount);

                spent += amount * goldPer;
            }

            return spent;
        }

        private static CharacterObject PickBestUpgradeTarget(
            MobileParty party,
            CharacterObject troop,
            PartyTroopUpgradeModel model)
        {
            var targets = troop.UpgradeTargets;
            if (targets == null || targets.Length == 0)
                return null;

            if (targets.Length == 1)
                return targets[0];

            CharacterObject best = null;
            float bestWeight = -1f;

            for (int i = 0; i < targets.Length; i++)
            {
                float weight = model.GetUpgradeChanceForTroopUpgrade(party.Party, troop, i);
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    best = targets[i];
                }
            }

            return best;
        }
    }
}