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
using TaleWorlds.Library;
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
         CategoryOrder("EliteMilitia", 2),
         CategoryOrder("Restrictions", 3)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=ReinforceEnable}Enabled"),
             LocCategory("General", "{=GeneralCat}General"),
             LocDescription("{=ReinforceEnableDesc}Enable reinforcement command"),
             PropertyOrder(1), UsedImplicitly]
            public bool Enabled { get; set; } = true;

            // Militia Settings
            [LocDisplayName("{=MilitiaEnabled}Militia Enabled"),
             LocCategory("Militia", "{=MilitiaCat}Militia"),
             LocDescription("{=MilitiaEnabledDesc}Allow adding militia to settlements"),
             PropertyOrder(1), UsedImplicitly]
            public bool MilitiaEnabled { get; set; } = true;

            [LocDisplayName("{=MilitiaCostPerUnit}Gold Cost Per Militia"),
             LocCategory("Militia", "{=MilitiaCat}Militia"),
             LocDescription("{=MilitiaCostDesc}Gold cost per militia unit added"),
             PropertyOrder(2), UsedImplicitly]
            public int MilitiaCostPerUnit { get; set; } = 15000;

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
            public int MilitiaCap { get; set; } = 100;

            [LocDisplayName("{=MilitiaCapital}Capital Max Bonus"),
             LocCategory("Militia", "{=MilitiaCat}Militia"),
             LocDescription("{=MilitiaCapitalDesc}Bonus to the maximum total BLT reinforcements a Kingdom Capital can have (this is added to standard reinforcement cap)"),
             PropertyOrder(6), UsedImplicitly]
            public int CapitalMilitiaBonus { get; set; } = 150;

            // Elite Militia Settings (carbon copy)
            [LocDisplayName("{=EliteEnabled}Elite Militia Enabled"),
             LocCategory("EliteMilitia", "{=EliteCat}EliteMilitia"),
             LocDescription("{=EliteEnabledDesc}Allow adding elite militia to settlements"),
             PropertyOrder(1), UsedImplicitly]
            public bool EliteEnabled { get; set; } = true;

            [LocDisplayName("{=EliteCostPerUnit}Gold Cost Per Elite Militia"),
             LocCategory("EliteMilitia", "{=EliteCat}EliteMilitia"),
             LocDescription("{=EliteCostDesc}Gold cost per elite militia unit added"),
             PropertyOrder(2), UsedImplicitly]
            public int EliteCostPerUnit { get; set; } = 30000;

            [LocDisplayName("{=EliteMin}Minimum Elite Militia"),
             LocCategory("EliteMilitia", "{=EliteCat}EliteMilitia"),
             LocDescription("{=EliteMinDesc}Minimum elite militia that can be added at once"),
             PropertyOrder(3), UsedImplicitly]
            public int MinEliteMilitia { get; set; } = 1;

            [LocDisplayName("{=EliteMax}Maximum Elite Militia"),
             LocCategory("EliteMilitia", "{=EliteCat}EliteMilitia"),
             LocDescription("{=EliteMaxDesc}Maximum elite militia that can be added at once"),
             PropertyOrder(4), UsedImplicitly]
            public int MaxEliteMilitia { get; set; } = 100;

            [LocDisplayName("{=EliteCap}Settlement Elite Reinforcement Cap"),
             LocCategory("EliteMilitia", "{=EliteCat}EliteMilitia"),
             LocDescription("{=EliteCapDesc}Maximum total BLT elite reinforcements a settlement can have (0 = no cap)"),
             PropertyOrder(5), UsedImplicitly]
            public int EliteMilitiaCap { get; set; } = 50;

            [LocDisplayName("{=EliteCapital}Capital Max Bonus"),
             LocCategory("EliteMilitia", "{=EliteCat}EliteMilitia"),
             LocDescription("{=EliteCapitalDesc}Bonus to the maximum total BLT reinforcements a Kingdom Capital can have (this is added to standard reinforcement cap)"),
             PropertyOrder(6), UsedImplicitly]
            public int CapitalEliteBonus { get; set; } = 50;

            [LocDisplayName("{=RequireClanLeader}Require Clan Leader"),
             LocCategory("Restrictions", "{=RestrictionsCat}Restrictions"),
             LocDescription("{=RequireClanLeaderDesc}Only clan leaders can add reinforcements, or anyone in clan"),
             PropertyOrder(1), UsedImplicitly]
            public bool RequireClanLeader { get; set; } = true;

            [LocDisplayName("{=AllowKingdomLeaders}Allow Kingdom Leaders"),
             LocCategory("Restrictions", "{=RestrictionsCat}Restrictions"),
             LocDescription("{=AllowKingdomLeadersDesc}Allow kingdom rulers to reinforce any settlement owned by their kingdom"),
             PropertyOrder(2), UsedImplicitly]
            public bool AllowKingdomLeaders { get; set; } = false;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                if (!Enabled)
                {
                    generator.Value("<strong>Enabled:</strong> No");
                    return;
                }

                generator.Value("<strong>Enabled:</strong> Yes");
                generator.Value("<strong>Militia cost per unit:</strong> {cost}{icon}".Translate(("cost", MilitiaCostPerUnit), ("icon", Naming.Gold)));
                generator.Value("<strong>Militia cap per settlement:</strong> {cap}".Translate(("cap", MilitiaCap)));
                generator.Value("<strong>Extra militia cap for Kingdom Capital:</strong> {bonus}".Translate(("bonus", CapitalMilitiaBonus)));
                generator.Value("<strong>Elite cost per unit:</strong> {cost}{icon}".Translate(("cost", EliteCostPerUnit), ("icon", Naming.Gold)));
                generator.Value("<strong>Elite cap per settlement:</strong> {cap}".Translate(("cap", EliteMilitiaCap)));
                generator.Value("<strong>Extra elite militia cap for Kingdom Capital:</strong> {elitebonus}".Translate(("elitebonus", CapitalEliteBonus)));
                if (AllowKingdomLeaders) generator.Value("<strong>Kingdom leaders allowed to reinforce kingdom settlements.</strong>".Translate(("kingallow", AllowKingdomLeaders)));
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
                onFailure("Usage: reinforce militia|elitemilitia <settlement> <#|all>  OR  reinforce info <settlement>");
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

                int militiaStored = ReinforcementBehavior.Current?.GetReinforcements(settlement) ?? 0;
                int eliteStored = ReinforcementBehavior.Current?.GetEliteReinforcements(settlement) ?? 0;
                int militiaCap = settings.MilitiaCap;
                int eliteCap = settings.EliteMilitiaCap;
                int militiaRemaining = militiaCap > 0 ? Math.Max(0, militiaCap - militiaStored) : -1;
                int eliteRemaining = eliteCap > 0 ? Math.Max(0, eliteCap - eliteStored) : -1;

                if (militiaRemaining >= 0)
                    onSuccess($"{settlement.Name} has {militiaStored} BLT militia (cap {militiaCap}, remaining {militiaRemaining}); {eliteStored} elite militia (cap {eliteCap}, remaining {eliteRemaining})");
                else
                    onSuccess($"{settlement.Name} has {militiaStored} BLT militia (no cap); {eliteStored} elite militia (no cap)");

                return;
            }

            // must be militia or elitemilitia for now
            if (arg != "militia" && arg != "elitemilitia")
            {
                onFailure("Invalid argument. Use 'militia', 'elitemilitia' or 'info'.");
                return;
            }

            if (split.Length < 3)
            {
                onFailure("Usage: reinforce militia|elitemilitia <settlement> <#|all>");
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

            if (!targetSettlement.IsUnderSiege || targetSettlement.SiegeEvent == null)
            {
                onFailure($"{targetSettlement.Name} is under siege.");
                return;
            }

            if (adoptedHero.Clan == null)
            {
                onFailure("You are not in a clan.");
                return;
            }

            // ownership/kingdom-leader permission check
            bool isOwnerClan = targetSettlement.OwnerClan == adoptedHero.Clan;

            bool isKingdomLeaderAllowed = false;
            if (settings.AllowKingdomLeaders && adoptedHero.Clan != null && adoptedHero.Clan.Kingdom != null)
            {
                // hero is the kingdom ruler?
                bool heroIsKingdomLeader = adoptedHero.Clan.Kingdom.Leader == adoptedHero;
                // settlement belongs to the same kingdom?
                bool settlementInSameKingdom = targetSettlement.OwnerClan != null && targetSettlement.OwnerClan.Kingdom == adoptedHero.Clan.Kingdom;
                isKingdomLeaderAllowed = heroIsKingdomLeader && settlementInSameKingdom;
            }

            if (!isOwnerClan && !isKingdomLeaderAllowed)
            {
                onFailure($"Your clan does not own {targetSettlement.Name}.");
                return;
            }

            if (settings.RequireClanLeader && !adoptedHero.IsClanLeader)
            {
                onFailure("Only clan leaders can add reinforcements.");
                return;
            }

            // compute amount and cost depending on type
            bool isElite = arg == "elitemilitia";
            int amountRequested = 0;
            int perCost = isElite ? settings.EliteCostPerUnit : settings.MilitiaCostPerUnit;
            int minAmount = isElite ? settings.MinEliteMilitia : settings.MinMilitia;
            int maxAmount = isElite ? settings.MaxEliteMilitia : settings.MaxMilitia;
            int cap = isElite ? settings.EliteMilitiaCap : settings.MilitiaCap;
            if (targetSettlement.OwnerClan.Leader.IsKingdomLeader && targetSettlement == targetSettlement.OwnerClan.HomeSettlement)
            {
                cap += isElite ? settings.CapitalEliteBonus : settings.CapitalMilitiaBonus;
                maxAmount += isElite ? settings.CapitalEliteBonus : settings.CapitalMilitiaBonus;
            }

            if (useAll)
            {
                int heroGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero);
                if (heroGold <= 0)
                {
                    onFailure("You have no gold.");
                    return;
                }

                int maxByGold = heroGold / perCost;
                maxByGold = Math.Min(maxByGold, maxAmount);
                int capRem = cap > 0 ? (isElite ? ReinforcementBehavior.Current.GetRemainingEliteCapacity(targetSettlement, cap) : ReinforcementBehavior.Current.GetRemainingCapacity(targetSettlement, cap)) : int.MaxValue;
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
            if (amountRequested < minAmount)
            {
                onFailure($"Minimum amount is {minAmount}.");
                return;
            }
            if (amountRequested > maxAmount)
            {
                onFailure($"Maximum per-command is {maxAmount}.");
                return;
            }

            // cap / partial-add
            var behavior = ReinforcementBehavior.Current;
            if (behavior == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[BLT Reinforcement] BLTReinforcementBehavior not initialized."));
                return;
            }

            int capRemaining = isElite
                ? behavior.GetRemainingEliteCapacity(targetSettlement, cap)
                : behavior.GetRemainingCapacity(targetSettlement, cap);

            if (cap > 0 && capRemaining <= 0)
            {
                onFailure($"{targetSettlement.Name} has reached its BLT reinforcement cap for this tier.");
                return;
            }

            int toAdd = amountRequested;
            if (cap > 0) toAdd = Math.Min(toAdd, capRemaining);

            int totalCost = toAdd * perCost;

            // gold check
            int heroCurrentGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero);
            if (heroCurrentGold < totalCost)
            {
                onFailure(Naming.NotEnoughGold(totalCost, heroCurrentGold));
                return;
            }

            // Deduct gold and add reinforcements (partial-add semantics)
            try
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -totalCost, true);

                int actuallyAdded = isElite
                    ? ReinforcementBehavior.Current.AddEliteReinforcements(targetSettlement, toAdd, cap)
                    : ReinforcementBehavior.Current.AddReinforcements(targetSettlement, toAdd, cap);

                if (actuallyAdded <= 0)
                {
                    // refund
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, totalCost, true);
                    onFailure($"{targetSettlement.Name} cannot accept more reinforcements.");
                    return;
                }

                int charged = actuallyAdded * perCost;
                int refund = totalCost - charged;
                if (refund > 0)
                {
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, refund, true);
                }

                string tierName = isElite ? "elite militia" : "BLT reinforcements";
                onSuccess($"Added {actuallyAdded} {tierName} to {targetSettlement.Name} for {charged}{Naming.Gold}.");

                Log.ShowInformation($"{adoptedHero.Name} added {actuallyAdded} {tierName} to {targetSettlement.Name}.", adoptedHero.CharacterObject, Log.Sound.Notification1);
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
