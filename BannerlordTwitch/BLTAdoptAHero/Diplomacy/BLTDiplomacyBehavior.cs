using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using BannerlordTwitch.Util;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Handles diplomacy event integration and cleanup.
    /// Peace blocking is done entirely in the Harmony patch (HarmonyPatches.cs BLTDiplomacyPatches).
    /// This class only handles post-peace cleanup and AI→BLT proposal creation.
    /// </summary>
    public class BLTDiplomacyBehavior : CampaignBehaviorBase
    {
        public static BLTDiplomacyBehavior Current { get; private set; }

        // Dedup: prevents the same AI peace attempt from creating duplicate proposals
        // within a short window (e.g. if the game engine retries the call).
        private readonly Dictionary<string, CampaignTime> _recentAIPeaceAttempts = new();

        public BLTDiplomacyBehavior()
        {
            Current = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // _recentAIPeaceAttempts is runtime-only; no persistence needed.
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        private void OnDailyTick()
        {
            // Expire old dedup records (older than 5 days)
            var stale = _recentAIPeaceAttempts
                .Where(kvp => (CampaignTime.Now - kvp.Value).ToDays > 5)
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var key in stale)
                _recentAIPeaceAttempts.Remove(key);
        }

        private string MakeKey(Kingdom k1, Kingdom k2)
        {
            var ids = new[] { k1.StringId, k2.StringId }.OrderBy(x => x).ToArray();
            return $"{ids[0]}_{ids[1]}";
        }

        // ── Public API (called by Harmony patch) ──────────────────────────────

        /// <summary>
        /// Called by the Harmony patch when an AI kingdom attempts to make peace with a BLT
        /// kingdom. The actual peace has already been blocked by the patch — no war re-declaration
        /// is needed here. We only create a pending proposal for the BLT player to accept/reject.
        /// </summary>
        public void HandleAIPeaceAttempt(Kingdom aiKingdom, Kingdom bltKingdom)
        {
            try
            {
                if (BLTTreatyManager.Current == null) return;

                // Dedup: ignore rapid-fire duplicate calls for the same pair
                string key = MakeKey(aiKingdom, bltKingdom);
                if (_recentAIPeaceAttempts.ContainsKey(key))
                {
                    _recentAIPeaceAttempts.Remove(key);
#if DEBUG
                    Log.Trace($"[BLT] Dedup: ignoring duplicate AI peace attempt {aiKingdom.Name} -> {bltKingdom.Name}");
#endif
                    return;
                }
                _recentAIPeaceAttempts[key] = CampaignTime.Now;

                // Use the base-game tribute model to price the proposal
                int dailyTribute = Campaign.Current.Models.DiplomacyModel.GetDailyTributeToPay(
                    aiKingdom.RulingClan, bltKingdom.RulingClan, out int duration);
                bool isOffer = dailyTribute > 0;

#if DEBUG
                Log.Trace($"[BLT] AI peace proposal: {aiKingdom.Name} -> {bltKingdom.Name} " +
                          $"(tribute: {dailyTribute}/day for {duration} days)");
#endif

                BLTTreatyManager.Current.CreatePeaceProposal(
                    aiKingdom, bltKingdom,
                    isOffer, Math.Abs(dailyTribute), duration,
                    goldCost: 0, influenceCost: 0, daysToAccept: 15);

                if (bltKingdom.Leader != null)
                {
                    string leaderName = bltKingdom.Leader.FirstName.ToString()
                        .Replace(BLTAdoptAHeroModule.Tag, "")
                        .Replace(BLTAdoptAHeroModule.DevTag, "")
                        .Trim();

                    string tributeMsg = dailyTribute != 0
                        ? $" {(isOffer ? "offering" : "demanding")} {Math.Abs(dailyTribute)}{Naming.Gold}/day for {duration} days"
                        : "";

                    Log.LogFeedResponse(
                        $"@{leaderName} {aiKingdom.Name} has proposed peace{tributeMsg}! " +
                        $"Use !diplomacy accept peace {aiKingdom.Name}");
                }

                Log.ShowInformation(
                    $"{aiKingdom.Name} has proposed peace with {bltKingdom.Name}",
                    bltKingdom.Leader?.CharacterObject);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] HandleAIPeaceAttempt error: {ex}");
            }
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnMakePeace(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
        {
            // NOTE: Because the Harmony patch on MakePeaceAction.ApplyInternal blocks peace for all
            // BLT-involved cases BEFORE the event fires, this handler only receives:
            //   a) BLT-initiated peace  (_allowDiplomacyAction == true)
            //   b) Purely AI-vs-AI peace (no BLT kingdoms)
            // There is no need to re-declare war here.
            try
            {
                if (BLTTreatyManager.Current == null) return;

                var k1 = faction1 as Kingdom;
                var k2 = faction2 as Kingdom;
                if (k1 == null || k2 == null) return;

                if (AdoptedHeroFlags._allowDiplomacyAction)
                {
                    // BLT-controlled peace — clean up our tracking records
                    BLTTreatyManager.Current.RemoveWar(k1, k2);
                    BLTTreatyManager.Current.RemovePeaceProposal(k1, k2);
                    BLTTreatyManager.Current.RemovePeaceProposal(k2, k1);
#if DEBUG
                    Log.Trace($"[BLT] BLT-initiated peace cleanup: {k1.Name} <-> {k2.Name}");
#endif
                    return;
                }

                // AI-vs-AI peace — just discard any stale war record
                if (BLTTreatyManager.Current.GetWar(k1, k2) != null)
                {
                    BLTTreatyManager.Current.RemoveWar(k1, k2);
#if DEBUG
                    Log.Trace($"[BLT] AI-only peace cleanup: {k1.Name} <-> {k2.Name}");
#endif
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnMakePeace error: {ex}");
            }
        }

        private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        {
            try
            {
                if (BLTTreatyManager.Current == null) return;

                var k1 = faction1 as Kingdom;
                var k2 = faction2 as Kingdom;
                if (k1 == null || k2 == null) return;

                // Skip if already tracked (e.g. BLTAllianceBehavior already created the record)
                if (BLTTreatyManager.Current.GetWar(k1, k2) != null)
                {
#if DEBUG
                    Log.Trace($"[BLT] War already tracked: {k1.Name} vs {k2.Name}");
#endif
                    var AttackerK = BLTTreatyManager.Current.GetWar(k1, k2).GetAttacker();
                    var DefenderK = BLTTreatyManager.Current.GetWar(k1, k2).GetDefender();
                    if (DefenderK?.Leader.IsAdopted() == true)
                        ActionManager.SendChat($"@{DefenderK.Leader.ToString().TrimEnd(']', 'T', 'L', 'B', '[', ' ')} {AttackerK} has declared war on {DefenderK}!");
                    return;
                }

                BLTTreatyManager.Current.CreateWar(k1, k2);
                var AttackerKingdom = BLTTreatyManager.Current.GetWar(k1, k2).GetAttacker();
                var DefenderKingdom = BLTTreatyManager.Current.GetWar(k1, k2).GetDefender();
                if (DefenderKingdom?.Leader.IsAdopted() == true)
                    ActionManager.SendChat($"@{DefenderKingdom.Leader.ToString().TrimEnd(']', 'T', 'L', 'B', '[', ' ')} {AttackerKingdom} has declared war on {DefenderKingdom}!");
#if DEBUG
                Log.Trace($"[BLT] War tracked: {k1.Name} vs {k2.Name} (Detail: {detail})");
#endif

                // Clear any stale peace proposals between these kingdoms
                if (BLTTreatyManager.Current.GetPeaceProposal(k1, k2) != null)
                    BLTTreatyManager.Current.RemovePeaceProposal(k1, k2);
                if (BLTTreatyManager.Current.GetPeaceProposal(k2, k1) != null)
                    BLTTreatyManager.Current.RemovePeaceProposal(k2, k1);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnWarDeclared error: {ex}");
            }
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            try
            {
#if DEBUG
                if (clan?.Leader != null && clan.Leader.IsAdopted())
                    Log.Trace($"[BLT] Adopted clan {clan.Name} changed from " +
                              $"{oldKingdom?.Name.ToString() ?? "none"} to {newKingdom?.Name.ToString() ?? "none"}");
#endif
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnClanChangedKingdom error: {ex}");
            }
        }

        private void OnKingdomDestroyed(Kingdom kingdom)
        {
            try
            {
#if DEBUG
                if (kingdom != null)
                    Log.Trace($"[BLT] Kingdom destroyed: {kingdom.Name}");
#endif
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnKingdomDestroyed error: {ex}");
            }
        }
    }
}