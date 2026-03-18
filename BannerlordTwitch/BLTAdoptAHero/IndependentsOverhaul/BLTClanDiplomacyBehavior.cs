using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem.Party;

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
    /// Clan-to-clan alliance system.
    /// Independent (kingdom-less) clans: full alliance support.
    /// Landed clans (own at least one fief) may also form alliances with kingdoms
    /// and other landed clans via the kingdom-level proposal/accept flow, but lose
    /// all non-war diplomatic agreements if they later lose all fiefs.
    /// Vassals are never shown directly — allied clan display uses a (+N) suffix instead.
    /// </summary>
    public class BLTClanDiplomacyBehavior : CampaignBehaviorBase
    {
        public static BLTClanDiplomacyBehavior Current { get; private set; }

        // Max independent-clan alliances (set from Diplomacy.cs Settings)
        public int MaxClanAlliances { get; set; } = 3;

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
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
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
            int count = new[] {
                _proposalKeys.Count, _proposerIds.Count,
                _targetIds.Count, _proposalExpireDays.Count, _proposalGoldCost.Count
            }.Min();
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

        // ── Helpers: landed status ────────────────────────────────────────────

        /// <summary>True if the clan owns at least one fief.</summary>
        public static bool IsLanded(Clan c) => c?.Fiefs != null && c.Fiefs.Count > 0;

        /// <summary>
        /// True if a clan is eligible for kingdom-level clan diplomacy
        /// (landed independent clan or a kingdom).
        /// </summary>
        public static bool CanUseKingdomLevelDiplomacy(Clan c) =>
            c != null && c.Kingdom == null && IsLanded(c);

        // ── Helpers: vassal count ─────────────────────────────────────────────

        /// <summary>
        /// Returns the number of vassal clans a clan has, safely.
        /// </summary>
        private static int GetVassalCount(Clan c)
        {
            if (c == null) return 0;
            try
            {
                return VassalBehavior.Current?.GetVassalClans(c)?.Count ?? 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Returns a display label for a clan, e.g. "ClanName (+2)" if they have vassals.
        /// </summary>
        public static string ClanDisplayLabel(Clan c)
        {
            if (c == null) return "?";
            int vassals = GetVassalCount(c);
            return vassals > 0 ? $"{c.Name} (+{vassals})" : c.Name.ToString();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Validates and creates a pending alliance proposal from proposer → target.
        /// Both independent-clan and kingdom-level proposals go through here.
        /// Returns a failure string on error, null on success.
        /// </summary>
        public string CreateProposal(Clan proposer, Clan target, int goldCost, int daysToAccept)
        {
            if (proposer == null || target == null)
                return "Invalid clan";
            if (proposer == target)
                return "Cannot ally with yourself";

            // Proposer must be independent
            if (proposer.Kingdom != null)
                return $"{proposer.Name} is in a kingdom — use the kingdom diplomacy system";

            // Target may be independent or landed
            if (target.Kingdom != null)
                return $"{target.Name} is in a kingdom — use the kingdom diplomacy system";

            // At least one side must be BLT-adopted
            if (target.Leader == null || !target.Leader.IsAdopted())
                return "Target clan must be BLT-led to form a clan alliance";

            if (HasAlliance(proposer, target))
                return $"Already allied with {target.Name}";
            if (GetProposal(proposer, target) != null)
                return $"Alliance proposal already pending with {target.Name}";

            // If neither side is landed, apply the independent-clan max
            if (!IsLanded(proposer) && !IsLanded(target))
            {
                int current = GetAlliancesFor(proposer).Count;
                if (MaxClanAlliances > 0 && current >= MaxClanAlliances)
                    return $"Maximum clan alliances reached ({current}/{MaxClanAlliances})";
            }

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

        // Runtime-only — not persisted (proposals are short-lived)
        [NonSerialized]
        private readonly Dictionary<string, (string proposerClanId, double expiresAtDays) > _clanPeaceProposals = new();

        public string CreateClanPeaceProposal(Clan proposer, Clan target, int daysToAccept)
        {
            if (proposer == null || target == null) return "Invalid clan";
            if (!proposer.IsAtWarWith(target)) return $"Not at war with {target.Name}";
            var key = MakeKey(proposer, target);
            _clanPeaceProposals[key] = (proposer.StringId, CampaignTime.Now.ToDays + daysToAccept);
            return null;
        }

        public bool HasClanPeaceProposalFrom(Clan proposer, Clan target)
        {
            var key = MakeKey(proposer, target);
            if (!_clanPeaceProposals.TryGetValue(key, out var p)) return false;
            if (CampaignTime.Now.ToDays >= p.expiresAtDays)
            { _clanPeaceProposals.Remove(key); return false; }
            return p.proposerClanId == proposer?.StringId;
        }

        public void RemoveClanPeaceProposal(Clan proposer, Clan target)
        {
            var key = MakeKey(proposer, target);
            _clanPeaceProposals.Remove(key);
        }

        /// <summary>
        /// Accepts a pending proposal.  Also registers all vassals of both sides
        /// as silent members of the alliance.
        /// Returns failure string or null on success.
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

        private class ClanCTWProposal
        {
            public string CallerClanId;
            public string CalledClanId;
            public string TargetId;
            public bool TargetIsKingdom;
            public double ExpiresAtDays;
            public bool IsExpired() => CampaignTime.Now.ToDays >= ExpiresAtDays;
            public int DaysRemaining() => Math.Max(0, (int)(ExpiresAtDays - CampaignTime.Now.ToDays));
        }

        [NonSerialized]
        private readonly Dictionary<string, ClanCTWProposal> _clanCTWProposals = new();

        private string MakeCTWKey(Clan caller, Clan called) =>
            $"ctw_{caller?.StringId}_{called?.StringId}";

        public string CreateClanCTWProposal(Clan caller, Clan called, IFaction target, int daysToAccept)
        {
            if (caller == null || called == null || target == null) return "Invalid arguments";
            if (!HasAlliance(caller, called))
                return $"Not allied with {called.Name}";
            if (!caller.IsAtWarWith(target))
                return $"You are not at war with {target.Name}";
            if (called.IsAtWarWith(target))
                return $"{called.Name} is already at war with {target.Name}";

            _clanCTWProposals[MakeCTWKey(caller, called)] = new ClanCTWProposal
            {
                CallerClanId = caller.StringId,
                CalledClanId = called.StringId,
                TargetId = target.StringId,
                TargetIsKingdom = target is Kingdom,
                ExpiresAtDays = CampaignTime.Now.ToDays + daysToAccept
            };
            return null;
        }

        public List<(Clan caller, IFaction target, int daysLeft)> GetClanCTWProposalsFor(Clan c)
        {
            return _clanCTWProposals.Values
                .Where(p => p.CalledClanId == c?.StringId && !p.IsExpired())
                .Select(p =>
                {
                    var caller = Clan.All.FirstOrDefault(x => x.StringId == p.CallerClanId);
                    IFaction target = p.TargetIsKingdom
                        ? (IFaction)Kingdom.All.FirstOrDefault(k => k.StringId == p.TargetId)
                        : Clan.All.FirstOrDefault(x => x.StringId == p.TargetId);
                    return (caller, target, p.DaysRemaining());
                })
                .Where(t => t.caller != null && t.target != null)
                .ToList();
        }

        public string AcceptClanCTW(Clan accepter, Clan caller, out IFaction target)
        {
            target = null;
            var key = MakeCTWKey(caller, accepter);
            if (!_clanCTWProposals.TryGetValue(key, out var proposal))
                return $"No CTW proposal from {caller?.Name}";
            if (proposal.IsExpired())
            { _clanCTWProposals.Remove(key); return $"CTW proposal from {caller?.Name} has expired"; }

            target = proposal.TargetIsKingdom
                ? (IFaction)Kingdom.All.FirstOrDefault(k => k.StringId == proposal.TargetId)
                : Clan.All.FirstOrDefault(c => c.StringId == proposal.TargetId);

            if (target == null)
            { _clanCTWProposals.Remove(key); return "Target faction no longer exists"; }

            _clanCTWProposals.Remove(key);
            return null;
        }

        // ── Fief-loss enforcement ─────────────────────────────────────────────

        /// <summary>
        /// Called when a clan loses its last fief.  Strips all non-war diplomatic
        /// agreements (alliances, proposals).  Active wars are left intact.
        /// </summary>
        private void OnClanLostLastFief(Clan clan)
        {
            if (clan == null) return;

            // Only strip alliances where the OTHER side is a kingdom or a landed clan
            // (i.e. agreements that required land to enter). Pure clan-to-clan alliances
            // between two independent clans are left intact.
            foreach (var a in GetAlliancesFor(clan).ToList())
            {
                var other = a.GetOther(clan);
                if (other == null) continue;

                bool otherIsKingdom = other.Kingdom != null;
                bool otherIsLanded = IsLanded(other);

                if (otherIsKingdom || otherIsLanded)
                {
                    BreakAlliance(clan, other,
                        $"{clan.Name} lost all fiefs — kingdom-level diplomatic agreement dissolved");
                }
            }

            // Cancel only proposals that involve a kingdom or landed clan on the other side
            foreach (var kvp in _proposals
                .Where(p => p.Value.ProposerClanId == clan.StringId
                         || p.Value.TargetClanId == clan.StringId)
                .ToList())
            {
                var proposal = kvp.Value;
                var other = proposal.ProposerClanId == clan.StringId
                    ? proposal.GetTarget()
                    : proposal.GetProposer();

                if (other == null || other.Kingdom != null || IsLanded(other))
                    _proposals.Remove(kvp.Key);
            }

            NotifyClanLeader(clan,
                "You have lost all your fiefs. Kingdom-level diplomatic agreements have been dissolved.");
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            try
            {
                if (clan == null || newKingdom == null) return;

                foreach (var alliance in GetAlliancesFor(clan).ToList())
                {
                    var other = alliance.GetOther(clan);
                    BreakAlliance(clan, other,
                        $"{clan.Name} has joined {newKingdom.Name} — clan alliance dissolved");
                }

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

        // We use SettlementEntered as a lightweight hook to detect fief loss
        // (MobileParty entering a settlement fires after ownership may have changed).
        // The daily tick is the real enforcement point.
        private readonly Dictionary<string, int> _lastKnownFiefCount = new();

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            // No-op: fief loss is polled on DailyTick to avoid excessive callbacks.
        }

        private void OnDailyTick()
        {
            // Expire stale proposals
            foreach (var key in _proposals
                .Where(kvp => kvp.Value.IsExpired())
                .Select(kvp => kvp.Key).ToList())
                _proposals.Remove(key);

            // Expire clan peace proposals
            foreach (var key in _clanPeaceProposals
                .Where(kvp => CampaignTime.Now.ToDays >= kvp.Value.expiresAtDays)
                .Select(kvp => kvp.Key).ToList())
                _clanPeaceProposals.Remove(key);

            foreach (var key in _clanCTWProposals
                .Where(kvp => kvp.Value.IsExpired())
                .Select(kvp => kvp.Key).ToList())
                _clanCTWProposals.Remove(key);

            // Fief-loss check for landed clans that have alliances
            foreach (var a in _alliances.Values.ToList())
            {
                CheckFiefLoss(a.GetClan1());
                CheckFiefLoss(a.GetClan2());
            }
        }

        private void CheckFiefLoss(Clan c)
        {
            if (c == null || c.Kingdom != null) return;
            int prev = _lastKnownFiefCount.TryGetValue(c.StringId, out int v) ? v : -1;
            int curr = c.Fiefs?.Count ?? 0;
            _lastKnownFiefCount[c.StringId] = curr;

            // Transitioned from landed → landless
            if (prev > 0 && curr == 0)
                OnClanLostLastFief(c);
        }

        private void OnHeroKilled(Hero victim, Hero killer,
            KillCharacterAction.KillCharacterActionDetail detail, bool show)
        {
            try
            {
                if (victim?.Clan == null || victim.Clan.Leader != victim) return;

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

        // ── Key helpers ───────────────────────────────────────────────────────

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

        // ── Notifications ─────────────────────────────────────────────────────

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
            if (clan?.Leader == null || !clan.Leader.IsAdopted()) return;

            string name = clan.Leader.FirstName.ToString()
                .Replace(BLTAdoptAHeroModule.Tag, "")
                .Replace(BLTAdoptAHeroModule.DevTag, "")
                .Trim();

            Log.LogFeedResponse($"@{name} {message}");
            Log.ShowInformation(message, clan.Leader.CharacterObject);
        }

        // ── Info builder ──────────────────────────────────────────────────────

        /// <summary>
        /// Appends a clan diplomacy section to a StringBuilder for !diplomacy info.
        /// Allied clans are shown with a (+N) vassal suffix; vassals themselves are hidden.
        /// </summary>
        public void AppendInfoSection(Clan clan, StringBuilder sb)
        {
            if (clan == null || clan.Kingdom != null) return;

            var alliances = GetAlliancesFor(clan);
            var proposals = GetProposalsFor(clan);

            if (alliances.Count == 0 && proposals.Count == 0) return;

            sb.Append(" | [Clan Alliances]");
            foreach (var a in alliances)
            {
                var other = a.GetOther(clan);
                if (other == null) continue;
                int days = (int)(CampaignTime.Now.ToDays - a.StartDays);
                string label = ClanDisplayLabel(other); // shows (+N) for vassals
                sb.Append($" {label}(+{days}d)");
            }

            if (proposals.Count > 0)
            {
                sb.Append(" | [Pending]");
                foreach (var p in proposals)
                {
                    var proposer = p.GetProposer();
                    if (proposer == null) continue;
                    sb.Append($" {ClanDisplayLabel(proposer)}({p.DaysRemaining()}d)");
                }
            }
        }
    }
}