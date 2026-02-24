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

namespace BLTAdoptAHero
{
    public class BLTAdoptAHeroCampaignBehavior : CampaignBehaviorBase
    {
        public static BLTAdoptAHeroCampaignBehavior Current => Campaign.Current?.GetCampaignBehavior<BLTAdoptAHeroCampaignBehavior>();

        #region HeroData
        private class HeroData
        {
            public class RetinueData
            {
                public CharacterObject TroopType { get; set; }
                public int Level { get; set; }
                public int SavedTroopIndex { get; set; }
            }

            public class Retinue2Data
            {
                public CharacterObject TroopType { get; set; }
                public int Level { get; set; }
                public int SavedTroopIndex { get; set; }
            }

            public int Gold { get; set; }
            [UsedImplicitly]
            public List<RetinueData> Retinue { get; set; } = new();
            public List<Retinue2Data> Retinue2 { get; set; } = new();
            public int SpentGold { get; set; }
            public int EquipmentTier { get; set; } = -2;
            public Guid EquipmentClassID { get; set; }
            public Guid ClassID { get; set; }
            public string Owner { get; set; }
            public int Iteration { get; set; }
            public bool IsRetiredOrDead { get; set; }
            public bool IsCreatedHero { get; set; } = false;
            public string LegacyName { get; set; } = null;

            //public bool MesssageFlag { get; set; } = false;
            //public string MessageContent { get; set; } = null;

            [UsedImplicitly]
            public AchievementStatsData AchievementStats { get; set; } = new();

            public class SavedEquipment
            {
                public ItemObject Item { get; set; }
                [UsedImplicitly]
                public string ItemModifierId { get; set; }
                public int ItemSaveIndex { get; set; }

                [UsedImplicitly]
                public SavedEquipment() { }

                public SavedEquipment(EquipmentElement element)
                {
                    Item = element.Item;
                    ItemModifierId = element.ItemModifier.StringId;
                }

                public static explicit operator EquipmentElement(SavedEquipment m)
                    => new(m.Item, MBObjectManager.Instance.GetObject<ItemModifier>(m.ItemModifierId));
            }

            [UsedImplicitly]
            public List<SavedEquipment> SavedCustomItems { get; set; } = new();

            [JsonIgnore]
            public List<EquipmentElement> CustomItems { get; set; } = new();

            public void PreSave()
            {
                SavedCustomItems = CustomItems.Select(c => new SavedEquipment(c)).ToList();
            }

            public void PostLoad()
            {
                CustomItems = SavedCustomItems.Select(c => (EquipmentElement)c).ToList();
            }
        }

        private Dictionary<Hero, HeroData> heroData = new();
        public Dictionary<Hero, Hero> heirList = new();
        private Dictionary<Hero, HashSet<Guid>> heroAchievementPassivePowers = new();
        #endregion

        #region CampaignBehaviorBase        
        public override void RegisterEvents()
        {
            // We put all initialization that relies on loading being complete into this listener
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, () =>
            {
                // Ensure all existing heroes are registered
                foreach (var hero in CampaignHelpers.AllHeroes.Where(h => h.IsAdopted()))
                {
                    GetHeroData(hero);
                }

                // Clean up hero data
                foreach (var (hero, data) in heroData)
                {
                    // Remove invalid troop types (delayed to ensure all troop types are loaded)
                    data.Retinue.RemoveAll(r => r.TroopType == null);

                    // Remove invalid custom items
                    int removedCustomItems = data.CustomItems.RemoveAll(
                        i => i.Item == null || i.Item.Type == ItemObject.ItemTypeEnum.Invalid);
                    if (removedCustomItems > 0)
                    {
                        // Compensate with gold for each one lost
                        data.Gold += removedCustomItems * 50000;

                        Log.LogFeedSystem(
                            "{=hoRPbRrb}Compensated @{HeroName} with {GoldAmount}{GoldSymbol} for {CustomItemsCount} invalid custom items"
                                .Translate(
                                    ("HeroName", hero.Name),
                                    ("GoldAmount", removedCustomItems * 50000),
                                    ("GoldSymbol", Naming.Gold),
                                    ("CustomItemsCount", removedCustomItems)));
                    }

                    // Also remove them from the equipment
                    foreach (var (element, index) in hero.BattleEquipment
                        .YieldFilledEquipmentSlots()
                        .Where(i => i.element.Item.Type == ItemObject.ItemTypeEnum.Invalid))
                    {
                        hero.BattleEquipment[index] = EquipmentElement.Invalid;
                    }                   
                }

                // Retire up any dead heroes (do this last to ensure all other stuff related to this hero is updated, in-case retirement interferes with it)
                foreach (var (hero, _) in heroData.Where(h => h.Key.IsDead && !h.Value.IsRetiredOrDead))
                {
                    RetireHero(hero);
                }
                MapHub.UpdateMapData();
                heroAchievementPassivePowers ??= new Dictionary<Hero, HashSet<Guid>>();
            });

            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, (victim, killer, detail, _) =>
            {
                if (victim?.IsAdopted() == true || killer?.IsAdopted() == true)
                {
                    string verb = KillDetailVerb(detail);
                    if (killer != null && victim != null)
                    {
                        Log.LogFeedEvent("{=PCPU0lPX}@{VictimName} {Verb} by @{KillerName}!"
                            .Translate(
                                ("VictimName", victim.Name),
                                ("Verb", verb),
                                ("KillerName", killer.Name)));
                    }
                    else if (killer != null)
                    {
                        Log.LogFeedEvent("{=Bji2ULge}@{KillerName} {Verb}!"
                            .Translate(
                                ("KillerName", killer.Name),
                                ("Verb", verb)));
                    }
                }
            });

