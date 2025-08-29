using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero.Behaviors
{
    public class BLTMarriageBehavior : CampaignBehaviorBase
    {
        private readonly Dictionary<Hero, Clan> _previousClan = new();

        public override void RegisterEvents()
        {
            CampaignEvents.OnHeroChangedClanEvent.AddNonSerializedListener(this, OnHeroChangedClanEvent);
            CampaignEvents.HeroesMarried.AddNonSerializedListener(this, OnHeroesMarried);
        }

        private void OnHeroChangedClanEvent(Hero hero, Clan oldClan)
        {
            if (hero != null && !_previousClan.ContainsKey(hero))
                _previousClan[hero] = oldClan;
            Log.Trace($"oldClan {oldClan}");
        }

        private void OnHeroesMarried(Hero hero1, Hero hero2, bool showNotification)
        {
            if (hero1 == null || hero2 == null) return;

            _previousClan.TryGetValue(hero1, out Clan old1);
            _previousClan.TryGetValue(hero2, out Clan old2);

            bool h1WasBlt = old1?.Name.ToString().Contains("[BLT Clan]") ?? false;
            bool h2WasBlt = old2?.Name.ToString().Contains("[BLT Clan]") ?? false;

            if (h1WasBlt && h2WasBlt)
            {
                Log.Trace($"Clans BLT");
                return;
            }

            if (h1WasBlt && !h2WasBlt && !hero2.IsClanLeader)
            {
                hero1.Clan = old1;
                hero2.Clan = old1;
                Log.Trace($"hero2 to hero1");
            }
            else if (h2WasBlt && !h1WasBlt && !hero1.IsClanLeader)
            {
                hero1.Clan = old2;
                hero2.Clan = old2;
                Log.Trace($"hero1 to hero2");
            }

            _previousClan.Remove(hero1);
            _previousClan.Remove(hero2);
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}


