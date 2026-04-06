using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BannerlordTwitch;
using BannerlordTwitch.Annotations;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.SaveSystem;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Achievements;
using BLTAdoptAHero.UI;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using TaleWorlds.CampaignSystem.Party;
using Helpers;
using TaleWorlds.LinQuick;

namespace BLTAdoptAHero.Behaviors
{
    public class BLTHeirBehavior : CampaignBehaviorBase
    {
        public Dictionary<Hero, Hero> heirList = new();
        public HashSet<Hero> _heirs = new();
        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, () =>
            {
                _heirs = heirList.Values.ToHashSet();
            });

            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, (victim, killer, actionDetail, showNotification) =>
            {
                if (_heirs.Contains(victim))
                {
                    _heirs.Remove(victim);
                    var key = heirList.FirstOrDefault(h => h.Value == victim).Key;
                    heirList.Remove(key);
                    Log.ShowInformation($"{key.Name}'s heir has died. Select a new one");
                }
            });

            CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, hero =>
            {
                var father = hero.Father;
                if (father != null && father.IsAdopted() && !heirList.ContainsKey(father))
                {
                    heirList[father] = hero;
                    _heirs.Add(hero);
                }
                else 
                {
                    var mother = hero.Mother;
                    if (mother != null && mother.IsAdopted() && !heirList.ContainsKey(mother))
                    {
                        heirList[mother] = hero;
                        _heirs.Add(hero);
                    }
                }
                
            });


        }
        public override void SyncData(IDataStore dataStore)
        {
            using var scopedJsonSync = new ScopedJsonSync(dataStore, nameof(BLTHeirBehavior));
            scopedJsonSync.SyncDataAsJson("HeirData", ref heirList);
        }
    }
}
