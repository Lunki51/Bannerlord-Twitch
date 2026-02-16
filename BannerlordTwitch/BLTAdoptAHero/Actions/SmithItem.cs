using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
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
            [LocDisplayName("{=}Item Type"),
             LocCategory("General", "{=C5T5nnix}General"),
             LocDescription("{=}Smithed item type"),
             PropertyOrder(1), UsedImplicitly]
            public RewardHelpers.RewardType Type { get; set; } = RewardHelpers.RewardType.Weapon;

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
             LocDescription("{=OQISx7Jz}Gold cost to smith"),
             PropertyOrder(4), UsedImplicitly]
            public int GoldCost { get; set; }

            [LocDisplayName("{=}Allow Culture Selection"),
             LocCategory("Culture", "{=}Culture"),
             LocDescription("{=}Allow viewers to specify a culture when smithing (e.g., !smith vlandia)"),
             PropertyOrder(5), UsedImplicitly]
            public bool AllowCultureSelection { get; set; } = true;

            [LocDisplayName("{=}Culture Gold Cost"),
             LocCategory("Culture", "{=}Culture"),
             LocDescription("{=}Additional gold cost when smithing culture-specific items (0 for no additional cost)"),
             PropertyOrder(6), UsedImplicitly]
            public int CultureGoldCost { get; set; } = 0;

            [LocDisplayName("{=}Use Hero Culture Default"),
             LocCategory("Culture", "{=}Culture"),
             LocDescription("{=}When no culture is specified, use the hero's culture instead of random"),
             PropertyOrder(7), UsedImplicitly]
            public bool UseHeroCultureDefault { get; set; } = false;
        }

        protected override Type ConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            var settings = (Settings)config;

            // Parse culture name from context args if culture selection is enabled
            CultureObject targetCulture = null;
            int totalGoldCost = settings.GoldCost;

            if (settings.AllowCultureSelection && context.Args?.Length > 0)
            {
                string cultureName = string.Join(" ", context.Args);
                targetCulture = FindCultureByName(cultureName);

                // Check if user explicitly specified "null" to filter for items without culture
                if (cultureName.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    targetCulture = null;
                }
                else if (targetCulture == null)
                {
                    var validCultures = string.Join(", ", CampaignHelpers.MainCultures
                        .Select(c => c.Name.ToString())
                        .Distinct()
                        .OrderBy(n => n));

                    onFailure($"{{=}}Invalid culture '{cultureName}'. Valid cultures: {validCultures}".Translate());
                    return;
                }

                totalGoldCost += settings.CultureGoldCost;
            }
            else if (settings.UseHeroCultureDefault && adoptedHero.Culture?.IsMainCulture == true)
            {
                targetCulture = adoptedHero.Culture;
            }

            int availableGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero);
            if (availableGold < totalGoldCost)
            {
                onFailure(Naming.NotEnoughGold(totalGoldCost, availableGold));
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
                onFailure("{=}Hero class must be set to smith a weapon!".Translate());
                return;
            }

            var (item, itemModifier, slot) = RewardHelpers.GenerateCulturedRewardType(
                settings.Type,
                6,
                adoptedHero,
                BLTAdoptAHeroCampaignBehavior.Current.GetClass(adoptedHero),
                true,
                BLTAdoptAHeroModule.CommonConfig.CustomRewardModifiers,
                settings.ItemName,
                settings.ItemPower,
                targetCulture);

            if (item == null)
            {
                string failureMessage = targetCulture != null
                    ? $"{{=}}Could not find any valid {targetCulture.Name} item!".Translate()
                    : "{=}Could not find any valid item!".Translate();
                onFailure(failureMessage);
            }
            else
            {
                string cultureName = targetCulture != null ? $" ({targetCulture.Name})" : "";
                onSuccess(RewardHelpers.AssignCustomReward(adoptedHero, item, itemModifier, slot) + cultureName);
                int newGold = BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -totalGoldCost);
                ActionManager.SendReply(context, $"{Naming.Dec}{totalGoldCost}{Naming.Gold}{Naming.To}{newGold}{Naming.Gold}");
            }
        }

        private CultureObject FindCultureByName(string cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
                return null;

            // Get all main cultures from kingdoms
            var mainCultures = Campaign.Current.Kingdoms
                .Where(k => k.Culture?.IsMainCulture == true)
                .Select(k => k.Culture)
                .Distinct();

            // Try exact match first (case-insensitive)
            var culture = mainCultures.FirstOrDefault(c =>
                c.Name.ToString().Equals(cultureName, StringComparison.OrdinalIgnoreCase));

            // Try partial match if exact match fails
            if (culture == null)
            {
                culture = mainCultures.FirstOrDefault(c =>
                    c.Name.ToString().StartsWith(cultureName, StringComparison.OrdinalIgnoreCase));
            }

            return culture;
        }
    }
}