using System;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=HSuPuNDk}Hero to Hero Gold"),
     LocDescription("{=wqR23RYf}Allows viewer Heroes to give gold to other viewers Heroes, by (username) (amount)"),
     UsedImplicitly]
    public class HeroToHeroGold : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            var splitArgs = context.Args.Split(' ');
            if (splitArgs.Count() != 2)
            {
                onFailure("(username) (gold)");
                return;
            }
            string targetHeroName = splitArgs[0].Replace("@", string.Empty);
            var targetHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(targetHeroName);
            if (targetHero == null)
            {
                onFailure("Couldn't find a hero for that username");
                return;
            }
            if (targetHero == adoptedHero)
            {
                onFailure("You can't send gold to yourself");
                return;
            }
            if (int.TryParse(splitArgs[1], out int amount))
            {
                if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < amount)
                {
                    onFailure("You don't have that much gold to send");
                    return;
                }
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -amount);
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(targetHero, amount);
                onSuccess($"Sent {amount} gold to {targetHero.Name}");
                return;
            }
            else
            {
                onFailure("Invalid (amount)");
                return;
            }
        }
    }
}