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

namespace BLTAdoptAHero
{
    [LocDisplayName("{=TESTING}Leaderboard"),
     LocDescription("{=TESTING}Shows hero or clan leaderboards"),
     UsedImplicitly]
    public class Leaderboard : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                onFailure("Invalid usage. Use: leaderboard hero | leaderboard clan");
                return;
            }

            var mode = context.Args.Trim().ToLower();

            switch (mode)
            {
                case "hero":
                    onSuccess(BuildHeroLeaderboard(adoptedHero));
                    break;

                case "clan":
                    onSuccess(BuildClanLeaderboard(adoptedHero));
                    break;

                default:
                    onFailure("Invalid subcommand. Use: leaderboard hero | leaderboard clan");
                    break;
            }
        }

        // --- Hero leaderboard ---
        private string BuildHeroLeaderboard(Hero userHero)
        {
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

                return $"Family: {string.Join(" ", top3)}";
            }

            var sb = new StringBuilder();
            sb.Append(BuildStatLine("KILLS", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalKills)));
            sb.Append(" | ");
            sb.Append(BuildStatLine("DEATHS", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalDeaths)));
            sb.Append(" | ");
            sb.Append(BuildStatLine("BATTLES", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.Battles)));
            sb.Append(" | ");
            sb.Append(BuildStatLine("TOURNAMENTS", h => BLTAdoptAHeroCampaignBehavior.Current.GetAchievementTotalStat(h, AchievementStatsData.Statistic.TotalTournamentFinalWins)));
            sb.Append(" | ");
            sb.Append(BuildFamilyLine());

            return sb.ToString();
        }

        // --- Clan leaderboard ---
        private string BuildClanLeaderboard(Hero userHero)
        {
            if (userHero.Clan == null || !userHero.Clan.Leader.IsAdopted())
                return "You have no clan.";

            var bltClans = Clan.All
                .Where(c => c != null && c.Leader != null && c.Leader.IsAdopted())
                .ToList();

            // Shortens numbers for display (Gold)
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
                    .OrderByDescending(x => x.Value)
                    .ToList();

                var top3 = sorted.Take(3)
                    .Select((x, i) => $"{i + 1}-{x.Clan.Name}({(label == "GOLD" ? FormatGold(x.Value) : x.Value.ToString())})")
                    .ToList();

                int userRank = sorted.FindIndex(x => x.Clan == userHero.Clan) + 1;
                if (userRank > 3)
                {
                    var userValue = sorted[userRank - 1].Value;
                    top3.Add($"{userRank}-{userHero.Clan.Name}({(label == "GOLD" ? FormatGold(userValue) : userValue.ToString())})");
                }

                return $"{label}: {string.Join(" ", top3)}";
            }

            var sb = new StringBuilder();
            sb.Append(BuildClanStatLine("POWER", c => (int)c.CurrentTotalStrength));
            sb.Append(" | ");
            sb.Append(BuildClanStatLine("RENOWN", c => (int)c.Renown));
            sb.Append(" | ");
            sb.Append(BuildClanStatLine("MEMBERS", c => c.Heroes.Count));
            sb.Append(" | ");
            sb.Append(BuildClanStatLine("FIEFS", c => c.Fiefs.Count));
            sb.Append(" | ");
            sb.Append(BuildClanStatLine("GOLD", c => c.Gold));

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
