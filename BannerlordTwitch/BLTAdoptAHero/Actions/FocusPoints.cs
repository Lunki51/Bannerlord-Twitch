using System;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=WihQZ5uq}Focus Points"),
     LocDescription("{=01L8w1ZW}Add focus points to heroes skills"),
     UsedImplicitly]
    internal class FocusPoints : HeroCommandHandlerBase
    {
        [CategoryOrder("Costs", 0)]
        protected class FocusPointsSettings : IDocumentable
        {
            [LocDisplayName("{=TESTING}Focus 1"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=TESTING}Gold cost"),
             PropertyOrder(1), UsedImplicitly]
            public int Focus1 { get; set; } = 30000;

            [LocDisplayName("{=TESTING}Focus 2"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=TESTING}Gold cost"),
             PropertyOrder(2), UsedImplicitly]
            public int Focus2 { get; set; } = 40000;

            [LocDisplayName("{=TESTING}Focus 3"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=TESTING}Gold cost"),
             PropertyOrder(3), UsedImplicitly]
            public int Focus3 { get; set; } = 50000;

            [LocDisplayName("{=TESTING}Focus 4"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=TESTING}Gold cost"),
             PropertyOrder(4), UsedImplicitly]
            public int Focus4 { get; set; } = 60000;

            [LocDisplayName("{=TESTING}Focus 5"),
             LocCategory("Costs", "{=r7sc3Tvg}Costs"),
             LocDescription("{=TESTING}Gold cost"),
             PropertyOrder(5), UsedImplicitly]
            public int Focus5 { get; set; } = 75000;

            public int GetFocusCost(int tier)
            {
                return tier switch
                {
                    0 => Focus1,
                    1 => Focus2,
                    2 => Focus3,
                    3 => Focus4,
                    4 => Focus5,
                    _ => Focus5
                };
            }

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {

            }
        }

        public override Type HandlerConfigType => typeof(FocusPointsSettings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not FocusPointsSettings settings) return;

            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }
            var splitArgs = context.Args.Split(' ');
            string args = splitArgs[0];
            Focus(settings, adoptedHero, args, onSuccess, onFailure);
        }

        private void Focus(FocusPointsSettings settings, Hero adoptedHero, string args, Action<string> onSuccess, Action<string> onFailure)
        {

            if (string.IsNullOrWhiteSpace(args))
            {
                onFailure(
                     "{=i9ziqTXG}Provide the skill name to improve (or part of it)".Translate());
                return;
            }
            var skill = Skills.All.Find(c =>
                c.Name.ToString().IndexOf(args, StringComparison.OrdinalIgnoreCase) >= 0);


            if (skill == null)
            {
                onFailure(
                    "{=LE3POzUs}Couldn't find skill matching '{Args}'!"
                        .Translate(("Args", args)));
                return;
            }
            int focus = adoptedHero.HeroDeveloper.GetFocus(skill);
            int cost = settings.GetFocusCost(focus);
            if (focus >= 5)
            {
                onFailure($"Max focus for {skill.Name}");
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < cost)
            {
                onFailure(Naming.NotEnoughGold(cost, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            adoptedHero.HeroDeveloper.AddFocus(skill, 1, checkUnspentFocusPoints: false);
            int newFocus = adoptedHero.HeroDeveloper.GetFocus(skill);
            onSuccess(
                ("{=HLFMWOJA}You have gained a focus point in {Skill}, you now have {NewAmount}!")
                .Translate(("Skill", skill.Name), ("NewAmount", newFocus)));
            return;
        }
    }
}