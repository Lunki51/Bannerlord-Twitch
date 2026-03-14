using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero
{
    // ── Data classes ──────────────────────────────────────────────────────────

    public class BLTClanAlliance
    {
        public string Clan1Id { get; set; }
        public string Clan2Id { get; set; }
        public double StartDays { get; set; }

        public Clan GetClan1() => Clan.All.FirstOrDefault(c => c.StringId == Clan1Id);
        public Clan GetClan2() => Clan.All.FirstOrDefault(c => c.StringId == Clan2Id);

        public bool Involves(Clan c) =>
            Clan1Id == c?.StringId || Clan2Id == c?.StringId;

        public Clan GetOther(Clan c)
        {
            if (Clan1Id == c?.StringId) return GetClan2();
            if (Clan2Id == c?.StringId) return GetClan1();
            return null;
        }
    }

    public class BLTClanAllianceProposal
    {
        public string ProposerClanId { get; set; }
        public string TargetClanId { get; set; }
        public double ExpiresAtDays { get; set; }
        public int GoldCost { get; set; }

        public Clan GetProposer() => Clan.All.FirstOrDefault(c => c.StringId == ProposerClanId);
        public Clan GetTarget() => Clan.All.FirstOrDefault(c => c.StringId == TargetClanId);
        public bool IsExpired() => CampaignTime.Now.ToDays >= ExpiresAtDays;
        public int DaysRemaining() => Math.Max(0, (int)(ExpiresAtDays - CampaignTime.Now.ToDays));
    }

    // ── Behavior ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Lightweight clan-to-clan alliance system for independent (kingdom-less) clans.
    /// Only BLT-adopted clan leaders can propose/accept alliances.
    /// Alliances are automatically dissolved when either clan joins a kingdom.
    /// </summary>
    public class BLTClanDiplomacyBehavior : CampaignBehaviorBase
    {
        public static BLTClanDiplomacyBehavior Current { get; private set; }

        private Dictionary<string, BLTClanAlliance> _alliances = new();
        private Dictionary<string, BLTClanAllianceProposal> _proposals = new();

        // ── Persistence lists ─────────────────────────────────────────────────
        private List<string> _allianceKeys = new();
        private List<string> _allianceClan1 = new();
        private List<string> _allianceClan2 = new();
        private List<double> _allianceStartDays = new();

        private List<string> _proposalKeys = new();
        private List<string> _proposerIds = new();
        private List<string> _targetIds = new();
        private List<double> _proposalExpireDays = new();
        private List<int> _proposalGoldCost = new();

        public BLTClanDiplomacyBehavior() { Current = this; }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public override void RegisterEvents()
        {
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("BLT_ClanAllianceKeys", ref _allianceKeys);
            dataStore.SyncData("BLT_ClanAllianceClan1", ref _allianceClan1);
            dataStore.SyncData("BLT_ClanAllianceClan2", ref _allianceClan2);
            dataStore.SyncData("BLT_ClanAllianceStartDays", ref _allianceStartDays);
            dataStore.SyncData("BLT_ClanProposalKeys", ref _proposalKeys);
            dataStore.SyncData("BLT_ClanProposerIds", ref _proposerIds);
            dataStore.SyncData("BLT_ClanTargetIds", ref _targetIds);
            dataStore.SyncData("BLT_ClanProposalExpire", ref _proposalExpireDays);
            dataStore.SyncData("BLT_ClanProposalGold", ref _proposalGoldCost);

            if (dataStore.IsLoading) LoadFromLists();
            else if (dataStore.IsSaving) SaveToLists();
        }

        // ── Save / Load ───────────────────────────────────────────────────────

        private void SaveToLists()
        {
            _allianceKeys.Clear(); _allianceClan1.Clear();
            _allianceClan2.Clear(); _allianceStartDays.Clear();

            foreach (var kvp in _alliances)
            {
                _allianceKeys.Add(kvp.Key);
                _allianceClan1.Add(kvp.Value.Clan1Id);
                _allianceClan2.Add(kvp.Value.Clan2Id);
                _allianceStartDays.Add(kvp.Value.StartDays);
            }

            _proposalKeys.Clear(); _proposerIds.Clear();
            _targetIds.Clear(); _proposalExpireDays.Clear(); _proposalGoldCost.Clear();

            foreach (var kvp in _proposals)
            {
                _proposalKeys.Add(kvp.Key);
                _proposerIds.Add(kvp.Value.ProposerClanId);
                _targetIds.Add(kvp.Value.TargetClanId);
                _proposalExpireDays.Add(kvp.Value.ExpiresAtDays);
                _proposalGoldCost.Add(kvp.Value.GoldCost);
            }
        }

        private void LoadFromLists()
        {
            _alliances.Clear();
            for (int i = 0; i < _allianceKeys.Count; i++)
            {
                _alliances[_allianceKeys[i]] = new BLTClanAlliance
                {
                    Clan1Id = _allianceClan1[i],
                    Clan2Id = _allianceClan2[i],
                    StartDays = _allianceStartDays[i]
                };
            }

            _proposals.Clear();
            int count = new[] { _proposalKeys.Count, _proposerIds.Count,
                _targetIds.Count, _proposalExpireDays.Count, _proposalGoldCost.Count }.Min();
            for (int i = 0; i < count; i++)
            {
                _proposals[_proposalKeys[i]] = new BLTClanAllianceProposal
                {
                    ProposerClanId = _proposerIds[i],
                    TargetClanId = _targetIds[i],
                    ExpiresAtDays = _proposalExpireDays[i],
                    GoldCost = _proposalGoldCost[i]
                };
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Validates and creates a pending alliance proposal from proposer to target.
        /// Returns a failure string on error, null on success.
        /// </summary>
        public string CreateProposal(Clan proposer, Clan target, int goldCost, int daysToAccept)
        {
            if (proposer == null || target == null)
                return "Invalid clan";
            if (proposer == target)
                return "Cannot ally with yourself";
            if (proposer.Kingdom != null)
                return $"{proposer.Name} is in a kingdom — use the kingdom diplomacy system";
            if (target.Kingdom != null)
                return $"{target.Name} is in a kingdom — independent clan alliances only";
            if (!proposer.Leader.IsAdopted() && !target.Leader.IsAdopted())
                return "At least one clan must be BLT-adopted to form a clan alliance";
            if (HasAlliance(proposer, target))
                return $"Already allied with {target.Name}";
            if (GetProposal(proposer, target) != null)
                return $"Alliance proposal already pending with {target.Name}";

            var key = MakeKey(proposer, target);
            _proposals[key] = new BLTClanAllianceProposal
            {
                ProposerClanId = proposer.StringId,
                TargetClanId = target.StringId,
                ExpiresAtDays = CampaignTime.Now.ToDays + daysToAccept,
                GoldCost = goldCost
            };
            return null; // success
        }

        /// <summary>
        /// Accepts a pending proposal. Returns failure string or null on success.
        /// </summary>
        public string AcceptProposal(Clan accepter, Clan proposer)
        {
            if (accepter == null || proposer == null) return "Invalid clan";

            var proposal = GetProposal(proposer, accepter);
            if (proposal == null)
                return $"No pending alliance proposal from {proposer.Name}";
            if (proposal.IsExpired())
            {
                RemoveProposal(proposer, accepter);
                return $"The proposal from {proposer.Name} has expired";
            }
            if (proposer.Kingdom != null || accepter.Kingdom != null)
            {
                RemoveProposal(proposer, accepter);
                return "One or both clans have joined a kingdom — proposal cancelled";
            }

            var key = MakeKey(proposer, accepter);
            _alliances[key] = new BLTClanAlliance
            {
                Clan1Id = proposer.StringId,
                Clan2Id = accepter.StringId,
                StartDays = CampaignTime.Now.ToDays
            };
            RemoveProposal(proposer, accepter);
            return null; // success
        }

        /// <summary>
        /// Breaks an existing alliance, notifying both parties if adopted.
        /// reason is shown in the chat notification.
        /// </summary>
        public void BreakAlliance(Clan c1, Clan c2, string reason)
        {
            var key = MakeKey(c1, c2);
            if (!_alliances.ContainsKey(key)) return;
            _alliances.Remove(key);
            NotifyAllianceBroken(c1, c2, reason);
        }

        public bool HasAlliance(Clan c1, Clan c2) =>
            _alliances.ContainsKey(MakeKey(c1, c2));

        public BLTClanAlliance GetAlliance(Clan c1, Clan c2)
        {
            _alliances.TryGetValue(MakeKey(c1, c2), out var a);
            return a;
        }

        public BLTClanAllianceProposal GetProposal(Clan proposer, Clan target)
        {
            _proposals.TryGetValue(MakeKey(proposer, target), out var p);
            // Directional: key is symmetric but ProposerClanId distinguishes direction
            if (p != null && p.ProposerClanId == proposer?.StringId) return p;
            return null;
        }

        public List<BLTClanAlliance> GetAlliancesFor(Clan c) =>
            _alliances.Values.Where(a => a.Involves(c)).ToList();

        public List<Clan> GetAlliedClans(Clan c) =>
            GetAlliancesFor(c)
                .Select(a => a.GetOther(c))
                .Where(other => other != null)
                .ToList();

        public List<BLTClanAllianceProposal> GetProposalsFor(Clan c) =>
            _proposals.Values
                .Where(p => p.TargetClanId == c?.StringId && !p.IsExpired())
                .ToList();

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            try
            {
                if (clan == null || newKingdom == null) return;

                // Break all alliances that involve this clan, since it is no longer independent.
                foreach (var alliance in GetAlliancesFor(clan).ToList())
                {
                    var other = alliance.GetOther(clan);
                    BreakAlliance(clan, other,
                        $"{clan.Name} has joined {newKingdom.Name} — clan alliance dissolved");
                }

                // Also cancel any pending proposals.
                foreach (var kvp in _proposals
                    .Where(p => p.Value.ProposerClanId == clan.StringId
                             || p.Value.TargetClanId == clan.StringId)
                    .ToList())
                {
                    _proposals.Remove(kvp.Key);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] BLTClanDiplomacyBehavior.OnClanChangedKingdom error: {ex}");
            }
        }

        private void OnDailyTick()
        {
            // Expire stale proposals.
            foreach (var key in _proposals.Where(kvp => kvp.Value.IsExpired())
                                          .Select(kvp => kvp.Key).ToList())
                _proposals.Remove(key);
        }

        private void OnHeroKilled(Hero victim, Hero killer,
            KillCharacterAction.KillCharacterActionDetail detail, bool show)
        {
            try
            {
                if (victim?.Clan == null) return;
                // If a clan leader dies and there's no heir, the clan may be discontinued.
                // We clean up alliances defensively here as well.
                if (victim.Clan.Leader != victim) return;

                foreach (var alliance in GetAlliancesFor(victim.Clan).ToList())
                {
                    var other = alliance.GetOther(victim.Clan);
                    BreakAlliance(victim.Clan, other,
                        $"{victim.Clan.Name}'s leader has fallen — clan alliance dissolved");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] BLTClanDiplomacyBehavior.OnHeroKilled error: {ex}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string MakeKey(Clan c1, Clan c2)
        {
            if (c1 == null || c2 == null) return null;
            var ids = new[] { c1.StringId, c2.StringId }.OrderBy(x => x).ToArray();
            return $"ca_{ids[0]}_{ids[1]}";
        }

        private void RemoveProposal(Clan proposer, Clan target)
        {
            var key = MakeKey(proposer, target);
            if (key != null) _proposals.Remove(key);
        }

        private static void NotifyAllianceBroken(Clan c1, Clan c2, string reason)
        {
            try
            {
                NotifyClanLeader(c1, $"Your clan alliance with {c2?.Name} has been broken — {reason}");
                NotifyClanLeader(c2, $"Your clan alliance with {c1?.Name} has been broken — {reason}");
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] NotifyAllianceBroken error: {ex}");
            }
        }

        internal static void NotifyClanLeader(Clan clan, string message)
        {
            if (clan?.Leader == null) return;
            if (!clan.Leader.IsAdopted()) return;

            string name = clan.Leader.FirstName.ToString()
                .Replace(BLTAdoptAHeroModule.Tag, "")
                .Replace(BLTAdoptAHeroModule.DevTag, "")
                .Trim();

            Log.LogFeedResponse($"@{name} {message}");
            Log.ShowInformation(message, clan.Leader.CharacterObject);
        }

        // ── Info builder (called by Diplomacy.cs HandleInfoCommand) ──────────

        /// <summary>
        /// Appends a clan diplomacy section to a StringBuilder for use in !diplomacy info.
        /// Only outputs content if there is something to show.
        /// </summary>
        public void AppendInfoSection(Clan clan, System.Text.StringBuilder sb)
        {
            if (clan == null || clan.Kingdom != null) return; // only for independent clans

            var alliances = GetAlliancesFor(clan);
            var proposals = GetProposalsFor(clan);

            if (alliances.Count == 0 && proposals.Count == 0) return;

            sb.Append(" | [Clan Alliances]");
            foreach (var a in alliances)
            {
                var other = a.GetOther(clan);
                int days = (int)(CampaignTime.Now.ToDays - a.StartDays);
                sb.Append($" {other?.Name}(+{days}d)");
            }

            if (proposals.Count > 0)
            {
                sb.Append(" | [Pending]");
                foreach (var p in proposals)
                    sb.Append($" {p.GetProposer()?.Name}({p.DaysRemaining()}d)");
            }
        }
    }
}