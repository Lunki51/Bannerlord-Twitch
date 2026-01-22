using System;
using System.Linq;
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
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=MercArmyCmd}Mercenary Army"),
     LocDescription("{=MercArmyDesc}Hire a mercenary army to capture a specific settlement"),
     UsedImplicitly]
    public class MercenaryArmyAction : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0),
         CategoryOrder("Costs", 1),
         CategoryOrder("Army Config", 2),
         CategoryOrder("Restrictions", 3),
         CategoryOrder("Lifetime", 4)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=MercArmyEnabled}Enabled"),
             LocCategory("General", "{=GeneralCat}General"),
             LocDescription("{=MercArmyEnabledDesc}Enable mercenary army command"),
             PropertyOrder(1), UsedImplicitly]
            public bool Enabled { get; set; } = true;

            [LocDisplayName("{=BaseCost}Base Cost"),
             LocCategory("Costs", "{=CostsCat}Costs"),
             LocDescription("{=BaseCostDesc}Base cost to hire a mercenary army"),
             PropertyOrder(1), UsedImplicitly]
            public int BaseCost { get; set; } = 50000;

            [LocDisplayName("{=CostPerTroop}Cost Per Troop"),
             LocCategory("Costs", "{=CostsCat}Costs"),
             LocDescription("{=CostPerTroopDesc}Additional cost per mercenary troop"),
             PropertyOrder(2), UsedImplicitly]
            public int CostPerTroop { get; set; } = 500;

            [LocDisplayName("{=RefundOnCancellation}Refund on Cancellation %"),
             LocCategory("Costs", "{=CostsCat}Costs"),
             LocDescription("{=RefundOnCancellationDesc}Percentage of cost refunded when army is cancelled (0-100)"),
             PropertyOrder(3), UsedImplicitly]
            public int RefundPercentage { get; set; } = 75;

            [LocDisplayName("{=MinTroops}Minimum Troops"),
             LocCategory("Army Config", "{=ArmyConfigCat}Army Configuration"),
             LocDescription("{=MinTroopsDesc}Minimum number of troops required to hire"),
             PropertyOrder(1), UsedImplicitly]
            public int MinTroops { get; set; } = 50;

            [LocDisplayName("{=MaxTroops}Maximum Troops"),
             LocCategory("Army Config", "{=ArmyConfigCat}Army Configuration"),
             LocDescription("{=MaxTroopsDesc}Maximum number of troops that can be hired"),
             PropertyOrder(2), UsedImplicitly]
            public int MaxTroops { get; set; } = 200;

            [LocDisplayName("{=MinThreshold}Minimum Troop Threshold"),
             LocCategory("Army Config", "{=ArmyConfigCat}Army Configuration"),
             LocDescription("{=MinThresholdDesc}Army disbands if troops fall below this number (unless actively sieging/fighting)"),
             PropertyOrder(3), UsedImplicitly]
            public int MinimumTroopThreshold { get; set; } = 20;

            [LocDisplayName("{=ElitePercent}Elite Troop Percentage"),
             LocCategory("Army Config", "{=ArmyConfigCat}Army Configuration"),
             LocDescription("{=ElitePercentDesc}Percentage of troops that should be elite (0-100)"),
             PropertyOrder(4), UsedImplicitly]
            public int EliteTroopPercentage { get; set; } = 20;

            [LocDisplayName("{=MaxActiveArmies}Max Active Armies Per Hero"),
             LocCategory("Restrictions", "{=RestrictionsCat}Restrictions"),
             LocDescription("{=MaxActiveArmiesDesc}Maximum number of active mercenary armies per hero (0 = unlimited)"),
             PropertyOrder(1), UsedImplicitly]
            public int MaxActiveArmiesPerHero { get; set; } = 1;

            [LocDisplayName("{=AllowClanLeaderOnly}Clan Leader Only"),
             LocCategory("Restrictions", "{=RestrictionsCat}Restrictions"),
             LocDescription("{=AllowClanLeaderOnlyDesc}Only clan leaders can hire mercenary armies"),
             PropertyOrder(2), UsedImplicitly]
            public bool ClanLeaderOnly { get; set; } = true;

            [LocDisplayName("{=MaxLifetimeDays}Maximum Lifetime (Days)"),
             LocCategory("Lifetime", "{=LifetimeCat}Lifetime"),
             LocDescription("{=MaxLifetimeDaysDesc}Maximum days an army can exist before auto-disbanding with refund (0 = unlimited)"),
             PropertyOrder(1), UsedImplicitly]
            public int MaxLifetimeDays { get; set; } = 90;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                if (!Enabled)
                {
                    generator.Value("<strong>Enabled:</strong> No");
                    return;
                }

                generator.Value("<strong>Command:</strong> !mercenary <settlement> <troops>");
                generator.Value("<strong>Example:</strong> !mercenary Pravend 100");
                generator.Value("<strong>Base cost:</strong> {base}{icon}".Translate(("base", BaseCost), ("icon", Naming.Gold)));
                generator.Value("<strong>Cost per troop:</strong> {cost}{icon}".Translate(("cost", CostPerTroop), ("icon", Naming.Gold)));
                generator.Value("<strong>Troop range:</strong> {min}-{max}".Translate(("min", MinTroops), ("max", MaxTroops)));
                generator.Value("<strong>Elite troops:</strong> {percent}%".Translate(("percent", EliteTroopPercentage)));
                generator.Value("<strong>Max active armies:</strong> {max}".Translate(("max", MaxActiveArmiesPerHero == 0 ? "Unlimited" : MaxActiveArmiesPerHero.ToString())));
                generator.Value("<strong>Max lifetime:</strong> {days} days".Translate(("days", MaxLifetimeDays == 0 ? "Unlimited" : MaxLifetimeDays.ToString())));
                generator.Value("<strong>Refund on cancel:</strong> {percent}%".Translate(("percent", RefundPercentage)));
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings)
            {
                onFailure("Invalid configuration");
                return;
            }

            // === PHASE 1: VALIDATION ===
            if (!ValidateBasicRequirements(adoptedHero, settings, onFailure))
                return;

            // === PHASE 2: PARSE ARGUMENTS ===
            if (!TryParseArguments(context.Args, settings, out string settlementName, out int troopCount, onFailure))
                return;

            // === PHASE 3: FIND AND VALIDATE SETTLEMENT ===
            if (!TryFindValidSettlement(settlementName, adoptedHero, out Settlement targetSettlement, onFailure))
                return;

            // === PHASE 4: VALIDATE WAR STATUS ===
            if (!ValidateWarStatus(adoptedHero, targetSettlement, onFailure))
                return;

            // === PHASE 5: CHECK ARMY LIMITS ===
            if (!CheckArmyLimits(adoptedHero, settings, onFailure))
                return;

            // === PHASE 6: CALCULATE TOTAL COST ===
            int totalCost = CalculateTotalCost(settings, troopCount);

            // === PHASE 7: VALIDATE FUNDS ===
            if (!ValidateFunds(adoptedHero, totalCost, onFailure))
                return;

            // === PHASE 8: CREATE ARMY ===
            CreateMercenaryArmy(adoptedHero, targetSettlement, troopCount, totalCost, settings, onSuccess, onFailure);
        }

        private bool ValidateBasicRequirements(Hero adoptedHero, Settings settings, Action<string> onFailure)
        {
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return false;
            }

            if (!settings.Enabled)
            {
                onFailure("Mercenary armies are currently disabled");
                return false;
            }

            if (Mission.Current != null)
            {
                onFailure("Cannot hire mercenaries during a mission");
                return false;
            }

            if (adoptedHero.Clan == null)
            {
                onFailure("You must be in a clan to hire mercenary armies");
                return false;
            }

            if (settings.ClanLeaderOnly && !adoptedHero.IsClanLeader)
            {
                onFailure("Only clan leaders can hire mercenary armies");
                return false;
            }

            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("Your clan must be in a kingdom to hire mercenary armies");
                return false;
            }

            return true;
        }

        private bool TryParseArguments(string argsString, Settings settings, out string settlementName, out int troopCount, Action<string> onFailure)
        {
            settlementName = null;
            troopCount = 0;

            var args = argsString?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (args == null || args.Length < 2)
            {
                onFailure($"Usage: mercenary <settlement name> <troop count>\nExample: mercenary Pravend 100");
                return false;
            }

            // Last argument must be troop count
            string troopCountStr = args[args.Length - 1];
            if (!int.TryParse(troopCountStr, out troopCount) || troopCount <= 0)
            {
                onFailure($"Invalid troop count '{troopCountStr}'. Must be a positive number.");
                return false;
            }

            // Everything before the last argument is the settlement name
            settlementName = string.Join(" ", args.Take(args.Length - 1));

            if (troopCount < settings.MinTroops)
            {
                onFailure($"Minimum troops: {settings.MinTroops}");
                return false;
            }

            if (troopCount > settings.MaxTroops)
            {
                onFailure($"Maximum troops: {settings.MaxTroops}");
                return false;
            }

            return true;
        }

        private bool TryFindValidSettlement(string settlementName, Hero hero, out Settlement targetSettlement, Action<string> onFailure)
        {
            targetSettlement = null;

            // Try exact match first (case-insensitive)
            targetSettlement = Settlement.All.FirstOrDefault(s =>
                s?.Name?.ToString().Equals(settlementName, StringComparison.OrdinalIgnoreCase) == true);

            // If no exact match, try partial match
            if (targetSettlement == null)
            {
                var partialMatches = Settlement.All
                    .Where(s => s?.Name?.ToString().IndexOf(settlementName, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                if (partialMatches.Count == 0)
                {
                    onFailure($"Settlement '{settlementName}' not found");
                    return false;
                }
                else if (partialMatches.Count == 1)
                {
                    targetSettlement = partialMatches[0];
                }
                else
                {
                    string suggestions = string.Join(", ", partialMatches.Take(5).Select(s => s.Name.ToString()));
                    onFailure($"Multiple matches found for '{settlementName}': {suggestions}. Please be more specific.");
                    return false;
                }
            }

            // Validate settlement type
            if (targetSettlement.Town == null && !targetSettlement.IsCastle)
            {
                onFailure($"{targetSettlement.Name} must be a town or castle (villages cannot be targeted)");
                return false;
            }

            // Check if already owned by hero's kingdom
            if (targetSettlement.OwnerClan?.Kingdom == hero.Clan.Kingdom)
            {
                onFailure($"{targetSettlement.Name} is already owned by your kingdom");
                return false;
            }

            return true;
        }

        private bool ValidateWarStatus(Hero hero, Settlement targetSettlement, Action<string> onFailure)
        {
            var targetFaction = targetSettlement.OwnerClan?.Kingdom ?? targetSettlement.OwnerClan?.MapFaction;

            if (targetFaction == null)
            {
                onFailure($"{targetSettlement.Name} has no valid owner");
                return false;
            }

            if (!FactionManager.IsAtWarAgainstFaction(hero.Clan.Kingdom, targetFaction))
            {
                onFailure($"Your kingdom is not at war with {targetSettlement.Name}'s owner ({targetFaction.Name})");
                return false;
            }

            return true;
        }

        private bool CheckArmyLimits(Hero hero, Settings settings, Action<string> onFailure)
        {
            if (settings.MaxActiveArmiesPerHero <= 0)
                return true; // Unlimited

            var behavior = MercenaryArmyBehavior.Current;
            if (behavior == null)
            {
                onFailure("Mercenary army system not initialized");
                return false;
            }

            int activeArmies = behavior.GetActiveArmiesForHero(hero);
            if (activeArmies >= settings.MaxActiveArmiesPerHero)
            {
                onFailure($"You already have {activeArmies} active mercenary {(activeArmies == 1 ? "army" : "armies")} (maximum: {settings.MaxActiveArmiesPerHero})");
                return false;
            }

            return true;
        }

        private int CalculateTotalCost(Settings settings, int troopCount)
        {
            int baseCost = settings.BaseCost;
            int troopCost = troopCount * settings.CostPerTroop;

            return baseCost + troopCost;
        }

        private bool ValidateFunds(Hero hero, int totalCost, Action<string> onFailure)
        {
            int heroGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero);

            if (heroGold < totalCost)
            {
                onFailure(Naming.NotEnoughGold(totalCost, heroGold));
                return false;
            }

            return true;
        }

        private void CreateMercenaryArmy(Hero hero, Settlement target, int troopCount, int totalCost, Settings settings, Action<string> onSuccess, Action<string> onFailure)
        {
            try
            {
                var behavior = MercenaryArmyBehavior.Current;
                if (behavior == null)
                {
                    onFailure("Mercenary army system not initialized");
                    return;
                }

                // Calculate refund amount based on settings
                int refundAmount = (int)(totalCost * (settings.RefundPercentage / 100f));

                // Create the army
                var result = behavior.CreateMercenaryArmy(
                    originalHero: hero,
                    targetSettlement: target,
                    troopCount: troopCount,
                    elitePercentage: settings.EliteTroopPercentage,
                    minTroopThreshold: settings.MinimumTroopThreshold,
                    totalCost: totalCost,
                    refundAmount: refundAmount,
                    maxLifetimeDays: settings.MaxLifetimeDays
                );

                if (!result.Success)
                {
                    onFailure($"Failed to create mercenary army: {result.ErrorMessage}");
                    return;
                }

                // Deduct gold only after successful creation
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -totalCost, true);

                // Success notification
                string message = $"Hired {troopCount} mercenaries to capture {target.Name} for {totalCost}{Naming.Gold}";
                if (settings.MaxLifetimeDays > 0)
                {
                    message += $" (contract expires in {settings.MaxLifetimeDays} days)";
                }

                onSuccess(message);

                Log.ShowInformation(
                    $"{hero.Name} hired mercenary army ({troopCount} troops) to capture {target.Name}",
                    hero.CharacterObject,
                    Log.Sound.Horns2
                );
            }
            catch (Exception ex)
            {
                onFailure($"Error creating mercenary army: {ex.Message}");
                Log.Error($"[BLT] MercenaryArmyAction error: {ex}");
            }
        }
    }
}