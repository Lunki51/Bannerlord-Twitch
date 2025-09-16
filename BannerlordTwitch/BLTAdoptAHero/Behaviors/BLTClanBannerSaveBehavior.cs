using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;


namespace BLTAdoptAHero
{
    public class BLTClanBannerSaveBehavior : CampaignBehaviorBase
    {
        public Dictionary<string, string> _banners = new();

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);

        }
        private void OnGameLoaded(CampaignGameStarter _)
        {
            foreach (var entry in _banners)
            {
                var clan = Clan.All.FirstOrDefault(c => c.StringId == entry.Key);
                if (clan != null)
                    ApplySavedBannerToClan(clan, entry.Value);
                Log.Trace($"clan {entry.Key}, banner {entry.Value}");
            }
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
                                          ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            if (clan != null && _banners.TryGetValue(clan.StringId, out var bannerCode))
                ApplySavedBannerToClan(clan, bannerCode);
        }

        public void UpdateClanBanner(Clan clan, string bannerCode)
        {
            ApplySavedBannerToClan(clan, bannerCode);
            SaveBanner(clan.StringId, bannerCode);
        }

        private void ApplySavedBannerToClan(Clan clan, string bannerCode)
        {
            try
            {
                clan.Banner.Deserialize(bannerCode);
                clan.Banner.SetBannerVisual(null);

                var visual = clan.Banner.BannerVisual;
                var convert = visual?.GetType().GetMethod("ConvertToMultiMesh",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                convert?.Invoke(visual, null);

                // Restore banner colors that may have been reset by game logic
                clan.Color = clan.Banner.GetPrimaryColor();
                clan.Color2 = clan.Banner.GetFirstIconColor();
            }
            catch
            {
                // Ignore unexpected errors
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("SavedBanners", ref _banners);
        }

        public void SaveBanner(string clanId, string bannerCode)
        {
            _banners[clanId] = bannerCode;
        }
    }
}
