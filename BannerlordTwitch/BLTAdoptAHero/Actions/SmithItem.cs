﻿using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=}Smith Item"),
     LocDescription("{=}Allows smithing of new weapons, armor, or horses"),
     UsedImplicitly]
    public class SmithItem : HeroActionHandlerBase
    {
        private class Settings
        {
            // [LocDisplayName("{=}Item"),
            //  LocCategory("General", "{=C5T5nnix}General"), 
            //  LocDescription("{=}Item specification, as this is for smithing the CustomWeight should be kept at 1, and probably Tier6 only"), 
            //  PropertyOrder(1), ExpandableObject, Expand, UsedImplicitly, Document]
            // public GeneratedRewardDef Item { get; set; } = new()
            // {
            //     ArmorWeight = 0.3f,
            //     WeaponWeight = 1f,
            //     MountWeight = 0.1f,
            //     Tier1Weight = 0,
            //     Tier2Weight = 0,
            //     Tier3Weight = 0,
            //     Tier4Weight = 0,
            //     Tier5Weight = 0,
            //     Tier6Weight = 0,
            //     CustomWeight = 1,
            //     CustomItemName = "{=}Smithed {ITEMNAME}",
            //     CustomItemPower = 1,
            // };
            [LocDisplayName("{=}Item Type"),
             LocCategory("General", "{=C5T5nnix}General"),
             LocDescription("{=}Smithed item type"),
             PropertyOrder(1), UsedImplicitly]
            public RewardHelpers.RewardType Type { get; set; } = RewardHelpers.RewardType.Weapon;
            // [LocDisplayName("{=}Item Tier"),
            //  LocCategory("General", "{=C5T5nnix}General"),
            //  LocDescription("{=}What tier of item to smith, this should usually be 6"), 
            //  PropertyOrder(1), Document, UsedImplicitly]
            // public int ItemTier { get; set; } = 6;
            // [LocDisplayName("{=}Allow Duplicates"),
            //  LocCategory("General", "{=C5T5nnix}General"),
            //  LocDescription("{=}Whether to allow smithed items to be generated if the hero already has one of the same type"), 
            //  PropertyOrder(1), Document, UsedImplicitly]
            // public bool AllowDuplicates { get; set; } = false;

            [LocDisplayName("{=}Item Power"),
             LocCategory("General", "{=C5T5nnix}General"),
             LocDescription("{=}Smithed item power multiplier, applies on top of the global multiplier"),
             Range(0, 5), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
             PropertyOrder(2), UsedImplicitly]
            public float ItemPower { get; set; } = 1f;

            [LocDisplayName("{=}Item Name"),
             LocCategory("General", "{=C5T5nnix}General"),
             LocDescription("{=vqNeCCNy}Name format for custom item, {ITEMNAME} is the placeholder for the base item name"),
             PropertyOrder(3), UsedImplicitly]
            public string ItemName { get; set; } = "{=}Smithed {ITEMNAME}";

            [LocDisplayName("{=HOZnxjGb}Gold Cost"),
             LocCategory("General", "{=C5T5nnix}General"),
             LocDescription("{=OQISx7Jz}Gold cost to summon"),
             PropertyOrder(4), UsedImplicitly]
            public int GoldCost { get; set; }
        }

        protected override Type ConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            var settings = (Settings)config;

            int availableGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero);
            if (availableGold < settings.GoldCost)
            {
                onFailure(Naming.NotEnoughGold(settings.GoldCost, availableGold));
                return;
            }

            if (BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(adoptedHero).Count >=
                BLTAdoptAHeroModule.CommonConfig.CustomItemLimit)
            {
                onFailure("{=}You have too many custom items (limit is {LIMIT}), get rid of some before smithing".Translate(("LIMIT", BLTAdoptAHeroModule.CommonConfig.CustomItemLimit)));
                return;
            }

            if (settings.Type == RewardHelpers.RewardType.Weapon && adoptedHero.GetClass() == null)
            {
                // Can't determine what weapon to smith if they don't have a class, so it will fail
                onFailure("{=}Hero class must be set to smith a weapon!".Translate());
                return;
            }

            var (item, itemModifier, slot) = RewardHelpers.GenerateRewardType(settings.Type, 6, adoptedHero,
                BLTAdoptAHeroCampaignBehavior.Current.GetClass(adoptedHero),
                true, BLTAdoptAHeroModule.CommonConfig.CustomRewardModifiers,
                settings.ItemName, settings.ItemPower);

            if (item == null)
            {
                // Should never happen, as it falls back to allow duplicates anyway
                onFailure("{=}Could not find any valid item!".Translate());
            }
            else
            {
                onSuccess(RewardHelpers.AssignCustomReward(adoptedHero, item, itemModifier, slot));
                int newGold = BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.GoldCost);
                ActionManager.SendReply(context, $"{Naming.Dec}{settings.GoldCost}{Naming.Gold}{Naming.To}{newGold}{Naming.Gold}");
                //$"{Naming.Dec}{settings.GoldCost}{Naming.Gold}");
            }
        }
    }
}