            CampaignEvents.HeroLevelledUp.AddNonSerializedListener(this, (hero, _) =>
            {
                if (hero.IsAdopted())
                    Log.LogFeedEvent("{=8aTmTvl8}@{HeroName} is now level {Level}!"
                        .Translate(("HeroName", hero.Name), ("Level", hero.Level)));
            });

            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, (party, hero) =>
            {
                if (hero.IsAdopted())
                {
                    if (party != null)
                        Log.LogFeedEvent("{=4PqVnFWY}@{HeroName} was taken prisoner by {PartyName}!"
                            .Translate(("HeroName", hero.Name), ("PartyName", party.Name)));
                    else
                        Log.LogFeedEvent("{=WeRWLpKn}@{HeroName} was taken prisoner!"
                            .Translate(("HeroName", hero.Name)));
                }
            });

            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, (hero, party, faction, endDetail, capturedDuringBattle) =>
            {
                if (hero.IsAdopted())
                {
                    if (party != null)
                        Log.LogFeedEvent("{=tQANBoTK}@{HeroName} is no longer a prisoner of {PartyName}!"
                            .Translate(("HeroName", hero.Name), ("PartyName", party.Name)));
                    else
                        Log.LogFeedEvent("{=MQslOwr0}@{HeroName} is no longer a prisoner!"
                            .Translate(("HeroName", hero.Name)));
                }
            });


            CampaignEvents.OnHeroChangedClanEvent.AddNonSerializedListener(this, (hero, clan) =>
            {
                if (hero.IsAdopted())
                    Log.LogFeedEvent("{=SUdnIyfw}@{HeroName} moved from {FromClanName} to {ToClanName}!"
                        .Translate(
                            ("HeroName", hero.Name),
                            ("FromClanName", clan?.Name.ToString() ?? "no clan"),
                            ("ToClanName", hero.Clan?.Name.ToString() ?? "no clan")));
            });

            CampaignEvents.MapEventStarted.AddNonSerializedListener(this,
            (mapEvent, attackerParty, defenderParty) =>
            {
                if (mapEvent == null) return;
                else if (mapEvent.IsPlayerMapEvent)
                {
                    MapHub.UpdateMapData();
                    return;
                }
                string eventType = mapEvent.EventType switch
                {
                    MapEvent.BattleTypes.FieldBattle => "field battle",
                    MapEvent.BattleTypes.Raid => "raid",
                    MapEvent.BattleTypes.Siege => "siege",
                    MapEvent.BattleTypes.Hideout => "hideout battle",
                    MapEvent.BattleTypes.SallyOut => "sally out",
                    MapEvent.BattleTypes.SiegeOutside => "siege relief",
                    _ => "unknown battle"
                };
                
                foreach (var side in mapEvent.InvolvedParties)
                {
                    var hero = side.MobileParty?.LeaderHero;
                    if (hero != null && hero.IsAdopted())
                    {

                        var adoptedSide = hero.PartyBelongedTo.MapEventSide;
                        var enemySide = hero.PartyBelongedTo.MapEventSide.OtherSide;

                        string opponentName = enemySide.LeaderParty.Name.ToString();

                        Log.LogFeedEvent("{=fFW54iwx}@{HeroParty} is involved in a {EventType} against {Opponent}!"
                            .Translate(
                                ("HeroParty", hero.PartyBelongedTo.Name.ToString()),
                                ("EventType", eventType),
                                ("Opponent", opponentName)
                            ));
                    }
                }
            });

            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, mapEvent =>
            {
                if (mapEvent == null || mapEvent.BattleState == 0) return;
                else if (mapEvent.IsPlayerMapEvent)
                {
                    MapHub.UpdateMapData();
                    return;
                }
                string eventType = mapEvent.EventType switch
                {
                    MapEvent.BattleTypes.FieldBattle => "field battle",
                    MapEvent.BattleTypes.Raid => "raid",
                    MapEvent.BattleTypes.Siege => "siege",
                    MapEvent.BattleTypes.Hideout => "hideout battle",
                    MapEvent.BattleTypes.SallyOut => "sally out",
                    MapEvent.BattleTypes.SiegeOutside => "outside siege",
                    _ => "unknown battle"
                };
                foreach (var side in mapEvent.InvolvedParties)
                {
                    var hero = side.MobileParty?.LeaderHero;
                    if (hero != null && hero.IsAdopted())
                    {

                        var adoptedSide = hero.PartyBelongedTo.MapEventSide;

                        bool result = mapEvent.Winner == hero.PartyBelongedTo.MapEventSide;

                        var enemySide = hero.PartyBelongedTo.MapEventSide.OtherSide;
                        string opponentName = enemySide.LeaderParty.Name.ToString();

                        Log.LogFeedEvent("{=Bsfc1uYG}@{HeroName} has {Result} a {EventType} against {Opponent}!"
                        .Translate(
                            ("HeroName", hero.PartyBelongedTo.Name.ToString()),
                            ("Result", result ? "won" : "lost"),
                            ("EventType", eventType),
                            ("Opponent", opponentName))); 
                    }
                }
            });

            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, siegeEvent =>
            {
                if (siegeEvent == null || !siegeEvent.BesiegedSettlement.Owner.IsAdopted()) return;
                else Log.LogFeedEvent("{=TESTING}@{HeroName} settlement {sett} is under siege!".Translate(("HeroName", siegeEvent.BesiegedSettlement.Owner.Name.ToString()), ("sett", siegeEvent.BesiegedSettlement.Name.ToString())));
            });

            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, JoinTournament.SetupGameMenus);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        private void OnDailyTick()
        {
            MapHub.UpdateMapData();
        }

        //private Mission lastMission = null;
        //private void OnTick(float dt)
        //{
        //    // Check mission status every tick to immediately hide/show map
        //    if (Mission.Current != lastMission)
        //    {
        //        lastMission = Mission.Current;
        //        MapHub.UpdateMapData(); // Force immediate update when mission changes
        //    }
        //}
        public override void SyncData(IDataStore dataStore)
        {
            using var scopedJsonSync = new ScopedJsonSync(dataStore, nameof(BLTAdoptAHeroCampaignBehavior));

            scopedJsonSync.SyncDataAsJson("HeroData", ref heroData);

            if (dataStore.IsLoading)
            {
                Dictionary<Hero, HeroData> oldHeroData = null;
                scopedJsonSync.SyncDataAsJson("HeroData", ref oldHeroData);

                List<Hero> usedHeroList = null;
                dataStore.SyncData("UsedHeroObjectList", ref usedHeroList);

                List<CharacterObject> usedCharList = null;
                dataStore.SyncData("UsedCharacterObjectList", ref usedCharList);

                Dictionary<int, HeroData> heroData2 = null;
                scopedJsonSync.SyncDataAsJson("HeroData2", ref heroData2);
                if (heroData2 == null && oldHeroData != null)
                {
                    heroData = oldHeroData;
                }
                else if (heroData2 != null)
                {
                    heroData = heroData2.ToDictionary(kv
                        => usedHeroList[kv.Key], kv => kv.Value);
                    foreach (var r in heroData.Values.SelectMany(h => h.Retinue))
                    {
                        r.TroopType = usedCharList[r.SavedTroopIndex];
                    }
                }

                List<ItemObject> saveItemList = null;
                dataStore.SyncData("SavedItems", ref saveItemList);
                foreach (var h in heroData.Values)
                {
                    if (saveItemList != null)
                    {
                        foreach (var i in h.SavedCustomItems)
                        {
                            i.Item = saveItemList[i.ItemSaveIndex];
                        }
                    }
                    h.PostLoad();
                }

                foreach (var (hero, data) in heroData)
                {
                    // Try and find an appropriate character to replace the missing retinue with
                    foreach (var r in data.Retinue.Where(r => r.TroopType == null))
                    {
                        r.TroopType = hero.Culture?.EliteBasicTroop
                            ?.UpgradeTargets?.SelectRandom()
                            ?.UpgradeTargets?.SelectRandom();
                    }

                    // Remove any we couldn't replace
                    int removedRetinue = data.Retinue.RemoveAll(r => r.TroopType == null);

                    // Compensate with gold for each one lost
                    data.Gold += removedRetinue * 50000;

                    // Update EquipmentTier if it isn't set
                    if (data.EquipmentTier == -2)
                    {
                        data.EquipmentTier = EquipHero.CalculateHeroEquipmentTier(hero);
                    }

                    // Set owner name from the hero name
                    data.Owner ??= hero.FirstName.ToString();

                    data.IsRetiredOrDead = !hero.IsAdopted();
                }

                // // Move heroes already marked as dead or retired (no BLT tag) into the dead/retired list
                // foreach (var (hero, data) in heroData.Where(h => !h.Key.IsAdopted()).ToList())
                // {
                //     heroData.Remove(hero);
                //     retiredOrDeadHeroData.Add(hero, data);
                // }

                //Achievements
                Dictionary<int, List<Guid>> achievementPowersIndexed = null;
                scopedJsonSync.SyncDataAsJson("HeroAchievementPassivePowers", ref achievementPowersIndexed);

                if (achievementPowersIndexed != null && usedHeroList != null)
                {
                    heroAchievementPassivePowers = achievementPowersIndexed
                        .Where(kvp => kvp.Key < usedHeroList.Count)
                        .ToDictionary(
                            kvp => usedHeroList[kvp.Key],
                            kvp => new HashSet<Guid>(kvp.Value)
                        );
                }
                else
                {
                    heroAchievementPassivePowers = new Dictionary<Hero, HashSet<Guid>>();
                }
            }
            else
            {               

                // Need to explicitly write out the Heroes and CharacterObjects so we can look them up by index in the HeroData
                var usedCharList = heroData.Values
                    .SelectMany(h => h.Retinue.Select(r => r.TroopType)).Distinct().ToList();
                dataStore.SyncData("UsedCharacterObjectList", ref usedCharList);

                var usedHeroList = heroData.Keys.ToList();
                dataStore.SyncData("UsedHeroObjectList", ref usedHeroList);

                // var heroImtes = heroData.Values.SelectMany(h => h.)

                foreach (var r in heroData.Values.SelectMany(h => h.Retinue))
                {
                    r.SavedTroopIndex = usedCharList.IndexOf(r.TroopType);
                }

                var saveItemList = new List<ItemObject>();
                foreach (var h in heroData.Values)
                {
                    // PreSave first to update SavedCustomItems
                    h.PreSave();
                    foreach (var i in h.SavedCustomItems)
                    {
                        i.ItemSaveIndex = saveItemList.Count;
                        saveItemList.Add(i.Item);
                    }
                }

                dataStore.SyncData("SavedItems", ref saveItemList);

                var heroDataSavable = heroData.ToDictionary(kv
                    => usedHeroList.IndexOf(kv.Key), kv => kv.Value);
                scopedJsonSync.SyncDataAsJson("HeroData2", ref heroDataSavable);

                // Achievement Powers
                var achievementPowersIndexed = heroAchievementPassivePowers
                    .Where(kvp => usedHeroList.Contains(kvp.Key))
                    .ToDictionary(
                        kvp => usedHeroList.IndexOf(kvp.Key),
                        kvp => kvp.Value.ToList()
                    );
                scopedJsonSync.SyncDataAsJson("HeroAchievementPassivePowers", ref achievementPowersIndexed);
            }
        }
        #endregion

        #region Adoption
        public void InitAdoptedHero(Hero newHero, string userName)
        {
            var hd = GetHeroData(newHero);
            hd.Owner = userName;
            hd.IsRetiredOrDead = false;
            hd.LegacyName = newHero.Name.ToString();
            hd.Iteration = GetAncestors(userName).Max(a => (int?)a.Iteration + 1) ?? 0;
            SetHeroAdoptedName(newHero, userName);
        }

        public Hero GetAdoptedHero(string name)
        {
            var foundHero = heroData.FirstOrDefault(h
                    => !h.Value.IsRetiredOrDead
                       && (string.Equals(h.Key.FirstName?.Raw(), name, StringComparison.CurrentCultureIgnoreCase)
                           || string.Equals(h.Value.Owner, name, StringComparison.CurrentCultureIgnoreCase)))
                .Key;

            // correct the name to match the viewer name casing
            if (foundHero != null && foundHero.FirstName?.Raw() != name)
            {
                SetHeroAdoptedName(foundHero, name);
            }

            if (foundHero?.IsDead == true)
            {
                RetireHero(foundHero);
                return null;
            }

            return foundHero;
        }

        public Hero GetRetiredHero(string name)
        {
            var foundHero = heroData.LastOrDefault(h
                    => h.Value.IsRetiredOrDead
                       && (string.Equals(h.Key.FirstName?.Raw(), name, StringComparison.CurrentCultureIgnoreCase)
                           || string.Equals(h.Value.Owner, name, StringComparison.CurrentCultureIgnoreCase) 
                            ))
                .Key;

            return foundHero;
        }

        public void RetireHero(Hero hero)
        {
            var data = GetHeroData(hero, suppressAutoRetire: true);
            if (data.IsRetiredOrDead) return;

            string desc = hero.IsDead ? "deceased" : "retired";
            string oldName = hero.Name.ToString();
            string baseName = oldName.Replace(" [BLT]", "").Trim();
            baseName = baseName.Replace(" [DEV]", "").Trim();
            var all = Hero.AllAliveHeroes.Concat(Hero.DeadOrDisabledHeroes);
            int highest = 0;

            foreach (var h in all)
            {
                if (h == hero || h.Name == null) continue;
                var t = h.Name.ToString().Split(' ');
                int di = Array.FindIndex(t, x => x.Equals("retired", StringComparison.OrdinalIgnoreCase) || x.Equals("deceased", StringComparison.OrdinalIgnoreCase));
                if (di < 1) continue;
                string rn = "", mn = "";
                if (di >= 2 && t[di - 1].All(c => "IVXLCDM".Contains(char.ToUpper(c))))
                {
                    rn = t[di - 1];
                    mn = t[di - 2];
                }
                else
                {
                    mn = t[di - 1];
                }
                if (!mn.Equals(baseName, StringComparison.OrdinalIgnoreCase)) continue;
                int val = 0;
                if (!string.IsNullOrEmpty(rn))
                {
                    for (int i = 0; i < rn.Length; i++)
                    {
                        int cur = "IVXLCDM".IndexOf(char.ToUpper(rn[i])) switch
                        {
                            0 => 1,
                            1 => 5,
                            2 => 10,
                            3 => 50,
                            4 => 100,
                            5 => 500,
                            6 => 1000,
                            _ => 0
                        };
                        int next = i + 1 < rn.Length ? "IVXLCDM".IndexOf(char.ToUpper(rn[i + 1])) switch
                        {
                            0 => 1,
                            1 => 5,
                            2 => 10,
                            3 => 50,
                            4 => 100,
                            5 => 500,
                            6 => 1000,
                            _ => 0
                        } : 0;
                        val += cur < next ? -cur : cur;
                    }
                }

                if (val > highest) highest = val;
            }

            string finalName = $"{baseName} {ToRoman(highest + 1)} {desc}";
            CampaignHelpers.SetHeroName(hero, new TextObject(finalName), new TextObject(baseName));
            CampaignHelpers.RemoveEncyclopediaBookmarkFromItem(hero);
            BLTTournamentQueueBehavior.Current.RemoveFromQueue(hero);

            Log.LogFeedEvent("{=2PHPNmuv}{OldName} is {RetireType}!"
                .Translate(("OldName", oldName), ("RetireType", desc)));

            Log.Info("{=wzpkEmTL}Dead or retired hero {OldName} renamed to {HeroName}"
                .Translate(("OldName", oldName), ("HeroName", hero.Name)));

            data.IsRetiredOrDead = true;
        }

        #endregion

        #region Gold
        public int GetHeroGold(Hero hero) =>
            // #if DEBUG
            // 1000000000
            // #else
            GetHeroData(hero).Gold
        // #endif
        ;

        public void SetHeroGold(Hero hero, int gold) => GetHeroData(hero).Gold = gold;

        public int ChangeHeroGold(Hero hero, int change, bool isSpending = false)
        {
            var hd = GetHeroData(hero);
            hd.Gold = Math.Max(0, change + hd.Gold);
            if (isSpending && change < 0)
            {
                hd.SpentGold += -change;
            }
            return hd.Gold;
        }

        public int InheritGold(Hero inheritor, float amount)
        {
            var ancestors = GetAncestors(inheritor.FirstName.ToString());
            int inheritance = (int)(ancestors.Sum(a => a.SpentGold + a.Gold) * amount);
            ChangeHeroGold(inheritor, inheritance);
            foreach (var data in ancestors)
            {
                data.SpentGold = 0;
                data.Gold = 0;
            }
            return inheritance;
        }

        private List<HeroData> GetAncestors(string name) =>
            heroData
                .Where(h
                    => h.Value.IsRetiredOrDead
                       && string.Equals(h.Value.Owner, name, StringComparison.CurrentCultureIgnoreCase))
                .Select(kv => kv.Value)
                .OrderBy(d => d.Iteration)
                .ToList();

        #endregion

        #region Stats and achievements
        public void IncreaseKills(Hero hero, Agent killed, WeaponClass WeaponClass = 0)
        {
            if (killed?.IsAdopted() == true)
            {
                IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalViewerKills, 1);
            }
            else if (killed?.GetHero() == Hero.MainHero)
            {
                IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalStreamerKills, 1);
            }
            else if (killed?.IsHero == true)
            {
                IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalHeroKills, 1);
            }
            else if (killed?.IsMount == true)
            {
                IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalMountKills, 1);
            }
            IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalKills, 1);

            var Weapon = WeaponClass.ToString();
            if (Weapon == "Undefined")
            {
                if (Weapon == "Dagger")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalDaggerKills, 1);
                    }
                else if (Weapon == "OneHandedSword")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.Total1HSwordKills, 1);
                    }
                else if (Weapon == "TwoHandedSword")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.Total2HSwordKills, 1);
                    }
                else if (Weapon == "OneHandedAxe")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.Total1HAxeKills, 1);
                    }
                else if (Weapon == "TwoHandedAxe")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.Total2HAxeKills, 1);
                    }
                else if (Weapon == "Mace")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.Total1HMaceKills, 1);
                    }
                else if (Weapon == "Pick")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalPickKills, 1);
                    }
                else if (Weapon == "TwoHandedMace")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.Total2HMaceKills, 1);
                    }
                else if (Weapon == "OneHandedPolearm")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.Total1HPoleKills, 1);
                    }
                else if (Weapon == "TwoHandedPolearm")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.Total2HPoleKills, 1);
                    }
                else if (Weapon == "Stone")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalStoneKills, 1);
                    }
                else if (Weapon == "Bow" || Weapon == "Arrow")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalBowKills, 1);
                    }
                else if (Weapon == "Crossbow" || Weapon == "Bolt")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalXBowKills, 1);
                    }
                else if (Weapon == "Sling" || Weapon == "SlingStone")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalSlingKills, 1);
                    }
                else if (Weapon == "ThrowingAxe")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalThrowAxeKills, 1);
                    }
                else if (Weapon == "ThrowingKnife")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalThrowKnifeKills, 1);
                    }
                else if (Weapon == "Javelin")
                    {
                        IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalJavelinKills, 1);
                    }
            }
        }

        public void IncreaseParticipationCount(Hero hero, bool playerSide, bool forced)
        {
            IncreaseStatistic(hero, playerSide
                ? AchievementStatsData.Statistic.Summons
                : AchievementStatsData.Statistic.Attacks, 1, forced);
        }

        public void IncreaseHeroDeaths(Hero hero, Agent killer)
        {
            if (killer?.IsAdopted() == true)
            {
                IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalViewerDeaths, 1);
            }
            else if (killer?.GetHero() == Hero.MainHero)
            {
                IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalStreamerDeaths, 1);
            }
            else if (killer?.IsMount == true)
            {
                IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalMountDeaths, 1);
            }
            else if (killer?.Character?.IsHero == true)
            {
                IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalHeroDeaths, 1);
            }

            IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalDeaths, 1);
        }

        public void IncreaseTournamentRoundLosses(Hero hero)
            => IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalTournamentRoundLosses, 1);

        public void IncreaseTournamentRoundWins(Hero hero)
            => IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalTournamentRoundWins, 1);

        public void IncreaseTournamentChampionships(Hero hero)
            => IncreaseStatistic(hero, AchievementStatsData.Statistic.TotalTournamentFinalWins, 1);

        private void IncreaseStatistic(Hero hero, AchievementStatsData.Statistic statistic, int amount, bool forced = false)
        {
            var achievementStatsData = GetHeroData(hero).AchievementStats;

            achievementStatsData.UpdateValue(statistic, hero.GetClass()?.ID ?? default, amount, forced);

            CheckForAchievements(hero);
        }

        private void CheckForAchievements(Hero hero)
        {
            var achievementData = GetHeroData(hero).AchievementStats;

            var newAchievements = BLTAdoptAHeroModule.CommonConfig.ValidAchievements?
                .Where(a => a.IsAchieved(hero))
                .Where(a
                    => !achievementData.Achievements.Contains(a.ID)) ?? Enumerable.Empty<AchievementDef>();

            foreach (var achievement in newAchievements)
            {
                if (!LocString.IsNullOrEmpty(achievement.NotificationText))
                {
                    string message = achievement.NotificationText.ToString()
                        .Replace("[viewer]", hero.FirstName.ToString())
                        .Replace("[name]", achievement.Name.ToString())
                        ;
                    Log.ShowInformation(message, hero.CharacterObject,
                        BLTAdoptAHeroModule.CommonConfig.KillStreakPopupAlertSound);
                }

                achievementData.Achievements.Add(achievement.ID);

                achievement.Apply(hero);
            }
        }

        public int GetAchievementTotalStat(Hero hero, AchievementStatsData.Statistic type)
            => GetHeroData(hero)?.AchievementStats?.GetTotalValue(type) ?? 0;

        public int GetAchievementClassStat(Hero hero, AchievementStatsData.Statistic type)
            => GetHeroData(hero)?.AchievementStats?.GetClassValue(type, hero.GetClass()?.ID ?? Guid.Empty) ?? 0;

        public int GetAchievementClassStat(Hero hero, Guid classGuid, AchievementStatsData.Statistic type)
            => GetHeroData(hero)?.AchievementStats?.GetClassValue(type, classGuid) ?? 0;

        public IEnumerable<AchievementDef> GetAchievements(Hero hero) =>
            GetHeroData(hero)?.AchievementStats?.Achievements?
                .Select(a => BLTAdoptAHeroModule.CommonConfig.GetAchievement(a))
                .Where(a => a != null)
            ?? Enumerable.Empty<AchievementDef>();

        #endregion

        #region Equipment
        public int GetEquipmentTier(Hero hero) => GetHeroData(hero).EquipmentTier;
        public void SetEquipmentTier(Hero hero, int tier) => GetHeroData(hero).EquipmentTier = tier;
        public HeroClassDef GetEquipmentClass(Hero hero)
            => BLTAdoptAHeroModule.HeroClassConfig.GetClass(GetHeroData(hero).EquipmentClassID);
        public void SetEquipmentClass(Hero hero, HeroClassDef classDef)
            => GetHeroData(hero).EquipmentClassID = classDef?.ID ?? Guid.Empty;
        #endregion

        #region Custom Items

        public (EquipmentElement element, string error) FindCustomItemByIndex(Hero hero, string itemIndexStr)
            => FindCustomItem(hero, itemIndexStr.StartsWith("#") ? itemIndexStr : "#" + itemIndexStr);

        public (EquipmentElement element, string error) FindCustomItem(Hero hero, string itemName)
        {
            var customItems = GetCustomItems(hero);
            if (!customItems.Any())
            {
                return (EquipmentElement.Invalid, "{=Tfj1U5BB}No custom items".Translate());
            }

            if (itemName.StartsWith("#"))
            {
                // Index is 1 based, not 0
                if (!int.TryParse(itemName.Substring(1), out int index) || index < 1 || index > customItems.Count)
                {
                    return (EquipmentElement.Invalid, "{=T6XnYOC9}Invalid item index".Translate());
                }
                return (customItems[index - 1], default);
            }
            var matchingItems = customItems.Where(i => i.GetModifiedItemName()
                    .ToString().IndexOf(itemName, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .ToList();

            if (matchingItems.Count == 0)
            {
                return (EquipmentElement.Invalid, "{=p0urrIvR}No custom items found matching '{Args}'".Translate(("Args", itemName)));
            }

            if (matchingItems.Count > 1)
            {
                return (EquipmentElement.Invalid, "{=Pzo2UJrl}{Count} custom items found matching '{Args}', be more specific"
                        .Translate(("Count", matchingItems.Count), ("Args", itemName)));
            }

            return (matchingItems.First(), default);
        }

        public List<EquipmentElement> GetCustomItems(Hero hero) => GetHeroData(hero).CustomItems;

        public void AddCustomItem(Hero hero, EquipmentElement element)
        {
            if (!BLTCustomItemsCampaignBehavior.Current.IsRegistered(element.ItemModifier))
            {
                Log.Error($"Item {element.GetModifiedItemName()} of {hero.Name} is NOT a custom item, so shouldn't be added to Custom Items storage");
                return;
            }
            var data = GetHeroData(hero);
            if (!data.CustomItems.Any(i => i.IsEqualTo(element)))
            {
                data.CustomItems.Add(element);
                Log.Info($"Item {element.GetModifiedItemName()} added to storage of {hero.Name}");
            }
        }

        public void RemoveCustomItem(Hero hero, EquipmentElement element)
        {
            var data = GetHeroData(hero);

            data.CustomItems.RemoveAll(i => i.IsEqualTo(element));

            foreach (var slot in hero.BattleEquipment
                .YieldEquipmentSlots()
                .Where(i => i.element.IsEqualTo(element)))
            {
                hero.BattleEquipment[slot.index] = EquipmentElement.Invalid;
            }

            foreach (var slot in hero.CivilianEquipment
                .YieldEquipmentSlots()
                .Where(i => i.element.IsEqualTo(element)))
            {
                hero.CivilianEquipment[slot.index] = EquipmentElement.Invalid;
            }
        }

        public IEnumerable<EquipmentElement> InheritCustomItems(Hero inheritor, int maxItems)
        {
            string inheritorName = inheritor.FirstName?.Raw();
            var ancestors = heroData.Where(h => h.Key != inheritor && h.Key.FirstName?.Raw() == inheritorName).ToList();
            var items = ancestors.SelectMany(a => a.Value.CustomItems).Shuffle().Take(maxItems).ToList();
            foreach (var item in items)
            {
                AddCustomItem(inheritor, item);
            }
            foreach (var (_, value) in ancestors)
            {
                value.CustomItems.Clear();
            }
            return items;
        }

        private class Auction
        {
            public EquipmentElement item;
            public Hero itemOwner;
            public int reservePrice;
            private readonly Dictionary<Hero, int> bids = new();

            public Auction(EquipmentElement item, Hero itemOwner, int reservePrice)
            {
                this.item = item;
                this.itemOwner = itemOwner;
                this.reservePrice = reservePrice;
            }

            public (bool success, string description) Bid(Hero bidder, int bid)
            {
                if (itemOwner == bidder)
                {
                    return (false, "{=2cbVbW91}You can't bid on your own item".Translate());
                }

                if (bid < reservePrice)
                {
                    return (false, "{=rbhzuJLm}Bid of {Bid}{GoldIcon} does not meet reserve price of {ReservePrice}{GoldIcon}"
                        .Translate(
                            ("Bid", bid),
                            ("GoldIcon", Naming.Gold),
                            ("ReservePrice", reservePrice)
                            ));
                }

                if (bids.Values.Any(v => v == bid))
                {
                    return (false, "{=83uZcndH}Another bid at {Bid}{GoldIcon} already exists"
                        .Translate(
                            ("Bid", bid),
                            ("GoldIcon", Naming.Gold)
                            ));
                }

                if (bids.TryGetValue(bidder, out int currBid) && currBid >= bid)
                {
                    return (false, "{=qeLF80xw}You already bid more ({Bid}{GoldIcon}), you can only raise your bid"
                        .Translate(
                            ("Bid", bid),
                            ("GoldIcon", Naming.Gold)
                        ));
                }

                int bidderGold = Current.GetHeroGold(bidder);
                if (bidderGold < bid)
                {
                    return (false, "{=Cqi0iYNR}You cannot cover a bid of {Bid}{GoldIcon}, you only have {BidderGold}{GoldIcon}"
                        .Translate(
                            ("Bid", bid),
                            ("GoldIcon", Naming.Gold),
                            ("BidderGold", bidderGold)
                        ));
                }

                bids[bidder] = bid;

                return (true, "{=M2B9yQ4w}Bid of {Bid}{GoldIcon} placed!"
                    .Translate(
                        ("Bid", bid),
                        ("GoldIcon", Naming.Gold)
                    ));
            }

            public (Hero hero, int bid) GetHighestValidBid() => bids
                .Select(x => (hero: x.Key, bid: x.Value))
                .Where(x => x.hero.IsAdopted() && !x.hero.IsDead && Current.GetHeroGold(x.hero) >= x.bid)
                .OrderByDescending(x => x.bid)
                .FirstOrDefault();
        }

        private Auction currentAuction;

        public bool AuctionInProgress => currentAuction != null;

        public async void StartItemAuction(EquipmentElement item, Hero itemOwner,
            int reservePrice, int durationInSeconds, int reminderInterval, Action<string> output)
        {
            if (AuctionInProgress)
                return;

            currentAuction = new(item, itemOwner, reservePrice);

            // Count down in chunks with reminder of the auction status
            while (durationInSeconds > reminderInterval)
            {
                await Task.Delay(TimeSpan.FromSeconds(reminderInterval));
                durationInSeconds -= reminderInterval;
                int seconds = durationInSeconds;
                MainThreadSync.Run(() =>
                {
                    var highestBid = currentAuction.GetHighestValidBid();
                    if (highestBid != default)
                    {
                        output("{=TeeDJyJ1}{Time} seconds left in auction of '{ItemName}', high bid is {HighestBid}{GoldIcon} (@{HighestBidderName})"
                            .Translate(
                                ("Time", seconds),
                                ("ItemName", RewardHelpers.GetItemNameAndModifiers(item)),
                                ("HighestBid", highestBid.bid),
                                ("GoldIcon", Naming.Gold),
                                ("HighestBidderName", highestBid.hero.FirstName)));
                    }
                    else
                    {
                        output("{=jNkGaKZw}{Time} seconds left in auction of '{ItemName}', no bids placed"
                            .Translate(
                                ("Time", seconds),
                                ("ItemName", RewardHelpers.GetItemNameAndModifiers(item))));
                    }
                });
            }

            await Task.Delay(TimeSpan.FromSeconds(durationInSeconds));

            MainThreadSync.Run(() =>
            {
                try
                {
                    var highestBid = currentAuction.GetHighestValidBid();
                    if (highestBid == default)
                    {
                        output("{=d9ooHVPU}Auction for '{ItemName}' is FINISHED! The item will remain with @{ItemOwnerName}, as no bid met the reserve price of {ReservePrice}{GoldIcon}."
                            .Translate(
                                ("ItemName", RewardHelpers.GetItemNameAndModifiers(currentAuction.item)),
                                ("ItemOwnerName", currentAuction.itemOwner.FirstName),
                                ("ReservePrice", currentAuction.reservePrice),
                                ("GoldIcon", Naming.Gold)));
                        return;
                    }

                    if (!currentAuction.itemOwner.IsAdopted() || currentAuction.itemOwner.IsDead)
                    {
                        output("{=SGuTRcui}Auction for '{ItemName}' is CANCELLED! @{ItemOwnerName} retired or died."
                            .Translate(
                                ("ItemName", RewardHelpers.GetItemNameAndModifiers(currentAuction.item)),
                                ("ItemOwnerName", currentAuction.itemOwner.FirstName)));
                        return;
                    }

                    if (!GetCustomItems(currentAuction.itemOwner).Any(i => i.IsEqualTo(currentAuction.item)))
                    {
                        output("{=NRV4IstE}Auction for '{ItemName}' is CANCELLED! @{ItemOwnerName} is no longer in possession of the item."
                            .Translate(
                                ("ItemName", RewardHelpers.GetItemNameAndModifiers(currentAuction.item)),
                                ("ItemOwnerName", currentAuction.itemOwner.FirstName)));
                        return;
                    }

                    output("{=jmbMoHta}Auction for '{ItemName}' is FINISHED! The item will go to @{HighestBidderName} for {HighestBid}{GoldIcon}."
                        .Translate(
                            ("ItemName", RewardHelpers.GetItemNameAndModifiers(currentAuction.item)),
                            ("HighestBidderName", highestBid.hero.FirstName),
                            ("HighestBid", highestBid.bid),
                            ("GoldIcon", Naming.Gold)));

                    TransferCustomItem(currentAuction.itemOwner, highestBid.hero,
                        currentAuction.item, highestBid.bid);
                }
                finally
                {
                    currentAuction = null;
                }
            });
        }

        public void TransferCustomItem(Hero oldOwner, Hero newOwner, EquipmentElement item, int transferFee)
        {
            if (transferFee != 0)
            {
                ChangeHeroGold(newOwner, -transferFee, isSpending: true);
                ChangeHeroGold(oldOwner, transferFee);
            }

            RemoveCustomItem(oldOwner, item);
            AddCustomItem(newOwner, item);

            // Update the equipment of both, this should only modify the slots related to the custom item
            // (the gap in the previous owners equipment and optionally equipping the new item)
            EquipHero.UpgradeEquipment(oldOwner, GetEquipmentTier(oldOwner), oldOwner.GetClass(), replaceSameTier: false);
            EquipHero.UpgradeEquipment(newOwner, GetEquipmentTier(newOwner), newOwner.GetClass(), replaceSameTier: false);
        }

        public void DiscardCustomItem(Hero owner, EquipmentElement item)
        {
            RemoveCustomItem(owner, item);

            // Update equipment, this should only modify the slots related to the custom item
            EquipHero.UpgradeEquipment(owner, GetEquipmentTier(owner), owner.GetClass(), replaceSameTier: false);
        }

        public (bool success, string description) AuctionBid(Hero bidder, int bid)
        {
            return currentAuction?.Bid(bidder, bid)
                   ?? (false, "{=Cy38Ckpk}No auction in progress".Translate());
        }

        #endregion

        #region Class
        public HeroClassDef GetClass(Hero hero)
            => BLTAdoptAHeroModule.HeroClassConfig.GetClass(GetHeroData(hero).ClassID);

        public void SetClass(Hero hero, HeroClassDef classDef)
            => GetHeroData(hero).ClassID = classDef?.ID ?? Guid.Empty;
        #endregion

        #region Achievement Passive Powers        

        public void AddAchievementPassivePower(Hero hero, Guid achievementId)
        {
            if (!heroAchievementPassivePowers.ContainsKey(hero))
            {
                heroAchievementPassivePowers[hero] = new HashSet<Guid>();
            }
            heroAchievementPassivePowers[hero].Add(achievementId);
        }

        public IEnumerable<Guid> GetHeroAchievementPassivePowers(Hero hero)
        {
            return heroAchievementPassivePowers.TryGetValue(hero, out var powers)
                ? powers
                : Enumerable.Empty<Guid>();
        }

        public void ApplyAchievementPassivePowers(Hero hero)
        {
            if (BLTAdoptAHeroModule.CommonConfig?.Achievements == null || !BLTAdoptAHeroModule.CommonConfig.Achievements.Any(a => a.GivePassivePower)) return;

            var achievementPowerIds = GetHeroAchievementPassivePowers(hero);
            var ach = BLTAdoptAHeroCampaignBehavior.Current
                        .GetAchievements(hero).Where(a => a.IsAchieved(hero))
                        .ToList();

            foreach (var achievementId in achievementPowerIds)
            {
                var achievement = BLTAdoptAHeroModule.CommonConfig.Achievements
                    .FirstOrDefault(a => a.ID == achievementId);

                if (achievement?.GivePassivePower == true && achievement.PassivePowerReward != null && ach.Contains(achievement))
                {
                    // The PassivePowerGroup.OnHeroJoinedBattle will handle creating/getting handlers
                    achievement.PassivePowerReward.OnHeroJoinedBattle(hero);
                }
            }
        }


        #endregion

        #region Retinue
        public IEnumerable<CharacterObject> GetRetinue(Hero hero)
            => GetHeroData(hero).Retinue.Select(r => r.TroopType);

        [CategoryOrder("Limits", 1),
         CategoryOrder("Costs", 2),
         CategoryOrder("Troop Types", 3)]
        public class RetinueSettings : IDocumentable
        {
            [LocDisplayName("{=wAGE7h6U}Max Retinue Size"),
             LocCategory("Limits", "{=1lHWj3nT}Limits"),
             LocDescription("{=EOGB8EWN}Maximum number of units in the retinue. Recommend less than 20, summons to NOT obey the games unit limits."),
             PropertyOrder(1), UsedImplicitly]
            public int MaxRetinueSize { get; set; } = 5;

            [LocDisplayName("{=VvdtvdQJ}Cost Tier 1"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=9bvn5R5A}Gold cost for Tier 1 retinue"),
             PropertyOrder(1), UsedImplicitly]
            public int CostTier1 { get; set; } = 25000;

            [LocDisplayName("{=engRDMZx}Cost Tier 2"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=pD2ZvRVH}Gold cost for Tier 2 retinue"),
             PropertyOrder(2), UsedImplicitly]
            public int CostTier2 { get; set; } = 50000;

            [LocDisplayName("{=3jxmITht}Cost Tier 3"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=dyB8loLF}Gold cost for Tier 3 retinue"),
             PropertyOrder(3), UsedImplicitly]
            public int CostTier3 { get; set; } = 100000;

            [LocDisplayName("{=dhwd4ccF}Cost Tier 4"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=aji2HxKa}Gold cost for Tier 4 retinue"),
             PropertyOrder(4), UsedImplicitly]
            public int CostTier4 { get; set; } = 175000;

            [LocDisplayName("{=zJkb4AIh}Cost Tier 5"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=fnEOFst7}Gold cost for Tier 5 retinue"),
             PropertyOrder(5), UsedImplicitly]
            public int CostTier5 { get; set; } = 275000;

            [LocDisplayName("{=1hh3cOJO}Cost Tier 6"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=PieENBSG}Gold cost for Tier 6 retinue"),
             PropertyOrder(6), UsedImplicitly]
            public int CostTier6 { get; set; } = 400000;

            // etc..
            public int GetTierCost(int tier)
            {
                return tier switch
                {
                    0 => CostTier1,
                    1 => CostTier2,
                    2 => CostTier3,
                    3 => CostTier4,
                    4 => CostTier5,
                    5 => CostTier6,
                    _ => CostTier6
                };
            }

            [LocDisplayName("{=q1Rkm3Rq}Use Heroes Culture Units"),
             LocCategory("Troop Types", "{=qYhM3gcn}Troop Types"),
             LocDescription("{=9qAD6eZR}Whether to use the adopted hero's culture (if not enabled then a random one is used)"),
             PropertyOrder(1), UsedImplicitly]
            public bool UseHeroesCultureUnits { get; set; } = true;

            [LocDisplayName("{=dbU7WEKG}Include Bandit Units"),
             LocCategory("Troop Types", "{=qYhM3gcn}Troop Types"),
             LocDescription("{=06KnYhyh}Whether to allow bandit units when UseHeroesCultureUnits is disabled"),
             PropertyOrder(2), UsedImplicitly]
            public bool IncludeBanditUnits { get; set; }

            [LocDisplayName("{=E2RBmb1K}Use Basic Troops"),
             LocCategory("Troop Types", "{=qYhM3gcn}Troop Types"),
             LocDescription("{=uPwaOKdT}Whether to allow basic troops"),
             PropertyOrder(3), UsedImplicitly]
            public bool UseBasicTroops { get; set; } = true;

            [LocDisplayName("{=lnz7d1BI}Use Elite Troops"),
             LocCategory("Troop Types", "{=qYhM3gcn}Troop Types"),
             LocDescription("{=EPr2clqT}Whether to allow elite troops"),
             PropertyOrder(4), UsedImplicitly]
            public bool UseEliteTroops { get; set; } = true;

            [LocDisplayName("{=MilitiaRetAllowName}Use Militia Troops"),
             LocCategory("Troop Types", "{=qYhM3gcn}Troop Types"),
             LocDescription("{=MilitiaRetAllowDesc}Whether to allow Militia troops (Will be taken from Hero's culture!)"),
             PropertyOrder(3), UsedImplicitly]
            public bool UseMilitiaTroops { get; set; } = true;

            [LocDisplayName("{=EliteMilitiaRetAllowName}Use Elite Militia Troops"),
             LocCategory("Troop Types", "{=qYhM3gcn}Troop Types"),
             LocDescription("{=EliteMilitiaRetAllowDesc}Whether to allow Elite Militia troops (Will be taken from Hero's culture!)"),
             PropertyOrder(3), UsedImplicitly]
            public bool UseEliteMilitiaTroops { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.PropertyValuePair("{=UhUpH8C8}Max retinue".Translate(), $"{MaxRetinueSize}");
                generator.PropertyValuePair("{=VBuncBq5}Tier costs".Translate(), $"1={CostTier1}{Naming.Gold}, 2={CostTier2}{Naming.Gold}, 3={CostTier3}{Naming.Gold}, 4={CostTier4}{Naming.Gold}, 5={CostTier5}{Naming.Gold}, 6={CostTier6}{Naming.Gold}");
                var allowed = new List<string>();
                if (UseHeroesCultureUnits) allowed.Add("{=R7rU0TbD}Same culture only".Translate());
                if (IncludeBanditUnits) allowed.Add("{=c2qOsXvs}Bandits".Translate());
                if (UseBasicTroops) allowed.Add("{=RmTwEFzy}Basic troops".Translate());
                if (UseEliteTroops) allowed.Add("{=3gumlthG}Elite troops".Translate());
                if (UseMilitiaTroops) allowed.Add("{=MilitiaTag}Militia troops".Translate());
                if (UseEliteMilitiaTroops) allowed.Add("{=EliteMilitiaTag}Elite militia troops".Translate());
                generator.PropertyValuePair("{=uL7MfYPc}Allowed".Translate(), string.Join(", ", allowed));
            }
        }

        public (bool success, string status) UpgradeRetinue(Hero hero, RetinueSettings settings, int maxToUpgrade)
        {
            var availableTroops = CampaignHelpers.AllCultures
                .Where(c => settings.IncludeBanditUnits || c.IsMainCulture)
                .SelectMany(c =>
                {
                    var troopTypes = new List<CharacterObject>();
                    if (settings.UseBasicTroops && c.BasicTroop != null) troopTypes.Add(c.BasicTroop);
                    if (settings.UseEliteTroops && c.EliteBasicTroop != null) troopTypes.Add(c.EliteBasicTroop);
                    if (settings.UseMilitiaTroops && (c.MeleeMilitiaTroop != null && c.RangedMilitiaTroop != null)) troopTypes.Add(c.MeleeMilitiaTroop); troopTypes.Add(c.RangedMilitiaTroop);
                    if (settings.UseEliteMilitiaTroops && (c.MeleeEliteMilitiaTroop != null && c.RangedEliteMilitiaTroop != null)) troopTypes.Add(c.MeleeEliteMilitiaTroop); troopTypes.Add(c.RangedEliteMilitiaTroop);
                    return troopTypes;
                })
                // At least 2 upgrade tiers available
                .Where(c => (c.UpgradeTargets?.FirstOrDefault()?.UpgradeTargets?.Any() == true) || ((settings.UseMilitiaTroops || settings.UseEliteMilitiaTroops) && (c == c.Culture.MeleeMilitiaTroop || c == c.Culture.RangedMilitiaTroop || c == c.Culture.MeleeEliteMilitiaTroop || c == c.Culture.RangedEliteMilitiaTroop)))
                .ToList();

            if (!availableTroops.Any())
            {
                return (false, "{=bBCyH0vV}No valid troop types could be found, please check your settings".Translate());
            }

            var heroRetinue = GetHeroData(hero).Retinue;

            var retinueChanges = new Dictionary<HeroData.RetinueData, (CharacterObject oldTroopType, int totalSpent)>();

            int heroGold = GetHeroGold(hero);
            int totalCost = 0;

            var results = new List<string>();

            while (maxToUpgrade-- > 0)
            {
                // first fill in any missing ones
                if (heroRetinue.Count < settings.MaxRetinueSize)
                {
                    var troopType = availableTroops
                        .Shuffle()
                        // Sort same culture units to the front if required, but still include other units in-case the hero
                        // culture doesn't contain the requires units
                        .OrderBy(c => settings.UseHeroesCultureUnits && c.Culture != hero.Culture)
                        .FirstOrDefault();

                    int cost = settings.GetTierCost(0);
                    if (totalCost + cost > heroGold)
                    {
                        results.Add(retinueChanges.IsEmpty()
                            ? Naming.NotEnoughGold(cost, heroGold)
                            : "{=zcbOq6Tb}Spent {TotalCost}{GoldIcon}, {RemainingGold}{GoldIcon} remaining"
                                .Translate(
                                    ("TotalCost", totalCost),
                                    ("GoldIcon", Naming.Gold),
                                    ("RemainingGold", heroGold - totalCost)));
                        break;
                    }
                    totalCost += cost;

                    var retinue = new HeroData.RetinueData { TroopType = troopType, Level = 1 };
                    heroRetinue.Add(retinue);
                    retinueChanges.Add(retinue, (null, cost));
                }
                else
                {
                    // upgrade the lowest tier unit
                    var retinueToUpgrade = heroRetinue
                        .OrderBy(h => h.TroopType.Tier)
                        .FirstOrDefault(t => t.TroopType.UpgradeTargets?.Any() == true);

                    if (retinueToUpgrade != null)
                    {
                        int cost = settings.GetTierCost(retinueToUpgrade.Level);
                        if (totalCost + cost > heroGold)
                        {
                            results.Add(retinueChanges.IsEmpty()
                                ? Naming.NotEnoughGold(cost, heroGold)
                                : "{=zcbOq6Tb}Spent {TotalCost}{GoldIcon}, {RemainingGold}{GoldIcon} remaining"
                                    .Translate(
                                        ("TotalCost", totalCost),
                                        ("GoldIcon", Naming.Gold),
                                        ("RemainingGold", heroGold - totalCost)));
                            break;
                        }

                        totalCost += cost;

                        var oldTroopType = retinueToUpgrade.TroopType;
                        retinueToUpgrade.TroopType = oldTroopType.UpgradeTargets.SelectRandom();
                        retinueToUpgrade.Level++;
                        if (retinueChanges.TryGetValue(retinueToUpgrade, out var upgradeRecord))
                        {
                            retinueChanges[retinueToUpgrade] =
                                (upgradeRecord.oldTroopType ?? oldTroopType, upgradeRecord.totalSpent + cost);
                        }
                        else
                        {
                            retinueChanges.Add(retinueToUpgrade, (oldTroopType, cost));
                        }
                    }
                    else
                    {
                        results.Add("{=PQRLJ04i}Can't upgrade retinue any further!".Translate());
                        break;
                    }
                }
            }

            var troopUpgradeSummary = new List<string>();
            foreach ((var oldTroopType, var newTroopType, int cost, int num) in retinueChanges
                .GroupBy(r
                    => (r.Value.oldTroopType, newTroopType: r.Key.TroopType))
                .Select(g => (
                        g.Key.oldTroopType,
                        g.Key.newTroopType,
                        cost: g.Sum(f => f.Value.totalSpent),
                        num: g.Count()))
                .OrderBy(g => g.oldTroopType == null)
                .ThenBy(g => g.num)
            )
            {
                if (oldTroopType != null)
                {
                    troopUpgradeSummary.Add($"{oldTroopType}{Naming.To}{newTroopType}" +
                                            (num > 1 ? $" x{num}" : "") +
                                            $" ({Naming.Dec}{cost}{Naming.Gold})");
                }
                else
                {
                    troopUpgradeSummary.Add($"{newTroopType}" +
                                            (num > 1 ? $" x{num}" : "") +
                                            $" ({Naming.Dec}{cost}{Naming.Gold})");

                }
            }

            if (totalCost > 0)
            {
                ChangeHeroGold(hero, -totalCost, isSpending: true);
            }

            return (retinueChanges.Any(), Naming.JoinList(troopUpgradeSummary.Concat(results)));
        }

        public void KillRetinue(Hero retinueOwnerHero, BasicCharacterObject retinueCharacterObject)
        {
            var heroRetinue = GetHeroData(retinueOwnerHero).Retinue;
            var matchingRetinue = heroRetinue.FirstOrDefault(r => r.TroopType == retinueCharacterObject);
            if (matchingRetinue != null)
            {
                heroRetinue.Remove(matchingRetinue);
            }
            else
            {
                //Log.Error($"Couldn't find matching retinue type {retinueCharacterObject} " +
                //          $"for {retinueOwnerHero} to remove");
            }
        }

        public void KillRetinueAtIndex(Hero retinueOwnerHero, int index)
        {
            var heroRetinue = GetHeroData(retinueOwnerHero).Retinue;

            if (index >= 0 && index < heroRetinue.Count)
            {
                heroRetinue.RemoveAt(index);
            }
            else
            {
                Log.Error($"Invalid retinue index {index} for {retinueOwnerHero}. Retinue count: {heroRetinue.Count}");
            }
        }

        #endregion

        #region retinue2
        public IEnumerable<CharacterObject> GetRetinue2(Hero hero)
            => GetHeroData(hero).Retinue2.Select(r => r.TroopType);

        [CategoryOrder("Limits", 1),
         CategoryOrder("Costs", 2),
         CategoryOrder("Troop Types", 3)]
        public class Retinue2Settings : IDocumentable
        {
            [LocDisplayName("{=wAGE7h6U}Max secondary retinue Size"),
             LocCategory("Limits", "{=1lHWj3nT}Limits"),
             LocDescription("{=EOGB8EWN}Maximum number of units in the secondary retinue. Recommend less than 20, summons do NOT obey the games unit limits."),
             PropertyOrder(1), UsedImplicitly]
            public int MaxRetinue2Size { get; set; } = 5;

            [LocDisplayName("{=VvdtvdQJ}Cost Tier 1"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=9bvn5R5A}Gold cost for Tier 1 secondary retinue"),
             PropertyOrder(1), UsedImplicitly]
            public int CostTier1 { get; set; } = 25000;

            [LocDisplayName("{=engRDMZx}Cost Tier 2"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=pD2ZvRVH}Gold cost for Tier 2 secondary retinue"),
             PropertyOrder(2), UsedImplicitly]
            public int CostTier2 { get; set; } = 50000;

            [LocDisplayName("{=3jxmITht}Cost Tier 3"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=dyB8loLF}Gold cost for Tier 3 secondary retinue"),
             PropertyOrder(3), UsedImplicitly]
            public int CostTier3 { get; set; } = 100000;

            [LocDisplayName("{=dhwd4ccF}Cost Tier 4"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=aji2HxKa}Gold cost for Tier 4 secondary retinue"),
             PropertyOrder(4), UsedImplicitly]
            public int CostTier4 { get; set; } = 175000;

            [LocDisplayName("{=zJkb4AIh}Cost Tier 5"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=fnEOFst7}Gold cost for Tier 5 secondary retinue"),
             PropertyOrder(5), UsedImplicitly]
            public int CostTier5 { get; set; } = 275000;

            [LocDisplayName("{=1hh3cOJO}Cost Tier 6"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=PieENBSG}Gold cost for Tier 6 secondary retinue"),
             PropertyOrder(6), UsedImplicitly]
            public int CostTier6 { get; set; } = 400000;

            // etc..
            public int GetTierCost(int tier)
            {
                return tier switch
                {
                    0 => CostTier1,
                    1 => CostTier2,
                    2 => CostTier3,
                    3 => CostTier4,
                    4 => CostTier5,
                    5 => CostTier6,
                    _ => CostTier6
                };
            }

            [LocDisplayName("{=q1Rkm3Rq}Use Heroes Culture Units"),
             LocCategory("Troop Types", "{=qYhM3gcn}Troop Types"),
             LocDescription("{=9qAD6eZR}Whether to use the adopted hero's culture (if not enabled then a random one is used)"),
             PropertyOrder(1), UsedImplicitly]
            public bool UseHeroesCultureUnits { get; set; } = true;

            [LocDisplayName("{=dbU7WEKG}Include Bandit Units"),
             LocCategory("Troop Types", "{=qYhM3gcn}Troop Types"),
             LocDescription("{=06KnYhyh}Whether to allow bandit units when UseHeroesCultureUnits is disabled"),
             PropertyOrder(2), UsedImplicitly]
            public bool IncludeBanditUnits { get; set; }

            [LocDisplayName("{=E2RBmb1K}Use Basic Troops"),
             LocCategory("Troop Types", "{=qYhM3gcn}Troop Types"),
             LocDescription("{=uPwaOKdT}Whether to allow basic troops"),
             PropertyOrder(3), UsedImplicitly]
            public bool UseBasicTroops { get; set; } = true;

            [LocDisplayName("{=lnz7d1BI}Use Elite Troops"),
             LocCategory("Troop Types", "{=qYhM3gcn}Troop Types"),
             LocDescription("{=EPr2clqT}Whether to allow elite troops"),
             PropertyOrder(4), UsedImplicitly]
            public bool UseEliteTroops { get; set; } = true;

            [LocDisplayName("{=MilitiaRetAllowName}Use Militia Troops"),
             LocCategory("Troop Types", "{=qYhM3gcn}Troop Types"),
             LocDescription("{=MilitiaRetAllowDesc}Whether to allow Militia troops (Will be taken from Hero's culture!)"),
             PropertyOrder(3), UsedImplicitly]
            public bool UseMilitiaTroops { get; set; } = true;

            [LocDisplayName("{=EliteMilitiaRetAllowName}Use Elite Militia Troops"),
             LocCategory("Troop Types", "{=qYhM3gcn}Troop Types"),
             LocDescription("{=EliteMilitiaRetAllowDesc}Whether to allow Elite Militia troops (Will be taken from Hero's culture!)"),
             PropertyOrder(3), UsedImplicitly]
            public bool UseEliteMilitiaTroops { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.PropertyValuePair("{=UhUpH8C8}Max secondary retinue".Translate(), $"{MaxRetinue2Size}");
                generator.PropertyValuePair("{=VBuncBq5}Tier costs".Translate(), $"1={CostTier1}{Naming.Gold}, 2={CostTier2}{Naming.Gold}, 3={CostTier3}{Naming.Gold}, 4={CostTier4}{Naming.Gold}, 5={CostTier5}{Naming.Gold}, 6={CostTier6}{Naming.Gold}");
                var allowed = new List<string>();
                if (UseHeroesCultureUnits) allowed.Add("{=R7rU0TbD}Same culture only".Translate());
                if (IncludeBanditUnits) allowed.Add("{=c2qOsXvs}Bandits".Translate());
                if (UseBasicTroops) allowed.Add("{=RmTwEFzy}Basic troops".Translate());
                if (UseEliteTroops) allowed.Add("{=3gumlthG}Elite troops".Translate());
                if (UseMilitiaTroops) allowed.Add("{=MilitiaTag}Militia troops".Translate());
                if (UseEliteMilitiaTroops) allowed.Add("{=EliteMilitiaTag}Elite militia troops".Translate());
                generator.PropertyValuePair("{=uL7MfYPc}Allowed".Translate(), string.Join(", ", allowed));
            }
        }

        public (bool success, string status) UpgradeRetinue2(Hero hero, Retinue2Settings settings, int maxToUpgrade)
        {
            var availableTroops = CampaignHelpers.AllCultures
                .Where(c => settings.IncludeBanditUnits || c.IsMainCulture)
                .SelectMany(c =>
                {
                    var troopTypes = new List<CharacterObject>();
                    if (settings.UseBasicTroops && c.BasicTroop != null) troopTypes.Add(c.BasicTroop);
                    if (settings.UseEliteTroops && c.EliteBasicTroop != null) troopTypes.Add(c.EliteBasicTroop);
                    if (settings.UseMilitiaTroops && (c.MeleeMilitiaTroop != null && c.RangedMilitiaTroop != null)) troopTypes.Add(c.MeleeMilitiaTroop); troopTypes.Add(c.RangedMilitiaTroop);
                    if (settings.UseEliteMilitiaTroops && (c.MeleeEliteMilitiaTroop != null && c.RangedEliteMilitiaTroop != null)) troopTypes.Add(c.MeleeEliteMilitiaTroop); troopTypes.Add(c.RangedEliteMilitiaTroop);
                    return troopTypes;
                })
                // At least 2 upgrade tiers available
                .Where(c => (c.UpgradeTargets?.FirstOrDefault()?.UpgradeTargets?.Any() == true) || ((settings.UseMilitiaTroops || settings.UseEliteMilitiaTroops) && (c == c.Culture.MeleeMilitiaTroop || c == c.Culture.RangedMilitiaTroop || c == c.Culture.MeleeEliteMilitiaTroop || c == c.Culture.RangedEliteMilitiaTroop)))
                .ToList();

            if (!availableTroops.Any())
            {
                return (false, "{=bBCyH0vV}No valid troop types could be found, please check out settings".Translate());
            }

            var heroretinue2 = GetHeroData(hero).Retinue2;

            var retinue2Changes = new Dictionary<HeroData.Retinue2Data, (CharacterObject oldTroopType, int totalSpent)>();

            int heroGold = GetHeroGold(hero);
            int totalCost = 0;

            var results = new List<string>();

            while (maxToUpgrade-- > 0)
            {
                // first fill in any missing ones
                if (heroretinue2.Count < settings.MaxRetinue2Size)
                {
                    var troopType = availableTroops
                        .Shuffle()
                        // Sort same culture units to the front if required, but still include other units in-case the hero
                        // culture doesn't contain the requires units
                        .OrderBy(c => settings.UseHeroesCultureUnits && c.Culture != hero.Culture)
                        .FirstOrDefault();

                    int cost = settings.GetTierCost(0);
                    if (totalCost + cost > heroGold)
                    {
                        results.Add(retinue2Changes.IsEmpty()
                            ? Naming.NotEnoughGold(cost, heroGold)
                            : "{=zcbOq6Tb}Spent {TotalCost}{GoldIcon}, {RemainingGold}{GoldIcon} remaining"
                                .Translate(
                                    ("TotalCost", totalCost),
                                    ("GoldIcon", Naming.Gold),
                                    ("RemainingGold", heroGold - totalCost)));
                        break;
                    }
                    totalCost += cost;

                    var retinue2 = new HeroData.Retinue2Data { TroopType = troopType, Level = 1 };
                    heroretinue2.Add(retinue2);
                    retinue2Changes.Add(retinue2, (null, cost));
                }
                else
                {
                    // upgrade the lowest tier unit
                    var Retinue2ToUpgrade = heroretinue2
                        .OrderBy(h => h.TroopType.Tier)
                        .FirstOrDefault(t => t.TroopType.UpgradeTargets?.Any() == true);

                    if (Retinue2ToUpgrade != null)
                    {
                        int cost = settings.GetTierCost(Retinue2ToUpgrade.Level);
                        if (totalCost + cost > heroGold)
                        {
                            results.Add(retinue2Changes.IsEmpty()
                                ? Naming.NotEnoughGold(cost, heroGold)
                                : "{=zcbOq6Tb}Spent {TotalCost}{GoldIcon}, {RemainingGold}{GoldIcon} remaining"
                                    .Translate(
                                        ("TotalCost", totalCost),
                                        ("GoldIcon", Naming.Gold),
                                        ("RemainingGold", heroGold - totalCost)));
                            break;
                        }

                        totalCost += cost;

                        var oldTroopType = Retinue2ToUpgrade.TroopType;
                        Retinue2ToUpgrade.TroopType = oldTroopType.UpgradeTargets.SelectRandom();
                        Retinue2ToUpgrade.Level++;
                        if (retinue2Changes.TryGetValue(Retinue2ToUpgrade, out var upgradeRecord))
                        {
                            retinue2Changes[Retinue2ToUpgrade] =
                                (upgradeRecord.oldTroopType ?? oldTroopType, upgradeRecord.totalSpent + cost);
                        }
                        else
                        {
                            retinue2Changes.Add(Retinue2ToUpgrade, (oldTroopType, cost));
                        }
                    }
                    else
                    {
                        results.Add("{=PQRLJ04i}Can't upgrade secondary retinue any further!".Translate());
                        break;
                    }
                }
            }

            var troopUpgradeSummary = new List<string>();
            foreach ((var oldTroopType, var newTroopType, int cost, int num) in retinue2Changes
                .GroupBy(r
                    => (r.Value.oldTroopType, newTroopType: r.Key.TroopType))
                .Select(g => (
                        g.Key.oldTroopType,
                        g.Key.newTroopType,
                        cost: g.Sum(f => f.Value.totalSpent),
                        num: g.Count()))
                .OrderBy(g => g.oldTroopType == null)
                .ThenBy(g => g.num)
            )
            {
                if (oldTroopType != null)
                {
                    troopUpgradeSummary.Add($"{oldTroopType}{Naming.To}{newTroopType}" +
                                            (num > 1 ? $" x{num}" : "") +
                                            $" ({Naming.Dec}{cost}{Naming.Gold})");
                }
                else
                {
                    troopUpgradeSummary.Add($"{newTroopType}" +
                                            (num > 1 ? $" x{num}" : "") +
                                            $" ({Naming.Dec}{cost}{Naming.Gold})");

                }
            }

            if (totalCost > 0)
            {
                ChangeHeroGold(hero, -totalCost, isSpending: true);
            }

            return (retinue2Changes.Any(), Naming.JoinList(troopUpgradeSummary.Concat(results)));
        }

        public void KillRetinue2(Hero retinue2OwnerHero, BasicCharacterObject RetinueCharacterObject)
        {
            var heroRetinue2 = GetHeroData(retinue2OwnerHero).Retinue2;
            var matchingRetinue2 = heroRetinue2.FirstOrDefault(r => r.TroopType == RetinueCharacterObject);
            if (matchingRetinue2 != null)
            {
                heroRetinue2.Remove(matchingRetinue2);
            }
            else
            {
                //Log.Error($"Couldn't find matching secondary retinue type {RetinueCharacterObject} " +
                //          $"for {retinue2OwnerHero} to remove");
            }
        }

        public void KillRetinue2AtIndex(Hero Retinue2OwnerHero, int index)
        {
            var heroretinue2 = GetHeroData(Retinue2OwnerHero).Retinue2;

            if (index >= 0 && index < heroretinue2.Count)
            {
                heroretinue2.RemoveAt(index);
            }
            else
            {
                Log.Error($"Invalid secondary retinue index {index} for {Retinue2OwnerHero}. secondary retinue count: {heroretinue2.Count}");
            }
        }

        #endregion

        #region Helper Functions

        public static void SetAgentStartingHealth(Hero hero, Agent agent)
        {
            if (hero is null)
            {
                throw new ArgumentNullException(nameof(hero));
            }

            if (BLTAdoptAHeroModule.CommonConfig.StartWithFullHealth)
            {
                agent.Health = agent.HealthLimit;
            }

            bool inTournament = MissionHelpers.InTournament();
            float multiplier = inTournament
                ? BLTAdoptAHeroModule.TournamentConfig.StartHealthMultiplier
                : BLTAdoptAHeroModule.CommonConfig.StartHealthMultiplier;

            agent.BaseHealthLimit *= Math.Max(1, multiplier);
            agent.HealthLimit *= Math.Max(1, multiplier);
            agent.Health *= Math.Max(1, multiplier);
        }

        public static IEnumerable<Hero> GetAvailableHeroes(Func<Hero, bool> filter = null) =>
            CampaignHelpers.AliveHeroes.Where(h =>
                    // Some buggy mods can result in null heroes
                    h != null &&
                    // Some buggy mods can result in heroes with out valid names
                    h.Name != null &&
                    // Not the player of course
                    h != Hero.MainHero
                    // Don't want notables ever
                    && !h.IsNotable
                    // Only of age characters can be used
                    && h.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge)
                .Where(filter ?? (_ => true))
                .Where(n => !n.Name.Contains(BLTAdoptAHeroModule.Tag) || !n.Name.Contains(BLTAdoptAHeroModule.DevTag));

        public static IEnumerable<Hero> GetAllAdoptedHeroes() => CampaignHelpers.AliveHeroes.Where(n => n.Name?.Contains(BLTAdoptAHeroModule.Tag) == true || n.Name?.Contains(BLTAdoptAHeroModule.DevTag) == true);

        public static string GetFullName(string name)
        {
            string tag = TwitchDevUsers.Developers.Contains(name)
                ? BLTAdoptAHeroModule.DevTag
                : BLTAdoptAHeroModule.Tag;

            return $"{name} {tag}";
        }

        public static void SetHeroAdoptedName(Hero hero, string userName) =>
            CampaignHelpers.SetHeroName(hero, new(GetFullName(userName)), new(userName));
        public string GetHeroLegacyName(Hero hero) =>
            GetHeroData(hero).LegacyName;
        public bool GetIsCreatedHero(Hero hero) =>
            GetHeroData(hero).IsCreatedHero;
        //public bool GetMessageFlag(Hero hero) =>
        //    GetHeroData(hero).MesssageFlag;
        public void SetIsCreatedHero(Hero hero, bool value) =>
            GetHeroData(hero).IsCreatedHero = value;
        //public void SetMessageFlag(Hero hero, bool value) =>
        //    GetHeroData(hero).MesssageFlag = value;
        //public string GetMessageContent(Hero hero) =>
        //    GetHeroData(hero).MessageContent;
        //public string ClearMessageContent(Hero hero) =>
        //    GetHeroData(hero).MessageContent = null;

        private HeroData GetHeroData(Hero hero, bool suppressAutoRetire = false)
        {
            // Better create it now if it doesn't exist
            if (!heroData.TryGetValue(hero, out var hd))
            {
                hd = new HeroData
                {
                    Gold = hero.Gold,
                    EquipmentTier = EquipHero.CalculateHeroEquipmentTier(hero),
                };
                heroData.Add(hero, hd);
            }

            if (!suppressAutoRetire && hero.IsDead && !hd.IsRetiredOrDead)
            {
                RetireHero(hero);
            }

            return hd;
        }

        private static string KillDetailVerb(KillCharacterAction.KillCharacterActionDetail detail)
        {
            switch (detail)
            {
                case KillCharacterAction.KillCharacterActionDetail.Murdered:
                    return "{=LhHul2lV}was murdered".Translate();
                case KillCharacterAction.KillCharacterActionDetail.DiedInLabor:
                    return "{=HwjR45XN}died in labor".Translate();
                case KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge:
                    return "{=5GOjfkzW}died of old age".Translate();
                case KillCharacterAction.KillCharacterActionDetail.DiedInBattle:
                    return "{=ZKrgqWav}died in battle".Translate();
                case KillCharacterAction.KillCharacterActionDetail.WoundedInBattle:
                    return "{=jp4sldTL}was wounded in battle".Translate();
                case KillCharacterAction.KillCharacterActionDetail.Executed:
                    return "{=SkFFXsI1}was executed".Translate();
                case KillCharacterAction.KillCharacterActionDetail.Lost:
                    return "{=HMHdXDaK}was lost".Translate();
                default:
                case KillCharacterAction.KillCharacterActionDetail.None:
                    return "{=lrOJnThZ}was ended".Translate();
            }
        }

        private static string ToRoman(int number)
        {
            return number switch
            {
                < 0 => throw new ArgumentOutOfRangeException(nameof(number), "must be between 1 and 3999"),
                > 3999 => throw new ArgumentOutOfRangeException(nameof(number), "must be between 1 and 3999"),
                < 1 => string.Empty,
                >= 1000 => "M" + ToRoman(number - 1000),
                >= 900 => "CM" + ToRoman(number - 900),
                >= 500 => "D" + ToRoman(number - 500),
                >= 400 => "CD" + ToRoman(number - 400),
                >= 100 => "C" + ToRoman(number - 100),
                >= 90 => "XC" + ToRoman(number - 90),
                >= 50 => "L" + ToRoman(number - 50),
                >= 40 => "XL" + ToRoman(number - 40),
                >= 10 => "X" + ToRoman(number - 10),
                >= 9 => "IX" + ToRoman(number - 9),
                >= 5 => "V" + ToRoman(number - 5),
                >= 4 => "IV" + ToRoman(number - 4),
                >= 1 => "I" + ToRoman(number - 1)
            };
        }

        #endregion

        #region Console Commands

        [CommandLineFunctionality.CommandLineArgumentFunction("addstat", "blt")]
        [UsedImplicitly]
        public static string SetHeroStat(List<string> strings)
        {
            var parts = string.Join(" ", strings).Split(',').Select(p => p.Trim()).ToList();

            if (parts.Count != 3)
            {
                return "Arguments: hero,stat,amount";
            }

            var hero = Current.GetAdoptedHero(parts[0]);
            if (hero == null)
            {
                return $"Couldn't find hero {parts[0]}";
            }

            if (!Enum.TryParse(parts[1], out AchievementStatsData.Statistic stat))
            {
                return $"Couldn't find stat {parts[1]}";
            }

            if (!int.TryParse(parts[2], out int amount))
            {
                return $"Couldn't parse amount {parts[2]}";
            }

            Current.GetHeroData(hero).AchievementStats.UpdateValue(stat, hero.GetClass()?.ID ?? Guid.Empty, amount);

            return $"Added {amount} to {stat} stat of {hero.Name}";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("killBLT", "blt")]
        [UsedImplicitly]
        public static string KillAdoptedHeroAgent(List<string> strings)
        {
            Hero targetHero = null;
            // allow multi-word hero names
            var heroName = string.Join(" ", strings).Trim();
            bool hasBLTTag = heroName.EndsWith(" [BLT]");
            bool hasDEVTag = heroName.EndsWith(" [DEV]");
            if (string.IsNullOrEmpty(heroName))
                return "Usage: killBLT <hero_name>";

            // get the behavior safely
            var behavior = BLTAdoptAHeroCampaignBehavior.Current
                           ?? Campaign.Current?.GetCampaignBehavior<BLTAdoptAHeroCampaignBehavior>();
            if (behavior == null)
                return "BLT adopt-a-hero behavior is not available";

            // check active mission
            var mission = Mission.Current;
            if (mission == null)
                return "No active mission/battle found";

            Hero adoptedRecord = null;
            string adoptedTaggedName = null;
            try
            {
                // try the obvious call - if the behavior exposes other overloads you can prefer them
                if (targetHero == null)
                {
                    targetHero = behavior.GetAdoptedHero(heroName);

                    if (targetHero == null && !hasBLTTag && !hasDEVTag)
                    {
                        adoptedTaggedName = heroName.Add(" [BLT]");
                        adoptedRecord = behavior.GetAdoptedHero(adoptedTaggedName);
                        if (adoptedRecord == null)
                        {
                            adoptedTaggedName = heroName.Add(" [DEV]");
                            adoptedRecord = behavior.GetAdoptedHero(adoptedTaggedName);
                        }
                        
                        if (adoptedRecord != null)
                        {
                            targetHero = adoptedRecord;
                        }
                    }
                }
            }
            catch
            {
                // ignore - we'll fall back to scanning heroes
            }

            //Find a live hero by name
            if (targetHero == null)
            {
                targetHero = Hero.AllAliveHeroes
                    .FirstOrDefault(h => string.Equals(h.Name?.ToString(), heroName, StringComparison.OrdinalIgnoreCase));
            }

            if (targetHero == null)
                return $"Couldn't find adopted hero: {heroName}";

            // find agent safely - avoid throwing inside LINQ
            Agent agent = null;
            foreach (var a in mission.Agents)
            {
                try
                {
                    if (a == null) continue;
                    // prefer direct CharacterObject equality when available
                    if (a.Character != null && targetHero.CharacterObject != null)
                    {
                        if (object.ReferenceEquals(a.Character, targetHero.CharacterObject))
                        {
                            agent = a;
                            break;
                        }
                    }

                    // fallback: compare names (safe)
                    var aName = a.Character?.Name?.ToString();
                    if (!string.IsNullOrEmpty(aName) && aName.Equals(heroName, StringComparison.OrdinalIgnoreCase))
                    {
                        agent = a;
                        break;
                    }
                }
                catch
                {
                    // continue - don't let one bad agent crash the search
                    continue;
                }
            }

            if (agent == null)
                return $"Agent for hero {targetHero.Name} not found in current battle";

            if (!agent.IsActive())
                return $"Agent for hero {targetHero.Name} is not active";

            // Kill the agent
            var blow = new Blow(agent.Index);
            blow.DamageType = DamageTypes.Blunt; // Blunt to avoid accidentally killing characters with lethal damage type
            blow.BoneIndex = agent.Monster.HeadLookDirectionBoneIndex;
            blow.GlobalPosition = agent.Position;
            blow.BaseMagnitude = agent.HealthLimit;
            blow.InflictedDamage = (int)agent.HealthLimit;

            try
            {
                agent.Die(blow);
            }
            catch (Exception ex)
            {
                // show a helpful debug message without crashing the game
                InformationManager.DisplayMessage(new InformationMessage($"killBLT failed: {ex.Message}"));
                return $"Failed to kill agent: {ex.Message}";
            }

            return $"Killed agent of {targetHero.Name}";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("ChangeGold", "blt")]
        [UsedImplicitly]
        public static string ChangeGold(List<string> strings)
        {
            if (Campaign.Current == null)
            {
                return "Campaign is not active";
            }

            if (strings.Count < 2)
            {
                return "Usage: blt.change_gold <hero_name> <amount> (e.g., blt.change_gold TestUser 1000)";
            }

            // Last element is the amount, everything else is the hero name
            if (!int.TryParse(strings[strings.Count - 1], out int amount))
            {
                return $"Invalid gold amount: {strings[strings.Count - 1]}";
            }

            // Join all strings except the last one (the amount) to get the hero name
            var heroName = string.Join(" ", strings.Take(strings.Count - 1)).Trim();
            bool hasBLTTag = heroName.EndsWith(" [BLT]");
            bool hasDEVTag = heroName.EndsWith(" [DEV]");

            if (string.IsNullOrEmpty(heroName))
            {
                return "Usage: blt.change_gold <hero_name> <amount>";
            }

            // get the behavior safely
            var behavior = Campaign.Current?.GetCampaignBehavior<BLTAdoptAHeroCampaignBehavior>();
            if (behavior == null)
            {
                return "BLT Adopt-a-hero behavior is not available";
            }

            Hero targetHero = null;
            Hero adoptedRecord = null;
            string adoptedTaggedName = null;

            try
            {
                // try the obvious call - if the behavior exposes other overloads you can prefer them
                if (targetHero == null)
                {
                    targetHero = behavior.GetAdoptedHero(heroName);

                    if (targetHero == null && !hasBLTTag && !hasDEVTag)
                    {
                        adoptedTaggedName = heroName.Add(" [BLT]");
                        adoptedRecord = behavior.GetAdoptedHero(adoptedTaggedName);
                        if (adoptedRecord == null)
                        {
                            adoptedTaggedName = heroName.Add(" [DEV]");
                            adoptedRecord = behavior.GetAdoptedHero(adoptedTaggedName);
                        }

                        if (adoptedRecord != null)
                        {
                            targetHero = adoptedRecord;
                        }
                    }
                }
            }
            catch
            {
                // ignore - we'll fall back to scanning heroes
            }

            // Find a live hero by name
            if (targetHero == null)
            {
                targetHero = Hero.AllAliveHeroes
                    .FirstOrDefault(h => string.Equals(h.Name?.ToString(), heroName, StringComparison.OrdinalIgnoreCase));
            }

            if (targetHero == null)
            {
                return $"Could not find BLT hero: {heroName}";
            }

            // Change the hero's gold in a try/catch so we don't crash if the behavior throws
            try
            {
                behavior.ChangeHeroGold(targetHero, amount, true);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"blt.change_gold failed: {ex.Message}"));
                return $"Error changing gold: {ex.Message}";
            }

            int currentGold = behavior.GetHeroGold(targetHero);
            return $"Changed {targetHero.Name}'s gold by {amount}. Current balance: {currentGold}";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("CleanupShellParties", "blt")]
        [UsedImplicitly]
        public static string CleanupShellParties(List<string> strings)
        {
            if (Campaign.Current == null)
            {
                return "Campaign is not active";
            }

            var Heroes = new List<Hero>();
            var partiesToDelete = new List<MobileParty>();
            int checkedCount = 0;
            int skippedCount = 0;

            try
            {
                foreach (var hero in Hero.AllAliveHeroes.ToList().Where(h => h.IsAdopted()))
                {
                    Heroes.Add(hero);
                }

                // Collect all parties that should be deleted
                // Use ToList() to avoid modifying collection during enumeration
                foreach (var party in MobileParty.All.ToList())
                {
                    if (party == null)
                        continue;

                    checkedCount++;

                    // Skip the main party (player's party)
                    if (party.IsMainParty)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Skip caravans - they don't need a LeaderHero
                    if (party.IsCaravan)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Skip villagers - they don't need a LeaderHero
                    if (party.IsVillager)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Skip bandits - they don't need a LeaderHero
                    if (party.IsBandit)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Skip militia - they don't need a LeaderHero
                    if (party.IsMilitia)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Skip garrison parties (shouldn't be mobile but just in case)
                    if (party.IsGarrison)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Skip patrol parties - counted as Lord Parties for some reason
                    if (party.IsPatrolParty)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Now check if this is a lord party without a leader
                    // IsLordParty should be true for parties that need a LeaderHero
                    string partyName = party.Name?.ToString() ?? string.Empty;

                    bool nameMatchesAdoptedHero =
                        Heroes.Any(h =>
                            !string.IsNullOrEmpty(h.Name?.ToString()) &&
                            partyName.Contains(h.Name.ToString()));

                    if (party.IsLordParty &&
                        (party.LeaderHero == null ||
                         party.LeaderHero.IsAdopted() ||
                         party.LeaderHero.Name.Contains("retired") ||
                         nameMatchesAdoptedHero))
                    {
                        if (!party.IsActive)
                            party.IsActive = true;

                        partiesToDelete.Add(party);
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                // Now delete the collected parties
                int deletedCount = 0;
                var deletionErrors = new List<string>();

                foreach (var party in partiesToDelete)
                {
                    try
                    {
                        string partyInfo = $"{party.Name.ToString() ?? "Unnamed"}";
                        if (party.Party?.Owner != null)
                        {
                            partyInfo += $" (Owner: {party.Party.Owner.Name})";
                        }

                        DestroyPartyAction.Apply(null, party);
                        deletedCount++;

                        InformationManager.DisplayMessage(
                            new InformationMessage($"Deleted shell party: {partyInfo}")
                        );
                    }
                    catch (Exception ex)
                    {
                        deletionErrors.Add($"Failed to delete party {party.Name}: {ex.Message}");
                    }
                }

                // Build result message
                var result = $"Cleanup complete:" +
                             $"- Checked: {checkedCount} parties" +
                             $"- Skipped: {skippedCount} (valid parties)" +
                             $"- Deleted: {deletedCount} shell lord parties";

                if (deletionErrors.Any())
                {
                    result += $"- Errors: { deletionErrors.Count}";
                    foreach (var error in deletionErrors)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"Error: {error}"));
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Cleanup failed: {ex.Message}";
            }
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("FixVassals", "blt")]
        [UsedImplicitly]
        public static string FixVassals(List<string> strings)
        {
            if (Campaign.Current == null)
            {
                return "Campaign is not active";
            }

            if (VassalBehavior.Current == null)
            {
                return "Vassal Behavior is not active";
            }

            int checkedCount = 0;
            int alreadyCorrect = 0;
            int fixedCount = 0;
            int errorCount = 0;
            var errors = new List<string>();

            try
            {
                // Get all clans and check if they're vassals
                foreach (var clan in Clan.All.ToList())
                {
                    if (clan == null) continue;

                    // Check if this is a vassal clan
                    if (!VassalBehavior.Current.IsVassal(clan))
                        continue;

                    checkedCount++;

                    var masterClan = VassalBehavior.Current.GetMasterClan(clan);

                    if (masterClan == null)
                    {
                        errors.Add($"Vassal {clan.Name} has no master clan found");
                        errorCount++;
                        continue;
                    }

                    // Check if vassal is already in correct kingdom and correct status
                    bool inCorrectKingdom = clan.Kingdom == masterClan.Kingdom;
                    bool correctMercStatus = clan.IsUnderMercenaryService == masterClan.IsUnderMercenaryService;

                    if (inCorrectKingdom && correctMercStatus)
                    {
                        alreadyCorrect++;
                        continue;
                    }

                    try
                    {
                        InformationManager.DisplayMessage(
                            new InformationMessage($"[BLT] Fixing vassal {clan.Name} (master: {masterClan.Name})")
                        );

                        AdoptedHeroFlags._allowKingdomMove = true;

                        // If vassal has fiefs and is leaving a kingdom, transfer them
                        if (clan.Kingdom != null && clan.Kingdom != masterClan.Kingdom && clan.Settlements.Any())
                        {
                            foreach (var fief in clan.Settlements.ToList())
                            {
                                try
                                {
                                    // Give fiefs to the kingdom ruler
                                    Hero ruler = clan.Kingdom?.RulingClan?.Leader;
                                    if (ruler != null && ruler != clan.Leader)
                                    {
                                        ChangeOwnerOfSettlementAction.ApplyByDefault(ruler, fief);
                                        InformationManager.DisplayMessage(
                                            new InformationMessage($"[BLT] Transferred {fief.Name} from {clan.Name} to {ruler.Name}")
                                        );
                                    }
                                }
                                catch (Exception fiefEx)
                                {
                                    errors.Add($"Failed to transfer fief {fief.Name} from {clan.Name}: {fiefEx.Message}");
                                }
                            }
                        }

                        // Leave current kingdom if in one
                        if (clan.Kingdom != null && clan.Kingdom != masterClan.Kingdom)
                        {
                            if (clan.IsUnderMercenaryService)
                            {
                                clan.EndMercenaryService(true);
                            }
                            clan.ClanLeaveKingdom(true);
                        }

                        // Join master's kingdom with correct status
                        if (masterClan.Kingdom != null)
                        {
                            if (masterClan.IsUnderMercenaryService)
                            {
                                ChangeKingdomAction.ApplyByJoinFactionAsMercenary(clan, masterClan.Kingdom, default, clan.MercenaryAwardMultiplier);
                                InformationManager.DisplayMessage(
                                    new InformationMessage($"[BLT] {clan.Name} joined {masterClan.Kingdom.Name} as mercenary")
                                );
                            }
                            else
                            {
                                ChangeKingdomAction.ApplyByJoinToKingdom(clan, masterClan.Kingdom, default, false);
                                InformationManager.DisplayMessage(
                                    new InformationMessage($"[BLT] {clan.Name} joined {masterClan.Kingdom.Name} as vassal")
                                );
                            }
                        }
                        else
                        {
                            // Master is independent, vassal should be too
                            InformationManager.DisplayMessage(
                                new InformationMessage($"[BLT] {clan.Name} is now independent (master is independent)")
                            );
                        }

                        AdoptedHeroFlags._allowKingdomMove = false;
                        fixedCount++;
                    }
                    catch (Exception clanEx)
                    {
                        AdoptedHeroFlags._allowKingdomMove = false;
                        errors.Add($"Failed to fix vassal {clan.Name}: {clanEx.Message}");
                        errorCount++;
                    }
                }

                // Build result message
                var result = $"Vassal fix complete: " +
                             $"Checked: {checkedCount} vassals | " +
                             $"Already correct: {alreadyCorrect} | " +
                             $"Fixed: {fixedCount} | " +
                             $"Errors: {errorCount}";

                if (errors.Any())
                {
                    foreach (var error in errors)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[BLT Error] {error}"));
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                AdoptedHeroFlags._allowKingdomMove = false;
                return $"Vassal fix failed: {ex.Message}";
            }
        }

        #endregion
    }
}
