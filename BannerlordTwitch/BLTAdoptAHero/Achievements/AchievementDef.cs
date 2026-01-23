using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Powers;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using YamlDotNet.Serialization;

namespace BLTAdoptAHero.Achievements
{
    [CategoryOrder("General", 1)]
    [CategoryOrder("Requirements", 2)]
    [CategoryOrder("Reward", 3)]
    [LocDisplayName("{=CEZZzIAq}Achievement Definition")]
    public sealed class AchievementDef : ICloneable, INotifyPropertyChanged
    {
        [ReadOnly(true), UsedImplicitly]
        public Guid ID { get; set; } = Guid.NewGuid();

        [LocDisplayName("{=sPKWnVA0}Enabled"), LocCategory("General", "{=C5T5nnix}General"),
         PropertyOrder(1), UsedImplicitly]
        public bool Enabled { get; set; }

        [LocDisplayName("{=uUzmy7Lh}Name"), LocCategory("General", "{=C5T5nnix}General"),
         InstanceName, PropertyOrder(2), UsedImplicitly]
        public LocString Name { get; set; } = "{=nLaRVBVV}New Achievement";

        [LocDisplayName("{=guJFFskH}Notification Text"), LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=SwIUyQNw}Text that will display when the achievement is gained and when the player lists their achievements. Placeholders: [viewer] for the viewers name, and [name] for the name of the achievement."),
         PropertyOrder(3), UsedImplicitly]
        public LocString NotificationText { get; set; } = "{=TAWcPGIc}[viewer] got [name]!";

        [LocDisplayName("{=TfwiBMGr}Requirements"), LocCategory("Requirements", "{=TFbiD0CZ}Requirements"),
         PropertyOrder(1), UsedImplicitly,
         Editor(typeof(DerivedClassCollectionEditor<IAchievementRequirement>),
             typeof(DerivedClassCollectionEditor<IAchievementRequirement>))]
        public ObservableCollection<IAchievementRequirement> Requirements { get; set; } = new();

        [LocDisplayName("{=NitYwoHr}Gold Gain"), LocCategory("Reward", "{=sHWjkhId}Reward"),
         LocDescription("{=l0xBVhOe}Gold awarded for gaining this achievement."),
         PropertyOrder(1), UsedImplicitly]
        public int GoldGain { get; set; }

        [LocDisplayName("{=JShqb5Be}XP Gain"), LocCategory("Reward", "{=sHWjkhId}Reward"),
         PropertyOrder(2),
         LocDescription("{=tx7YV26l}Experience awarded for gaining this achievement."),
         UsedImplicitly]
        public int XPGain { get; set; }

        [LocDisplayName("{=OmwtPK7J}Give Item Reward"), LocCategory("Reward", "{=sHWjkhId}Reward"),
         PropertyOrder(3),
         LocDescription("{=vTb0ysRw}Whether to give a custom item as reward (defined below)"),
         UsedImplicitly]
        public bool GiveItemReward { get; set; }

        [LocDisplayName("{=ZJikxJYo}Item Reward"), LocCategory("Reward", "{=sHWjkhId}Reward"),
         PropertyOrder(4),
         LocDescription("{=B4gODHog}Specifications of the custom item reward, if enabled"),
         ExpandableObject,
         UsedImplicitly]
        public GeneratedRewardDef ItemReward { get; set; } = new();

        [LocDisplayName("{=AchPassPower}Give Passive Power"), LocCategory("Reward", "{=sHWjkhId}Reward"),
         PropertyOrder(5),
         LocDescription("{=AchPassPowerDesc}Whether to grant a permanent passive power as a reward (applies to all classes)"),
         UsedImplicitly]
        public bool GivePassivePower { get; set; }

        [LocDisplayName("{=AchPassPowerReward}Passive Power Reward"), LocCategory("Reward", "{=sHWjkhId}Reward"),
         PropertyOrder(6),
         LocDescription("{=AchPassPowerRewardDesc}Passive power that will be permanently granted when this achievement is earned. This power will apply in all future battles regardless of class."),
         ExpandableObject,
         UsedImplicitly]
        public PassivePowerGroup PassivePowerReward { get; set; } = new();

        public bool IsAchieved(Hero hero) => Requirements.All(r => r.IsMet(hero));

        // For UI use
        [YamlIgnore, Browsable(false)]
        public string RewardsDescription => (GoldGain > 0 ? $"{GoldGain}{Naming.Gold}\n" : "") +
                                            (XPGain > 0 ? $"{XPGain}{Naming.XP}\n" : "") +
                                            (GiveItemReward ? Naming.Item + "\n" : "") +
                                            (GivePassivePower ? "{=AchPassPowerShort}Passive Power\n".Translate() : "");

        public override string ToString()
            => $"{Name} " +
               string.Join("+", Requirements.Select(r => r.ToString())) +
               $" " + "{=sHWjkhId}Reward".Translate() + ": " + RewardsDescription;

        public object Clone()
        {
            var clone = CloneHelpers.CloneProperties(this);
            clone.ID = Guid.NewGuid();
            clone.Requirements = new(CloneHelpers.CloneCollection(Requirements).ToList());
            if (GivePassivePower && PassivePowerReward != null)
            {
                clone.PassivePowerReward = (PassivePowerGroup)PassivePowerReward.Clone();
            }
            return clone;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Apply(Hero hero)
        {
            var results = new List<string> { "{=Qtv96c9S}Unlocked {NAME}".Translate(("NAME", Name)) };

            if (GoldGain > 0)
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, GoldGain);
                BLTAdoptAHeroCommonMissionBehavior.Current?.RecordGoldGain(hero, GoldGain);
                results.Add($"{Naming.Inc}{GoldGain}{Naming.Gold}");
            }

            if (XPGain > 0)
            {
                (bool success, string description) = SkillXP.ImproveSkill(hero, XPGain, SkillsEnum.All, auto: true);
                if (success)
                {
                    results.Add(description);
                }
                BLTAdoptAHeroCommonMissionBehavior.Current?.RecordXPGain(hero, XPGain);
            }

            if (GiveItemReward)
            {
                var (item, modifier, slot) = ItemReward.Generate(hero);
                if (item != null)
                {
                    results.Add(RewardHelpers.AssignCustomReward(hero, item, modifier, slot));
                }
            }

            if (GivePassivePower && PassivePowerReward != null)
            {
                // Register this achievement as granting a passive power to this hero
                BLTAdoptAHeroCampaignBehavior.Current.AddAchievementPassivePower(hero, ID);
                results.Add("{=AchPassPowerGranted}Granted passive power: {PowerName}".Translate(("PowerName", PassivePowerReward.Name)));
            }

            if (results.Any())
            {
                Log.LogFeedResponse(hero.FirstName.ToString(), results.ToArray());
            }
        }
    }
}