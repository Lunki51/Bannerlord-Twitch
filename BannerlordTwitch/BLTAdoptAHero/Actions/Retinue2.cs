using System;
using System.Reflection;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using System.Linq;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=tLSFX9Xc}Secondary Retinue"),
     LocDescription("{=bhC3VcmU}Add and improve adopted heroes secondary retinue"),
     UsedImplicitly]
    public class Retinue2 : ActionHandlerBase
    {
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=tLSFX9Xc}Secondary Retinue"),
             LocDescription("{=iNoFrKsN}Secondary Retinue Upgrade Settings"),
             PropertyOrder(1), ExpandableObject, Expand, UsedImplicitly]
            public BLTAdoptAHeroCampaignBehavior.Retinue2Settings Retinue2 { get; set; } = new();

            [LocDisplayName("{=nIsuuFMC}All By Default"),
             LocDescription("{=mJSGvWlR}Whether this action should attempt to buy/upgrade as many times as possible when called with no parameter."),
             PropertyOrder(2), UsedImplicitly]
            public bool AllByDefault { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                Retinue2.GenerateDocumentation(generator);
            }
        }

        protected override Type ConfigType => typeof(Settings);

        protected override void ExecuteInternal(ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            var settings = (Settings)config;
            var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);

            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (Mission.Current != null)
            {
                onFailure("{=mCcpMwrN}You cannot modify secondary retinue while a mission is active!".Translate());
                return;
            }

            int numToUpgrade = settings.AllByDefault ? int.MaxValue : 1;

            if (!string.IsNullOrEmpty(context.Args))
            {
                var args = context.Args.Split(' ');

                // Handle !secondary retinue clear <index>
                if (args.Length > 0 && string.Compare(args[0], "clear", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    // Handle !secondary retinue clear all
                    if (args.Length > 1 && args[1].ToLower() == "all")
                    {
                        int count = BLTAdoptAHeroCampaignBehavior.Current.GetRetinue2(adoptedHero).Count();
                        for (int i = 0; i < count; i++)
                        {
                            BLTAdoptAHeroCampaignBehavior.Current.KillRetinue2AtIndex(adoptedHero, 0);
                        }
                        onSuccess("Cleared all secondary retinue slots.");
                    }
                    else if (args.Length > 1 && int.TryParse(args[1], out int index))
                    {
                        BLTAdoptAHeroCampaignBehavior.Current.KillRetinue2AtIndex(adoptedHero, index - 1);
                        onSuccess($"Removed secondary retinue at slot {index}.");
                    }
                    else
                    {
                        onFailure("You must specify a valid secondary retinue index to clear.");
                    }
                    return; // exit after clear
                }

                // Handle upgrades (!secondary retinue all or !secondary retinue <number>)
                if (string.Compare(args[0], "all", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    numToUpgrade = int.MaxValue;
                }
                else if (!int.TryParse(args[0], out numToUpgrade) || numToUpgrade <= 0)
                {
                    onFailure(context.ArgsErrorMessage("{=NexXxYvj}(number, or all)".Translate()));
                    return;
                }
            }

            // Perform upgrade
            (bool success, string status) = BLTAdoptAHeroCampaignBehavior.Current
                .UpgradeRetinue2(adoptedHero, settings.Retinue2, numToUpgrade);

            if (success)
                onSuccess(status);
            else
                onFailure(status);
        }   
    }
}
