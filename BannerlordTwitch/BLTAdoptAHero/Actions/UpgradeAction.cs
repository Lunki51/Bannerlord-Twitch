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

                GenerateUpgradeCounts(generator, config);
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
                if (config.FiefUpgrades != null && config.FiefUpgrades.Count > 0)
                {
                    var standard = config.FiefUpgrades.Where(u => !u.CoastalOnly).ToList();
                    var coastal = config.FiefUpgrades.Where(u => u.CoastalOnly).ToList();
                    generator.H2("Fief Upgrades");
                    if (standard.Count > 0) { generator.H3("Standard Fief Upgrades"); GenerateFiefUpgradeTable(generator, standard); }
                    if (coastal.Count > 0) { generator.H3("Coastal Only Fief Upgrades"); GenerateFiefUpgradeTable(generator, coastal); }
                }

                if (config.ClanUpgrades != null && config.ClanUpgrades.Count > 0)
                {
                    var std = config.ClanUpgrades.Where(u => (u.MercOnly && u.LordOnly && !u.ApplyToVassals) || (!u.MercOnly && !u.LordOnly && !u.ApplyToVassals)).ToList();
                    var lord = config.ClanUpgrades.Where(u => u.LordOnly && !u.MercOnly && !u.ApplyToVassals).ToList();
                    var merc = config.ClanUpgrades.Where(u => u.MercOnly && !u.LordOnly && !u.ApplyToVassals).ToList();
                    var vassal = config.ClanUpgrades.Where(u => u.ApplyToVassals).ToList();
                    generator.H2("Clan Upgrades");
                    if (std.Count > 0) { generator.H3("Standard Clan Upgrades"); GenerateClanUpgradeTable(generator, std); }
                    if (lord.Count > 0) { generator.H3("Lord Only Clan Upgrades"); GenerateClanUpgradeTable(generator, lord); }
                    if (merc.Count > 0) { generator.H3("Mercenary Only Clan Upgrades"); GenerateClanUpgradeTable(generator, merc); }
                    if (vassal.Count > 0) { generator.H3("Vassal Only Clan Upgrades"); GenerateClanUpgradeTable(generator, vassal); }
                }

                if (config.KingdomUpgrades != null && config.KingdomUpgrades.Count > 0)
                {
                    generator.H2("Kingdom Upgrades");
                    GenerateKingdomUpgradeTable(generator, config.KingdomUpgrades.ToList());
                }
            }

            private void GenerateFiefUpgradeTable(IDocumentationGenerator generator, List<FiefUpgrade> upgrades)
            {
                generator.Table("upgrade-table", () =>
                {
                    generator.TR(() => { generator.TH("ID"); generator.TH("Name"); generator.TH("Cost"); generator.TH("Tier"); generator.TH("Required"); generator.TH("Description"); });
                    foreach (var u in upgrades)
                    {
                        generator.TR(() =>
                        {
                            generator.TD(u.ID);
                            generator.TD(u.Name);
                            generator.TD($"{u.GoldCost}{Naming.Gold}");
                            generator.TD(u.TierLevel > 0 ? u.TierLevel.ToString() : "-");
                            generator.TD(!string.IsNullOrEmpty(u.RequiredUpgradeID) ? u.RequiredUpgradeID : "-");
                            generator.TD(() =>
                            {
                                generator.P(u.Description);
                                if (ShouldShowFullDescription(u.ID))
                                    generator.Details(() => { generator.Summary("View Details"); var fx = GetUpgradeEffects(u); if (!string.IsNullOrEmpty(fx)) generator.P(fx); });
                            });
                        });
                    }
                });
            }

            private void GenerateClanUpgradeTable(IDocumentationGenerator generator, List<ClanUpgrade> upgrades)
            {
                generator.Table("upgrade-table", () =>
                {
                    generator.TR(() => { generator.TH("ID"); generator.TH("Name"); generator.TH("Cost"); generator.TH("Tier"); generator.TH("Required"); generator.TH("Description"); });
                    foreach (var u in upgrades)
                    {
                        generator.TR(() =>
                        {
                            generator.TD(u.ID);
                            generator.TD(u.Name);
                            generator.TD($"{u.GoldCost}{Naming.Gold}");
                            generator.TD(u.TierLevel > 0 ? u.TierLevel.ToString() : "-");
                            generator.TD(!string.IsNullOrEmpty(u.RequiredUpgradeID) ? u.RequiredUpgradeID : "-");
                            generator.TD(() =>
                            {
                                generator.P(u.Description);
                                if (ShouldShowFullDescription(u.ID))
                                    generator.Details(() => { generator.Summary("View Details"); var fx = GetUpgradeEffects(u); if (!string.IsNullOrEmpty(fx)) generator.P(fx); });
                            });
                        });
                    }
                });
            }

            private void GenerateKingdomUpgradeTable(IDocumentationGenerator generator, List<KingdomUpgrade> upgrades)
            {
                generator.Table("upgrade-table", () =>
                {
                    generator.TR(() => { generator.TH("ID"); generator.TH("Name"); generator.TH("Cost"); generator.TH("Tier"); generator.TH("Required"); generator.TH("Description"); });
                    foreach (var u in upgrades)
                    {
                        generator.TR(() =>
                        {
                            generator.TD(u.ID);
                            generator.TD(u.Name);
                            generator.TD(u.GetCostString());
                            generator.TD(u.TierLevel > 0 ? u.TierLevel.ToString() : "-");
                            generator.TD(!string.IsNullOrEmpty(u.RequiredUpgradeID) ? u.RequiredUpgradeID : "-");
                            generator.TD(() =>
                            {
                                generator.P(u.Description);
                                if (ShouldShowFullDescription(u.ID))
                                    generator.Details(() => { generator.Summary("View Details"); var fx = GetUpgradeEffects(u); if (!string.IsNullOrEmpty(fx)) generator.P(fx); });
                            });
                        });
                    }
                });
            }

            private string GetUpgradeEffects(FiefUpgrade u)
            {
                var sb = new StringBuilder();
                sb.AppendLine("<strong>Effects:</strong><br>");
                if (u.CanBeRemoved) sb.AppendLine($"Can Be Removed<br>");
                if (u.LoyaltyDailyFlat != 0) sb.AppendLine($"Loyalty: {Signed(u.LoyaltyDailyFlat)}/day<br>");
                if (u.LoyaltyDailyPercent != 0) sb.AppendLine($"Loyalty: {Signed(u.LoyaltyDailyPercent)}%/day<br>");
                if (u.ProsperityDailyFlat != 0) sb.AppendLine($"Prosperity: {Signed(u.ProsperityDailyFlat)}/day<br>");
                if (u.ProsperityDailyPercent != 0) sb.AppendLine($"Prosperity: {Signed(u.ProsperityDailyPercent)}%/day<br>");
                if (u.SecurityDailyFlat != 0) sb.AppendLine($"Security: {Signed(u.SecurityDailyFlat)}/day<br>");
                if (u.SecurityDailyPercent != 0) sb.AppendLine($"Security: {Signed(u.SecurityDailyPercent)}%/day<br>");
                if (u.MilitiaDailyFlat != 0) sb.AppendLine($"Militia: {Signed(u.MilitiaDailyFlat)}/day<br>");
                if (u.MilitiaDailyPercent != 0) sb.AppendLine($"Militia: {Signed(u.MilitiaDailyPercent)}%/day<br>");
                if (u.FoodDailyFlat != 0) sb.AppendLine($"Food: {Signed(u.FoodDailyFlat)}/day<br>");
                if (u.FoodDailyPercent != 0) sb.AppendLine($"Food: {Signed(u.FoodDailyPercent)}%/day<br>");
                if (u.TaxIncomeFlat != 0) sb.AppendLine($"Tax Income: {Signed(u.TaxIncomeFlat)}{Naming.Gold}/day<br>");
                if (u.TaxIncomePercent != 0) sb.AppendLine($"Tax Income: {Signed(u.TaxIncomePercent)}%<br>");
                if (u.GarrisonCapacityBonus != 0) sb.AppendLine($"Garrison Capacity: {Signed(u.GarrisonCapacityBonus)}<br>");
                if (u.HearthDaily != 0) sb.AppendLine($"Hearth: {Signed(u.HearthDaily)}<br>");
                return sb.Length > 0 ? sb.ToString() : "No effects configured";
            }

            private string GetUpgradeEffects(ClanUpgrade u)
            {
                var sb = new StringBuilder();
                if (u.RenownDaily != 0 || u.PartySizeBonus != 0 || u.PartySpeedBonus != 0 || u.PartyAmountBonus != 0 || u.MaxVassalsBonus != 0 || u.MercIncomeFlat != 0 || u.MercIncomePercent != 0)
                {
                    sb.AppendLine("<strong>Clan Effects:</strong><br>");
                    if (u.RenownDaily != 0) sb.AppendLine($"Renown: {Signed(u.RenownDaily)}/day<br>");
                    if (u.InfluenceDaily != 0) sb.AppendLine($"Influence: {Signed(u.InfluenceDaily)}/day<br>");
                    if (u.PartySizeBonus != 0) sb.AppendLine($"Party Size: {Signed(u.PartySizeBonus)}<br>");
                    if (u.PartySpeedBonus != 0) sb.AppendLine($"Party Speed: {Signed(u.PartySpeedBonus)}<br>");
                    if (u.PartyAmountBonus != 0) sb.AppendLine($"Party Limit: {Signed(u.PartyAmountBonus)}<br>");
                    if (u.MaxVassalsBonus != 0) sb.AppendLine($"Vassal Limit: {Signed(u.MaxVassalsBonus)}<br>");
                    if (u.MercIncomeFlat != 0) sb.AppendLine($"Merc Income (Flat): {Signed(u.MercIncomeFlat)}/day<br>");
                    if (u.MercIncomePercent != 0) sb.AppendLine($"Merc Income (%): {Signed(u.MercIncomePercent)}%/day<br>");
                }
                if (u.LoyaltyDailyFlat != 0 || u.LoyaltyDailyPercent != 0 || u.ProsperityDailyFlat != 0 || u.ProsperityDailyPercent != 0 ||
                    u.SecurityDailyFlat != 0 || u.SecurityDailyPercent != 0 || u.MilitiaDailyFlat != 0 || u.MilitiaDailyPercent != 0 ||
                    u.FoodDailyFlat != 0 || u.FoodDailyPercent != 0 || u.TaxIncomeFlat != 0 || u.TaxIncomePercent != 0 ||
                    u.GarrisonCapacityBonus != 0 || u.HearthDaily != 0)
                {
                    sb.AppendLine("<br><strong>Settlement Effects:</strong><br>");
                    if (u.LoyaltyDailyFlat != 0) sb.AppendLine($"Loyalty: {Signed(u.LoyaltyDailyFlat)}/day<br>");
                    if (u.LoyaltyDailyPercent != 0) sb.AppendLine($"Loyalty: {Signed(u.LoyaltyDailyPercent)}%/day<br>");
                    if (u.ProsperityDailyFlat != 0) sb.AppendLine($"Prosperity: {Signed(u.ProsperityDailyFlat)}/day<br>");
                    if (u.ProsperityDailyPercent != 0) sb.AppendLine($"Prosperity: {Signed(u.ProsperityDailyPercent)}%/day<br>");
                    if (u.SecurityDailyFlat != 0) sb.AppendLine($"Security: {Signed(u.SecurityDailyFlat)}/day<br>");
                    if (u.SecurityDailyPercent != 0) sb.AppendLine($"Security: {Signed(u.SecurityDailyPercent)}%/day<br>");
                    if (u.MilitiaDailyFlat != 0) sb.AppendLine($"Militia: {Signed(u.MilitiaDailyFlat)}/day<br>");
                    if (u.MilitiaDailyPercent != 0) sb.AppendLine($"Militia: {Signed(u.MilitiaDailyPercent)}%/day<br>");
                    if (u.FoodDailyFlat != 0) sb.AppendLine($"Food: {Signed(u.FoodDailyFlat)}/day<br>");
                    if (u.FoodDailyPercent != 0) sb.AppendLine($"Food: {Signed(u.FoodDailyPercent)}%/day<br>");
                    if (u.TaxIncomeFlat != 0) sb.AppendLine($"Tax Income: {Signed(u.TaxIncomeFlat)}{Naming.Gold}/day<br>");
                    if (u.TaxIncomePercent != 0) sb.AppendLine($"Tax Income: {Signed(u.TaxIncomePercent)}%<br>");
                    if (u.GarrisonCapacityBonus != 0) sb.AppendLine($"Garrison Capacity: {Signed(u.GarrisonCapacityBonus)}<br>");
                    if (u.HearthDaily != 0) sb.AppendLine($"Hearth: {Signed(u.HearthDaily)}<br>");
                }
                if (u.DailyTroopSpawnAmount > 0 || u.TroopTierBonus > 0)
                {
                    sb.AppendLine("<br><strong>Troop Spawning:</strong><br>");
                    if (u.DailyTroopSpawnAmount > 0)
                    {
                        sb.AppendLine($"Daily Spawn: {u.DailyTroopSpawnAmount} troops/day<br>");
                        sb.AppendLine($"Troop Tree: {u.TroopTree}<br>");
                        sb.AppendLine($"Base Tier: {u.TroopTier}<br>");
                    }
                    if (u.TroopTierBonus > 0 && !string.IsNullOrEmpty(u.BuffsTroopTierOf))
                        sb.AppendLine($"Tier Bonus: +{u.TroopTierBonus} to {u.BuffsTroopTierOf}<br>");
                }
                return sb.Length > 0 ? sb.ToString() : "No effects configured";
            }

            private string GetUpgradeEffects(KingdomUpgrade u)
            {
                var sb = new StringBuilder();
                if (u.InfluenceDaily != 0 || u.MaxClansBonus != 0)
                {
                    sb.AppendLine("<strong>Kingdom Effects:</strong><br>");
                    if (u.InfluenceDaily != 0) sb.AppendLine($"Influence: {Signed(u.InfluenceDaily)}/day (ruler only)<br>");
                    if (u.MaxClansBonus != 0) sb.AppendLine($"Max Clans: {Signed(u.MaxClansBonus)}<br>");
                }
                if (u.RenownDaily != 0 || u.PartySizeBonus != 0 || u.PartySpeedBonus != 0)
                {
                    sb.AppendLine("<br><strong>Clan Effects (All Kingdom Clans):</strong><br>");
                    if (u.RenownDaily != 0) sb.AppendLine($"Renown: {Signed(u.RenownDaily)}/day<br>");
                    if (u.PartySizeBonus != 0) sb.AppendLine($"Party Size: {Signed(u.PartySizeBonus)}<br>");
                    if (u.PartySpeedBonus != 0) sb.AppendLine($"Party Speed: {Signed(u.PartySpeedBonus)}<br>");
                    if (u.InfluenceDaily != 0) sb.AppendLine($"Influence: {Signed(u.InfluenceDaily)}/day (all clans)<br>");
                }
                if (u.LoyaltyDailyFlat != 0 || u.LoyaltyDailyPercent != 0 || u.ProsperityDailyFlat != 0 || u.ProsperityDailyPercent != 0 ||
                    u.SecurityDailyFlat != 0 || u.SecurityDailyPercent != 0 || u.MilitiaDailyFlat != 0 || u.MilitiaDailyPercent != 0 ||
                    u.FoodDailyFlat != 0 || u.FoodDailyPercent != 0 || u.TaxIncomeFlat != 0 || u.TaxIncomePercent != 0 ||
                    u.GarrisonCapacityBonus != 0 || u.HearthDaily != 0)
                {
                    sb.AppendLine("<br><strong>Settlement Effects (All Kingdom Settlements):</strong><br>");
                    if (u.LoyaltyDailyFlat != 0) sb.AppendLine($"Loyalty: {Signed(u.LoyaltyDailyFlat)}/day<br>");
                    if (u.LoyaltyDailyPercent != 0) sb.AppendLine($"Loyalty: {Signed(u.LoyaltyDailyPercent)}%/day<br>");
                    if (u.ProsperityDailyFlat != 0) sb.AppendLine($"Prosperity: {Signed(u.ProsperityDailyFlat)}/day<br>");
                    if (u.ProsperityDailyPercent != 0) sb.AppendLine($"Prosperity: {Signed(u.ProsperityDailyPercent)}%/day<br>");
                    if (u.SecurityDailyFlat != 0) sb.AppendLine($"Security: {Signed(u.SecurityDailyFlat)}/day<br>");
                    if (u.SecurityDailyPercent != 0) sb.AppendLine($"Security: {Signed(u.SecurityDailyPercent)}%/day<br>");
                    if (u.MilitiaDailyFlat != 0) sb.AppendLine($"Militia: {Signed(u.MilitiaDailyFlat)}/day<br>");
                    if (u.MilitiaDailyPercent != 0) sb.AppendLine($"Militia: {Signed(u.MilitiaDailyPercent)}%/day<br>");
                    if (u.FoodDailyFlat != 0) sb.AppendLine($"Food: {Signed(u.FoodDailyFlat)}/day<br>");
                    if (u.FoodDailyPercent != 0) sb.AppendLine($"Food: {Signed(u.FoodDailyPercent)}%/day<br>");
                    if (u.TaxIncomeFlat != 0) sb.AppendLine($"Tax Income: {Signed(u.TaxIncomeFlat)}{Naming.Gold}/day<br>");
                    if (u.TaxIncomePercent != 0) sb.AppendLine($"Tax Income: {Signed(u.TaxIncomePercent)}%<br>");
                    if (u.GarrisonCapacityBonus != 0) sb.AppendLine($"Garrison Capacity: {Signed(u.GarrisonCapacityBonus)}<br>");
                    if (u.HearthDaily != 0) sb.AppendLine($"Hearth: {Signed(u.HearthDaily)}<br>");
                }
                if (u.DailyTroopSpawnAmount > 0 || u.TroopTierBonus > 0)
                {
                    sb.AppendLine("<br><strong>Troop Spawning (All Kingdom Clans):</strong><br>");
                    if (u.DailyTroopSpawnAmount > 0)
                    {
                        sb.AppendLine($"Daily Spawn: {u.DailyTroopSpawnAmount} troops/day per clan<br>");
                        sb.AppendLine($"Troop Tree: {u.TroopTree}<br>");
                        sb.AppendLine($"Base Tier: {u.TroopTier}<br>");
                    }
                    if (u.TroopTierBonus > 0 && !string.IsNullOrEmpty(u.BuffsTroopTierOf))
                        sb.AppendLine($"Tier Bonus: +{u.TroopTierBonus} to {u.BuffsTroopTierOf}<br>");
                }
                return sb.Length > 0 ? sb.ToString() : "No effects configured";
            }

            private static string Signed(float v) => v > 0 ? $"+{v}" : v.ToString();
            private static string Signed(int v) => v > 0 ? $"+{v}" : v.ToString();

            private bool ShouldShowFullDescription(string upgradeId)
            {
                var m = System.Text.RegularExpressions.Regex.Match(upgradeId, @"^(.+?)(\d+)$");
                return m.Success ? int.Parse(m.Groups[2].Value) == 1 : true;
            }
        }

        // ── Shared string-comparison shorthand ─────────────────────────────────
        private static readonly StringComparison OIC = StringComparison.OrdinalIgnoreCase;

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) { onFailure("Invalid configuration"); return; }
            if (adoptedHero == null) { onFailure(AdoptAHero.NoHeroMessage); return; }
            if (!settings.Enabled) { onFailure("{=BLT_UpgradeDisabled}The upgrade system is disabled".Translate()); return; }
            if (Mission.Current != null) { onFailure("{=BLT_NoMission}Cannot use this command during a mission".Translate()); return; }
            if (context.Args.IsEmpty())
            {
                onFailure("Usage:  [auto|bulk] <fief|clan|kingdom> [all|allk] <name> <upgrade>  |  info <fief|clan|kingdom> <name>  |  list [fief|clan|kingdom]  |  remove <fief|clan|kingdom> <name> <upgrade>");
                return;
            }

            var globalConfig = GlobalCommonConfig.Get();
            if (globalConfig == null) { onFailure("Configuration not available"); return; }

            // ── Parse special flags (position-independent) ──────────────────────
            var rawArgs = context.Args.Split(' ');

            bool autoBuy = rawArgs.Any(a => a.Equals("auto", OIC) || a.Equals("bulk", OIC));
            bool applyAll = rawArgs.Any(a => a.Equals("all", OIC));
            bool applyAllK = rawArgs.Any(a => a.Equals("allk", OIC));

            if (applyAll && applyAllK)
            {
                onFailure("'all' and 'allk' cannot be used together");
                return;
            }

            // Strip flag keywords so remaining args match the original parsing logic
            var cleanArgs = rawArgs
                .Where(a => !a.Equals("auto", OIC) && !a.Equals("bulk", OIC)
                         && !a.Equals("all", OIC) && !a.Equals("allk", OIC))
                .ToArray();

            if (cleanArgs.Length == 0)
            {
                onFailure("No command specified after flags");
                return;
            }

            var command = cleanArgs[0].ToLowerInvariant();

            // ── list ────────────────────────────────────────────────────────────
            if (command == "list")
            {
                if (!settings.AllowListCommand) { onFailure("The list command is disabled"); return; }
                string type = cleanArgs.Length > 1 ? cleanArgs[1].ToLowerInvariant() : "all";
                HandleListCommand(type, globalConfig, onSuccess, onFailure);
                return;
            }

            // ── info ────────────────────────────────────────────────────────────
            if (command == "info")
            {
                if (cleanArgs.Length < 2) { onFailure("Usage: info <fief|clan|kingdom> <name>"); return; }
                string type = cleanArgs[1].ToLowerInvariant();
                string name = string.Join(" ", cleanArgs.Skip(2));
                HandleInfoCommand(type, name, adoptedHero, globalConfig, onSuccess, onFailure);
                return;
            }

            // ── remove ──────────────────────────────────────────────────────────
            if (command == "remove")
            {
                if (cleanArgs.Length < 3) { onFailure("Usage: remove <fief|clan|kingdom> <settlement_name/upgrade_id> [upgrade_id]"); return; }
                string type = cleanArgs[1].ToLowerInvariant();
                if (type == "fief")
                {
                    if (cleanArgs.Length < 4) { onFailure("Usage: remove fief <settlement_name> <upgrade_id>"); return; }
                    string tName = string.Join(" ", cleanArgs.Skip(2).Take(cleanArgs.Length - 3));
                    string uId = cleanArgs.Last();
                    HandleRemoveCommand(type, tName, uId, adoptedHero, settings, globalConfig, onSuccess, onFailure);
                }
                else
                {
                    HandleRemoveCommand(type, null, cleanArgs[2], adoptedHero, settings, globalConfig, onSuccess, onFailure);
                }
                return;
            }

            // ── purchase (fief / clan / kingdom) ────────────────────────────────
            bool needsSettlementName = command == "fief" && !applyAll && !applyAllK;

            if (needsSettlementName && cleanArgs.Length < 3)
            {
                onFailure("Usage: [auto|bulk] fief <settlement_name> <upgrade_id>");
                return;
            }
            else if (!needsSettlementName && (command == "fief" || command == "clan" || command == "kingdom") && cleanArgs.Length < 2)
            {
                onFailure($"Usage: [auto|bulk] {command} [all|allk] <upgrade_id>");
                return;
            }
            else if (command != "fief" && command != "clan" && command != "kingdom")
            {
                onFailure($"Unknown command '{command}'. Use fief, clan, or kingdom");
                return;
            }

            string upgradeId;
            string targetName = null;

            if (command == "fief" && !applyAll && !applyAllK)
            {
                upgradeId = cleanArgs.Last();
                targetName = string.Join(" ", cleanArgs.Skip(1).Take(cleanArgs.Length - 2));
            }
            else
            {
                upgradeId = cleanArgs.Last();
            }

            HandlePurchaseCommand(command, targetName, upgradeId, adoptedHero, settings, globalConfig, autoBuy, applyAll, applyAllK, onSuccess, onFailure);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Auto-buy prerequisite chain builders
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns an ordered list of upgrade IDs that must be purchased to satisfy all
        /// prerequisites of <paramref name="targetId"/>, including transitively, excluding
        /// any already in <paramref name="owned"/>. The list is in dependency order
        /// (deepest prerequisite first, target last).
        /// </summary>
        private List<string> BuildFiefPurchaseChain(string targetId, HashSet<string> owned, GlobalCommonConfig config)
        {
            var result = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Recurse(string id)
            {
                if (visited.Contains(id)) return;
                visited.Add(id);
                if (owned.Contains(id)) return;
                var up = config.FiefUpgrades?.FirstOrDefault(u => string.Equals(u.ID, id, OIC));
                if (up == null) return;
                foreach (var req in up.RequiredUpgradeIDs) Recurse(req);
                result.Add(id);
            }
            Recurse(targetId);
            return result;
        }

        private List<string> BuildClanPurchaseChain(string targetId, HashSet<string> owned, GlobalCommonConfig config)
        {
            var result = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Recurse(string id)
            {
                if (visited.Contains(id)) return;
                visited.Add(id);
                if (owned.Contains(id)) return;
                var up = config.ClanUpgrades?.FirstOrDefault(u => string.Equals(u.ID, id, OIC));
                if (up == null) return;
                foreach (var req in up.RequiredUpgradeIDs) Recurse(req);
                result.Add(id);
            }
            Recurse(targetId);
            return result;
        }

        private List<string> BuildKingdomPurchaseChain(string targetId, HashSet<string> owned, GlobalCommonConfig config)
        {
            var result = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Recurse(string id)
            {
                if (visited.Contains(id)) return;
                visited.Add(id);
                if (owned.Contains(id)) return;
                var up = config.KingdomUpgrades?.FirstOrDefault(u => string.Equals(u.ID, id, OIC));
                if (up == null) return;
                foreach (var req in up.RequiredUpgradeIDs) Recurse(req);
                result.Add(id);
            }
            Recurse(targetId);
            return result;
        }

        // ════════════════════════════════════════════════════════════════════════
        // List / Info helpers (unchanged)
        // ════════════════════════════════════════════════════════════════════════

        private void HandleListCommand(string type, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Available Upgrades ===");

            if (type == "all" || type == "fief")
            {
                sb.AppendLine("\n[Fief Upgrades]");
                if (gc.FiefUpgrades?.Count > 0)
                    foreach (var u in gc.FiefUpgrades) { sb.AppendLine($"  {u.ID}: {u.Name} - {u.GetCostString()}"); sb.AppendLine($"    {u.Description}"); }
                else sb.AppendLine("  No fief upgrades configured");
            }
            if (type == "all" || type == "clan")
            {
                sb.AppendLine("\n[Clan Upgrades]");
                if (gc.ClanUpgrades?.Count > 0)
                    foreach (var u in gc.ClanUpgrades) { sb.AppendLine($"  {u.ID}: {u.Name} - {u.GetCostString()}"); sb.AppendLine($"    {u.Description}"); }
                else sb.AppendLine("  No clan upgrades configured");
            }
            if (type == "all" || type == "kingdom")
            {
                sb.AppendLine("\n[Kingdom Upgrades]");
                if (gc.KingdomUpgrades?.Count > 0)
                    foreach (var u in gc.KingdomUpgrades) { sb.AppendLine($"  {u.ID}: {u.Name} - {u.GetCostString()}"); sb.AppendLine($"    {u.Description}"); }
                else sb.AppendLine("  No kingdom upgrades configured");
            }

            if (type != "all" && type != "fief" && type != "clan" && type != "kingdom")
            { fail($"Invalid type '{type}'. Use 'all', 'fief', 'clan', or 'kingdom'"); return; }

            ok(sb.ToString());
        }

        private void HandleInfoCommand(string type, string name, Hero hero, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            switch (type)
            {
                case "fief": ShowFiefInfo(name, hero, gc, ok, fail); break;
                case "clan": ShowClanInfo(name, hero, gc, ok, fail); break;
                case "kingdom": ShowKingdomInfo(name, hero, gc, ok, fail); break;
                default: fail("Invalid type. Use 'fief', 'clan', or 'kingdom'"); break;
            }
        }

        private void ShowFiefInfo(string name, Hero hero, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            if (string.IsNullOrEmpty(name)) { fail("Usage: info <fief> <name>"); return; }
            var settlement = FindSettlement(name);
            if (settlement == null) { fail($"Settlement '{name}' not found"); return; }
            if (settlement.Town == null || settlement.IsVillage) { fail("Only towns and castles can have upgrades"); return; }

            var ids = UpgradeBehavior.Current?.GetFiefUpgrades(settlement) ?? new List<string>();
            var sb = new StringBuilder();
            sb.AppendLine($"=== {settlement.Name} Upgrades ===");
            if (ids.Count == 0) { sb.AppendLine("No upgrades purchased yet"); }
            else
            {
                sb.AppendLine("Purchased Upgrades:");
                foreach (var u in HighestTierOnly(ids.Select(id => gc.FiefUpgrades.FirstOrDefault(u => u.ID == id)).Where(u => u != null).Cast<object>()))
                    sb.AppendLine($"  • {((FiefUpgrade)u).Name}");
            }
            ok(sb.ToString());
        }

        private void ShowClanInfo(string name, Hero hero, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            if (string.IsNullOrEmpty(name)) { fail("Usage: info <clan> <name>"); return; }
            var clan = FindClan(name);
            if (clan == null) { fail($"Clan '{name}' not found"); return; }
            var ids = UpgradeBehavior.Current?.GetClanUpgrades(clan) ?? new List<string>();
            var sb = new StringBuilder();
            sb.AppendLine($"=== {clan.Name} Upgrades ===");
            if (ids.Count == 0) { sb.AppendLine("No upgrades purchased yet"); }
            else
            {
                sb.AppendLine("Purchased Upgrades:");
                foreach (var u in HighestTierOnly(ids.Select(id => gc.ClanUpgrades.FirstOrDefault(u => u.ID == id)).Where(u => u != null).Cast<object>()))
                    sb.AppendLine($"  • {((ClanUpgrade)u).Name}");
            }
            ok(sb.ToString());
        }

        private void ShowKingdomInfo(string name, Hero hero, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            if (string.IsNullOrEmpty(name)) { fail("Usage: info <kingdom> <name>"); return; }
            var kingdom = FindKingdom(name);
            if (kingdom == null) { fail($"Kingdom '{name}' not found"); return; }
            var ids = UpgradeBehavior.Current?.GetKingdomUpgrades(kingdom) ?? new List<string>();
            var sb = new StringBuilder();
            sb.AppendLine($"=== {kingdom.Name} Upgrades ===");
            if (ids.Count == 0) { sb.AppendLine("No upgrades purchased yet"); }
            else
            {
                sb.AppendLine("Purchased Upgrades:");
                foreach (var u in HighestTierOnly(ids.Select(id => gc.KingdomUpgrades.FirstOrDefault(u => u.ID == id)).Where(u => u != null).Cast<object>()))
                    sb.AppendLine($"  • {((KingdomUpgrade)u).Name}");
            }
            ok(sb.ToString());
        }

        /// <summary>Groups upgrades by their base ID (strip trailing digits) and keeps only the highest-tier entry.</summary>
        private static IEnumerable<object> HighestTierOnly(IEnumerable<object> items)
        {
            // Each item must expose an "ID" property — use dynamic or a shared interface if available.
            // Using reflection-free approach: cast to dynamic to read ID.
            return items
                .GroupBy(u => { var id = GetId(u); var m = System.Text.RegularExpressions.Regex.Match(id, @"^(.+?)(\d+)$"); return m.Success ? m.Groups[1].Value : id; })
                .Select(g => g.OrderByDescending(u => { var m = System.Text.RegularExpressions.Regex.Match(GetId(u), @"(\d+)$"); return m.Success ? int.Parse(m.Groups[1].Value) : 0; }).First())
                .OrderBy(u => GetId(u));
        }

        private static string GetId(object u)
        {
            if (u is FiefUpgrade f) return f.ID;
            if (u is ClanUpgrade c) return c.ID;
            if (u is KingdomUpgrade k) return k.ID;
            return "";
        }

        // ════════════════════════════════════════════════════════════════════════
        // Purchase routing
        // ════════════════════════════════════════════════════════════════════════

        private void HandlePurchaseCommand(
            string type, string name, string upgradeId,
            Hero hero, Settings settings, GlobalCommonConfig gc,
            bool autoBuy, bool applyAll, bool applyAllK,
            Action<string> ok, Action<string> fail)
        {
            switch (type)
            {
                case "fief":
                    if (applyAll || applyAllK)
                        PurchaseFiefUpgradeMulti(upgradeId, hero, settings, gc, autoBuy, applyAllK, ok, fail);
                    else
                        PurchaseFiefUpgrade(name, upgradeId, hero, settings, gc, autoBuy, ok, fail);
                    break;
                case "clan":
                    PurchaseClanUpgrade(upgradeId, hero, settings, gc, autoBuy, ok, fail);
                    break;
                case "kingdom":
                    PurchaseKingdomUpgrade(upgradeId, hero, gc, autoBuy, ok, fail);
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // Fief purchase — single settlement
        // ════════════════════════════════════════════════════════════════════════

        private void PurchaseFiefUpgrade(
            string name, string upgradeId,
            Hero hero, Settings settings, GlobalCommonConfig gc,
            bool autoBuy,
            Action<string> ok, Action<string> fail)
        {
            var settlement = FindSettlement(name);
            if (settlement == null) { fail($"Settlement '{name}' not found"); return; }
            if (settlement.Town == null) { fail("Only towns and castles can have upgrades"); return; }

            // Permission check
            bool isOwner = settlement.OwnerClan == hero.Clan;
            if (!isOwner && VassalBehavior.Current != null)
                foreach (Clan v in VassalBehavior.Current.GetVassalClans(hero.Clan))
                    if (v == settlement.OwnerClan) { isOwner = true; break; }

            bool isKingdomLeader = settings.AllowKingdomLeadersForFiefs
                && hero.Clan?.Kingdom != null
                && hero.Clan.Kingdom.Leader == hero
                && settlement.OwnerClan?.Kingdom == hero.Clan.Kingdom;

            if (!isOwner && !isKingdomLeader) { fail($"You don't have permission to upgrade {settlement.Name}"); return; }
            if (!hero.IsClanLeader && !isKingdomLeader) { fail("Only clan leaders can purchase fief upgrades"); return; }

            // Resolve the upgrade object for the final target first (validate it exists)
            var targetUpgrade = gc.FiefUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (targetUpgrade == null) { fail($"Upgrade '{upgradeId}' not found"); return; }
            if (targetUpgrade.CoastalOnly && !settlement.HasPort) { fail("This is a Coastal Only upgrade, try again on a coastal settlement"); return; }

            // Build purchase chain (includes prerequisites when autoBuy is true)
            var owned = new HashSet<string>(UpgradeBehavior.Current?.GetFiefUpgrades(settlement) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            if (owned.Contains(upgradeId)) { fail($"{settlement.Name} already has this upgrade"); return; }

            List<string> chain;
            if (autoBuy)
            {
                chain = BuildFiefPurchaseChain(upgradeId, owned, gc);
                if (chain.Count == 0) { fail($"{settlement.Name} already has this upgrade"); return; }
            }
            else
            {
                // Original prerequisite check
                var reqIds = targetUpgrade.RequiredUpgradeIDs;
                if (reqIds.Count > 0 && !targetUpgrade.AreRequiredUpgradesMet(owned))
                {
                    var missing = reqIds.Where(id => !owned.Contains(id, StringComparer.OrdinalIgnoreCase));
                    fail($"Requires upgrade(s) first: {string.Join(", ", missing)}");
                    return;
                }
                chain = new List<string> { upgradeId };
            }

            // Execute the chain
            var results = ExecuteFiefChain(settlement, chain, hero, gc, owned);
            ReportChainResults(results, upgradeId, $"for {settlement.Name}", ok, fail);

            if (results.Any(r => r.Success))
                Log.ShowInformation($"{hero.Name} purchased upgrades for {settlement.Name}", hero.CharacterObject, Log.Sound.Notification1);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Fief purchase — multi-settlement (all / allk)
        // ════════════════════════════════════════════════════════════════════════

        private void PurchaseFiefUpgradeMulti(
            string upgradeId,
            Hero hero, Settings settings, GlobalCommonConfig gc,
            bool autoBuy, bool forKingdom,
            Action<string> ok, Action<string> fail)
        {
            IEnumerable<Settlement> targets;

            if (forKingdom)
            {
                if (hero.Clan == null) { fail("You are not in a clan!"); return; }
                var kingdom = hero.Clan.Kingdom;
                if (kingdom == null) { fail("You are not in a kingdom!"); return; }
                if (kingdom.Leader != hero && !settings.AllowKingdomLeadersForFiefs)
                { fail("Only the kingdom ruler can use 'allk'"); return; }
                targets = Settlement.All.Where(s => s.OwnerClan?.Kingdom == kingdom && s.Town != null);
            }
            else // applyAll — clan's own fiefs + vassal fiefs (same scope as single-settlement permission)
            {
                if (hero.Clan == null) { fail("You are not in a clan!"); return; }
                if (!hero.IsClanLeader) { fail("Only clan leaders can use 'all'"); return; }
                var allowedClans = new HashSet<Clan> { hero.Clan };
                if (VassalBehavior.Current != null)
                    foreach (var v in VassalBehavior.Current.GetVassalClans(hero.Clan))
                        allowedClans.Add(v);
                targets = Settlement.All.Where(s => allowedClans.Contains(s.OwnerClan) && s.Town != null);
            }

            var targetList = targets.ToList();
            if (targetList.Count == 0) { fail("No valid settlements found"); return; }

            // Validate the upgrade exists once up-front
            var targetUpgrade = gc.FiefUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (targetUpgrade == null) { fail($"Upgrade '{upgradeId}' not found"); return; }

            int settlementsSuccess = 0, settlementsSkipped = 0, settlementsFailed = 0;
            int totalBought = 0;
            var failMessages = new List<string>();

            foreach (var settlement in targetList)
            {
                // Skip non-coastal settlements for coastal-only upgrades silently
                if (targetUpgrade.CoastalOnly && !settlement.HasPort) { settlementsSkipped++; continue; }

                var owned = new HashSet<string>(UpgradeBehavior.Current?.GetFiefUpgrades(settlement) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

                // Already fully upgraded — skip silently
                if (owned.Contains(upgradeId)) { settlementsSkipped++; continue; }

                List<string> chain;
                if (autoBuy)
                {
                    chain = BuildFiefPurchaseChain(upgradeId, owned, gc);
                    if (chain.Count == 0) { settlementsSkipped++; continue; }
                }
                else
                {
                    var reqIds = targetUpgrade.RequiredUpgradeIDs;
                    if (reqIds.Count > 0 && !targetUpgrade.AreRequiredUpgradesMet(owned))
                    {
                        var missing = reqIds.Where(id => !owned.Contains(id, StringComparer.OrdinalIgnoreCase));
                        failMessages.Add($"{settlement.Name}: missing {string.Join(", ", missing)}");
                        settlementsFailed++;
                        continue;
                    }
                    chain = new List<string> { upgradeId };
                }

                var results = ExecuteFiefChain(settlement, chain, hero, gc, owned);
                int bought = results.Count(r => r.Success);
                if (bought > 0) { settlementsSuccess++; totalBought += bought; }
                else
                {
                    settlementsFailed++;
                    var firstFail = results.FirstOrDefault(r => !r.Success);
                    if (firstFail != null) failMessages.Add($"{settlement.Name}: {firstFail.Message}");
                }
            }

            if (settlementsSuccess > 0)
                Log.ShowInformation($"{hero.Name} purchased upgrades across multiple settlements", hero.CharacterObject, Log.Sound.Notification1);

            var sb = new StringBuilder();
            string scope = forKingdom ? "kingdom" : "clan";
            sb.AppendLine($"=== Upgrade Results ({scope}) ===");
            sb.AppendLine($"Settlements updated : {settlementsSuccess}");
            sb.AppendLine($"Settlements skipped : {settlementsSkipped}  (already owned or ineligible)");
            sb.AppendLine($"Settlements failed  : {settlementsFailed}");
            if (totalBought > 0) sb.AppendLine($"Total upgrades bought: {totalBought}");
            if (failMessages.Count > 0)
            {
                sb.AppendLine("Failures:");
                foreach (var m in failMessages) sb.AppendLine($"  • {m}");
            }

            if (settlementsSuccess > 0 || settlementsSkipped > 0) ok(sb.ToString());
            else fail(sb.ToString());
        }

        // ════════════════════════════════════════════════════════════════════════
        // Fief chain execution helper
        // ════════════════════════════════════════════════════════════════════════

        private class PurchaseResult
        {
            public bool Success;
            public string UpgradeId;
            public string UpgradeName; // display name from config
            public string Message;     // failure reason when !Success
        }

        private List<PurchaseResult> ExecuteFiefChain(Settlement settlement, List<string> chain, Hero hero, GlobalCommonConfig gc, HashSet<string> alreadyOwned)
        {
            var results = new List<PurchaseResult>();
            foreach (var id in chain)
            {
                if (alreadyOwned.Contains(id)) continue;
                var up = gc.FiefUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null)
                { results.Add(new PurchaseResult { UpgradeId = id, UpgradeName = id, Success = false, Message = $"Upgrade '{id}' not found in config" }); return results; }
                int gold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero);
                if (gold < up.GoldCost)
                { results.Add(new PurchaseResult { UpgradeId = id, UpgradeName = up.Name, Success = false, Message = Naming.NotEnoughGold(up.GoldCost, gold) }); return results; }
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -up.GoldCost, true);
                UpgradeBehavior.Current?.AddFiefUpgrade(settlement, id);
                alreadyOwned.Add(id);
                results.Add(new PurchaseResult { UpgradeId = id, UpgradeName = up.Name, Success = true });
            }
            return results;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Clan purchase
        // ════════════════════════════════════════════════════════════════════════

        private void PurchaseClanUpgrade(
            string upgradeId, Hero hero, Settings settings, GlobalCommonConfig gc,
            bool autoBuy,
            Action<string> ok, Action<string> fail)
        {
            var clan = hero?.Clan;
            if (clan == null) { fail("You are not in a clan!"); return; }

            if (!settings.AllowAnyClanMemberForClanUpgrades && !hero.IsClanLeader)
            { fail("Only clan leaders can purchase clan upgrades"); return; }

            var targetUpgrade = gc.ClanUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (targetUpgrade == null) { fail($"Upgrade '{upgradeId}' not found"); return; }

            var owned = new HashSet<string>(UpgradeBehavior.Current?.GetClanUpgrades(clan) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (owned.Contains(upgradeId)) { fail($"{clan.Name} already has this upgrade"); return; }

            List<string> chain;
            if (autoBuy)
            {
                chain = BuildClanPurchaseChain(upgradeId, owned, gc);
                if (chain.Count == 0) { fail($"{clan.Name} already has this upgrade"); return; }
            }
            else
            {
                var reqIds = targetUpgrade.RequiredUpgradeIDs;
                if (reqIds.Count > 0 && !targetUpgrade.AreRequiredUpgradesMet(owned))
                {
                    var missing = reqIds.Where(id => !owned.Contains(id, StringComparer.OrdinalIgnoreCase));
                    fail($"Requires upgrade(s) first: {string.Join(", ", missing)}");
                    return;
                }
                chain = new List<string> { upgradeId };
            }

            var results = ExecuteClanChain(clan, chain, hero, gc, owned);
            ReportChainResults(results, upgradeId, $"for {clan.Name}", ok, fail);

            if (results.Any(r => r.Success))
                Log.ShowInformation($"{hero.Name} purchased clan upgrade(s) for {clan.Name}", hero.CharacterObject, Log.Sound.Notification1);
        }

        private List<PurchaseResult> ExecuteClanChain(Clan clan, List<string> chain, Hero hero, GlobalCommonConfig gc, HashSet<string> owned)
        {
            var results = new List<PurchaseResult>();
            foreach (var id in chain)
            {
                if (owned.Contains(id)) continue;
                var up = gc.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) { results.Add(new PurchaseResult { UpgradeId = id, Success = false, Message = $"Upgrade '{id}' not found" }); return results; }
                int gold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero);
                if (gold < up.GoldCost) { results.Add(new PurchaseResult { UpgradeId = id, Success = false, Message = Naming.NotEnoughGold(up.GoldCost, gold) }); return results; }
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -up.GoldCost, true);
                UpgradeBehavior.Current?.AddClanUpgrade(clan, id);
                owned.Add(id);
                results.Add(new PurchaseResult { UpgradeId = id, UpgradeName = up.Name, Success = true, Message = $"Purchased '{up.Name}'" });
            }
            return results;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Kingdom purchase
        // ════════════════════════════════════════════════════════════════════════

        private void PurchaseKingdomUpgrade(
            string upgradeId, Hero hero, GlobalCommonConfig gc,
            bool autoBuy,
            Action<string> ok, Action<string> fail)
        {
            if (hero.Clan == null) { fail("You're not in a clan!"); return; }
            var kingdom = hero.Clan.Kingdom;
            if (kingdom == null) { fail("You're not in a kingdom!"); return; }
            if (kingdom.Leader != hero) { fail("Only the kingdom ruler can purchase kingdom upgrades"); return; }

            var targetUpgrade = gc.KingdomUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (targetUpgrade == null) { fail($"Upgrade '{upgradeId}' not found"); return; }

            var owned = new HashSet<string>(UpgradeBehavior.Current?.GetKingdomUpgrades(kingdom) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (owned.Contains(upgradeId)) { fail($"{kingdom.Name} already has this upgrade"); return; }

            List<string> chain;
            if (autoBuy)
            {
                chain = BuildKingdomPurchaseChain(upgradeId, owned, gc);
                if (chain.Count == 0) { fail($"{kingdom.Name} already has this upgrade"); return; }
            }
            else
            {
                var reqIds = targetUpgrade.RequiredUpgradeIDs;
                if (reqIds.Count > 0 && !targetUpgrade.AreRequiredUpgradesMet(owned))
                {
                    var missing = reqIds.Where(id => !owned.Contains(id, StringComparer.OrdinalIgnoreCase));
                    fail($"Requires upgrade(s) first: {string.Join(", ", missing)}");
                    return;
                }
                chain = new List<string> { upgradeId };
            }

            var results = ExecuteKingdomChain(kingdom, chain, hero, gc, owned);
            ReportChainResults(results, upgradeId, $"for {kingdom.Name}", ok, fail);

            if (results.Any(r => r.Success))
                Log.ShowInformation($"{hero.Name} purchased kingdom upgrade(s) for {kingdom.Name}", hero.CharacterObject, Log.Sound.Horns2);
        }

        private List<PurchaseResult> ExecuteKingdomChain(Kingdom kingdom, List<string> chain, Hero hero, GlobalCommonConfig gc, HashSet<string> owned)
        {
            var results = new List<PurchaseResult>();
            foreach (var id in chain)
            {
                if (owned.Contains(id)) continue;
                var up = gc.KingdomUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) { results.Add(new PurchaseResult { UpgradeId = id, Success = false, Message = $"Upgrade '{id}' not found" }); return results; }
                int gold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero);
                if (gold < up.GoldCost) { results.Add(new PurchaseResult { UpgradeId = id, Success = false, Message = Naming.NotEnoughGold(up.GoldCost, gold) }); return results; }
                if (up.InfluenceCost > 0 && hero.Clan.Influence < up.InfluenceCost)
                {
                    results.Add(new PurchaseResult { UpgradeId = id, Success = false, Message = $"Not enough influence (need {up.InfluenceCost}, have {(int)hero.Clan.Influence})" });
                    return results;
                }
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -up.GoldCost, true);
                if (up.InfluenceCost > 0) hero.Clan.Influence -= up.InfluenceCost;
                UpgradeBehavior.Current?.AddKingdomUpgrade(kingdom, id);
                owned.Add(id);
                results.Add(new PurchaseResult { UpgradeId = id, UpgradeName = up.Name, Success = true, Message = $"Purchased '{up.Name}'" });
            }
            return results;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Chain result reporter
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Reports purchase results as at most two lines:
        ///   "Auto-purchased: Prereq 1-3, OtherThing"   (only if prereqs were bought)
        ///   "Purchased 'Target' {context}!"
        /// Plus an optional "Stopped: {reason}" line if the chain was cut short by lack of gold/influence.
        /// </summary>
        private static void ReportChainResults(List<PurchaseResult> results, string targetId, string context, Action<string> ok, Action<string> fail)
        {
            var bought = results.Where(r => r.Success).ToList();
            var blocked = results.FirstOrDefault(r => !r.Success);

            if (bought.Count == 0)
            {
                fail(blocked?.Message ?? "Purchase failed");
                return;
            }

            var prereqs = bought.Where(r => !r.UpgradeId.Equals(targetId, OIC)).ToList();
            var target = bought.FirstOrDefault(r => r.UpgradeId.Equals(targetId, OIC));

            var sb = new StringBuilder();

            if (prereqs.Count > 0)
                sb.AppendLine($"Auto-purchased: {CollapseChainNames(prereqs)}");

            if (target != null)
                sb.Append($"Purchased '{target.UpgradeName}' {context}!");
            else // target wasn't reached (ran out of gold mid-chain), but some prereqs succeeded
                sb.Append($"Partially purchased prerequisites {context}");

            if (blocked != null)
                sb.Append($" | Stopped: {blocked.Message}");

            ok(sb.ToString());
        }

        /// <summary>
        /// Collapses a list of purchased upgrades into compact display strings.
        /// Consecutive numerically-suffixed upgrades sharing the same base ID are merged:
        ///   vineyards1, vineyards2, vineyards3 → "Vineyards 1-3"
        ///   vineyards1, vineyards3             → "Vineyards 1, 3"
        /// Non-numbered upgrades are shown by their display name as-is.
        /// </summary>
        private static string CollapseChainNames(IEnumerable<PurchaseResult> items)
        {
            var numPat = new System.Text.RegularExpressions.Regex(@"^(.+?)(\d+)$");
            var namePat = new System.Text.RegularExpressions.Regex(@"^(.+?)(\d+)$");

            // Annotate each item with its base-ID and numeric suffix
            var annotated = items.Select(r =>
            {
                var m = numPat.Match(r.UpgradeId);
                return new
                {
                    r.UpgradeName,
                    r.UpgradeId,
                    Base = m.Success ? m.Groups[1].Value : r.UpgradeId,
                    Num = m.Success ? int.Parse(m.Groups[2].Value) : (int?)null
                };
            });

            // Group by base ID, preserving first-seen order
            var groups = annotated
                .GroupBy(x => x.Base)
                .OrderBy(g => items.ToList().FindIndex(r => r.UpgradeId.StartsWith(g.Key, OIC)));

            var parts = new List<string>();
            foreach (var g in groups)
            {
                var unnumbered = g.Where(x => x.Num == null).ToList();
                var numbered = g.Where(x => x.Num != null).OrderBy(x => x.Num).ToList();

                foreach (var u in unnumbered)
                    parts.Add(u.UpgradeName);

                if (numbered.Count == 0) continue;
                if (numbered.Count == 1) { parts.Add(numbered[0].UpgradeName); continue; }

                // Derive display base name from the first item's Name (strip trailing digits)
                var nm = namePat.Match(numbered[0].UpgradeName);
                string baseName = nm.Success ? nm.Groups[1].Value.TrimEnd() : numbered[0].UpgradeName;

                // Split into consecutive runs
                var runs = new List<List<int>>();
                var cur = new List<int> { numbered[0].Num!.Value };
                for (int i = 1; i < numbered.Count; i++)
                {
                    if (numbered[i].Num == numbered[i - 1].Num + 1) cur.Add(numbered[i].Num!.Value);
                    else { runs.Add(cur); cur = new List<int> { numbered[i].Num!.Value }; }
                }
                runs.Add(cur);

                var runStrs = runs.Select(r => r.Count == 1 ? r[0].ToString() : $"{r.First()}-{r.Last()}");
                parts.Add($"{baseName} {string.Join(", ", runStrs)}");
            }

            return string.Join(", ", parts);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Name lookups
        // ════════════════════════════════════════════════════════════════════════

        private Settlement FindSettlement(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var settlement = Settlement.All.FirstOrDefault(s => s?.Name?.ToString().Equals(name, OIC) == true);
            if (settlement?.IsVillage == true)
            {
                name = name.Add(" Castle", false);
                settlement = Settlement.All.FirstOrDefault(s => s?.Name?.ToString().Equals(name, OIC) == true);
            }
            return settlement;
        }

        private Clan FindClan(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var clan = Clan.All.FirstOrDefault(c => c?.Name?.ToString().Equals(name, OIC) == true);
            return clan;
        }

        private Kingdom FindKingdom(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var kingdom = Kingdom.All.FirstOrDefault(k => k?.Name?.ToString().Equals(name, OIC) == true);
            return kingdom;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Remove command (unchanged logic, refactored slightly)
        // ════════════════════════════════════════════════════════════════════════

        private void HandleRemoveCommand(string type, string name, string upgradeId, Hero hero, Settings settings, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            switch (type)
            {
                case "fief": RemoveFiefUpgrade(name, upgradeId, hero, settings, gc, ok, fail); break;
                case "clan": RemoveClanUpgrade(upgradeId, hero, settings, gc, ok, fail); break;
                case "kingdom": RemoveKingdomUpgrade(upgradeId, hero, gc, ok, fail); break;
                default: fail("Invalid type. Use 'fief', 'clan', or 'kingdom'"); break;
            }
        }

        private void RemoveFiefUpgrade(string name, string upgradeId, Hero hero, Settings settings, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            var settlement = FindSettlement(name);
            if (settlement == null) { fail($"Settlement '{name}' not found"); return; }
            if (settlement.Town == null) { fail("Only towns and castles can have upgrades"); return; }

            bool isOwner = settlement.OwnerClan == hero.Clan;
            bool isKingdomLeader = settings.AllowKingdomLeadersForFiefs && hero.Clan?.Kingdom != null && hero.Clan.Kingdom.Leader == hero && settlement.OwnerClan?.Kingdom == hero.Clan.Kingdom;
            if (!isOwner && !isKingdomLeader) { fail($"You don't have permission to modify {settlement.Name}"); return; }
            if (!hero.IsClanLeader && !isKingdomLeader) { fail("Only clan leaders can remove fief upgrades"); return; }

            var up = gc.FiefUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (up == null) { fail($"Upgrade '{upgradeId}' not found"); return; }
            if (!up.CanBeRemoved) { fail($"'{up.Name}' cannot be removed"); return; }
            if (UpgradeBehavior.Current?.HasFiefUpgrade(settlement, upgradeId) != true) { fail($"{settlement.Name} doesn't have this upgrade"); return; }

            UpgradeBehavior.Current?.RemoveFiefUpgrade(settlement, upgradeId);
            ok($"Removed '{up.Name}' from {settlement.Name}!");
            Log.ShowInformation($"{hero.Name} removed {up.Name} from {settlement.Name}", hero.CharacterObject, Log.Sound.Notification1);
        }

        private void RemoveClanUpgrade(string upgradeId, Hero hero, Settings settings, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            var clan = hero?.Clan;
            if (clan == null) { fail("You are not in a clan!"); return; }
            if (!settings.AllowAnyClanMemberForClanUpgrades && !hero.IsClanLeader) { fail("Only clan leaders can remove clan upgrades"); return; }

            var up = gc.ClanUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (up == null) { fail($"Upgrade '{upgradeId}' not found"); return; }
            if (!up.CanBeRemoved) { fail($"'{up.Name}' cannot be removed"); return; }
            if (UpgradeBehavior.Current?.HasClanUpgrade(clan, upgradeId) != true) { fail($"{clan.Name} doesn't have this upgrade"); return; }

            UpgradeBehavior.Current?.RemoveClanUpgrade(clan, upgradeId);
            ok($"Removed '{up.Name}' from {clan.Name}!");
            Log.ShowInformation($"{hero.Name} removed {up.Name} from {clan.Name}", hero.CharacterObject, Log.Sound.Notification1);
        }

        private void RemoveKingdomUpgrade(string upgradeId, Hero hero, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            if (hero.Clan == null) { fail("You're not in a clan!"); return; }
            var kingdom = hero.Clan.Kingdom;
            if (kingdom == null) { fail("You're not in a kingdom!"); return; }
            if (kingdom.Leader != hero) { fail("Only the kingdom ruler can remove kingdom upgrades"); return; }

            var up = gc.KingdomUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (up == null) { fail($"Upgrade '{upgradeId}' not found"); return; }
            if (!up.CanBeRemoved) { fail($"'{up.Name}' cannot be removed"); return; }
            if (UpgradeBehavior.Current?.HasKingdomUpgrade(kingdom, upgradeId) != true) { fail($"{kingdom.Name} doesn't have this upgrade"); return; }

            UpgradeBehavior.Current?.RemoveKingdomUpgrade(kingdom, upgradeId);
            ok($"Removed '{up.Name}' from {kingdom.Name}!");
            Log.ShowInformation($"{hero.Name} removed {up.Name} from {kingdom.Name}", hero.CharacterObject, Log.Sound.Horns2);
        }
    }
}