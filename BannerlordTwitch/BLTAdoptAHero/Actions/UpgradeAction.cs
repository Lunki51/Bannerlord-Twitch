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
         CategoryOrder("Permissions", 1),
         CategoryOrder("Fief Upgrades", 2),
         CategoryOrder("Clan Upgrades", 3),
         CategoryOrder("Kingdom Upgrades", 4)]
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

            // Upgrade definitions
            [LocDisplayName("{=BLT_FiefUpgrades}Fief Upgrades"),
             LocCategory("Fief Upgrades", "{=BLT_FiefUpgrades}Fief Upgrades"),
             LocDescription("{=BLT_FiefUpgradesDesc}List of available fief (settlement) upgrades"),
             PropertyOrder(1), UsedImplicitly,
             Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor))]
            public ObservableCollection<FiefUpgrade> FiefUpgrades { get; set; } = new()
            {
                new FiefUpgrade
                {
                    ID = "fief_loyalty_1",
                    Name = "Improved Administration",
                    Description = "Better administration increases loyalty growth",
                    GoldCost = 15000,
                    LoyaltyDailyFlat = 0.5f
                },
                new FiefUpgrade
                {
                    ID = "fief_prosperity_1",
                    Name = "Trade Hub",
                    Description = "Attract more merchants to boost prosperity",
                    GoldCost = 20000,
                    ProsperityDailyFlat = 1.0f
                },
                new FiefUpgrade
                {
                    ID = "fief_security_1",
                    Name = "Guard Posts",
                    Description = "Additional guard posts improve security",
                    GoldCost = 12000,
                    SecurityDailyFlat = 0.5f
                },
                new FiefUpgrade
                {
                    ID = "fief_militia_1",
                    Name = "Militia Training",
                    Description = "Train civilians as militia",
                    GoldCost = 10000,
                    MilitiaDailyFlat = 2.0f
                },
                new FiefUpgrade
                {
                    ID = "fief_food_1",
                    Name = "Granary Expansion",
                    Description = "Larger granaries store more food",
                    GoldCost = 8000,
                    FoodDailyFlat = 5.0f
                }
            };

            [LocDisplayName("{=BLT_ClanUpgrades}Clan Upgrades"),
             LocCategory("Clan Upgrades", "{=BLT_ClanUpgrades}Clan Upgrades"),
             LocDescription("{=BLT_ClanUpgradesDesc}List of available clan-wide upgrades"),
             PropertyOrder(1), UsedImplicitly,
             Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor))]
            public ObservableCollection<ClanUpgrade> ClanUpgrades { get; set; } = new()
            {
                new ClanUpgrade
                {
                    ID = "clan_renown_1",
                    Name = "Clan Prestige",
                    Description = "Increase your clan's fame across the land",
                    GoldCost = 30000,
                    RenownDaily = 1.0f
                },
                new ClanUpgrade
                {
                    ID = "clan_party_1",
                    Name = "Recruitment Drive",
                    Description = "Allow larger party sizes for all clan members",
                    GoldCost = 40000,
                    PartySizeBonus = 20
                },
                new ClanUpgrade
                {
                    ID = "clan_settlements_1",
                    Name = "Clan Development",
                    Description = "Improve loyalty and prosperity in all clan settlements",
                    GoldCost = 50000,
                    LoyaltyDailyFlat = 0.3f,
                    ProsperityDailyFlat = 0.5f
                }
            };

            [LocDisplayName("{=BLT_KingdomUpgrades}Kingdom Upgrades"),
             LocCategory("Kingdom Upgrades", "{=BLT_KingdomUpgrades}Kingdom Upgrades"),
             LocDescription("{=BLT_KingdomUpgradesDesc}List of available kingdom-wide upgrades"),
             PropertyOrder(1), UsedImplicitly,
             Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor))]
            public ObservableCollection<KingdomUpgrade> KingdomUpgrades { get; set; } = new()
            {
                new KingdomUpgrade
                {
                    ID = "kingdom_influence_1",
                    Name = "Royal Authority",
                    Description = "Strengthen the ruler's influence",
                    GoldCost = 100000,
                    InfluenceCost = 500,
                    InfluenceDaily = 2.0f
                },
                new KingdomUpgrade
                {
                    ID = "kingdom_military_1",
                    Name = "Kingdom Military Reform",
                    Description = "Increase party sizes and militia across the kingdom",
                    GoldCost = 150000,
                    InfluenceCost = 1000,
                    PartySizeBonus = 15,
                    MilitiaDailyFlat = 1.0f
                },
                new KingdomUpgrade
                {
                    ID = "kingdom_prosperity_1",
                    Name = "Kingdom Prosperity Initiative",
                    Description = "Boost prosperity and loyalty in all kingdom settlements",
                    GoldCost = 200000,
                    InfluenceCost = 1500,
                    LoyaltyDailyFlat = 0.2f,
                    ProsperityDailyFlat = 0.5f
                }
            };

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value($"<strong>Enabled:</strong> {(Enabled ? "Yes" : "No")}");
                generator.Value($"<strong>Allow List Command:</strong> {(AllowListCommand ? "Yes" : "No")}");
                generator.Value($"<strong>Kingdom Leaders Can Upgrade Fiefs:</strong> {(AllowKingdomLeadersForFiefs ? "Yes" : "No")}");
                generator.Value($"<strong>Any Clan Member Can Upgrade Clan:</strong> {(AllowAnyClanMemberForClanUpgrades ? "Yes" : "No")}");

                generator.PropertyValuePair("Fief Upgrades Available", (FiefUpgrades?.Count ?? 0).ToString());
                if (FiefUpgrades != null && FiefUpgrades.Count > 0)
                {
                    foreach (var upgrade in FiefUpgrades)
                    {
                        generator.Value($"  • {upgrade.Name} ({upgrade.ID}) - {upgrade.GoldCost}{Naming.Gold}");
                    }
                }

                generator.PropertyValuePair("Clan Upgrades Available", (ClanUpgrades?.Count ?? 0).ToString());
                if (ClanUpgrades != null && ClanUpgrades.Count > 0)
                {
                    foreach (var upgrade in ClanUpgrades)
                    {
                        generator.Value($"  • {upgrade.Name} ({upgrade.ID}) - {upgrade.GoldCost}{Naming.Gold}");
                    }
                }

                generator.PropertyValuePair("Kingdom Upgrades Available", (KingdomUpgrades?.Count ?? 0).ToString());
                if (KingdomUpgrades != null && KingdomUpgrades.Count > 0)
                {
                    foreach (var upgrade in KingdomUpgrades)
                    {
                        generator.Value($"  • {upgrade.Name} ({upgrade.ID}) - {upgrade.GetCostString()}");
                    }
                }
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        // Store settings in behavior for runtime access - called by behavior on session start
        internal static Settings CurrentSettings { get; set; }

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings)
            {
                onFailure("Invalid configuration");
                return;
            }

            // IMPORTANT: Update current settings reference FIRST for behavior access
            CurrentSettings = settings;

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
                onFailure("Usage:  <fief|clan|kingdom> <name> <upgrade>  OR  info <fief|clan|kingdom> <name>  OR  list [fief|clan|kingdom]");
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
                HandleListCommand(type, settings, onSuccess, onFailure);
                return;
            }

            // Handle info command
            if (command == "info")
            {
                if (args.Length < 3)
                {
                    onFailure("Usage: info <fief|clan|kingdom> <name>");
                    return;
                }

                string type = args[1].ToLowerInvariant();
                string name = string.Join(" ", args.Skip(2));
                HandleInfoCommand(type, name, adoptedHero, settings, onSuccess, onFailure);
                return;
            }

            // Handle purchase command
            if (command != "fief" && command != "clan" && command != "kingdom")
            {
                onFailure("Invalid command. Use 'fief', 'clan', 'kingdom', 'info', or 'list'");
                return;
            }

            if (args.Length < 3)
            {
                onFailure($"Usage: {command} <name> <upgrade>");
                return;
            }

            // Find upgrade ID (last arg) and fief name (everything in between)
            string upgradeId = args.Last();
            string targetName = null;
            if (command == "fief")
            {
                targetName = string.Join(" ", args.Skip(1).Take(args.Length - 2));
            }
            else
            {
                targetName = null;
            }

            HandlePurchaseCommand(command, targetName, upgradeId, adoptedHero, settings, onSuccess, onFailure);
        }

        private void HandleListCommand(string type, Settings settings, Action<string> onSuccess, Action<string> onFailure)
        {
            if (settings == null)
            {
                onFailure("Settings not available");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Available Upgrades ===");

            if (type == "all" || type == "fief")
            {
                sb.AppendLine("\n[Fief Upgrades]");
                if (settings.FiefUpgrades != null && settings.FiefUpgrades.Count > 0)
                {
                    foreach (var upgrade in settings.FiefUpgrades)
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
                if (settings.ClanUpgrades != null && settings.ClanUpgrades.Count > 0)
                {
                    foreach (var upgrade in settings.ClanUpgrades)
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
                if (settings.KingdomUpgrades != null && settings.KingdomUpgrades.Count > 0)
                {
                    foreach (var upgrade in settings.KingdomUpgrades)
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

        private void HandleInfoCommand(string type, string name, Hero hero, Settings settings, Action<string> onSuccess, Action<string> onFailure)
        {
            switch (type)
            {
                case "fief":
                    ShowFiefInfo(name, hero, settings, onSuccess, onFailure);
                    break;
                case "clan":
                    ShowClanInfo(hero, settings, onSuccess, onFailure);
                    break;
                case "kingdom":
                    ShowKingdomInfo(hero, settings, onSuccess, onFailure);
                    break;
                default:
                    onFailure("Invalid type. Use 'fief', 'clan', or 'kingdom'");
                    break;
            }
        }

        private void ShowFiefInfo(string name, Hero hero, Settings settings, Action<string> onSuccess, Action<string> onFailure)
        {
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
                foreach (var upgradeId in upgrades)
                {
                    var upgrade = settings.FiefUpgrades.FirstOrDefault(u => u.ID == upgradeId);
                    if (upgrade != null)
                        sb.AppendLine($"  • {upgrade.Name}");
                }
            }

            onSuccess(sb.ToString());
        }

        private void ShowClanInfo(Hero hero, Settings settings, Action<string> onSuccess, Action<string> onFailure)
        {
            var clan = hero.Clan;
            if (clan == null)
            {
                onFailure($"Clan '{clan.Name}' not found");
                return;
            }

            var upgrades = UpgradeBehavior.Current?.GetClanUpgrades(clan) ?? new List<string>();
            var sb = new StringBuilder();
            sb.AppendLine($"=== {clan.Name} Upgrades ===");

            if (upgrades.Count == 0)
            {
                sb.AppendLine("No upgrades purchased yet");
            }
            else
            {
                sb.AppendLine("Active Upgrades:");
                foreach (var upgradeId in upgrades)
                {
                    var upgrade = settings.ClanUpgrades.FirstOrDefault(u => u.ID == upgradeId);
                    if (upgrade != null)
                        sb.AppendLine($"  • {upgrade.Name}");
                }
            }

            onSuccess(sb.ToString());
        }

        private void ShowKingdomInfo(Hero hero, Settings settings, Action<string> onSuccess, Action<string> onFailure)
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
                foreach (var upgradeId in upgrades)
                {
                    var upgrade = settings.KingdomUpgrades.FirstOrDefault(u => u.ID == upgradeId);
                    if (upgrade != null)
                        sb.AppendLine($"  • {upgrade.Name}");
                }
            }

            onSuccess(sb.ToString());
        }

        private void HandlePurchaseCommand(string type, string name, string upgradeId, Hero hero, Settings settings, Action<string> onSuccess, Action<string> onFailure)
        {
            switch (type)
            {
                case "fief":
                    PurchaseFiefUpgrade(name, upgradeId, hero, settings, onSuccess, onFailure);
                    break;
                case "clan":
                    PurchaseClanUpgrade(upgradeId, hero, settings, onSuccess, onFailure);
                    break;
                case "kingdom":
                    PurchaseKingdomUpgrade(upgradeId, hero, settings, onSuccess, onFailure);
                    break;
            }
        }

        private void PurchaseFiefUpgrade(string name, string upgradeId, Hero hero, Settings settings, Action<string> onSuccess, Action<string> onFailure)
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
                onFailure($"You don't have permission to upgrade {settlement.Name}");
                return;
            }

            if (!hero.IsClanLeader && !isKingdomLeader)
            {
                onFailure("Only clan leaders can purchase fief upgrades");
                return;
            }

            // Find upgrade
            var upgrade = settings.FiefUpgrades.FirstOrDefault(u => u.ID == upgradeId);
            if (upgrade == null)
            {
                onFailure($"Upgrade '{upgradeId}' not found");
                return;
            }

            // Check if already purchased
            if (UpgradeBehavior.Current?.HasFiefUpgrade(settlement, upgradeId) == true)
            {
                onFailure($"{settlement.Name} already has this upgrade");
                return;
            }

            // Check prerequisites
            if (!string.IsNullOrEmpty(upgrade.RequiredUpgradeID))
            {
                if (UpgradeBehavior.Current?.HasFiefUpgrade(settlement, upgrade.RequiredUpgradeID) != true)
                {
                    onFailure($"Requires upgrade '{upgrade.RequiredUpgradeID}' first");
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

        private void PurchaseClanUpgrade(string upgradeId, Hero hero, Settings settings, Action<string> onSuccess, Action<string> onFailure)
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
            var upgrade = settings.ClanUpgrades.FirstOrDefault(u => u.ID == upgradeId);
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

            // Check prerequisites
            if (!string.IsNullOrEmpty(upgrade.RequiredUpgradeID))
            {
                if (UpgradeBehavior.Current?.HasClanUpgrade(clan, upgrade.RequiredUpgradeID) != true)
                {
                    onFailure($"Requires upgrade '{upgrade.RequiredUpgradeID}' first");
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

        private void PurchaseKingdomUpgrade(string upgradeId, Hero hero, Settings settings, Action<string> onSuccess, Action<string> onFailure)
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
            var upgrade = settings.KingdomUpgrades.FirstOrDefault(u => u.ID == upgradeId);
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

            // Check prerequisites
            if (!string.IsNullOrEmpty(upgrade.RequiredUpgradeID))
            {
                if (UpgradeBehavior.Current?.HasKingdomUpgrade(kingdom, upgrade.RequiredUpgradeID) != true)
                {
                    onFailure($"Requires upgrade '{upgrade.RequiredUpgradeID}' first");
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
            return Settlement.All.FirstOrDefault(s =>
                s?.Name?.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) == true);
        }
    }
}