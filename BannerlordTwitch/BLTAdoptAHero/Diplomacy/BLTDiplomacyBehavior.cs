using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using BannerlordTwitch.Util;
using BannerlordTwitch.Localization;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Handles diplomacy event integration and cleanup
    /// NOTE: Defensive alliance auto-join is handled by BLTAllianceBehavior.cs
    /// </summary>
    public class BLTDiplomacyBehavior : CampaignBehaviorBase
    {
        // Track recent AI peace attempts to prevent immediate re-war
        private System.Collections.Generic.Dictionary<string, CampaignTime> _recentAIPeaceAttempts
            = new System.Collections.Generic.Dictionary<string, CampaignTime>();

        public override void RegisterEvents()
        {
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No critical data to sync - _recentAIPeaceAttempts is runtime only
        }

        private void OnDailyTick()
        {
            // Clean up old peace attempt records (older than 5 days)
            var keysToRemove = _recentAIPeaceAttempts
                .Where(kvp => (CampaignTime.Now - kvp.Value).ToDays > 5)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _recentAIPeaceAttempts.Remove(key);
            }
        }

        /// <summary>
        /// Attempts to preserve war statistics when re-declaring war
        /// </summary>
        private void PreserveWarStats(Kingdom k1, Kingdom k2, BLTWar originalWar)
        {
            if (originalWar == null)
                return;

            try
            {
                var stance = k1.GetStanceWith(k2);
                if (stance == null)
                {
#if DEBUG
                    Log.Trace($"[BLT] Could not get stance to preserve stats");
#endif
                    return;
                }

                // Preserve the original war start date from our BLTWar record
                CampaignTime originalStartDate = originalWar.StartDate;

                // Try to set WarStartDate - check if it's settable
                var warStartDateProp = stance.GetType().GetProperty("WarStartDate");
                if (warStartDateProp != null && warStartDateProp.CanWrite)
                {
                    warStartDateProp.SetValue(stance, originalStartDate);
#if DEBUG
                    Log.Trace($"[BLT] Preserved war start date: {originalStartDate.ToString()}");
#endif
                }
                else if (warStartDateProp != null)
                {
                    // Property exists but is read-only, try reflection to force it
                    var backingField = stance.GetType().GetField("<WarStartDate>k__BackingField",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (backingField != null)
                    {
                        backingField.SetValue(stance, originalStartDate);
#if DEBUG
                        Log.Trace($"[BLT] Preserved war start date via reflection: {originalStartDate.ToString()}");
#endif
                    }
                    else
                    {
#if DEBUG
                        Log.Trace($"[BLT] Could not preserve war start date - property is read-only and no backing field found");
#endif
                    }
                }

                // Note: Casualties are typically tracked through battle events and may not be directly settable
                // The game's internal systems handle casualty tracking, so we may not be able to preserve these

#if DEBUG
                var warDuration = (CampaignTime.Now - originalStartDate).ToDays;
                Log.Trace($"[BLT] War stats preservation attempted for {k1.Name} vs {k2.Name} (original duration: {(int)warDuration} days)");
#endif
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Error preserving war stats: {ex.Message}");
            }
        }

        private string MakeKey(Kingdom k1, Kingdom k2)
        {
            var ids = new[] { k1.StringId, k2.StringId }.OrderBy(x => x).ToArray();
            return $"{ids[0]}_{ids[1]}";
        }

        private void OnMakePeace(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                if (BLTTreatyManager.Current == null)
                    return;

                var k1 = faction1 as Kingdom;
                var k2 = faction2 as Kingdom;

                if (k1 == null || k2 == null)
                    return;

                // If this is a BLT-controlled peace action, just track it and allow it
                if (AdoptedHeroFlags._allowDiplomacyAction)
                {
                    var war = BLTTreatyManager.Current.GetWar(k1, k2);
                    if (war != null)
                    {
                        BLTTreatyManager.Current.RemoveWar(k1, k2);
                    }
#if DEBUG
                    Log.Trace($"[BLT] BLT-controlled peace completed: {k1.Name} and {k2.Name}");
#endif
                    return;
                }

                // Get war record to check minimum duration
                var existingWar = BLTTreatyManager.Current.GetWar(k1, k2);

                // Check if either kingdom is BLT-controlled (non-player)
                bool k1IsBLT = k1.Leader != null && k1.Leader.IsAdopted() && k1 != Hero.MainHero?.Clan?.Kingdom;
                bool k2IsBLT = k2.Leader != null && k2.Leader.IsAdopted() && k2 != Hero.MainHero?.Clan?.Kingdom;
                bool anyBLT = k1IsBLT || k2IsBLT;

                // CRITICAL: Check minimum war duration FIRST, regardless of who's involved
                // This applies to BOTH BLT→AI and AI→BLT peace
                if (existingWar != null && anyBLT && !BLTTreatyManager.Current.CanMakePeace(k1, k2, out string minDurationReason))
                {
#if DEBUG
                    Log.Trace($"[BLT] Peace BLOCKED (min duration): {k1.Name} and {k2.Name} - {minDurationReason}");
#endif
                    // Re-declare war immediately - this is NOT a BLT diplomacy action
                    DeclareWarAction.ApplyByDefault(k1, k2);
                    FactionManager.DeclareWar(k1, k2);

                    // Preserve original war stats
                    PreserveWarStats(k1, k2, existingWar);

                    // Show message to both if BLT
                    if (k1IsBLT && k1.Leader != null)
                    {
                        string leaderName = k1.Leader.FirstName.ToString()
                            .Replace(BLTAdoptAHeroModule.Tag, "")
                            .Replace(BLTAdoptAHeroModule.DevTag, "")
                            .Trim();
                        Log.LogFeedResponse($"@{leaderName} Peace with {k2.Name} rejected - {minDurationReason}");
                    }
                    if (k2IsBLT && k2.Leader != null)
                    {
                        string leaderName = k2.Leader.FirstName.ToString()
                            .Replace(BLTAdoptAHeroModule.Tag, "")
                            .Replace(BLTAdoptAHeroModule.DevTag, "")
                            .Trim();
                        Log.LogFeedResponse($"@{leaderName} Peace with {k1.Name} rejected - {minDurationReason}");
                    }

                    Log.ShowInformation($"Peace rejected - {minDurationReason}", k1.Leader?.CharacterObject);
                    return;
                }

                // If no BLT kingdoms involved, just clean up war record
                if (!anyBLT)
                {
                    if (existingWar != null)
                    {
                        BLTTreatyManager.Current.RemoveWar(k1, k2);
#if DEBUG
                        Log.Trace($"[BLT] Cleaned up war tracking for AI-only peace: {k1.Name} and {k2.Name}");
#endif
                    }
                    return;
                }

                // At this point: at least one kingdom is BLT, and minimum duration is satisfied (or no war record)
                // NOW we handle AI→BLT peace proposals

                // Determine which is AI and which is BLT
                Kingdom aiKingdom = k1IsBLT ? k2 : k1;
                Kingdom bltKingdom = k1IsBLT ? k1 : k2;

                // If BOTH are BLT, don't create a proposal - this shouldn't happen via AI peace
                if (k1IsBLT && k2IsBLT)
                {
#if DEBUG
                    Log.Trace($"[BLT] Both kingdoms are BLT in non-BLT peace: {k1.Name} and {k2.Name} - cleaning up");
#endif
                    if (existingWar != null)
                    {
                        BLTTreatyManager.Current.RemoveWar(k1, k2);
                    }
                    return;
                }

                // Check deduplication
                string key = MakeKey(k1, k2);
                if (_recentAIPeaceAttempts.ContainsKey(key))
                {
#if DEBUG
                    Log.Trace($"[BLT] Duplicate peace attempt detected (dedup): {k1.Name} and {k2.Name}");
#endif
                    if (existingWar != null)
                    {
                        BLTTreatyManager.Current.RemoveWar(k1, k2);
                    }
                    _recentAIPeaceAttempts.Remove(key);
                    return;
                }

                // Mark that we're processing this peace
                _recentAIPeaceAttempts[key] = CampaignTime.Now;

#if DEBUG
                Log.Trace($"[BLT] Processing AI→BLT peace: {aiKingdom.Name} → {bltKingdom.Name}");
#endif

                // Calculate tribute using base game model
                int duration;
                int dailyTribute = Campaign.Current.Models.DiplomacyModel.GetDailyTributeToPay(
                    aiKingdom.RulingClan,
                    bltKingdom.RulingClan,
                    out duration
                );

                bool isOffer = dailyTribute > 0;

                // CRITICAL: Re-declare war FIRST to undo the peace
                DeclareWarAction.ApplyByDefault(k1, k2);
                FactionManager.DeclareWar(k1, k2);

                // Preserve original war stats if we had a war record
                PreserveWarStats(k1, k2, existingWar);

#if DEBUG
                Log.Trace($"[BLT] War re-declared between {k1.Name} and {k2.Name}");
#endif

                // Restore or create war record
                if (existingWar == null)
                {
                    existingWar = BLTTreatyManager.Current.CreateWar(k1, k2);
#if DEBUG
                    Log.Trace($"[BLT] Created new war record");
#endif
                }

                // Create peace proposal
                BLTTreatyManager.Current.CreatePeaceProposal(
                    aiKingdom,
                    bltKingdom,
                    isOffer,
                    Math.Abs(dailyTribute),
                    duration,
                    0, // No gold cost for AI proposals
                    0, // No influence cost for AI proposals
                    15  // 15 days to accept
                );

#if DEBUG
                Log.Trace($"[BLT] Created peace proposal from {aiKingdom.Name} to {bltKingdom.Name} (tribute: {dailyTribute})");
#endif

                // Notify the BLT kingdom leader
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
                        $"@{leaderName} {aiKingdom.Name} has proposed peace{tributeMsg}! Use !diplomacy accept peace {aiKingdom.Name}"
                    );
                }

                Log.ShowInformation(
                    $"{aiKingdom.Name} has proposed peace with {bltKingdom.Name}",
                    bltKingdom.Leader?.CharacterObject
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnMakePeace error: {ex}");
            }
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            try
            {
                if (BLTTreatyManager.Current == null)
                    return;

                if (clan?.Leader != null && clan.Leader.IsAdopted())
                {
#if DEBUG
                    Log.Trace($"[BLT] Adopted clan {clan.Name} changed from {oldKingdom?.Name.ToString() ?? "none"} to {newKingdom?.Name.ToString() ?? "none"}");
#endif
                }
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
                {
                    Log.Trace($"[BLT] Kingdom destroyed: {kingdom.Name}");
                }
#endif
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] OnKingdomDestroyed error: {ex}");
            }
        }
    }
}