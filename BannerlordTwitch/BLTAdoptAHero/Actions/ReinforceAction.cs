using System;
using System.Linq;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=ReinforceCmd}Reinforce"),
     LocDescription("{=ReinforceDesc}Allow clan leaders to add Reinforcement militia to their settlements"),
     UsedImplicitly]
    public class ReinforceAction : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0),
         CategoryOrder("Militia", 1),
         CategoryOrder("Restrictions", 2)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=ReinforceEnable}Enabled"),
             LocCategory("General", "{=GeneralCat}General"),
             LocDescription("{=ReinforceEnableDesc}Enable reinforcement command"),
             PropertyOrder(1), UsedImplicitly]
            public bool Enabled { get; set; } = true;

            [LocDisplayName("{=MilitiaEnabled}Militia Enabled"),
             LocCategory("Militia", "{=MilitiaCat}Militia"),
             LocDescription("{=MilitiaEnabledDesc}Allow adding militia to settlements"),
             PropertyOrder(1), UsedImplicitly]
            public bool MilitiaEnabled { get; set; } = true;

            [LocDisplayName("{=MilitiaCostPerUnit}Gold Cost Per Militia"),
             LocCategory("Militia", "{=MilitiaCat}Militia"),
             LocDescription("{=MilitiaCostDesc}Gold cost per militia unit added"),
             PropertyOrder(2), UsedImplicitly]
            public int MilitiaCostPerUnit { get; set; } = 15000; // default 15k as requested

            [LocDisplayName("{=MilitiaMin}Minimum Militia"),
             LocCategory("Militia", "{=MilitiaCat}Militia"),
             LocDescription("{=MilitiaMinDesc}Minimum militia that can be added at once"),
             PropertyOrder(3), UsedImplicitly]
            public int MinMilitia { get; set; } = 1;

            [LocDisplayName("{=MilitiaMax}Maximum Militia"),
             LocCategory("Militia", "{=MilitiaCat}Militia"),
             LocDescription("{=MilitiaMaxDesc}Maximum militia that can be added at once"),
             PropertyOrder(4), UsedImplicitly]
            public int MaxMilitia { get; set; } = 100;

            [LocDisplayName("{=MilitiaCap}Settlement Reinforcement Cap"),
             LocCategory("Militia", "{=MilitiaCat}Militia"),
             LocDescription("{=MilitiaCapDesc}Maximum total BLT reinforcements a settlement can have (0 = no cap)"),
             PropertyOrder(5), UsedImplicitly]
            public int MilitiaCap { get; set; } = 50;

            [LocDisplayName("{=RequireClanLeader}Require Clan Leader"),
             LocCategory("Restrictions", "{=RestrictionsCat}Restrictions"),
             LocDescription("{=RequireClanLeaderDesc}Only clan leaders can add reinforcements"),
             PropertyOrder(1), UsedImplicitly]
            public bool RequireClanLeader { get; set; } = true;

            [LocDisplayName("{=OnlyUnderSiege}Only Under Siege"),
             LocCategory("Restrictions", "{=RestrictionsCat}Restrictions"),
             LocDescription("{=OnlyUnderSiegeDesc}Only allow adding reinforcements when settlement is under siege"),
             PropertyOrder(2), UsedImplicitly]
            public bool OnlyUnderSiege { get; set; } = false;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                if (!Enabled)
                {
                    generator.Value("<strong>Enabled:</strong> No");
                    return;
                }

                generator.Value("<strong>Enabled:</strong> Yes");
                generator.Value("<strong>Cost per militia:</strong> {cost}{icon}".Translate(("cost", MilitiaCostPerUnit), ("icon", Naming.Gold)));
                generator.Value("<strong>Cap per settlement:</strong> {cap}".Translate(("cap", MilitiaCap)));
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;

            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (!settings.Enabled)
            {
                onFailure("Reinforcement is disabled");
                return;
            }

            if (Mission.Current != null)
            {
                onFailure("Cannot reinforce during a mission.");
                return;
            }

            if (context.Args.IsEmpty())
            {
                onFailure("Usage: reinforce militia <settlement> <#|all>  OR  reinforce info <settlement>");
                return;
            }

            var split = context.Args.Split(' ');
            var arg = split[0].ToLowerInvariant();

            if (arg == "info")
            {
                if (split.Length < 2)
                {
                    onFailure("Usage: reinforce info <settlement>");
                    return;
                }

                var name = string.Join(" ", split.Skip(1)).Trim();
                var settlement = FindSettlement(name);
                if (settlement == null)
                {
                    onFailure($"Settlement '{name}' not found");
                    return;
                }

                int stored = ReinforcementBehavior.Current?.GetReinforcements(settlement) ?? 0;
                int cap = settings.MilitiaCap;
                int remaining = cap > 0 ? Math.Max(0, cap - stored) : -1;

                if (remaining >= 0)
                    onSuccess($"{settlement.Name} has {stored} BLT reinforcements (cap {cap}, remaining {remaining})");
                else
                    onSuccess($"{settlement.Name} has {stored} BLT reinforcements (no cap)");

                return;
            }

            // must be militia for now
            if (arg != "militia")
            {
                onFailure("Invalid argument. Use 'militia' or 'info'.");
                return;
            }

            if (split.Length < 3)
            {
                onFailure("Usage: reinforce militia <settlement> <#|all>");
                return;
            }

            var last = split.Last().ToLowerInvariant();
            bool useAll = last == "all";
            string settlementName = string.Join(" ", split.Skip(1).Take(split.Length - 2)).Trim();

            if (string.IsNullOrWhiteSpace(settlementName))
            {
                onFailure("Invalid settlement name.");
                return;
            }

            var targetSettlement = FindSettlement(settlementName);
            if (targetSettlement == null)
            {
                onFailure($"Settlement '{settlementName}' not found");
                return;
            }

            if (targetSettlement.Town == null)
            {
                onFailure("You can only reinforce towns/castles.");
                return;
            }

            if (settings.OnlyUnderSiege && (!targetSettlement.IsUnderSiege || targetSettlement.SiegeEvent == null))
            {
                onFailure($"{targetSettlement.Name} is not under siege.");
                return;
            }

            if (adoptedHero.Clan == null)
            {
                onFailure("You are not in a clan.");
                return;
            }

            if (targetSettlement.OwnerClan != adoptedHero.Clan)
            {
                onFailure($"Your clan does not own {targetSettlement.Name}.");
                return;
            }

            if (settings.RequireClanLeader && !adoptedHero.IsClanLeader)
            {
                onFailure("Only clan leaders can add reinforcements.");
                return;
            }

            // compute amount
            int amountRequested = 0;
            if (useAll)
            {
                // Purchase as many as can be afforded and fit under cap & per-command max
                int heroGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero);
                if (heroGold <= 0)
                {
                    onFailure("You have no gold.");
                    return;
                }

                int perCost = settings.MilitiaCostPerUnit;
                // compute available by gold
                int maxByGold = heroGold / perCost;
                // clamp by per-command max
                maxByGold = Math.Min(maxByGold, settings.MaxMilitia);
                // clamp by cap remaining
                int capRem = settings.MilitiaCap > 0 ? ReinforcementBehavior.Current.GetRemainingCapacity(targetSettlement, settings.MilitiaCap) : int.MaxValue;
                amountRequested = Math.Min(maxByGold, capRem);
                if (amountRequested <= 0)
                {
                    onFailure($"{targetSettlement.Name} cannot accept more reinforcements (cap or funds).");
                    return;
                }
            }
            else
            {
                if (!int.TryParse(last, out amountRequested) || amountRequested <= 0)
                {
                    onFailure("Invalid amount.");
                    return;
                }
            }

            // enforce min/max per command
            if (amountRequested < settings.MinMilitia)
            {
                onFailure($"Minimum amount is {settings.MinMilitia}.");
                return;
            }
            if (amountRequested > settings.MaxMilitia)
            {
                onFailure($"Maximum per-command is {settings.MaxMilitia}.");
                return;
            }

            // cap / partial-add
            int capRemaining = ReinforcementBehavior.Current.GetRemainingCapacity(targetSettlement, settings.MilitiaCap);
            if (settings.MilitiaCap > 0 && capRemaining <= 0)
            {
                onFailure($"{targetSettlement.Name} has reached its BLT reinforcement cap.");
                return;
            }

            int toAdd = amountRequested;
            if (settings.MilitiaCap > 0) toAdd = Math.Min(toAdd, capRemaining);

            int costPerUnit = settings.MilitiaCostPerUnit;
            int totalCost = toAdd * costPerUnit;

            // gold check
            int heroCurrentGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero);
            if (heroCurrentGold < totalCost)
            {
                onFailure(Naming.NotEnoughGold(totalCost, heroCurrentGold));
                return;
            }

            // Deduct gold and add reinforcements (partial-add semantics: if AddReinforcements returns less than toAdd, refund difference)
            try
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -totalCost, true);

                int actuallyAdded = ReinforcementBehavior.Current.AddReinforcements(targetSettlement, toAdd, settings.MilitiaCap);
                if (actuallyAdded <= 0)
                {
                    // refund
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, totalCost, true);
                    onFailure($"{targetSettlement.Name} cannot accept more reinforcements.");
                    return;
                }

                int charged = actuallyAdded * costPerUnit;
                int refund = totalCost - charged;
                if (refund > 0)
                {
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, refund, true);
                }

                onSuccess($"Added {actuallyAdded} BLT reinforcements to {targetSettlement.Name} for {charged}{Naming.Gold}.");

                Log.ShowInformation($"{adoptedHero.Name} added {actuallyAdded} BLT reinforcements to {targetSettlement.Name}.", adoptedHero.CharacterObject, Log.Sound.Notification1);
            }
            catch (Exception ex)
            {
                onFailure($"Failed to add reinforcements: {ex.Message}");
            }
        }

        private Settlement FindSettlement(string name)
        {
            return Settlement.All.FirstOrDefault(s => s?.Name?.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) == true);
        }
    }
}
