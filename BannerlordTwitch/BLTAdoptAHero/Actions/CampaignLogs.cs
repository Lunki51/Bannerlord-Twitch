using System;
using System.Linq;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BannerlordTwitch.Helpers;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using BLTAdoptAHero;
using BLTAdoptAHero.Behaviors;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=TESTING}CampaignLogs"),
     LocDescription("{=TESTING}Logs with relevant info"),
     UsedImplicitly]
    public class CampaignLogs : HeroCommandHandlerBase
    {
        public static Settings CurrentSettings { get; private set; } = new Settings();
        [CategoryOrder("General", 0)]
        public class Settings : IDocumentable
        {
            // General
            [LocDisplayName("{=TESTING}Hero"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=TESTING}Maximum logs per hero"),
             PropertyOrder(1), UsedImplicitly]
            public int hLogs { get; set; } = 10;

            [LocDisplayName("{=TESTING}Clan"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=TESTING}Maximum logs per clan"),
             PropertyOrder(2), UsedImplicitly]
            public int cLogs { get; set; } = 10;

            [LocDisplayName("{=TESTING}Kingdom"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=TESTING}Maximum logs per kingdom"),
             PropertyOrder(3), UsedImplicitly]
            public int kLogs { get; set; } = 10;

            [LocDisplayName("{=TESTING}Fief"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=TESTING}Maximum logs per fief"),
             PropertyOrder(4), UsedImplicitly]
            public int fLogs { get; set; } = 10;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value($"Usage: !logs hero/clan (clan)/kingdom (kingdom)/fief (fief)");
            }

        }
        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }
            if (context.Args.Length == 0)
            {
                onFailure("invalid. Use !logs hero/clan (clan)/kingdom (kingdom)/fief (fief)");
                return;
            }
            var splitArgs = context.Args.Split(' ');
            var mode = splitArgs[0];
            string name = string.Join(" ", splitArgs.Skip(1));

            switch (mode)
            {
                case "hero":
                    HandleHeroLogs(adoptedHero, onSuccess, onFailure);
                    break;
                case "clan":
                    HandleClanLogs(adoptedHero, name, onSuccess, onFailure);
                    break;
                case "kingdom":
                    HandleKingdomLogs(adoptedHero, name, onSuccess, onFailure);
                    break;
                case "fief":
                    HandleFiefLogs(name, onSuccess, onFailure);
                    break;
                default:
                    onFailure("invalid. Use !logs hero/clan (clan)/kingdom (kingdom)/fief (fief)");
                    break;
            }

        }

        private void HandleHeroLogs(Hero hero, Action<string> onSuccess, Action<string> onFailure)
        {
            var logsBehavior = Campaign.Current.GetCampaignBehavior<BLTLogsBehavior>();

            if (logsBehavior == null)
            {
                onFailure("Logs behavior not found.");
                return;
            }

            var dict = logsBehavior.heroLogs._heroLogs;

            if (!dict.TryGetValue(hero.StringId, out var logs) || logs.Count == 0)
            {
                onFailure("No logs found for this hero.");
                return;
            }

            Reply(hero.Name.ToString(), logs, onSuccess);
        }

        private void HandleClanLogs(Hero adoptedHero, string filter, Action<string> onSuccess, Action<string> onFailure)
        {
            var logsBehavior = Campaign.Current.GetCampaignBehavior<BLTLogsBehavior>();

            if (logsBehavior == null)
            {
                onFailure("Logs behavior not found.");
                return;
            }

            Clan desiredClan = adoptedHero.Clan;
            if (string.IsNullOrWhiteSpace(filter) && desiredClan == null)
            {
                onFailure("{=DSNx7CFT}Need clan name".Translate());
                return;
            }
            else if (!string.IsNullOrWhiteSpace(filter))
            {
                desiredClan = Clan.All.Where(k => !k.IsEliminated).FirstOrDefault(c =>
                c.Name.ToString().ToLower() == filter.ToLower()) ?? Clan.All.FirstOrDefault(c =>
                c.Name.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (desiredClan == null)
            {
                onFailure($"Could not find a clan with the name {filter}");
                return;
            }


            var dict = logsBehavior.clanLogs._clanLogs;
            if (!dict.TryGetValue(desiredClan.StringId, out var logs) || logs.Count == 0)
            {
                onFailure("No logs found for this clan.");
                return;
            }

            Reply(desiredClan.Name.ToString(), logs, onSuccess);
        }

        private void HandleKingdomLogs(Hero adoptedHero, string filter, Action<string> onSuccess, Action<string> onFailure)
        {
            var logsBehavior = Campaign.Current.GetCampaignBehavior<BLTLogsBehavior>();

            if (logsBehavior == null)
            {
                onFailure("Logs behavior not found.");
                return;
            }

            Kingdom desiredKingdom = adoptedHero?.Clan?.Kingdom;
            if (string.IsNullOrWhiteSpace(filter) && desiredKingdom == null)
            {
                onFailure("{=DSNx7CFT}Need kingdom name".Translate());
                return;
            }
            else if (!string.IsNullOrWhiteSpace(filter))
            {
                desiredKingdom = Kingdom.All.Where(k => !k.IsEliminated).FirstOrDefault(c =>
                c.Name.ToString().ToLower() == filter.ToLower()) ?? Kingdom.All.FirstOrDefault(c =>
                c.Name.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (desiredKingdom == null)
            {
                onFailure($"Could not find a kingdom with the name {filter}");
                return;
            }



            var dict = logsBehavior.kingdomLogs._kingdomLogs;

            if (!dict.TryGetValue(desiredKingdom.StringId, out var logs) || logs.Count == 0)
            {
                onFailure("No logs found for this kingdom.");
                return;
            }

            Reply(desiredKingdom.Name.ToString(), logs, onSuccess);
        }

        private void HandleFiefLogs(string filter, Action<string> onSuccess, Action<string> onFailure)
        {
            var logsBehavior = Campaign.Current.GetCampaignBehavior<BLTLogsBehavior>();

            if (logsBehavior == null)
            {
                onFailure("Logs behavior not found.");
                return;
            }

            if (string.IsNullOrWhiteSpace(filter))
            {
                onFailure("Need fief name");
                return;
            }

            var desiredFief = Town.AllFiefs.FirstOrDefault(c =>
                c.Name.ToString().ToLower() == filter.ToLower());
            if (desiredFief == null)
            {
                desiredFief = Town.AllFiefs.FirstOrDefault(c =>
                c.Name.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (desiredFief == null)
            {
                onFailure($"Could not find a fief with the name {filter}");
                return;
            }


            var dict = logsBehavior.fiefLogs._fiefLogs;

            if (!dict.TryGetValue(desiredFief.StringId, out var logs) || logs.Count == 0)
            {
                onFailure("No logs found for this fief.");
                return;
            }
            Reply(desiredFief.Name.ToString(), logs, onSuccess);
        }

        private void Reply(string name, List<string> logs, Action<string> onSuccess)
        {
            const int maxLength = 500;
            string prefix = $"{name}:";
            string currentMessage = prefix;

            foreach (var log in logs)
            {
                string separator = currentMessage == prefix ? "" : " | ";
                string nextEntry = separator + log;

                // Check if adding this entry would exceed limit
                if (currentMessage.Length + nextEntry.Length > maxLength)
                {
                    // Send current message and start a new one
                    if (currentMessage != prefix)
                    {
                        onSuccess(currentMessage);
                    }
                    currentMessage = prefix + log;
                }
                else
                {
                    currentMessage += nextEntry;
                }
            }

            // Send remaining message
            if (currentMessage != prefix)
            {
                onSuccess(currentMessage);
            }
        }
    }
}
