using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Handles diplomacy event integration and cleanup
    /// NOTE: Defensive alliance auto-join is handled by BLTAllianceBehavior.cs
    /// </summary>
    public class BLTDiplomacyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No data to sync - this behavior just handles events
        }

        private void OnMakePeace(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
        {
            try
            {
                // Skip if BLT treaty system not initialized
                if (BLTTreatyManager.Current == null)
                    return;

                var k1 = faction1 as Kingdom;
                var k2 = faction2 as Kingdom;

                if (k1 == null || k2 == null)
                    return;

                // Clean up any BLT war tracking if peace was made outside BLT system
                var war = BLTTreatyManager.Current.GetWar(k1, k2);
                if (war != null && !AdoptedHeroFlags._allowDiplomacyAction)
                {
                    // Peace was made outside BLT system, clean up the war
                    BLTTreatyManager.Current.RemoveWar(k1, k2);
#if DEBUG
                    Log.Trace($"[BLT] Cleaned up war tracking for non-BLT peace: {k1.Name} and {k2.Name}");
#endif
                }
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
                // Skip if BLT treaty system not initialized
                if (BLTTreatyManager.Current == null)
                    return;

                // If a kingdom leader clan changes kingdom, we may need cleanup
                // Most cleanup is handled by OnKingdomDestroyed, but this catches edge cases
                if (clan?.Leader != null && clan.Leader.IsAdopted())
                {
#if DEBUG
                    Log.Trace($"[BLT] Adopted clan {clan.Name} changed from {oldKingdom?.Name ?? "none"} to {newKingdom?.Name ?? "none"}");
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
                // Cleanup is automatically handled by BLTTreatyManager.OnKingdomDestroyed
                // This event handler is here for any additional cleanup logic if needed
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