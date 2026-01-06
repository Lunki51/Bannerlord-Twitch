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

            // REMOVE all the ObservableCollection<FiefUpgrade>, etc.
            // These are now in GlobalCommonConfig

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value($"<strong>Enabled:</strong> {(Enabled ? "Yes" : "No")}");
                generator.Value($"<strong>Allow List Command:</strong> {(AllowListCommand ? "Yes" : "No")}");
                generator.Value($"<strong>Kingdom Leaders Can Upgrade Fiefs:</strong> {(AllowKingdomLeadersForFiefs ? "Yes" : "No")}");
                generator.Value($"<strong>Any Clan Member Can Upgrade Clan:</strong> {(AllowAnyClanMemberForClanUpgrades ? "Yes" : "No")}");

                // Access upgrade definitions from GlobalCommonConfig
                var config = GlobalCommonConfig.Get();

                generator.PropertyValuePair("Fief Upgrades Available", (config.FiefUpgrades?.Count ?? 0).ToString());
                if (config.FiefUpgrades != null && config.FiefUpgrades.Count > 0)
                {
                    foreach (var upgrade in config.FiefUpgrades)
                    {
                        generator.Value($"  • {upgrade.Name} ({upgrade.ID}) - {upgrade.GoldCost}{Naming.Gold}");
                    }
                }

                generator.PropertyValuePair("Clan Upgrades Available", (config.ClanUpgrades?.Count ?? 0).ToString());
                if (config.ClanUpgrades != null && config.ClanUpgrades.Count > 0)
                {
                    foreach (var upgrade in config.ClanUpgrades)
                    {
                        generator.Value($"  • {upgrade.Name} ({upgrade.ID}) - {upgrade.GoldCost}{Naming.Gold}");
                    }
                }

                generator.PropertyValuePair("Kingdom Upgrades Available", (config.KingdomUpgrades?.Count ?? 0).ToString());
                if (config.KingdomUpgrades != null && config.KingdomUpgrades.Count > 0)
                {
                    foreach (var upgrade in config.KingdomUpgrades)
                    {
                        generator.Value($"  • {upgrade.Name} ({upgrade.ID}) - {upgrade.GetCostString()}");
                    }
                }
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
                if (args.Length < 3)
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
                // Get upgrade objects and sort alphabetically by name
                var sortedUpgrades = upgrades
                    .Select(upgradeId => globalConfig.FiefUpgrades.FirstOrDefault(u => u.ID == upgradeId))
                    .Where(upgrade => upgrade != null)
                    .OrderBy(upgrade => upgrade.Name)
                    .ToList();

                foreach (var upgrade in sortedUpgrades)
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
            var sb = new StringBuilder();
            sb.AppendLine($"=== {clan.Name} Upgrades ===");

            if (upgrades.Count == 0)
            {
                sb.AppendLine("No upgrades purchased yet");
            }
            else
            {
                sb.AppendLine("Active Upgrades:");
                // Get upgrade objects and sort alphabetically by name
                var sortedUpgrades = upgrades
                    .Select(upgradeId => globalConfig.ClanUpgrades.FirstOrDefault(u => u.ID == upgradeId))
                    .Where(upgrade => upgrade != null)
                    .OrderBy(upgrade => upgrade.Name)
                    .ToList();

                foreach (var upgrade in sortedUpgrades)
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
                // Get upgrade objects and sort alphabetically by name
                var sortedUpgrades = upgrades
                    .Select(upgradeId => globalConfig.KingdomUpgrades.FirstOrDefault(u => u.ID == upgradeId))
                    .Where(upgrade => upgrade != null)
                    .OrderBy(upgrade => upgrade.Name)
                    .ToList();

                foreach (var upgrade in sortedUpgrades)
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