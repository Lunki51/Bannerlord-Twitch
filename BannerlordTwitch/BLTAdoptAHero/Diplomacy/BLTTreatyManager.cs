using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero
{
    public class BLTTreatyManager : CampaignBehaviorBase
    {
        public static BLTTreatyManager Current { get; private set; }

        // Runtime storage
        private Dictionary<string, BLTTruce> _truces = new Dictionary<string, BLTTruce>();
        private Dictionary<string, BLTNAP> _naps = new Dictionary<string, BLTNAP>();
        private Dictionary<string, BLTAlliance> _alliances = new Dictionary<string, BLTAlliance>();
        private Dictionary<string, BLTTribute> _tributes = new Dictionary<string, BLTTribute>();
        private Dictionary<string, BLTWar> _wars = new Dictionary<string, BLTWar>();
        private Dictionary<string, BLTCTWProposal> _ctwProposals = new Dictionary<string, BLTCTWProposal>();
        private Dictionary<string, BLTPeaceProposal> _peaceProposals = new Dictionary<string, BLTPeaceProposal>();
        private Dictionary<string, BLTAllianceProposal> _allianceProposals = new Dictionary<string, BLTAllianceProposal>();
        private Dictionary<string, BLTTradeProposal> _tradeProposals = new Dictionary<string, BLTTradeProposal>();
        private Dictionary<string, BLTNAPProposal> _napProposals = new Dictionary<string, BLTNAPProposal>();
        private Dictionary<string, CampaignTime> _ctwCooldowns = new Dictionary<string, CampaignTime>();

        // Serialization lists using NumTicks for CampaignTime
        // Truces
        private List<string> _truceKeys = new List<string>();
        private List<string> _truceK1 = new List<string>();
        private List<string> _truceK2 = new List<string>();
        private List<long> _truceStartTicks = new List<long>();
        private List<long> _truceExpireTicks = new List<long>();

        // NAPs
        private List<string> _napKeys = new List<string>();
        private List<string> _napK1 = new List<string>();
        private List<string> _napK2 = new List<string>();
        private List<long> _napStartTicks = new List<long>();

        // Alliances
        private List<string> _allianceKeys = new List<string>();
        private List<string> _allianceK1 = new List<string>();
        private List<string> _allianceK2 = new List<string>();
        private List<long> _allianceStartTicks = new List<long>();

        // Tributes
        private List<string> _tributeKeys = new List<string>();
        private List<string> _tributeK1 = new List<string>();
        private List<string> _tributeK2 = new List<string>();
        private List<string> _tributePayer = new List<string>();
        private List<int> _tributeAmount = new List<int>();
        private List<int> _tributeRemaining = new List<int>();
        private List<long> _tributeExpirationTicks = new List<long>();
        private List<long> _tributeStartTicks = new List<long>();

        // Wars
        private List<string> _warKeys = new List<string>();
        private List<string> _warAttacker = new List<string>();
        private List<string> _warDefender = new List<string>();
        private List<string> _warAttackerAllies = new List<string>(); // CSV
        private List<string> _warDefenderAllies = new List<string>(); // CSV
        private List<long> _warStartTicks = new List<long>();
        public int MinWarDurationDays { get; set; } = 30;

        // Peace Proposals
        private List<string> _peaceProposalKeys = new List<string>();
        private List<string> _peaceProposerIds = new List<string>();
        private List<string> _peaceTargetIds = new List<string>();
        private List<bool> _peaceIsOffer = new List<bool>();
        private List<int> _peaceTribute = new List<int>();
        private List<int> _peaceDuration = new List<int>();
        private List<int> _peaceGoldCost = new List<int>();
        private List<int> _peaceInfluenceCost = new List<int>();
        private List<long> _peaceExpireTicks = new List<long>();

        // Alliance Proposals
        private List<string> _allianceProposalKeys = new List<string>();
        private List<string> _allianceProposerIds = new List<string>();
        private List<string> _allianceTargetIds = new List<string>();
        private List<int> _allianceGoldCost = new List<int>();
        private List<int> _allianceInfluenceCost = new List<int>();
        private List<long> _allianceExpireTicks = new List<long>();

        // Trade Proposals
        private List<string> _tradeProposalKeys = new List<string>();
        private List<string> _tradeProposerIds = new List<string>();
        private List<string> _tradeTargetIds = new List<string>();
        private List<int> _tradeGoldCost = new List<int>();
        private List<int> _tradeInfluenceCost = new List<int>();
        private List<long> _tradeExpireTicks = new List<long>();

        // NAP Proposals
        private List<string> _napProposalKeys = new List<string>();
        private List<string> _napProposerIds = new List<string>();
        private List<string> _napTargetIds = new List<string>();
        private List<int> _napGoldCost = new List<int>();
        private List<int> _napInfluenceCost = new List<int>();
        private List<long> _napExpireTicks = new List<long>();

        // CTW Cooldowns
        private List<string> _ctwCooldownKeys = new List<string>();
        private List<long> _ctwCooldownTicks = new List<long>();

        public BLTTreatyManager()
        {
            Current = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Truces
            dataStore.SyncData("_truceKeys", ref _truceKeys);
            dataStore.SyncData("_truceK1", ref _truceK1);
            dataStore.SyncData("_truceK2", ref _truceK2);
            dataStore.SyncData("_truceStartTicks", ref _truceStartTicks);
            dataStore.SyncData("_truceExpireTicks", ref _truceExpireTicks);

            // NAPs
            dataStore.SyncData("_napKeys", ref _napKeys);
            dataStore.SyncData("_napK1", ref _napK1);
            dataStore.SyncData("_napK2", ref _napK2);
            dataStore.SyncData("_napStartTicks", ref _napStartTicks);

            // Alliances
            dataStore.SyncData("_allianceKeys", ref _allianceKeys);
            dataStore.SyncData("_allianceK1", ref _allianceK1);
            dataStore.SyncData("_allianceK2", ref _allianceK2);
            dataStore.SyncData("_allianceStartTicks", ref _allianceStartTicks);

            // Tributes
            dataStore.SyncData("_tributeKeys", ref _tributeKeys);
            dataStore.SyncData("_tributeK1", ref _tributeK1);
            dataStore.SyncData("_tributeK2", ref _tributeK2);
            dataStore.SyncData("_tributePayer", ref _tributePayer);
            dataStore.SyncData("_tributeAmount", ref _tributeAmount);
            dataStore.SyncData("_tributeRemaining", ref _tributeRemaining);
            dataStore.SyncData("_tributeStartTicks", ref _tributeStartTicks);

            // Wars
            dataStore.SyncData("_warKeys", ref _warKeys);
            dataStore.SyncData("_warAttacker", ref _warAttacker);
            dataStore.SyncData("_warDefender", ref _warDefender);
            dataStore.SyncData("_warAttackerAllies", ref _warAttackerAllies);
            dataStore.SyncData("_warDefenderAllies", ref _warDefenderAllies);
            dataStore.SyncData("_warStartTicks", ref _warStartTicks);

            // Peace Proposals
            dataStore.SyncData("_peaceProposalKeys", ref _peaceProposalKeys);
            dataStore.SyncData("_peaceProposerIds", ref _peaceProposerIds);
            dataStore.SyncData("_peaceTargetIds", ref _peaceTargetIds);
            dataStore.SyncData("_peaceIsOffer", ref _peaceIsOffer);
            dataStore.SyncData("_peaceTribute", ref _peaceTribute);
            dataStore.SyncData("_peaceDuration", ref _peaceDuration);
            dataStore.SyncData("_peaceGoldCost", ref _peaceGoldCost);
            dataStore.SyncData("_peaceInfluenceCost", ref _peaceInfluenceCost);
            dataStore.SyncData("_peaceExpireTicks", ref _peaceExpireTicks);

            // Alliance Proposals
            dataStore.SyncData("_allianceProposalKeys", ref _allianceProposalKeys);
            dataStore.SyncData("_allianceProposerIds", ref _allianceProposerIds);
            dataStore.SyncData("_allianceTargetIds", ref _allianceTargetIds);
            dataStore.SyncData("_allianceGoldCost", ref _allianceGoldCost);
            dataStore.SyncData("_allianceInfluenceCost", ref _allianceInfluenceCost);
            dataStore.SyncData("_allianceExpireTicks", ref _allianceExpireTicks);

            // Trade Proposals
            dataStore.SyncData("_tradeProposalKeys", ref _tradeProposalKeys);
            dataStore.SyncData("_tradeProposerIds", ref _tradeProposerIds);
            dataStore.SyncData("_tradeTargetIds", ref _tradeTargetIds);
            dataStore.SyncData("_tradeGoldCost", ref _tradeGoldCost);
            dataStore.SyncData("_tradeInfluenceCost", ref _tradeInfluenceCost);
            dataStore.SyncData("_tradeExpireTicks", ref _tradeExpireTicks);

            // NAP Proposals
            dataStore.SyncData("_napProposalKeys", ref _napProposalKeys);
            dataStore.SyncData("_napProposerIds", ref _napProposerIds);
            dataStore.SyncData("_napTargetIds", ref _napTargetIds);
            dataStore.SyncData("_napGoldCost", ref _napGoldCost);
            dataStore.SyncData("_napInfluenceCost", ref _napInfluenceCost);
            dataStore.SyncData("_napExpireTicks", ref _napExpireTicks);

            // CTW Cooldowns
            dataStore.SyncData("_ctwCooldownKeys", ref _ctwCooldownKeys);
            dataStore.SyncData("_ctwCooldownTicks", ref _ctwCooldownTicks);

            if (dataStore.IsLoading)
            {
                LoadFromLists();
            }
            else if (dataStore.IsSaving)
            {
                SaveToLists();
            }
        }

        private void SaveToLists()
        {
            // Clear all lists
            _truceKeys.Clear(); _truceK1.Clear(); _truceK2.Clear(); _truceStartTicks.Clear(); _truceExpireTicks.Clear();
            _napKeys.Clear(); _napK1.Clear(); _napK2.Clear(); _napStartTicks.Clear();
            _allianceKeys.Clear(); _allianceK1.Clear(); _allianceK2.Clear(); _allianceStartTicks.Clear();
            _tributeKeys.Clear(); _tributeK1.Clear(); _tributeK2.Clear(); _tributePayer.Clear(); _tributeAmount.Clear(); _tributeRemaining.Clear(); _tributeStartTicks.Clear();
            _warKeys.Clear(); _warAttacker.Clear(); _warDefender.Clear(); _warAttackerAllies.Clear(); _warDefenderAllies.Clear(); _warStartTicks.Clear();
            _peaceProposalKeys.Clear(); _peaceProposerIds.Clear(); _peaceTargetIds.Clear(); _peaceIsOffer.Clear(); _peaceTribute.Clear(); _peaceDuration.Clear(); _peaceGoldCost.Clear(); _peaceInfluenceCost.Clear(); _peaceExpireTicks.Clear();
            _allianceProposalKeys.Clear(); _allianceProposerIds.Clear(); _allianceTargetIds.Clear(); _allianceGoldCost.Clear(); _allianceInfluenceCost.Clear(); _allianceExpireTicks.Clear();
            _tradeProposalKeys.Clear(); _tradeProposerIds.Clear(); _tradeTargetIds.Clear(); _tradeGoldCost.Clear(); _tradeInfluenceCost.Clear(); _tradeExpireTicks.Clear();
            _napProposalKeys.Clear(); _napProposerIds.Clear(); _napTargetIds.Clear(); _napGoldCost.Clear(); _napInfluenceCost.Clear(); _napExpireTicks.Clear();
            _ctwCooldownKeys.Clear(); _ctwCooldownTicks.Clear();

            // Truces
            foreach (var kvp in _truces)
            {
                _truceKeys.Add(kvp.Key);
                _truceK1.Add(kvp.Value.Kingdom1Id);
                _truceK2.Add(kvp.Value.Kingdom2Id);
                _truceStartTicks.Add((long)kvp.Value.StartDate.ToDays);
                _truceExpireTicks.Add((long)kvp.Value.ExpirationDate.ToDays);
            }

            // NAPs
            foreach (var kvp in _naps)
            {
                _napKeys.Add(kvp.Key);
                _napK1.Add(kvp.Value.Kingdom1Id);
                _napK2.Add(kvp.Value.Kingdom2Id);
                _napStartTicks.Add((long)kvp.Value.StartDate.ToDays);
            }

            // Alliances
            foreach (var kvp in _alliances)
            {
                _allianceKeys.Add(kvp.Key);
                _allianceK1.Add(kvp.Value.Kingdom1Id);
                _allianceK2.Add(kvp.Value.Kingdom2Id);
                _allianceStartTicks.Add((long)kvp.Value.StartDate.ToDays);
            }

            // Tributes
            foreach (var kvp in _tributes)
            {
                _tributeKeys.Add(kvp.Key);
                _tributeK1.Add(kvp.Value.Kingdom1Id);
                _tributeK2.Add(kvp.Value.Kingdom2Id);
                _tributePayer.Add(kvp.Value.PayerKingdomId);
                _tributeAmount.Add(kvp.Value.DailyAmount);

                // Save expiration as absolute ticks (ToDays gives double, cast to long)
                _tributeExpirationTicks.Add((long)kvp.Value.ExpirationDate.ToDays);

                // Save start date for reference if needed
                _tributeStartTicks.Add((long)kvp.Value.StartDate.ToDays);
            }

            // Wars
            foreach (var kvp in _wars)
            {
                _warKeys.Add(kvp.Key);
                _warAttacker.Add(kvp.Value.Attacker1Id);
                _warDefender.Add(kvp.Value.Defender1Id);
                _warAttackerAllies.Add(string.Join(",", kvp.Value.Attacker1AlliesIds));
                _warDefenderAllies.Add(string.Join(",", kvp.Value.Defender1AlliesIds));
                _warStartTicks.Add((long)kvp.Value.StartDate.ToDays);
            }

            // Peace Proposals
            foreach (var kvp in _peaceProposals)
            {
                _peaceProposalKeys.Add(kvp.Key);
                _peaceProposerIds.Add(kvp.Value.ProposerKingdomId);
                _peaceTargetIds.Add(kvp.Value.TargetKingdomId);
                _peaceIsOffer.Add(kvp.Value.IsOffer);
                _peaceTribute.Add(kvp.Value.DailyTribute);
                _peaceDuration.Add(kvp.Value.Duration);
                _peaceGoldCost.Add(kvp.Value.GoldCost);
                _peaceInfluenceCost.Add(kvp.Value.InfluenceCost);
                _peaceExpireTicks.Add((long)kvp.Value.ExpirationDate.ToDays);
            }

            // Alliance Proposals
            foreach (var kvp in _allianceProposals)
            {
                _allianceProposalKeys.Add(kvp.Key);
                _allianceProposerIds.Add(kvp.Value.ProposerKingdomId);
                _allianceTargetIds.Add(kvp.Value.TargetKingdomId);
                _allianceGoldCost.Add(kvp.Value.GoldCost);
                _allianceInfluenceCost.Add(kvp.Value.InfluenceCost);
                _allianceExpireTicks.Add((long)kvp.Value.ExpirationDate.ToDays);
            }

            // Trade Proposals
            foreach (var kvp in _tradeProposals)
            {
                _tradeProposalKeys.Add(kvp.Key);
                _tradeProposerIds.Add(kvp.Value.ProposerKingdomId);
                _tradeTargetIds.Add(kvp.Value.TargetKingdomId);
                _tradeGoldCost.Add(kvp.Value.GoldCost);
                _tradeInfluenceCost.Add(kvp.Value.InfluenceCost);
                _tradeExpireTicks.Add((long)kvp.Value.ExpirationDate.ToDays);
            }

            // NAP Proposals
            foreach (var kvp in _napProposals)
            {
                _napProposalKeys.Add(kvp.Key);
                _napProposerIds.Add(kvp.Value.ProposerKingdomId);
                _napTargetIds.Add(kvp.Value.TargetKingdomId);
                _napGoldCost.Add(kvp.Value.GoldCost);
                _napInfluenceCost.Add(kvp.Value.InfluenceCost);
                _napExpireTicks.Add((long)kvp.Value.ExpirationDate.ToDays);
            }

            // CTW Cooldowns
            foreach (var kvp in _ctwCooldowns)
            {
                _ctwCooldownKeys.Add(kvp.Key);
                _ctwCooldownTicks.Add((long)kvp.Value.ToDays);
            }
        }

        private void LoadFromLists()
        {
            _truces.Clear();
            _naps.Clear();
            _alliances.Clear();
            _tributes.Clear();
            _wars.Clear();
            _ctwProposals.Clear();
            _peaceProposals.Clear();
            _allianceProposals.Clear();
            _tradeProposals.Clear();
            _napProposals.Clear();
            _ctwCooldowns.Clear();

            // Truces
            for (int i = 0; i < _truceKeys.Count; i++)
            {
                var truce = new BLTTruce
                {
                    Kingdom1Id = _truceK1[i],
                    Kingdom2Id = _truceK2[i],
                    StartDate = CampaignTime.Days(_truceStartTicks[i]),
                    ExpirationDate = CampaignTime.Days(_truceExpireTicks[i])
                };
                _truces[_truceKeys[i]] = truce;
            }

            // NAPs
            for (int i = 0; i < _napKeys.Count; i++)
            {
                var nap = new BLTNAP
                {
                    Kingdom1Id = _napK1[i],
                    Kingdom2Id = _napK2[i],
                    StartDate = CampaignTime.Days(_napStartTicks[i])
                };
                _naps[_napKeys[i]] = nap;
            }

            // Alliances
            for (int i = 0; i < _allianceKeys.Count; i++)
            {
                var alliance = new BLTAlliance
                {
                    Kingdom1Id = _allianceK1[i],
                    Kingdom2Id = _allianceK2[i],
                    StartDate = CampaignTime.Days(_allianceStartTicks[i])
                };
                _alliances[_allianceKeys[i]] = alliance;
            }

            // Tributes
            for (int i = 0; i < _tributeKeys.Count; i++)
            {
                var tribute = new BLTTribute
                {
                    Kingdom1Id = _tributeK1[i],
                    Kingdom2Id = _tributeK2[i],
                    PayerKingdomId = _tributePayer[i],
                    DailyAmount = _tributeAmount[i],
                    StartDate = CampaignTime.Days(_tributeStartTicks[i]),
                    ExpirationDate = CampaignTime.Days(_tributeExpirationTicks[i])
                };

                var payer = tribute.GetPayer();
                var receiver = tribute.GetReceiver();

                if (payer == null || receiver == null)
                    continue;

                if (tribute.StartDate == CampaignTime.Zero)
                {
                    if (tribute.ExpirationDate <= CampaignTime.Now)
                        continue;

                    tribute.StartDate = CampaignTime.Now;
                }

                if (tribute.ExpirationDate <= tribute.StartDate)
                    continue;

                _tributes[_tributeKeys[i]] = tribute;
            }


            // Wars
            for (int i = 0; i < _warKeys.Count; i++)
            {
                var war = new BLTWar
                {
                    Attacker1Id = _warAttacker[i],
                    Defender1Id = _warDefender[i],
                    Attacker1AlliesIds = ParseCSV(_warAttackerAllies[i]),
                    Defender1AlliesIds = ParseCSV(_warDefenderAllies[i]),
                    StartDate = CampaignTime.Days(_warStartTicks[i])
                };
                _wars[_warKeys[i]] = war;
            }

            // Peace Proposals
            for (int i = 0; i < _peaceProposalKeys.Count; i++)
            {
                var proposal = new BLTPeaceProposal
                {
                    ProposerKingdomId = _peaceProposerIds[i],
                    TargetKingdomId = _peaceTargetIds[i],
                    IsOffer = _peaceIsOffer[i],
                    DailyTribute = _peaceTribute[i],
                    Duration = _peaceDuration[i],
                    GoldCost = _peaceGoldCost[i],
                    InfluenceCost = _peaceInfluenceCost[i],
                    ExpirationDate = CampaignTime.Days(_peaceExpireTicks[i])
                };
                _peaceProposals[_peaceProposalKeys[i]] = proposal;
            }

            // Alliance Proposals
            for (int i = 0; i < _allianceProposalKeys.Count; i++)
            {
                var proposal = new BLTAllianceProposal
                {
                    ProposerKingdomId = _allianceProposerIds[i],
                    TargetKingdomId = _allianceTargetIds[i],
                    GoldCost = _allianceGoldCost[i],
                    InfluenceCost = _allianceInfluenceCost[i],
                    ExpirationDate = CampaignTime.Days(_allianceExpireTicks[i])
                };
                _allianceProposals[_allianceProposalKeys[i]] = proposal;
            }

            // Trade Proposals
            for (int i = 0; i < _tradeProposalKeys.Count; i++)
            {
                var proposal = new BLTTradeProposal
                {
                    ProposerKingdomId = _tradeProposerIds[i],
                    TargetKingdomId = _tradeTargetIds[i],
                    GoldCost = _tradeGoldCost[i],
                    InfluenceCost = _tradeInfluenceCost[i],
                    ExpirationDate = CampaignTime.Days(_tradeExpireTicks[i])
                };
                _tradeProposals[_tradeProposalKeys[i]] = proposal;
            }

            // NAP Proposals
            for (int i = 0; i < _napProposalKeys.Count; i++)
            {
                var proposal = new BLTNAPProposal
                {
                    ProposerKingdomId = _napProposerIds[i],
                    TargetKingdomId = _napTargetIds[i],
                    GoldCost = _napGoldCost[i],
                    InfluenceCost = _napInfluenceCost[i],
                    ExpirationDate = CampaignTime.Days(_napExpireTicks[i])
                };
                _napProposals[_napProposalKeys[i]] = proposal;
            }

            // CTW Cooldowns
            for (int i = 0; i < _ctwCooldownKeys.Count; i++)
            {
                _ctwCooldowns[_ctwCooldownKeys[i]] = CampaignTime.Days(_ctwCooldownTicks[i]);
            }
        }

        private List<string> ParseCSV(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return new List<string>();
            return csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private string MakeKey(Kingdom k1, Kingdom k2)
        {
            if (k1 == null || k2 == null) return null;
            var ids = new[] { k1.StringId, k2.StringId }.OrderBy(x => x).ToArray();
            return $"{ids[0]}_{ids[1]}";
        }

        private string MakeCTWCooldownKey(Kingdom proposer, Kingdom called)
        {
            if (proposer == null || called == null) return null;
            return $"{proposer.StringId}_{called.StringId}";
        }

        private void OnDailyTick()
        {
            ProcessTributeTransfers();
            RemoveExpiredTruces();
            RemoveExpiredTributes();
            RemoveExpiredProposals();
        }

        private void ProcessTributeTransfers()
        {
            foreach (var tribute in _tributes.Values.ToList())
            {
                if (tribute.IsExpired()) continue;

                var payer = tribute.GetPayer();
                var receiver = tribute.GetReceiver();

                if (payer == null || receiver == null || payer.Leader == null || receiver.Leader == null)
                {
                    // Skip this iteration if payer or receiver is unavailable
                    continue;
                }

                int amount = tribute.DailyAmount;

                // Transfer game gold
                int payerGold = payer.Leader.Gold;
                int gameGoldToTransfer = Math.Min(amount, payerGold);

                // Guarantee at least 50% even if not available
                if (gameGoldToTransfer < amount / 2)
                {
                    gameGoldToTransfer = amount / 2;
                }

                payer.Leader.Gold -= gameGoldToTransfer;
                receiver.Leader.Gold += gameGoldToTransfer;

                // Transfer BLT gold (same amount)
                int payerBLTGold = BLTAdoptAHeroCampaignBehavior.Current?.GetHeroGold(payer.Leader) ?? 0;
                int bltGoldToTransfer = Math.Min(amount, payerBLTGold);

                if (bltGoldToTransfer < amount / 2)
                {
                    bltGoldToTransfer = amount / 2;
                }

                BLTAdoptAHeroCampaignBehavior.Current?.ChangeHeroGold(payer.Leader, -bltGoldToTransfer, false);
                BLTAdoptAHeroCampaignBehavior.Current?.ChangeHeroGold(receiver.Leader, bltGoldToTransfer, false);

                // Compute days remaining based on CampaignTime
                int daysRemaining = tribute.DaysRemaining();

                // Update game's StanceLink for compatibility
                var stance = payer.GetStanceWith(receiver);
                if (stance != null && daysRemaining > 0)
                {
                    stance.SetDailyTributePaid(payer, amount, daysRemaining);
                }
            }
        }

        private void RemoveExpiredTruces()
        {
            var expired = _truces.Where(kvp => kvp.Value.IsExpired()).Select(kvp => kvp.Key).ToList();
            foreach (var key in expired)
            {
                _truces.Remove(key);
            }
        }

        private void RemoveExpiredTributes()
        {
            var expired = _tributes.Where(kvp => kvp.Value.IsExpired()).Select(kvp => kvp.Key).ToList();
            foreach (var key in expired)
            {
                _tributes.Remove(key);
            }
        }

        private void RemoveExpiredProposals()
        {
            // CTW Proposals (not persisted, but remove expired during runtime)
            var expiredCTW = _ctwProposals.Where(kvp => kvp.Value.IsExpired()).Select(kvp => kvp.Key).ToList();
            foreach (var key in expiredCTW)
            {
                _ctwProposals.Remove(key);
            }

            // Peace Proposals
            var expiredPeace = _peaceProposals.Where(kvp => kvp.Value.IsExpired()).Select(kvp => kvp.Key).ToList();
            foreach (var key in expiredPeace)
            {
                _peaceProposals.Remove(key);
            }

            // Alliance Proposals
            var expiredAlliance = _allianceProposals.Where(kvp => kvp.Value.IsExpired()).Select(kvp => kvp.Key).ToList();
            foreach (var key in expiredAlliance)
            {
                _allianceProposals.Remove(key);
            }

            // Trade Proposals
            var expiredTrade = _tradeProposals.Where(kvp => kvp.Value.IsExpired()).Select(kvp => kvp.Key).ToList();
            foreach (var key in expiredTrade)
            {
                _tradeProposals.Remove(key);
            }

            // NAP Proposals
            var expiredNAP = _napProposals.Where(kvp => kvp.Value.IsExpired()).Select(kvp => kvp.Key).ToList();
            foreach (var key in expiredNAP)
            {
                _napProposals.Remove(key);
            }
        }

        private void OnKingdomDestroyed(Kingdom kingdom)
        {
            if (kingdom == null) return;

            // Remove all treaties involving this kingdom
            var truceKeys = _truces.Where(kvp => kvp.Value.Involves(kingdom)).Select(kvp => kvp.Key).ToList();
            foreach (var key in truceKeys) _truces.Remove(key);

            var napKeys = _naps.Where(kvp => kvp.Value.Involves(kingdom)).Select(kvp => kvp.Key).ToList();
            foreach (var key in napKeys) _naps.Remove(key);

            var allianceKeys = _alliances.Where(kvp => kvp.Value.Involves(kingdom)).Select(kvp => kvp.Key).ToList();
            foreach (var key in allianceKeys) _alliances.Remove(key);

            var tributeKeys = _tributes.Where(kvp => kvp.Value.Involves(kingdom)).Select(kvp => kvp.Key).ToList();
            foreach (var key in tributeKeys) _tributes.Remove(key);

            var warKeys = _wars.Where(kvp => kvp.Value.Involves(kingdom)).Select(kvp => kvp.Key).ToList();
            foreach (var key in warKeys) _wars.Remove(key);

            // Remove proposals
            var ctwKeys = _ctwProposals.Where(kvp =>
                kvp.Value.ProposerKingdomId == kingdom.StringId ||
                kvp.Value.CalledKingdomId == kingdom.StringId ||
                kvp.Value.TargetKingdomId == kingdom.StringId).Select(kvp => kvp.Key).ToList();
            foreach (var key in ctwKeys) _ctwProposals.Remove(key);

            var peaceKeys = _peaceProposals.Where(kvp =>
                kvp.Value.ProposerKingdomId == kingdom.StringId ||
                kvp.Value.TargetKingdomId == kingdom.StringId).Select(kvp => kvp.Key).ToList();
            foreach (var key in peaceKeys) _peaceProposals.Remove(key);

            var alliancePropKeys = _allianceProposals.Where(kvp =>
                kvp.Value.ProposerKingdomId == kingdom.StringId ||
                kvp.Value.TargetKingdomId == kingdom.StringId).Select(kvp => kvp.Key).ToList();
            foreach (var key in alliancePropKeys) _allianceProposals.Remove(key);

            var tradePropKeys = _tradeProposals.Where(kvp =>
                kvp.Value.ProposerKingdomId == kingdom.StringId ||
                kvp.Value.TargetKingdomId == kingdom.StringId).Select(kvp => kvp.Key).ToList();
            foreach (var key in tradePropKeys) _tradeProposals.Remove(key);

            var napPropKeys = _napProposals.Where(kvp =>
                kvp.Value.ProposerKingdomId == kingdom.StringId ||
                kvp.Value.TargetKingdomId == kingdom.StringId).Select(kvp => kvp.Key).ToList();
            foreach (var key in napPropKeys) _napProposals.Remove(key);

            // Remove cooldowns
            var cooldownKeys = _ctwCooldowns.Keys.Where(k => k.Contains(kingdom.StringId)).ToList();
            foreach (var key in cooldownKeys) _ctwCooldowns.Remove(key);
        }

        #region Public API

        public bool CanDeclareWar(Kingdom declarer, Kingdom target, out string reason)
        {
            reason = null;

            if (declarer == null || target == null)
            {
                reason = "Invalid kingdoms";
                return false;
            }

            if (declarer == target)
            {
                reason = "Cannot declare war on yourself";
                return false;
            }

            if (declarer.IsAtWarWith(target))
            {
                reason = $"Already at war with {target.Name}";
                return false;
            }

            // Check for active truce
            var truce = GetTruce(declarer, target);
            if (truce != null && !truce.IsExpired())
            {
                reason = $"Truce with {target.Name} ({truce.DaysRemaining()} days remaining)";
                return false;
            }

            // Check for NAP (can only be overcome by defensive alliance call)
            var nap = GetNAP(declarer, target);
            if (nap != null)
            {
                reason = $"Non-aggression pact with {target.Name}";
                return false;
            }

            // Check for alliance
            var alliance = GetAlliance(declarer, target);
            if (alliance != null)
            {
                reason = $"Allied with {target.Name}";
                return false;
            }

            return true;
        }

        public bool CanCallToWar(Kingdom proposer, Kingdom ally, int cooldownDays, out string reason)
        {
            reason = null;

            // Check cooldown
            if (cooldownDays > 0)
            {
                var key = MakeCTWCooldownKey(proposer, ally);
                if (_ctwCooldowns.TryGetValue(key, out var lastCTW))
                {
                    int daysSince = (int)(CampaignTime.Now - lastCTW).ToDays;
                    if (daysSince < cooldownDays)
                    {
                        reason = $"CTW cooldown: {cooldownDays - daysSince} days remaining";
                        return false;
                    }
                }
            }

            return true;
        }

        public void RecordCTWCall(Kingdom proposer, Kingdom ally)
        {
            var key = MakeCTWCooldownKey(proposer, ally);
            _ctwCooldowns[key] = CampaignTime.Now;
        }

        // Treaties
        public BLTTruce CreateTruce(Kingdom k1, Kingdom k2, int durationDays)
        {
            var key = MakeKey(k1, k2);
            if (key == null) return null;

            var truce = new BLTTruce(k1, k2, durationDays);
            _truces[key] = truce;
            return truce;
        }

        public BLTNAP CreateNAP(Kingdom k1, Kingdom k2)
        {
            var key = MakeKey(k1, k2);
            if (key == null) return null;

            var nap = new BLTNAP(k1, k2);
            _naps[key] = nap;
            return nap;
        }

        public BLTAlliance CreateAlliance(Kingdom k1, Kingdom k2)
        {
            var key = MakeKey(k1, k2);
            if (key == null) return null;

            var alliance = new BLTAlliance(k1, k2);
            _alliances[key] = alliance;
            return alliance;
        }

        public BLTTribute CreateTribute(Kingdom payer, Kingdom receiver, int dailyAmount, int durationDays)
        {
            var key = MakeKey(payer, receiver);
            if (key == null) return null;

            var tribute = new BLTTribute(payer, receiver, dailyAmount, durationDays);
            _tributes[key] = tribute;
            return tribute;
        }

        public BLTWar CreateWar(Kingdom attacker, Kingdom defender)
        {
            var key = MakeKey(attacker, defender);
            if (key == null) return null;

            var war = new BLTWar(attacker, defender);
            _wars[key] = war;
            return war;
        }

        public bool CanMakePeace(Kingdom k1, Kingdom k2, out string reason)
        {
            reason = null;

            if (k1 == null || k2 == null)
            {
                reason = "Invalid kingdoms";
                return false;
            }

            if (!k1.IsAtWarWith(k2))
            {
                reason = "Not at war";
                return false;
            }

            // Check minimum war duration
            var war = GetWar(k1, k2);
            if (war != null && MinWarDurationDays > 0)
            {
                int daysSinceWarStart = (int)(CampaignTime.Now - war.StartDate).ToDays;
                if (daysSinceWarStart < MinWarDurationDays)
                {
                    int daysRemaining = MinWarDurationDays - daysSinceWarStart;
                    reason = $"War must last at least {MinWarDurationDays} days. {daysRemaining} days remaining.";
                    return false;
                }
            }

            return true;
        }

        // Proposals (with duplicate prevention - updates existing)
        public BLTPeaceProposal CreatePeaceProposal(Kingdom proposer, Kingdom target, bool isOffer, int dailyTribute, int duration, int goldCost, int influenceCost, int daysToAccept)
        {
            var key = MakeKey(proposer, target);
            if (key == null) return null;

            var proposal = new BLTPeaceProposal(proposer, target, isOffer, dailyTribute, duration, goldCost, influenceCost, daysToAccept);
            _peaceProposals[key] = proposal; // Overwrites existing
            return proposal;
        }

        public BLTAllianceProposal CreateAllianceProposal(Kingdom proposer, Kingdom target, int goldCost, int influenceCost, int daysToAccept, int breakAllianceCost, int ctwCost)
        {
            var key = MakeKey(proposer, target);
            if (key == null) return null;

            var proposal = new BLTAllianceProposal(proposer, target, goldCost, influenceCost, daysToAccept, breakAllianceCost, ctwCost);
            _allianceProposals[key] = proposal;
            return proposal;
        }

        public BLTTradeProposal CreateTradeProposal(Kingdom proposer, Kingdom target, int goldCost, int influenceCost, int daysToAccept)
        {
            var key = MakeKey(proposer, target);
            if (key == null) return null;

            var proposal = new BLTTradeProposal(proposer, target, goldCost, influenceCost, daysToAccept);
            _tradeProposals[key] = proposal;
            return proposal;
        }

        public BLTNAPProposal CreateNAPProposal(Kingdom proposer, Kingdom target, int goldCost, int influenceCost, int daysToAccept)
        {
            var key = MakeKey(proposer, target);
            if (key == null) return null;

            var proposal = new BLTNAPProposal(proposer, target, goldCost, influenceCost, daysToAccept);
            _napProposals[key] = proposal; // Overwrites existing
            return proposal;
        }

        public BLTCTWProposal CreateCTWProposal(Kingdom proposer, Kingdom called, Kingdom target, int daysToAccept)
        {
            var key = $"{proposer.StringId}_{called.StringId}_{target.StringId}";
            var proposal = new BLTCTWProposal(proposer, called, target, daysToAccept);
            _ctwProposals[key] = proposal;
            return proposal;
        }

        // Remove methods
        public void RemoveTruce(Kingdom k1, Kingdom k2)
        {
            var key = MakeKey(k1, k2);
            if (key != null) _truces.Remove(key);
        }

        public void RemoveNAP(Kingdom k1, Kingdom k2)
        {
            var key = MakeKey(k1, k2);
            if (key != null) _naps.Remove(key);
        }

        public void RemoveAlliance(Kingdom k1, Kingdom k2)
        {
            var key = MakeKey(k1, k2);
            if (key != null) _alliances.Remove(key);
        }

        public void RemoveTribute(Kingdom k1, Kingdom k2)
        {
            var key = MakeKey(k1, k2);
            if (key != null) _tributes.Remove(key);
        }

        public void RemoveWar(Kingdom k1, Kingdom k2)
        {
            var key = MakeKey(k1, k2);
            if (key != null) _wars.Remove(key);
        }

        public void RemovePeaceProposal(Kingdom proposer, Kingdom target)
        {
            var key = MakeKey(proposer, target);
            if (key != null) _peaceProposals.Remove(key);
        }

        public void RemoveAllianceProposal(Kingdom proposer, Kingdom target)
        {
            var key = MakeKey(proposer, target);
            if (key != null) _allianceProposals.Remove(key);
        }

        public void RemoveTradeProposal(Kingdom proposer, Kingdom target)
        {
            var key = MakeKey(proposer, target);
            if (key != null) _tradeProposals.Remove(key);
        }

        public void RemoveNAPProposal(Kingdom proposer, Kingdom target)
        {
            var key = MakeKey(proposer, target);
            if (key != null) _napProposals.Remove(key);
        }

        public void RemoveCTWProposal(Kingdom proposer, Kingdom called, Kingdom target)
        {
            var key = $"{proposer?.StringId}_{called?.StringId}_{target?.StringId}";
            _ctwProposals.Remove(key);
        }

        // Get methods
        public BLTTruce GetTruce(Kingdom k1, Kingdom k2)
        {
            var key = MakeKey(k1, k2);
            return key != null && _truces.TryGetValue(key, out var truce) ? truce : null;
        }

        public BLTNAP GetNAP(Kingdom k1, Kingdom k2)
        {
            var key = MakeKey(k1, k2);
            return key != null && _naps.TryGetValue(key, out var nap) ? nap : null;
        }

        public BLTAlliance GetAlliance(Kingdom k1, Kingdom k2)
        {
            var key = MakeKey(k1, k2);
            return key != null && _alliances.TryGetValue(key, out var alliance) ? alliance : null;
        }

        public BLTTribute GetTribute(Kingdom k1, Kingdom k2)
        {
            var key = MakeKey(k1, k2);
            return key != null && _tributes.TryGetValue(key, out var tribute) ? tribute : null;
        }

        public BLTWar GetWar(Kingdom k1, Kingdom k2)
        {
            var key = MakeKey(k1, k2);
            return key != null && _wars.TryGetValue(key, out var war) ? war : null;
        }

        public BLTPeaceProposal GetPeaceProposal(Kingdom proposer, Kingdom target)
        {
            var key = MakeKey(proposer, target);
            return key != null && _peaceProposals.TryGetValue(key, out var proposal) ? proposal : null;
        }

        public BLTAllianceProposal GetAllianceProposal(Kingdom proposer, Kingdom target)
        {
            var key = MakeKey(proposer, target);
            return key != null && _allianceProposals.TryGetValue(key, out var proposal) ? proposal : null;
        }
        public BLTTradeProposal GetTradeProposal(Kingdom proposer, Kingdom target)
        {
            var key = MakeKey(proposer, target);
            return key != null && _tradeProposals.TryGetValue(key, out var proposal) ? proposal : null;
        }

        public BLTNAPProposal GetNAPProposal(Kingdom proposer, Kingdom target)
        {
            var key = MakeKey(proposer, target);
            return key != null && _napProposals.TryGetValue(key, out var proposal) ? proposal : null;
        }

        // List methods
        public List<BLTWar> GetWarsInvolving(Kingdom k)
        {
            return _wars.Values.Where(w => w.Involves(k)).ToList();
        }

        public List<BLTCTWProposal> GetCTWProposalsFor(Kingdom k)
        {
            return _ctwProposals.Values
                .Where(p => p.CalledKingdomId == k?.StringId && !p.IsExpired())
                .ToList();
        }

        public List<BLTPeaceProposal> GetPeaceProposalsFor(Kingdom k)
        {
            return _peaceProposals.Values
                .Where(p => p.TargetKingdomId == k?.StringId && !p.IsExpired())
                .ToList();
        }

        public List<BLTAllianceProposal> GetAllianceProposalsFor(Kingdom k)
        {
            return _allianceProposals.Values
                .Where(p => p.TargetKingdomId == k?.StringId && !p.IsExpired())
                .ToList();
        }

        public List<BLTTradeProposal> GetTradeProposalsFor(Kingdom k)
        {
            return _tradeProposals.Values
                .Where(p => p.TargetKingdomId == k?.StringId && !p.IsExpired())
                .ToList();
        }

        public List<BLTNAPProposal> GetNAPProposalsFor(Kingdom k)
        {
            return _napProposals.Values
                .Where(p => p.TargetKingdomId == k?.StringId && !p.IsExpired())
                .ToList();
        }

        public List<BLTNAP> GetNAPsFor(Kingdom k)
        {
            return _naps.Values.Where(n => n.Involves(k)).ToList();
        }

        public List<BLTAlliance> GetAlliancesFor(Kingdom k)
        {
            return _alliances.Values.Where(a => a.Involves(k)).ToList();
        }

        public List<BLTTribute> GetTributesPayedBy(Kingdom k)
        {
            return _tributes.Values.Where(t => t.PayerKingdomId == k?.StringId).ToList();
        }

        public List<BLTTribute> GetTributesReceivedBy(Kingdom k)
        {
            return _tributes.Values.Where(t => t.GetReceiver() == k).ToList();
        }

        #endregion
    }
}