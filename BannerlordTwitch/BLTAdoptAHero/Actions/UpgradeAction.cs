using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Annotations;
using BLTAdoptAHero.Actions.Upgrades;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using BannerlordTwitch.Rewards;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=BLT_UpgradeCmd}Upgrade"),
     LocDescription("{=BLT_UpgradeCmdDesc}Purchase upgrades for fiefs, clans, or kingdoms"),
     UsedImplicitly]
    public class UpgradeAction : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0),
 CategoryOrder("Permissions", 1)]
        public class Settings : IDocumentable
        {
            [LocDisplayName("{=BLT_UpgradeEnabled}Enabled"),
             LocCategory("General", "{=GeneralCat}General"),
             LocDescription("{=BLT_UpgradeEnabledDesc}Enable the upgrade system"),
             PropertyOrder(1), UsedImplicitly]
            public bool Enabled { get; set; } = true;

            [LocDisplayName("{=BLT_AllowList}Allow List Command"),
             LocCategory("General", "{=GeneralCat}General"),
             LocDescription("{=BLT_AllowListDesc}Allow players to list all available upgrades"),
             PropertyOrder(2), UsedImplicitly]
            public bool AllowListCommand { get; set; } = true;

            // Permissions
            [LocDisplayName("{=BLT_KingdomLeaderFiefs}Kingdom Leaders Can Upgrade Fiefs"),
             LocCategory("Permissions", "{=BLT_Permissions}Permissions"),
             LocDescription("{=BLT_KingdomLeaderFiefsDesc}Allow kingdom rulers to purchase fief upgrades for settlements in their kingdom"),
             PropertyOrder(1), UsedImplicitly]
            public bool AllowKingdomLeadersForFiefs { get; set; } = false;

            [LocDisplayName("{=BLT_AnyClanMember}Any Clan Member Can Upgrade Clan"),
             LocCategory("Permissions", "{=BLT_Permissions}Permissions"),
             LocDescription("{=BLT_AnyClanMemberDesc}Allow any clan member to purchase clan upgrades (not just the leader)"),
             PropertyOrder(2), UsedImplicitly]
            public bool AllowAnyClanMemberForClanUpgrades { get; set; } = false;


            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.P($"<strong>Enabled:</strong> {(Enabled ? "Yes" : "No")}");
                generator.P($"<strong>Allow List Command:</strong> {(AllowListCommand ? "Yes" : "No")}");
                generator.P($"<strong>Kingdom Leaders Can Upgrade Fiefs:</strong> {(AllowKingdomLeadersForFiefs ? "Yes" : "No")}");
                generator.P($"<strong>Any Clan Member Can Upgrade Clan:</strong> {(AllowAnyClanMemberForClanUpgrades ? "Yes" : "No")}");
            }
        }

        public class UpgradeSystemDocumentation : IDocumentable
        {
            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.H1("Upgrade System");
                generator.P("This section contains all available upgrades organized by type and restrictions.");

                var config = GlobalCommonConfig.Get();
                if (config == null)
                {
                    generator.P("Configuration not available");
                    return;
                }

                // Generate upgrade counts summary
                GenerateUpgradeCounts(generator, config);

                // Generate all upgrade tables
                GenerateUpgradesTables(generator, config);
            }

            private void GenerateUpgradeCounts(IDocumentationGenerator generator, GlobalCommonConfig config)
            {
                generator.H2("Upgrade Counts");
                generator.P($"<strong>Fief Upgrades:</strong> {config.FiefUpgrades?.Count ?? 0}");
                generator.P($"<strong>Clan Upgrades:</strong> {config.ClanUpgrades?.Count ?? 0}");
                generator.P($"<strong>Kingdom Upgrades:</strong> {config.KingdomUpgrades?.Count ?? 0}");
            }

            private void GenerateUpgradesTables(IDocumentationGenerator generator, GlobalCommonConfig config)
            {
                // Fief Upgrades - Split by Coastal Only
                if (config.FiefUpgrades != null && config.FiefUpgrades.Count > 0)
                {
                    var standardFiefUpgrades = config.FiefUpgrades.Where(u => !u.CoastalOnly).ToList();
                    var coastalOnlyUpgrades = config.FiefUpgrades.Where(u => u.CoastalOnly).ToList();

                    generator.H2("Fief Upgrades");

                    if (standardFiefUpgrades.Count > 0)
                    {
                        generator.H3("Standard Fief Upgrades");
                        GenerateFiefUpgradeTable(generator, standardFiefUpgrades);
                    }

                    if (coastalOnlyUpgrades.Count > 0)
                    {
                        generator.H3("Coastal Only Fief Upgrades");
                        GenerateFiefUpgradeTable(generator, coastalOnlyUpgrades);
                    }
                }

                // Clan Upgrades - Split by MercOnly and ApplyToVassals
                if (config.ClanUpgrades != null && config.ClanUpgrades.Count > 0)
                {
                    var standardClanUpgrades = config.ClanUpgrades.Where(u => !u.MercOnly && !u.ApplyToVassals).ToList();
                    var mercOnlyUpgrades = config.ClanUpgrades.Where(u => u.MercOnly && !u.ApplyToVassals).ToList();
                    var vassalOnlyUpgrades = config.ClanUpgrades.Where(u => u.ApplyToVassals).ToList();

                    generator.H2("Clan Upgrades");

                    if (standardClanUpgrades.Count > 0)
                    {
                        generator.H3("Standard Clan Upgrades");
                        GenerateClanUpgradeTable(generator, standardClanUpgrades);
                    }

                    if (mercOnlyUpgrades.Count > 0)
                    {
                        generator.H3("Mercenary Only Clan Upgrades");
                        GenerateClanUpgradeTable(generator, mercOnlyUpgrades);
                    }

                    if (vassalOnlyUpgrades.Count > 0)
                    {
                        generator.H3("Vassal Only Clan Upgrades");
                        GenerateClanUpgradeTable(generator, vassalOnlyUpgrades);
                    }
                }

                // Kingdom Upgrades - Single table
                if (config.KingdomUpgrades != null && config.KingdomUpgrades.Count > 0)
                {
                    generator.H2("Kingdom Upgrades");
                    GenerateKingdomUpgradeTable(generator, config.KingdomUpgrades.ToList());
                }
            }

            private void GenerateFiefUpgradeTable(IDocumentationGenerator generator, System.Collections.Generic.List<FiefUpgrade> upgrades)
            {
                generator.Table("upgrade-table", () =>
                {
                    // Table header
                    generator.TR(() =>
                    {
                        generator.TH("ID");
                        generator.TH("Name");
                        generator.TH("Cost");
                        generator.TH("Tier");
                        generator.TH("Required");
                        generator.TH("Description");
                    });

                    // Table rows
                    foreach (var upgrade in upgrades)
                    {
                        generator.TR(() =>
                        {
                            generator.TD(upgrade.ID);
                            generator.TD(upgrade.Name);
                            generator.TD($"{upgrade.GoldCost}{Naming.Gold}");
                            generator.TD(upgrade.TierLevel > 0 ? upgrade.TierLevel.ToString() : "-");
                            generator.TD(!string.IsNullOrEmpty(upgrade.RequiredUpgradeID) ? upgrade.RequiredUpgradeID : "-");
                            generator.TD(() =>
                            {
                                generator.P(upgrade.Description);
                                if (ShouldShowFullDescription(upgrade.ID))
                                {
                                    generator.Details(() =>
                                    {
                                        generator.Summary("View Details");
                                        var effects = GetUpgradeEffects(upgrade);
                                        if (!string.IsNullOrEmpty(effects))
                                        {
                                            generator.P(effects);
                                        }
                                    });
                                }
                            });
                        });
                    }
                });
            }

            private void GenerateClanUpgradeTable(IDocumentationGenerator generator, System.Collections.Generic.List<ClanUpgrade> upgrades)
            {
                generator.Table("upgrade-table", () =>
                {
                    // Table header
                    generator.TR(() =>
                    {
                        generator.TH("ID");
                        generator.TH("Name");
                        generator.TH("Cost");
                        generator.TH("Tier");
                        generator.TH("Required");
                        generator.TH("Description");
                    });

                    // Table rows
                    foreach (var upgrade in upgrades)
                    {
                        generator.TR(() =>
                        {
                            generator.TD(upgrade.ID);
                            generator.TD(upgrade.Name);
                            generator.TD($"{upgrade.GoldCost}{Naming.Gold}");
                            generator.TD(upgrade.TierLevel > 0 ? upgrade.TierLevel.ToString() : "-");
                            generator.TD(!string.IsNullOrEmpty(upgrade.RequiredUpgradeID) ? upgrade.RequiredUpgradeID : "-");
                            generator.TD(() =>
                            {
                                generator.P(upgrade.Description);
                                if (ShouldShowFullDescription(upgrade.ID))
                                {
                                    generator.Details(() =>
                                    {
                                        generator.Summary("View Details");
                                        var effects = GetUpgradeEffects(upgrade);
                                        if (!string.IsNullOrEmpty(effects))
                                        {
                                            generator.P(effects);
                                        }
                                    });
                                }
                            });
                        });
                    }
                });
            }

            private void GenerateKingdomUpgradeTable(IDocumentationGenerator generator, System.Collections.Generic.List<KingdomUpgrade> upgrades)
            {
                generator.Table("upgrade-table", () =>
                {
                    // Table header
                    generator.TR(() =>
                    {
                        generator.TH("ID");
                        generator.TH("Name");
                        generator.TH("Cost");
                        generator.TH("Tier");
                        generator.TH("Required");
                        generator.TH("Description");
                    });

                    // Table rows
                    foreach (var upgrade in upgrades)
                    {
                        generator.TR(() =>
                        {
                            generator.TD(upgrade.ID);
                            generator.TD(upgrade.Name);
                            generator.TD(upgrade.GetCostString());
                            generator.TD(upgrade.TierLevel > 0 ? upgrade.TierLevel.ToString() : "-");
                            generator.TD(!string.IsNullOrEmpty(upgrade.RequiredUpgradeID) ? upgrade.RequiredUpgradeID : "-");
                            generator.TD(() =>
                            {
                                generator.P(upgrade.Description);
                                if (ShouldShowFullDescription(upgrade.ID))
                                {
                                    generator.Details(() =>
                                    {
                                        generator.Summary("View Details");
                                        var effects = GetUpgradeEffects(upgrade);
                                        if (!string.IsNullOrEmpty(effects))
                                        {
                                            generator.P(effects);
                                        }
                                    });
                                }
                            });
                        });
                    }
                });
            }

            private string GetUpgradeEffects(FiefUpgrade upgrade)
            {
                var effects = new System.Text.StringBuilder();
                effects.AppendLine("<strong>Effects:</strong><br>");

                if (upgrade.LoyaltyDailyFlat != 0) effects.AppendLine($"Loyalty: {(upgrade.LoyaltyDailyFlat > 0 ? "+" : "")}{upgrade.LoyaltyDailyFlat}/day<br>");
                if (upgrade.LoyaltyDailyPercent != 0) effects.AppendLine($"Loyalty: {(upgrade.LoyaltyDailyPercent > 0 ? "+" : "")}{upgrade.LoyaltyDailyPercent}%/day<br>");
                if (upgrade.ProsperityDailyFlat != 0) effects.AppendLine($"Prosperity: {(upgrade.ProsperityDailyFlat > 0 ? "+" : "")}{upgrade.ProsperityDailyFlat}/day<br>");
                if (upgrade.ProsperityDailyPercent != 0) effects.AppendLine($"Prosperity: {(upgrade.ProsperityDailyPercent > 0 ? "+" : "")}{upgrade.ProsperityDailyPercent}%/day<br>");
                if (upgrade.SecurityDailyFlat != 0) effects.AppendLine($"Security: {(upgrade.SecurityDailyFlat > 0 ? "+" : "")}{upgrade.SecurityDailyFlat}/day<br>");
                if (upgrade.SecurityDailyPercent != 0) effects.AppendLine($"Security: {(upgrade.SecurityDailyPercent > 0 ? "+" : "")}{upgrade.SecurityDailyPercent}%/day<br>");
                if (upgrade.MilitiaDailyFlat != 0) effects.AppendLine($"Militia: {(upgrade.MilitiaDailyFlat > 0 ? "+" : "")}{upgrade.MilitiaDailyFlat}/day<br>");
                if (upgrade.MilitiaDailyPercent != 0) effects.AppendLine($"Militia: {(upgrade.MilitiaDailyPercent > 0 ? "+" : "")}{upgrade.MilitiaDailyPercent}%/day<br>");
                if (upgrade.FoodDailyFlat != 0) effects.AppendLine($"Food: {(upgrade.FoodDailyFlat > 0 ? "+" : "")}{upgrade.FoodDailyFlat}/day<br>");
                if (upgrade.FoodDailyPercent != 0) effects.AppendLine($"Food: {(upgrade.FoodDailyPercent > 0 ? "+" : "")}{upgrade.FoodDailyPercent}%/day<br>");
                if (upgrade.TaxIncomeFlat != 0) effects.AppendLine($"Tax Income: {(upgrade.TaxIncomeFlat > 0 ? "+" : "")}{upgrade.TaxIncomeFlat}{Naming.Gold}/day<br>");
                if (upgrade.TaxIncomePercent != 0) effects.AppendLine($"Tax Income: {(upgrade.TaxIncomePercent > 0 ? "+" : "")}{upgrade.TaxIncomePercent}%<br>");
                if (upgrade.GarrisonCapacityBonus != 0) effects.AppendLine($"Garrison Capacity: {(upgrade.GarrisonCapacityBonus > 0 ? "+" : "")}{upgrade.GarrisonCapacityBonus}<br>");
                if (upgrade.HearthDaily != 0) effects.AppendLine($"Hearth: {(upgrade.HearthDaily > 0 ? "+" : "")}{upgrade.HearthDaily}<br>");

                return effects.Length > 0 ? effects.ToString() : "No effects configured";
            }

            private string GetUpgradeEffects(ClanUpgrade upgrade)
            {
                var effects = new System.Text.StringBuilder();

                // Clan Effects
                if (upgrade.RenownDaily != 0 || upgrade.PartySizeBonus != 0 || upgrade.PartySpeedBonus != 0 ||
                    upgrade.PartyAmountBonus != 0 || upgrade.MaxVassalsBonus != 0 || upgrade.MercIncomeFlat != 0 ||
                    upgrade.MercIncomePercent != 0)
                {
                    effects.AppendLine("<strong>Clan Effects:</strong><br>");
                    if (upgrade.RenownDaily != 0) effects.AppendLine($"Renown: {(upgrade.RenownDaily > 0 ? "+" : "")}{upgrade.RenownDaily}/day<br>");
                    if (upgrade.PartySizeBonus != 0) effects.AppendLine($"Party Size: {(upgrade.PartySizeBonus > 0 ? "+" : "")}{upgrade.PartySizeBonus}<br>");
                    if (upgrade.PartySpeedBonus != 0) effects.AppendLine($"Party Speed: {(upgrade.PartySpeedBonus > 0 ? "+" : "")}{upgrade.PartySpeedBonus}<br>");
                    if (upgrade.PartyAmountBonus != 0) effects.AppendLine($"Party Limit: {(upgrade.PartyAmountBonus > 0 ? "+" : "")}{upgrade.PartyAmountBonus}<br>");
                    if (upgrade.MaxVassalsBonus != 0) effects.AppendLine($"Vassal Limit: {(upgrade.MaxVassalsBonus > 0 ? "+" : "")}{upgrade.MaxVassalsBonus}<br>");
                    if (upgrade.MercIncomeFlat != 0) effects.AppendLine($"Merc Income (Flat): {(upgrade.MercIncomeFlat > 0 ? "+" : "")}{upgrade.MercIncomeFlat}/day<br>");
                    if (upgrade.MercIncomePercent != 0) effects.AppendLine($"Merc Income (%): {(upgrade.MercIncomePercent > 0 ? "+" : "")}{upgrade.MercIncomePercent}%/day<br>");
                }

                // Settlement Effects
                if (upgrade.LoyaltyDailyFlat != 0 || upgrade.LoyaltyDailyPercent != 0 || upgrade.ProsperityDailyFlat != 0 || upgrade.ProsperityDailyPercent != 0 ||
                    upgrade.SecurityDailyFlat != 0 || upgrade.SecurityDailyPercent != 0 || upgrade.MilitiaDailyFlat != 0 || upgrade.MilitiaDailyPercent != 0 ||
                    upgrade.FoodDailyFlat != 0 || upgrade.FoodDailyPercent != 0 || upgrade.TaxIncomeFlat != 0 || upgrade.TaxIncomePercent != 0 ||
                    upgrade.GarrisonCapacityBonus != 0 || upgrade.HearthDaily != 0)
                {
                    effects.AppendLine("<br><strong>Settlement Effects:</strong><br>");
                    if (upgrade.LoyaltyDailyFlat != 0) effects.AppendLine($"Loyalty: {(upgrade.LoyaltyDailyFlat > 0 ? "+" : "")}{upgrade.LoyaltyDailyFlat}/day<br>");
                    if (upgrade.LoyaltyDailyPercent != 0) effects.AppendLine($"Loyalty: {(upgrade.LoyaltyDailyPercent > 0 ? "+" : "")}{upgrade.LoyaltyDailyPercent}%/day<br>");
                    if (upgrade.ProsperityDailyFlat != 0) effects.AppendLine($"Prosperity: {(upgrade.ProsperityDailyFlat > 0 ? "+" : "")}{upgrade.ProsperityDailyFlat}/day<br>");
                    if (upgrade.ProsperityDailyPercent != 0) effects.AppendLine($"Prosperity: {(upgrade.ProsperityDailyPercent > 0 ? "+" : "")}{upgrade.ProsperityDailyPercent}%/day<br>");
                    if (upgrade.SecurityDailyFlat != 0) effects.AppendLine($"Security: {(upgrade.SecurityDailyFlat > 0 ? "+" : "")}{upgrade.SecurityDailyFlat}/day<br>");
                    if (upgrade.SecurityDailyPercent != 0) effects.AppendLine($"Security: {(upgrade.SecurityDailyPercent > 0 ? "+" : "")}{upgrade.SecurityDailyPercent}%/day<br>");
                    if (upgrade.MilitiaDailyFlat != 0) effects.AppendLine($"Militia: {(upgrade.MilitiaDailyFlat > 0 ? "+" : "")}{upgrade.MilitiaDailyFlat}/day<br>");
                    if (upgrade.MilitiaDailyPercent != 0) effects.AppendLine($"Militia: {(upgrade.MilitiaDailyPercent > 0 ? "+" : "")}{upgrade.MilitiaDailyPercent}%/day<br>");
                    if (upgrade.FoodDailyFlat != 0) effects.AppendLine($"Food: {(upgrade.FoodDailyFlat > 0 ? "+" : "")}{upgrade.FoodDailyFlat}/day<br>");
                    if (upgrade.FoodDailyPercent != 0) effects.AppendLine($"Food: {(upgrade.FoodDailyPercent > 0 ? "+" : "")}{upgrade.FoodDailyPercent}%/day<br>");
                    if (upgrade.TaxIncomeFlat != 0) effects.AppendLine($"Tax Income: {(upgrade.TaxIncomeFlat > 0 ? "+" : "")}{upgrade.TaxIncomeFlat}{Naming.Gold}/day<br>");
                    if (upgrade.TaxIncomePercent != 0) effects.AppendLine($"Tax Income: {(upgrade.TaxIncomePercent > 0 ? "+" : "")}{upgrade.TaxIncomePercent}%<br>");
                    if (upgrade.GarrisonCapacityBonus != 0) effects.AppendLine($"Garrison Capacity: {(upgrade.GarrisonCapacityBonus > 0 ? "+" : "")}{upgrade.GarrisonCapacityBonus}<br>");
                    if (upgrade.HearthDaily != 0) effects.AppendLine($"Hearth: {(upgrade.HearthDaily > 0 ? "+" : "")}{upgrade.HearthDaily}<br>");
                }

                // Troop Spawning
                if (upgrade.DailyTroopSpawnAmount > 0 || upgrade.TroopTierBonus > 0)
                {
                    effects.AppendLine("<br><strong>Troop Spawning:</strong><br>");
                    if (upgrade.DailyTroopSpawnAmount > 0)
                    {
                        effects.AppendLine($"Daily Spawn: {upgrade.DailyTroopSpawnAmount} troops/day<br>");
                        effects.AppendLine($"Troop Tree: {upgrade.TroopTree}<br>");
                        effects.AppendLine($"Base Tier: {upgrade.TroopTier}<br>");
                    }
                    if (upgrade.TroopTierBonus > 0 && !string.IsNullOrEmpty(upgrade.BuffsTroopTierOf))
                    {
                        effects.AppendLine($"Tier Bonus: +{upgrade.TroopTierBonus} to {upgrade.BuffsTroopTierOf}<br>");
                    }
                }

                return effects.Length > 0 ? effects.ToString() : "No effects configured";
            }

            private string GetUpgradeEffects(KingdomUpgrade upgrade)
            {
                var effects = new System.Text.StringBuilder();

                // Kingdom Effects
                if (upgrade.InfluenceDaily != 0 || upgrade.MaxClansBonus != 0)
                {
                    effects.AppendLine("<strong>Kingdom Effects:</strong><br>");
                    if (upgrade.InfluenceDaily != 0) effects.AppendLine($"Influence: {(upgrade.InfluenceDaily > 0 ? "+" : "")}{upgrade.InfluenceDaily}/day (ruler only)<br>");
                    if (upgrade.MaxClansBonus != 0) effects.AppendLine($"Max Clans: {(upgrade.MaxClansBonus > 0 ? "+" : "")}{upgrade.MaxClansBonus}<br>");
                }

                // Clan Effects
                if (upgrade.RenownDaily != 0 || upgrade.PartySizeBonus != 0 || upgrade.PartySpeedBonus != 0)
                {
                    effects.AppendLine("<br><strong>Clan Effects (All Kingdom Clans):</strong><br>");
                    if (upgrade.RenownDaily != 0) effects.AppendLine($"Renown: {(upgrade.RenownDaily > 0 ? "+" : "")}{upgrade.RenownDaily}/day<br>");
                    if (upgrade.PartySizeBonus != 0) effects.AppendLine($"Party Size: {(upgrade.PartySizeBonus > 0 ? "+" : "")}{upgrade.PartySizeBonus}<br>");
                    if (upgrade.PartySpeedBonus != 0) effects.AppendLine($"Party Speed: {(upgrade.PartySpeedBonus > 0 ? "+" : "")}{upgrade.PartySpeedBonus}<br>");
                }

                // Settlement Effects
                if (upgrade.LoyaltyDailyFlat != 0 || upgrade.LoyaltyDailyPercent != 0 || upgrade.ProsperityDailyFlat != 0 || upgrade.ProsperityDailyPercent != 0 ||
                    upgrade.SecurityDailyFlat != 0 || upgrade.SecurityDailyPercent != 0 || upgrade.MilitiaDailyFlat != 0 || upgrade.MilitiaDailyPercent != 0 ||
                    upgrade.FoodDailyFlat != 0 || upgrade.FoodDailyPercent != 0 || upgrade.TaxIncomeFlat != 0 || upgrade.TaxIncomePercent != 0 ||
                    upgrade.GarrisonCapacityBonus != 0 || upgrade.HearthDaily != 0)
                {
                    effects.AppendLine("<br><strong>Settlement Effects (All Kingdom Settlements):</strong><br>");
                    if (upgrade.LoyaltyDailyFlat != 0) effects.AppendLine($"Loyalty: {(upgrade.LoyaltyDailyFlat > 0 ? "+" : "")}{upgrade.LoyaltyDailyFlat}/day<br>");
                    if (upgrade.LoyaltyDailyPercent != 0) effects.AppendLine($"Loyalty: {(upgrade.LoyaltyDailyPercent > 0 ? "+" : "")}{upgrade.LoyaltyDailyPercent}%/day<br>");
                    if (upgrade.ProsperityDailyFlat != 0) effects.AppendLine($"Prosperity: {(upgrade.ProsperityDailyFlat > 0 ? "+" : "")}{upgrade.ProsperityDailyFlat}/day<br>");
                    if (upgrade.ProsperityDailyPercent != 0) effects.AppendLine($"Prosperity: {(upgrade.ProsperityDailyPercent > 0 ? "+" : "")}{upgrade.ProsperityDailyPercent}%/day<br>");
                    if (upgrade.SecurityDailyFlat != 0) effects.AppendLine($"Security: {(upgrade.SecurityDailyFlat > 0 ? "+" : "")}{upgrade.SecurityDailyFlat}/day<br>");
                    if (upgrade.SecurityDailyPercent != 0) effects.AppendLine($"Security: {(upgrade.SecurityDailyPercent > 0 ? "+" : "")}{upgrade.SecurityDailyPercent}%/day<br>");
                    if (upgrade.MilitiaDailyFlat != 0) effects.AppendLine($"Militia: {(upgrade.MilitiaDailyFlat > 0 ? "+" : "")}{upgrade.MilitiaDailyFlat}/day<br>");
                    if (upgrade.MilitiaDailyPercent != 0) effects.AppendLine($"Militia: {(upgrade.MilitiaDailyPercent > 0 ? "+" : "")}{upgrade.MilitiaDailyPercent}%/day<br>");
                    if (upgrade.FoodDailyFlat != 0) effects.AppendLine($"Food: {(upgrade.FoodDailyFlat > 0 ? "+" : "")}{upgrade.FoodDailyFlat}/day<br>");
                    if (upgrade.FoodDailyPercent != 0) effects.AppendLine($"Food: {(upgrade.FoodDailyPercent > 0 ? "+" : "")}{upgrade.FoodDailyPercent}%/day<br>");
                    if (upgrade.TaxIncomeFlat != 0) effects.AppendLine($"Tax Income: {(upgrade.TaxIncomeFlat > 0 ? "+" : "")}{upgrade.TaxIncomeFlat}{Naming.Gold}/day<br>");
                    if (upgrade.TaxIncomePercent != 0) effects.AppendLine($"Tax Income: {(upgrade.TaxIncomePercent > 0 ? "+" : "")}{upgrade.TaxIncomePercent}%<br>");
                    if (upgrade.GarrisonCapacityBonus != 0) effects.AppendLine($"Garrison Capacity: {(upgrade.GarrisonCapacityBonus > 0 ? "+" : "")}{upgrade.GarrisonCapacityBonus}<br>");
                    if (upgrade.HearthDaily != 0) effects.AppendLine($"Hearth: {(upgrade.HearthDaily > 0 ? "+" : "")}{upgrade.HearthDaily}<br>");
                }

                return effects.Length > 0 ? effects.ToString() : "No effects configured";
            }

            private bool ShouldShowFullDescription(string upgradeId)
            {
                var match = System.Text.RegularExpressions.Regex.Match(upgradeId, @"^(.+?)(\d+)$");
                if (match.Success)
                {
                    int tier = int.Parse(match.Groups[2].Value);
                    return tier == 1;
                }
                return true;
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

            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (!settings.Enabled)
            {
                onFailure("{=BLT_UpgradeDisabled}The upgrade system is disabled".Translate());
                return;
            }

            if (Mission.Current != null)
            {
                onFailure("{=BLT_NoMission}Cannot use this command during a mission".Translate());
                return;
            }

            if (context.Args.IsEmpty())
            {
                onFailure("Usage:  <fief|clan|kingdom> <name> <upgrade>  OR  info <fief|clan|kingdom> <name>  OR  list [fief|clan|kingdom]  OR  remove <fief|clan|kingdom> <name> <upgrade>");
                return;
            }

            var globalConfig = GlobalCommonConfig.Get();
            if (globalConfig == null)
            {
                onFailure("Configuration not available");
                return;
            }

            var args = context.Args.Split(' ');
            var command = args[0].ToLowerInvariant();


            // Handle list command
            if (command == "list")
            {
                if (!settings.AllowListCommand)
                {
                    onFailure("The list command is disabled");
                    return;
                }

                string type = args.Length > 1 ? args[1].ToLowerInvariant() : "all";
                HandleListCommand(type, globalConfig, onSuccess, onFailure);
                return;
            }

            // Handle info command
            if (command == "info")
            {
                if (args.Length < 2)
                {
                    onFailure("Usage: info <fief|clan|kingdom> <name>");
                    return;
                }

                string type = args[1].ToLowerInvariant();
                string name = string.Join(" ", args.Skip(2));
                HandleInfoCommand(type, name, adoptedHero, globalConfig, onSuccess, onFailure);
                return;
            }

            // Handle remove command
            if (command == "remove")
            {
                if (args.Length < 3)
                {
                    onFailure("Usage: remove <fief|clan|kingdom> <settlement_name/upgrade_id> [upgrade_id]");
                    return;
                }

                string type = args[1].ToLowerInvariant();

                // Parse based on type
                if (type == "fief")
                {
                    if (args.Length < 4)
                    {
                        onFailure("Usage: remove fief <settlement_name> <upgrade_id>");
                        return;
                    }
                    string tName = string.Join(" ", args.Skip(2).Take(args.Length - 3));
                    string uId = args.Last();
                    HandleRemoveCommand(type, tName, uId, adoptedHero, settings, globalConfig, onSuccess, onFailure);
                }
                else // clan or kingdom
                {
                    string uId = args[2];
                    HandleRemoveCommand(type, null, uId, adoptedHero, settings, globalConfig, onSuccess, onFailure);
                }
                return;
            }

            // Handle purchase command
            // Validate arg count based on command type
            if (command == "fief" && args.Length < 3)
            {
                onFailure("Usage: fief <settlement_name> <upgrade_id>");
                return;
            }
            else if ((command == "clan" || command == "kingdom") && args.Length < 2)
            {
                onFailure($"Usage: {command} <upgrade_id>");
                return;
            }

            // Parse args based on command type
            string upgradeId;
            string targetName = null;

            if (command == "fief")
            {
                upgradeId = args.Last();
                targetName = string.Join(" ", args.Skip(1).Take(args.Length - 2));
            }
            else
            {
                // clan or kingdom - just get the upgrade ID
                upgradeId = args.Last();
            }

            HandlePurchaseCommand(command, targetName, upgradeId, adoptedHero, settings, globalConfig, onSuccess, onFailure);
        }

        private void HandleListCommand(string type, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            if (globalConfig == null)
            {
                onFailure("Global Configs not available");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Available Upgrades ===");

            if (type == "all" || type == "fief")
            {
                sb.AppendLine("\n[Fief Upgrades]");
                if (globalConfig.FiefUpgrades != null && globalConfig.FiefUpgrades.Count > 0)
                {
                    foreach (var upgrade in globalConfig.FiefUpgrades)
                    {
                        sb.AppendLine($"  {upgrade.ID}: {upgrade.Name} - {upgrade.GetCostString()}");
                        sb.AppendLine($"    {upgrade.Description}");
                    }
                }
                else
                {
                    sb.AppendLine("  No fief upgrades configured");
                }
            }

            if (type == "all" || type == "clan")
            {
                sb.AppendLine("\n[Clan Upgrades]");
                if (globalConfig.ClanUpgrades != null && globalConfig.ClanUpgrades.Count > 0)
                {
                    foreach (var upgrade in globalConfig.ClanUpgrades)
                    {
                        sb.AppendLine($"  {upgrade.ID}: {upgrade.Name} - {upgrade.GetCostString()}");
                        sb.AppendLine($"    {upgrade.Description}");
                    }
                }
                else
                {
                    sb.AppendLine("  No clan upgrades configured");
                }
            }

            if (type == "all" || type == "kingdom")
            {
                sb.AppendLine("\n[Kingdom Upgrades]");
                if (globalConfig.KingdomUpgrades != null && globalConfig.KingdomUpgrades.Count > 0)
                {
                    foreach (var upgrade in globalConfig.KingdomUpgrades)
                    {
                        sb.AppendLine($"  {upgrade.ID}: {upgrade.Name} - {upgrade.GetCostString()}");
                        sb.AppendLine($"    {upgrade.Description}");
                    }
                }
                else
                {
                    sb.AppendLine("  No kingdom upgrades configured");
                }
            }

            if (type != "all" && type != "fief" && type != "clan" && type != "kingdom")
            {
                onFailure($"Invalid type '{type}'. Use 'all', 'fief', 'clan', or 'kingdom'");
                return;
            }

            onSuccess(sb.ToString());
        }

        private void HandleInfoCommand(string type, string name, Hero hero, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            switch (type)
            {
                case "fief":
                    ShowFiefInfo(name, hero, globalConfig, onSuccess, onFailure);
                    break;
                case "clan":
                    ShowClanInfo(hero, globalConfig, onSuccess, onFailure);
                    break;
                case "kingdom":
                    ShowKingdomInfo(hero, globalConfig, onSuccess, onFailure);
                    break;
                default:
                    onFailure("Invalid type. Use 'fief', 'clan', or 'kingdom'");
                    break;
            }
        }

        private void ShowFiefInfo(string name, Hero hero, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrEmpty(name))
            {
                onFailure("Usage: info <fief> <name>");
                return;
            }
            var settlement = FindSettlement(name);
            if (settlement == null)
            {
                onFailure($"Settlement '{name}' not found");
                return;
            }
            if (settlement.Town == null || settlement.IsVillage)
            {
                onFailure("Only towns and castles can have upgrades");
                return;
            }
            var upgrades = UpgradeBehavior.Current?.GetFiefUpgrades(settlement) ?? new List<string>();
            var sb = new StringBuilder();
            sb.AppendLine($"=== {settlement.Name} Upgrades ===");
            if (upgrades.Count == 0)
            {
                sb.AppendLine("No upgrades purchased yet");
            }
            else
            {
                sb.AppendLine("Active Upgrades:");

                // Get all upgrade objects
                var allUpgrades = upgrades
                .Select(upgradeId => globalConfig.FiefUpgrades.FirstOrDefault(u => u.ID == upgradeId))
                .Where(upgrade => upgrade != null)
                .ToList();

                // Group by base ID (remove trailing digits) and keep only the highest tier
                var filteredUpgrades = allUpgrades
                    .GroupBy(upgrade =>
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(upgrade.ID, @"^(.+?)(\d+)$");
                        return match.Success ? match.Groups[1].Value : upgrade.ID;
                    })
                    .Select(group => group.OrderByDescending(u =>
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(u.ID, @"(\d+)$");
                        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
                    }).First())
                    .OrderBy(upgrade => upgrade.Name)
                    .ToList();

                foreach (var upgrade in filteredUpgrades)
                {
                    sb.AppendLine($"  • {upgrade.Name}");
                }
            }
            onSuccess(sb.ToString());
        }

        private void ShowClanInfo(Hero hero, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            var clan = hero.Clan;
            if (clan == null)
            {
                onFailure($"Clan '{clan.Name}' not found");
                return;
            }

            var upgrades = UpgradeBehavior.Current?.GetClanUpgrades(clan) ?? new List<string>();
            //upgrades = clan.IsUnderMercenaryService ? upgrades.RemoveAll(up => up == ClanUpgrade) : upgrades;
            var sb = new StringBuilder();
            sb.AppendLine($"=== {clan.Name} Upgrades ===");

            if (upgrades.Count == 0)
            {
                sb.AppendLine("No upgrades purchased yet");
            }
            else
            {
                sb.AppendLine("Active Upgrades:");

                // Get all upgrade objects
                var allUpgrades = upgrades
                .Select(upgradeId => globalConfig.ClanUpgrades.FirstOrDefault(u => u.ID == upgradeId))
                .Where(upgrade => upgrade != null && ((upgrade.MercOnly && clan.IsUnderMercenaryService)|| upgrade.LordOnly && !clan.IsUnderMercenaryService))
                .ToList();

                // Group by base ID (remove trailing digits) and keep only the highest tier
                var filteredUpgrades = allUpgrades
                    .GroupBy(upgrade =>
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(upgrade.ID, @"^(.+?)(\d+)$");
                        return match.Success ? match.Groups[1].Value : upgrade.ID;
                    })
                    .Select(group => group.OrderByDescending(u =>
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(u.ID, @"(\d+)$");
                        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
                    }).First())
                    .OrderBy(upgrade => upgrade.Name)
                    .ToList();

                foreach (var upgrade in filteredUpgrades)
                {
                    sb.AppendLine($"  • {upgrade.Name}");
                }
            }

            onSuccess(sb.ToString());
        }

        private void ShowKingdomInfo(Hero hero, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            var kingdom = hero.Clan.Kingdom;
            if (hero.Clan == null)
            {
                onFailure($"You are not in a clan!");
                return;
            }

            if (kingdom == null)
            {
                onFailure($"Kingdom '{kingdom.Name}' not found");
                return;
            }

            var upgrades = UpgradeBehavior.Current?.GetKingdomUpgrades(kingdom) ?? new List<string>();
            var sb = new StringBuilder();
            sb.AppendLine($"=== {kingdom.Name} Upgrades ===");

            if (upgrades.Count == 0)
            {
                sb.AppendLine("No upgrades purchased yet");
            }
            else
            {
                sb.AppendLine("Active Upgrades:");

                // Get all upgrade objects
                var allUpgrades = upgrades
                .Select(upgradeId => globalConfig.KingdomUpgrades.FirstOrDefault(u => u.ID == upgradeId))
                .Where(upgrade => upgrade != null)
                .ToList();

                // Group by base ID (remove trailing digits) and keep only the highest tier
                var filteredUpgrades = allUpgrades
                    .GroupBy(upgrade =>
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(upgrade.ID, @"^(.+?)(\d+)$");
                        return match.Success ? match.Groups[1].Value : upgrade.ID;
                    })
                    .Select(group => group.OrderByDescending(u =>
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(u.ID, @"(\d+)$");
                        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
                    }).First())
                    .OrderBy(upgrade => upgrade.Name)
                    .ToList();

                foreach (var upgrade in filteredUpgrades)
                {
                    sb.AppendLine($"  • {upgrade.Name}");
                }
            }

            onSuccess(sb.ToString());
        }

        private void HandlePurchaseCommand(string type, string name, string upgradeId, Hero hero, Settings settings, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            switch (type)
            {
                case "fief":
                    PurchaseFiefUpgrade(name, upgradeId, hero, settings, globalConfig, onSuccess, onFailure);
                    break;
                case "clan":
                    PurchaseClanUpgrade(upgradeId, hero, settings, globalConfig, onSuccess, onFailure);
                    break;
                case "kingdom":
                    PurchaseKingdomUpgrade(upgradeId, hero, globalConfig, onSuccess, onFailure);
                    break;
            }
        }

        private void PurchaseFiefUpgrade(string name, string upgradeId, Hero hero, Settings settings, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            var settlement = FindSettlement(name);
            if (settlement == null)
            {
                onFailure($"Settlement '{name}' not found");
                return;
            }

            if (settlement.Town == null)
            {
                onFailure("Only towns and castles can have upgrades");
                return;
            }

            // Check permissions
            bool isOwner = settlement.OwnerClan == hero.Clan;
            foreach (Clan vassal in VassalBehavior.Current.GetVassalClans(hero.Clan))
            {
                if (vassal == settlement.OwnerClan)
                {
                    isOwner = true;
                }
            }

            bool isKingdomLeader = settings.AllowKingdomLeadersForFiefs &&
                                   hero.Clan?.Kingdom != null &&
                                   hero.Clan.Kingdom.Leader == hero &&
                                   settlement.OwnerClan?.Kingdom == hero.Clan.Kingdom;

            if (!isOwner && !isKingdomLeader)
            {
                onFailure($"You don't have permission to upgrade {settlement.Name}");
                return;
            }

            if (!hero.IsClanLeader && !isKingdomLeader)
            {
                onFailure("Only clan leaders can purchase fief upgrades");
                return;
            }

            // Find upgrade
            var upgrade = globalConfig.FiefUpgrades.FirstOrDefault(u => u.ID == upgradeId);
            if (upgrade == null)
            {
                onFailure($"Upgrade '{upgradeId}' not found");
                return;
            }

            if (upgrade.CoastalOnly && !settlement.HasPort)
            {
                onFailure($"This is a Coastal Only upgrade, try again on a coastal settlement");
                return;
            }

            // Check if already purchased
            if (UpgradeBehavior.Current?.HasFiefUpgrade(settlement, upgradeId) == true)
            {
                onFailure($"{settlement.Name} already has this upgrade");
                return;
            }

            // Check prerequisites - NOW SUPPORTS MULTIPLE REQUIRED IDs
            var requiredIds = upgrade.RequiredUpgradeIDs;
            if (requiredIds.Count > 0)
            {
                var ownedUpgrades = new HashSet<string>(
                    UpgradeBehavior.Current?.GetFiefUpgrades(settlement) ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase
                );

                if (!upgrade.AreRequiredUpgradesMet(ownedUpgrades))
                {
                    var missingUpgrades = requiredIds
                        .Where(id => !ownedUpgrades.Contains(id, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    onFailure($"Requires upgrade(s) first: {string.Join(", ", missingUpgrades)}");
                    return;
                }
            }

            // Check gold
            int heroGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero);
            if (heroGold < upgrade.GoldCost)
            {
                onFailure(Naming.NotEnoughGold(upgrade.GoldCost, heroGold));
                return;
            }

            // Purchase
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -upgrade.GoldCost, true);
            UpgradeBehavior.Current?.AddFiefUpgrade(settlement, upgradeId);

            onSuccess($"Purchased '{upgrade.Name}' for {settlement.Name}!");
            Log.ShowInformation($"{hero.Name} purchased {upgrade.Name} for {settlement.Name}", hero.CharacterObject, Log.Sound.Notification1);
        }

        private void PurchaseClanUpgrade(string upgradeId, Hero hero, Settings settings, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            var clan = hero?.Clan;
            if (clan == null)
            {
                onFailure($"Clan '{clan.Name}' not found");
                return;
            }

            if (!settings.AllowAnyClanMemberForClanUpgrades && !hero.IsClanLeader)
            {
                onFailure("Only clan leaders can purchase clan upgrades");
                return;
            }

            // Find upgrade
            var upgrade = globalConfig.ClanUpgrades.FirstOrDefault(u => u.ID == upgradeId);
            if (upgrade == null)
            {
                onFailure($"Upgrade '{upgradeId}' not found");
                return;
            }

            // Check if already purchased
            if (UpgradeBehavior.Current?.HasClanUpgrade(clan, upgradeId) == true)
            {
                onFailure($"{clan.Name} already has this upgrade");
                return;
            }

            // Check prerequisites - NOW SUPPORTS MULTIPLE REQUIRED IDs
            var requiredIds = upgrade.RequiredUpgradeIDs;
            if (requiredIds.Count > 0)
            {
                var ownedUpgrades = new HashSet<string>(
                    UpgradeBehavior.Current?.GetClanUpgrades(clan) ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase
                );

                if (!upgrade.AreRequiredUpgradesMet(ownedUpgrades))
                {
                    var missingUpgrades = requiredIds
                        .Where(id => !ownedUpgrades.Contains(id, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    onFailure($"Requires upgrade(s) first: {string.Join(", ", missingUpgrades)}");
                    return;
                }
            }

            // Check gold
            int heroGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero);
            if (heroGold < upgrade.GoldCost)
            {
                onFailure(Naming.NotEnoughGold(upgrade.GoldCost, heroGold));
                return;
            }

            // Purchase
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -upgrade.GoldCost, true);
            UpgradeBehavior.Current?.AddClanUpgrade(clan, upgradeId);

            onSuccess($"Purchased '{upgrade.Name}' for {clan.Name}!");
            Log.ShowInformation($"{hero.Name} purchased {upgrade.Name} for {clan.Name}", hero.CharacterObject, Log.Sound.Notification1);
        }

        private void PurchaseKingdomUpgrade(string upgradeId, Hero hero, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            var kingdom = hero?.Clan?.Kingdom;
            if (hero.Clan == null)
            {
                onFailure($"You're not in a clan!");
                return;
            }

            if (kingdom == null)
            {
                onFailure($"Kingdom '{kingdom.Name}' not found");
                return;
            }

            // Check permissions - only kingdom ruler
            if (kingdom.Leader != hero)
            {
                onFailure("Only the kingdom ruler can purchase kingdom upgrades");
                return;
            }

            // Find upgrade
            var upgrade = globalConfig.KingdomUpgrades.FirstOrDefault(u => u.ID == upgradeId);
            if (upgrade == null)
            {
                onFailure($"Upgrade '{upgradeId}' not found");
                return;
            }

            // Check if already purchased
            if (UpgradeBehavior.Current?.HasKingdomUpgrade(kingdom, upgradeId) == true)
            {
                onFailure($"{kingdom.Name} already has this upgrade");
                return;
            }

            // Check prerequisites - NOW SUPPORTS MULTIPLE REQUIRED IDs
            var requiredIds = upgrade.RequiredUpgradeIDs;
            if (requiredIds.Count > 0)
            {
                var ownedUpgrades = new HashSet<string>(
                    UpgradeBehavior.Current?.GetKingdomUpgrades(kingdom) ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase
                );

                if (!upgrade.AreRequiredUpgradesMet(ownedUpgrades))
                {
                    var missingUpgrades = requiredIds
                        .Where(id => !ownedUpgrades.Contains(id, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    onFailure($"Requires upgrade(s) first: {string.Join(", ", missingUpgrades)}");
                    return;
                }
            }

            // Check gold
            int heroGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero);
            if (heroGold < upgrade.GoldCost)
            {
                onFailure(Naming.NotEnoughGold(upgrade.GoldCost, heroGold));
                return;
            }

            // Check influence
            if (upgrade.InfluenceCost > 0)
            {
                if (hero.Clan.Influence < upgrade.InfluenceCost)
                {
                    onFailure($"Not enough influence (need {upgrade.InfluenceCost}, have {(int)hero.Clan.Influence})");
                    return;
                }
            }

            // Purchase
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -upgrade.GoldCost, true);
            if (upgrade.InfluenceCost > 0)
            {
                hero.Clan.Influence -= upgrade.InfluenceCost;
            }
            UpgradeBehavior.Current?.AddKingdomUpgrade(kingdom, upgradeId);

            onSuccess($"Purchased '{upgrade.Name}' for {kingdom.Name}!");
            Log.ShowInformation($"{hero.Name} purchased {upgrade.Name} for {kingdom.Name}", hero.CharacterObject, Log.Sound.Horns2);
        }

        private Settlement FindSettlement(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var settlement = Settlement.All.FirstOrDefault(s => s?.Name?.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) == true);
            if (settlement.IsVillage)
            {
                name = name.Add(" Castle", false);
                settlement = Settlement.All.FirstOrDefault(s => s?.Name?.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) == true);
            }
            return settlement;
        }

        private void HandleRemoveCommand(string type, string name, string upgradeId, Hero hero, Settings settings, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            switch (type)
            {
                case "fief":
                    RemoveFiefUpgrade(name, upgradeId, hero, settings, globalConfig, onSuccess, onFailure);
                    break;
                case "clan":
                    RemoveClanUpgrade(upgradeId, hero, settings, globalConfig, onSuccess, onFailure);
                    break;
                case "kingdom":
                    RemoveKingdomUpgrade(upgradeId, hero, globalConfig, onSuccess, onFailure);
                    break;
                default:
                    onFailure("Invalid type. Use 'fief', 'clan', or 'kingdom'");
                    break;
            }
        }

        private void RemoveFiefUpgrade(string name, string upgradeId, Hero hero, Settings settings, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            var settlement = FindSettlement(name);
            if (settlement == null)
            {
                onFailure($"Settlement '{name}' not found");
                return;
            }

            if (settlement.Town == null)
            {
                onFailure("Only towns and castles can have upgrades");
                return;
            }

            // Check permissions
            bool isOwner = settlement.OwnerClan == hero.Clan;
            bool isKingdomLeader = settings.AllowKingdomLeadersForFiefs &&
                                   hero.Clan?.Kingdom != null &&
                                   hero.Clan.Kingdom.Leader == hero &&
                                   settlement.OwnerClan?.Kingdom == hero.Clan.Kingdom;

            if (!isOwner && !isKingdomLeader)
            {
                onFailure($"You don't have permission to modify {settlement.Name}");
                return;
            }

            if (!hero.IsClanLeader && !isKingdomLeader)
            {
                onFailure("Only clan leaders can remove fief upgrades");
                return;
            }

            // Find upgrade
            var upgrade = globalConfig.FiefUpgrades.FirstOrDefault(u => u.ID == upgradeId);
            if (upgrade == null)
            {
                onFailure($"Upgrade '{upgradeId}' not found");
                return;
            }

            // Check if upgrade is removable
            if (!upgrade.CanBeRemoved)
            {
                onFailure($"'{upgrade.Name}' cannot be removed");
                return;
            }

            // Check if upgrade exists
            if (UpgradeBehavior.Current?.HasFiefUpgrade(settlement, upgradeId) != true)
            {
                onFailure($"{settlement.Name} doesn't have this upgrade");
                return;
            }

            // Remove upgrade
            UpgradeBehavior.Current?.RemoveFiefUpgrade(settlement, upgradeId);

            onSuccess($"Removed '{upgrade.Name}' from {settlement.Name}!");
            Log.ShowInformation($"{hero.Name} removed {upgrade.Name} from {settlement.Name}", hero.CharacterObject, Log.Sound.Notification1);
        }

        private void RemoveClanUpgrade(string upgradeId, Hero hero, Settings settings, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            var clan = hero?.Clan;
            if (clan == null)
            {
                onFailure("You are not in a clan!");
                return;
            }

            if (!settings.AllowAnyClanMemberForClanUpgrades && !hero.IsClanLeader)
            {
                onFailure("Only clan leaders can remove clan upgrades");
                return;
            }

            // Find upgrade
            var upgrade = globalConfig.ClanUpgrades.FirstOrDefault(u => u.ID == upgradeId);
            if (upgrade == null)
            {
                onFailure($"Upgrade '{upgradeId}' not found");
                return;
            }

            // Check if upgrade is removable
            if (!upgrade.CanBeRemoved)
            {
                onFailure($"'{upgrade.Name}' cannot be removed");
                return;
            }

            // Check if upgrade exists
            if (UpgradeBehavior.Current?.HasClanUpgrade(clan, upgradeId) != true)
            {
                onFailure($"{clan.Name} doesn't have this upgrade");
                return;
            }

            // Remove upgrade
            UpgradeBehavior.Current?.RemoveClanUpgrade(clan, upgradeId);

            onSuccess($"Removed '{upgrade.Name}' from {clan.Name}!");
            Log.ShowInformation($"{hero.Name} removed {upgrade.Name} from {clan.Name}", hero.CharacterObject, Log.Sound.Notification1);
        }

        private void RemoveKingdomUpgrade(string upgradeId, Hero hero, GlobalCommonConfig globalConfig, Action<string> onSuccess, Action<string> onFailure)
        {
            var kingdom = hero?.Clan?.Kingdom;
            if (hero.Clan == null)
            {
                onFailure("You're not in a clan!");
                return;
            }

            if (kingdom == null)
            {
                onFailure("You're not in a kingdom!");
                return;
            }

            // Check permissions - only kingdom ruler
            if (kingdom.Leader != hero)
            {
                onFailure("Only the kingdom ruler can remove kingdom upgrades");
                return;
            }

            // Find upgrade
            var upgrade = globalConfig.KingdomUpgrades.FirstOrDefault(u => u.ID == upgradeId);
            if (upgrade == null)
            {
                onFailure($"Upgrade '{upgradeId}' not found");
                return;
            }

            // Check if upgrade is removable
            if (!upgrade.CanBeRemoved)
            {
                onFailure($"'{upgrade.Name}' cannot be removed");
                return;
            }

            // Check if upgrade exists
            if (UpgradeBehavior.Current?.HasKingdomUpgrade(kingdom, upgradeId) != true)
            {
                onFailure($"{kingdom.Name} doesn't have this upgrade");
                return;
            }

            // Remove upgrade
            UpgradeBehavior.Current?.RemoveKingdomUpgrade(kingdom, upgradeId);

            onSuccess($"Removed '{upgrade.Name}' from {kingdom.Name}!");
            Log.ShowInformation($"{hero.Name} removed {upgrade.Name} from {kingdom.Name}", hero.CharacterObject, Log.Sound.Horns2);
        }
    }
}