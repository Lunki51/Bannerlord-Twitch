using System;
using System.Linq;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using BLTAdoptAHero.Actions.Upgrades;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using System.ComponentModel;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("Capital"),
     LocDescription("Designate a clan capital settlement for bonus effects and capital-only upgrades"),
     UsedImplicitly]
    public class CapitalAction : HeroCommandHandlerBase
    {
        public class Settings : IDocumentable
        {
            [LocDisplayName("Enabled"), LocCategory("General", "General"), PropertyOrder(1), UsedImplicitly]
            public bool Enabled { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator gen)
            {
                gen.P($"<strong>Enabled:</strong> {Enabled}");
                GlobalCommonConfig.Get()?.CapitalConfig?.GenerateDocumentation(gen);
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero hero, ReplyContext ctx, object config,
            Action<string> ok, Action<string> fail)
        {
            if (config is not Settings s) { fail("Invalid config"); return; }
            if (hero == null) { fail(AdoptAHero.NoHeroMessage); return; }
            if (!s.Enabled) { fail("The capital system is disabled"); return; }
            if (Mission.Current != null) { fail("Cannot use this during a mission"); return; }

            var cfg = GlobalCommonConfig.Get()?.CapitalConfig;
            if (cfg == null || !cfg.Enabled) { fail("Capital system is not configured"); return; }
            if (hero.Clan == null) { fail("You are not in a clan"); return; }
            if (!hero.IsClanLeader) { fail("Only clan leaders can manage the capital"); return; }

            if (ctx.Args.IsEmpty()) { ShowInfo(hero, cfg, ok, fail); return; }

            var args = ctx.Args.Split(' ');
            var cmd = args[0].ToLowerInvariant();
            var rest = string.Join(" ", args.Skip(1)).Trim();

            switch (cmd)
            {
                case "set": HandleSet(hero, rest, cfg, ok, fail); break;
                case "cancel": HandleCancel(hero, cfg, ok, fail); break;
                case "info": ShowInfo(hero, cfg, ok, fail); break;
                case "list":
                    if (cfg.AllowListCommand) ShowList(hero, ok, fail);
                    else fail("The list command is disabled");
                    break;
                default:
                    fail("Usage: capital [set <settlement>] [cancel] [info] [list]");
                    break;
            }
        }

        // ── Permission check ──────────────────────────────────────────────────
        private static bool HasPermission(Hero hero, CapitalConfig cfg, out string err)
        {
            err = null;
            bool independent = hero.Clan.Kingdom == null;
            bool isRuler = !independent && hero.Clan.Kingdom.Leader == hero;

            if (independent && cfg.AllowIndependentClans) return true;
            if (!independent && (!cfg.RequireRulingClan || isRuler)) return true;

            err = independent
                ? "Independent clans are not permitted to set a capital"
                : "Only the kingdom ruler can manage the capital";
            return false;
        }

        // ── Commands ──────────────────────────────────────────────────────────
        private void HandleSet(Hero hero, string settlementName, CapitalConfig cfg,
            Action<string> ok, Action<string> fail)
        {
            if (!HasPermission(hero, cfg, out var perm)) { fail(perm); return; }
            if (string.IsNullOrWhiteSpace(settlementName)) { fail("Usage: capital set <settlement name>"); return; }

            var bh = CapitalBehavior.Current;
            if (bh == null) { fail("Capital system not initialized"); return; }

            // Find the target settlement (town or castle)
            var s = Settlement.All.FirstOrDefault(
                x => x.Town != null && x.Name.ToString().Equals(settlementName, StringComparison.OrdinalIgnoreCase));
            if (s == null) { fail($"Town or castle '{settlementName}' not found"); return; }
            if (s.OwnerClan != hero.Clan) { fail($"You do not own {s.Name}"); return; }

            if (bh.IsInCooldown(hero.Clan))
            {
                fail($"Capital is on cooldown for {(int)Math.Ceiling(bh.GetCooldownDaysLeft(hero.Clan))} more day(s)");
                return;
            }

            // ── If no capital exists yet — initial set ────────────────────────
            if (!bh.HasActiveCapital(hero.Clan) && !bh.IsTransferPending(hero.Clan))
            {
                int gold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero);
                if (gold < cfg.SetCost) { fail(Naming.NotEnoughGold(cfg.SetCost, gold)); return; }
                if (!bh.SetCapital(hero.Clan, s, out var e)) { fail(e); return; }
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -cfg.SetCost, true);
                ok($"Capital established at {s.Name}! Cost: {cfg.SetCost}{Naming.Gold}");
                Log.ShowInformation($"{hero.Name} established {s.Name} as their capital!", hero.CharacterObject, Log.Sound.Horns2);
                return;
            }

            // ── Transfer to a new capital ─────────────────────────────────────
            // If a transfer is already in-flight, cancel it first (gold not refunded)
            if (bh.IsTransferPending(hero.Clan))
                bh.CancelTransfer(hero.Clan, out _);

            int g = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero);
            if (g < cfg.TransferCost) { fail(Naming.NotEnoughGold(cfg.TransferCost, g)); return; }
            if (!bh.BeginTransfer(hero.Clan, s, cfg.TransferDays, out var err2)) { fail(err2); return; }
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -cfg.TransferCost, true);
            ok($"Capital transfer to {s.Name} started! " +
               $"Completes in {cfg.TransferDays} days. " +
               $"Cost: {cfg.TransferCost}{Naming.Gold}. " +
               $"Old capital bonuses removed immediately.");
            Log.ShowInformation($"{hero.Name} is transferring their capital to {s.Name}!", hero.CharacterObject, Log.Sound.Horns2);
        }

        private void HandleCancel(Hero hero, CapitalConfig cfg, Action<string> ok, Action<string> fail)
        {
            if (!HasPermission(hero, cfg, out var perm)) { fail(perm); return; }
            var bh = CapitalBehavior.Current;
            if (bh == null) { fail("Capital system not initialized"); return; }
            if (!bh.CancelTransfer(hero.Clan, out var e)) { fail(e); return; }
            var cap = bh.GetCapital(hero.Clan);
            ok($"Transfer cancelled. Capital restored to {cap?.Name.ToString() ?? "none"}.");
        }

        // ── Info display ──────────────────────────────────────────────────────
        private void ShowInfo(Hero hero, CapitalConfig cfg, Action<string> ok, Action<string> fail)
        {
            var bh = CapitalBehavior.Current;
            if (bh == null) { fail("Capital system not initialized"); return; }
            var clan = hero.Clan;
            var sb = new StringBuilder();
            sb.AppendLine($"=== {clan.Name} Capital ===");

            if (bh.IsInCooldown(clan))
            {
                sb.AppendLine($"Status: COOLDOWN — {(int)Math.Ceiling(bh.GetCooldownDaysLeft(clan))} day(s) remaining");
                sb.AppendLine("Capital bonuses are suspended until cooldown expires.");
            }
            else if (bh.IsTransferPending(clan))
            {
                var tgt = bh.GetPendingTarget(clan);
                sb.AppendLine($"Status: TRANSFERRING → {tgt?.Name} | {(int)Math.Ceiling(bh.GetTransferDaysLeft(clan))} day(s) remaining");
                sb.AppendLine("Bonuses suspended during transfer. If target is lost, cooldown begins.");
            }
            else
            {
                var cap = bh.GetCapital(clan);
                if (cap == null)
                    sb.AppendLine("Status: No capital set.  Use: capital set <settlement>");
                else
                {
                    sb.AppendLine($"Status: ACTIVE | Capital: {cap.Name}");
                    AppendBonusSummary(sb, cfg);
                }
            }

            // Show owned capital upgrades
            var owned = bh.GetCapitalUpgrades(clan);
            if (owned.Count > 0)
            {
                var gc = GlobalCommonConfig.Get();
                sb.AppendLine("Capital-Only Upgrades:");
                foreach (var uid in owned)
                {
                    var up = gc?.FiefUpgrades?.FirstOrDefault(u => u.ID == uid);
                    sb.AppendLine($"  • {up?.Name ?? uid}");
                }
            }

            ok(sb.ToString());
        }

        private static void AppendBonusSummary(StringBuilder sb, CapitalConfig cfg)
        {
            static string S(float v) => v > 0 ? $"+{v}" : v.ToString();
            sb.AppendLine("Base Capital Bonuses:");
            if (cfg.LoyaltyDailyFlat != 0) sb.AppendLine($"  Loyalty: {S(cfg.LoyaltyDailyFlat)}/day");
            if (cfg.LoyaltyDailyPercent != 0) sb.AppendLine($"  Loyalty: {S(cfg.LoyaltyDailyPercent)}%/day");
            if (cfg.ProsperityDailyFlat != 0) sb.AppendLine($"  Prosperity: {S(cfg.ProsperityDailyFlat)}/day");
            if (cfg.ProsperityDailyPercent != 0) sb.AppendLine($"  Prosperity: {S(cfg.ProsperityDailyPercent)}%/day");
            if (cfg.SecurityDailyFlat != 0) sb.AppendLine($"  Security: {S(cfg.SecurityDailyFlat)}/day");
            if (cfg.SecurityDailyPercent != 0) sb.AppendLine($"  Security: {S(cfg.SecurityDailyPercent)}%/day");
            if (cfg.MilitiaDailyFlat != 0) sb.AppendLine($"  Militia: {S(cfg.MilitiaDailyFlat)}/day");
            if (cfg.MilitiaDailyPercent != 0) sb.AppendLine($"  Militia: {S(cfg.MilitiaDailyPercent)}%/day");
            if (cfg.FoodDailyFlat != 0) sb.AppendLine($"  Food: {S(cfg.FoodDailyFlat)}/day");
            if (cfg.FoodDailyPercent != 0) sb.AppendLine($"  Food: {S(cfg.FoodDailyPercent)}%/day");
            if (cfg.TaxIncomeFlat != 0) sb.AppendLine($"  Tax: {S(cfg.TaxIncomeFlat)}{Naming.Gold}/day");
            if (cfg.TaxIncomePercent != 0) sb.AppendLine($"  Tax: {S(cfg.TaxIncomePercent)}%");
            if (cfg.GarrisonCapacityBonus != 0) sb.AppendLine($"  Garrison Cap: {S(cfg.GarrisonCapacityBonus)}");
            if (cfg.HearthDaily != 0) sb.AppendLine($"  Hearth: {S(cfg.HearthDaily)}/day");
            if (cfg.PartySizeBonus != 0) sb.AppendLine($"  Party Size: {S(cfg.PartySizeBonus)}");
            if (cfg.PartySpeedBonus != 0) sb.AppendLine($"  Party Speed: {S(cfg.PartySpeedBonus)}");
            if (cfg.RenownDaily != 0) sb.AppendLine($"  Renown: {S(cfg.RenownDaily)}/day");
            if (cfg.InfluenceDaily != 0) sb.AppendLine($"  Influence: {S(cfg.InfluenceDaily)}/day");
        }

        private static void ShowList(Hero hero, Action<string> ok, Action<string> fail)
        {
            var gc = GlobalCommonConfig.Get();
            var caps = gc?.FiefUpgrades?.Where(u => u.CapitalOnly).ToList();
            if (caps == null || caps.Count == 0) { ok("No capital-only upgrades are configured"); return; }
            var bh = CapitalBehavior.Current;
            var owned = bh?.GetCapitalUpgrades(hero.Clan) ?? new System.Collections.Generic.List<string>();
            var sb = new StringBuilder();
            sb.AppendLine("=== Capital-Only Upgrades ===");
            foreach (var u in caps)
            {
                bool have = owned.Contains(u.ID, StringComparer.OrdinalIgnoreCase);
                sb.AppendLine($"  [{(have ? "✓" : " ")}] {u.ID}: {u.Name} — {u.GetCostString()}");
                sb.AppendLine($"      {u.Description}");
                if (!string.IsNullOrEmpty(u.RequiredUpgradeID))
                    sb.AppendLine($"      Requires: {u.RequiredUpgradeID}");
            }
            ok(sb.ToString());
        }
    }
}