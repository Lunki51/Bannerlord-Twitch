using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace BLTAdoptAHero.Behaviors
{
    /// <summary>
    /// Persists per-kingdom flags that control whether BLT (adopted) heroes
    /// and/or AI (non-adopted) clans are allowed to join or take mercenary
    /// contracts with that kingdom.  Defaults to allowed for both.
    ///
    /// Register in your module's CampaignGameStarter.AddBehavior call.
    /// </summary>
    public class KingdomJoinPermissionsBehavior : CampaignBehaviorBase
    {
        public static KingdomJoinPermissionsBehavior Current =>
            Campaign.Current?.GetCampaignBehavior<KingdomJoinPermissionsBehavior>();

        // Keyed by Kingdom.StringId; absence == true (allowed)
        private Dictionary<string, bool> _allowBLT = new();
        private Dictionary<string, bool> _allowAI = new();

        public override void RegisterEvents() { }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("BLT_KingdomAllowBLT", ref _allowBLT);
            dataStore.SyncData("BLT_KingdomAllowAI", ref _allowAI);
        }

        // ── Getters (default true) ────────────────────────────────────────────

        public bool GetAllowBLT(Kingdom kingdom) =>
            kingdom == null || !_allowBLT.TryGetValue(kingdom.StringId, out bool v) || v;

        public bool GetAllowAI(Kingdom kingdom) =>
            kingdom == null || !_allowAI.TryGetValue(kingdom.StringId, out bool v) || v;

        // ── Setters ───────────────────────────────────────────────────────────

        public void SetAllowBLT(Kingdom kingdom, bool allow)
        {
            if (kingdom != null) _allowBLT[kingdom.StringId] = allow;
        }

        public void SetAllowAI(Kingdom kingdom, bool allow)
        {
            if (kingdom != null) _allowAI[kingdom.StringId] = allow;
        }
    }
}