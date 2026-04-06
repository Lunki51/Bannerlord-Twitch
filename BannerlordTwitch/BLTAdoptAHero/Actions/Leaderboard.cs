using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BLTAdoptAHero.Achievements;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using JetBrains.Annotations;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=TESTING}Leaderboard"),
     LocDescription("{=TESTING}Shows hero or clan leaderboards"),
     UsedImplicitly]
    public class Leaderboard : HeroCommandHandlerBase
    {
        public class Settings : IDocumentable
        {
            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value($"Usage: !leaderboard hero (kills|deaths|battles|summons|attacks|tournaments|"/*1H-AXE|1H-MACE|1H-POLE|1H-SWORD|2H-AXE|2H-MACE|2H-POLE|2H-SWORD|BOW|DAGGER|JAVELIN|PICK|SLING|STONE|THROW-AXE|THROW-KNIFE|XBOW|*/+ "family)" +
                    " or !leaderboard clan (power|renown|members|dead|fiefs|gold|party|merc|prosperity)");
            }
        }
        public override Type HandlerConfigType => typeof(Settings);
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            string[] args = context.Args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0)
            {
                onFailure("Invalid usage. Use: leaderboard hero | leaderboard clan");
                return;
            }

            var mode = args[0].ToLower();
            string[] filter = args.Skip(1).ToArray();

            switch (mode)
            {
                case "hero":
                    onSuccess(BuildHeroLeaderboard(adoptedHero, filter));
                    break;

                case "clan":
                    onSuccess(BuildClanLeaderboard(adoptedHero, filter));
                    break;

                default:
                    onFailure("Invalid subcommand. Use: leaderboard hero | leaderboard clan");
                    break;
            }
        }

        // --- Hero leaderboard ---
        private string BuildHeroLeaderboard(Hero userHero, string[] filter)
        {
            if (filter.Length == 0)
            {
                return "kills|deaths|battles|summons|attacks|tournaments|"/*1H-AXE|1H-MACE|1H-POLE|1H-SWORD|2H-AXE|2H-MACE|2H-POLE|2H-SWORD|BOW|DAGGER|JAVELIN|PICK|SLING|STONE|THROW-AXE|THROW-KNIFE|XBOW|*/+ "family";
            }
            var adoptedHeroes = BLTAdoptAHeroCampaignBehavior.GetAllAdoptedHeroes();

            string BuildStatLine(string label, Func<Hero, int> statFunc)
            {
                var sorted = adoptedHeroes
                    .Select(h => new { Hero = h, Value = statFunc(h) })
                    .OrderByDescending(x => x.Value)
                    .ToList();

                var top3 = sorted.Take(3).Select((x, i) => $"{i + 1}-@{x.Hero.Name}({x.Value})").ToList();

                int userRank = sorted.FindIndex(x => x.Hero == userHero) + 1;
                if (userRank > 3)
                {
                    int userValue = sorted[userRank - 1].Value;
                    top3.Add($"{userRank}-@{userHero.Name}({userValue})");
                }

                return $"{label}: {string.Join(" ", top3)}";
            }

            string BuildFamilyLine()
            {
                var sorted = adoptedHeroes
                    .Select(h => new { Hero = h, FamilySize = CountFamily(h) })
                    .OrderByDescending(x => x.FamilySize)
                    .ToList();

                var top3 = sorted.Take(3).Select((x, i) => $"{i + 1}-@{x.Hero.Name}({x.FamilySize})").ToList();

                int userRank = sorted.FindIndex(x => x.Hero == userHero) + 1;
                if (userRank > 3)
                {
                    int userFamily = sorted[userRank - 1].FamilySize;
                    top3.Add($"{userRank}-@{userHero.Name}({userFamily})");
                }

                return $"FAMILY: {string.Join(" ", top3)}";
            }

            var sb = new StringBuilder();
            var userClass = userHero?.GetClass() ?? null;
            var userClassId = userClass?.ID ?? Guid.Empty;

            // Map filter strings to functions
            var statLines = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "KILLS", () => BuildStatLine("KILLS", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalKills)) },
                { "DEATHS", () => BuildStatLine("DEATHS", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalDeaths)) },
                { "BATTLES", () => BuildStatLine("BATTLES", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.Battles)) },
                { "SUMMONS", () => BuildStatLine("SUMMONS", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.Summons)) },
                { "ATTACKS", () => BuildStatLine("ATTACKS", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.Attacks)) },
                { "TOURNAMENTS", () => BuildStatLine("TOURNAMENTS", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalTournamentFinalWins)) },
                /*{ "1H-AXE", () => BuildStatLine("1H-AXE", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.Total1HAxeKills)) },
                { "1H-MACE", () => BuildStatLine("1H-MACE", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.Total1HMaceKills)) },
                { "1H-POLE", () => BuildStatLine("1H-POLE", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.Total1HPoleKills)) },
                { "1H-SWORD", () => BuildStatLine("1H-SWORD", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.Total1HSwordKills)) },
                { "2H-AXE", () => BuildStatLine("2H-AXE", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.Total2HAxeKills)) },
                { "2H-MACE", () => BuildStatLine("2H-MACE", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.Total2HMaceKills)) },
                { "2H-POLE", () => BuildStatLine("2H-POLE", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.Total2HPoleKills)) },
                { "2H-SWORD", () => BuildStatLine("2H-SWORD", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.Total2HSwordKills)) },
                { "BOW", () => BuildStatLine("BOW", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalBowKills)) },
                { "DAGGER", () => BuildStatLine("DAGGER", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalDaggerKills)) },
                { "JAVELIN", () => BuildStatLine("JAVELIN", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalJavelinKills)) },
                { "PICK", () => BuildStatLine("PICK", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalPickKills)) },
                { "SLING", () => BuildStatLine("SLING", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalSlingKills)) },
                { "STONE", () => BuildStatLine("STONE", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalStoneKills)) },
                { "THROW-AXE", () => BuildStatLine("THROW-AXE", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalThrowAxeKills)) },
                { "THROW-KNIFE", () => BuildStatLine("THROW-KNIFE", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalThrowKnifeKills)) },
                { "XBOW", () => BuildStatLine("XBOW", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalXBowKills)) },*/
                { "FAMILY", () => BuildFamilyLine() },
                { "CLASS", () => (userClass == null ? "Your hero is classless" : BuildStatLine($"{userClass.Name}", h => BLTAdoptAHeroCampaignBehavior.Current?.GetAchievementClassStat(h, userClassId, AchievementStatsData.Statistic.TotalKills) ?? 0)) }
            };

            // Append only the lines requested in filter, preserving order
            var linesToAppend = filter
                .Where(f => statLines.ContainsKey(f))
                .Select(f => statLines[f]())
                .ToList();

            sb.Append(string.Join(" | ", linesToAppend));

            return sb.ToString();
        }

        // --- Clan leaderboard ---
        private string BuildClanLeaderboard(Hero userHero, string[] filter)
        {
            if (filter.Length == 0)
            {
                return "power|renown|members|dead|fiefs|gold|party|merc|prosperity";
            }
            if (userHero.Clan == null || !userHero.Clan.Leader.IsAdopted())
                return "You have no clan.";

            var bltClans = Clan.All
                .Where(c => c != null && c.Leader != null && c.Leader.IsAdopted())
                .ToList();

            string FormatGold(int value)
            {
                return value >= 1_000_000 ? $"{value / 1_000_000D:0.#}M"
                     : value >= 1_000 ? $"{value / 1_000D:0.#}K"
                     : value.ToString();
            }

            string BuildClanStatLine(string label, Func<Clan, int> statFunc)
            {
                var sorted = bltClans
                    .Select(c => new { Clan = c, Value = statFunc(c) })
                    .Where(x => x.Value >= 0)
                    .OrderByDescending(x => x.Value)
                    .ToList();

                var top3 = sorted.Take(3)
                    .Select((x, i) => $"{i + 1}-{x.Clan.Name}({(label == "GOLD" ? FormatGold(x.Value) : x.Value.ToString())})")
                    .ToList();

                int userRank = sorted.FindIndex(x => x.Clan == userHero.Clan);
                if (userRank >= 0)
                {
                    userRank += 1;
                    if (userRank > 3)
                    {
                        var userValue = sorted[userRank - 1].Value;
                        top3.Add($"{userRank}-{userHero.Clan.Name}({(label == "GOLD" ? FormatGold(userValue) : userValue.ToString())})");
                    }
                }

                return $"{label}: {string.Join(" ", top3)}";
            }

            var sb = new StringBuilder();

            // Map filter strings to functions
            var statLines = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "POWER", () => BuildClanStatLine("POWER", c => (int)c.CurrentTotalStrength) },
                { "RENOWN", () => BuildClanStatLine("RENOWN", c => (int)c.Renown) },
                { "MEMBERS", () => BuildClanStatLine("MEMBERS", c => c.Heroes.Where(h => h.IsAlive).ToList().Count) },
                { "DEAD", () => BuildClanStatLine("DEAD", c => c.Heroes.Where(h => h.IsDead).ToList().Count) },
                { "FIEFS", () => BuildClanStatLine("FIEFS", c => c.Fiefs.Count) },
                { "GOLD", () => BuildClanStatLine("GOLD", c => c.Gold) },
                { "PARTY", () => BuildClanStatLine("PARTY", c => c.WarPartyComponents.Where(p => p != null && p.Party != null).Select(p => (int)p.Party.MemberRoster.TotalManCount).DefaultIfEmpty(0).Max()) },
                { "MERC", () =>  (bltClans.Any(c => c.IsUnderMercenaryService) ? BuildClanStatLine("MERC", c => (c.IsUnderMercenaryService ? (int)c.CurrentTotalStrength : -1)) : "MERC: No clans currently under mercenary service")  },
                { "PROSPERITY", () => BuildClanStatLine("PROSPERITY", c => (int)c.Fiefs.Select(f => f.Prosperity).DefaultIfEmpty(0).Max()) }
            };

            var linesToAppend = filter
                .Where(f => statLines.ContainsKey(f))
                .Select(f => statLines[f]())
                .ToList();

            sb.Append(string.Join(" | ", linesToAppend));

            return sb.ToString();
        }

        private static int CountFamily(Hero hero)
        {
            int count = 0;
            if (hero.Spouse != null) count++;
            if (hero.Children != null) count += hero.Children.Count;
            if (hero.Children != null)
            {
                foreach (var c in hero.Children)
                    if (c.Children != null)
                        count += c.Children.Count; // grandchildren
            }
            return count;
        }
    }
}