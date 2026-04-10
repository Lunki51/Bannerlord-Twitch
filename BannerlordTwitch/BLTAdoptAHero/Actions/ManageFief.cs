using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Helpers;

namespace BLTAdoptAHero.Actions
{
    public class ManageFief : HeroCommandHandlerBase
    {
        private class Documentation : IDocumentable
        {
            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value("<strong>Description:</strong> Allows a clan leader to manage their fiefs. You can view fief info, set building projects, adjust gold boosts for construction, and view building explanations.\n\n");
                generator.Value("<strong>Usage:</strong>\n");
                generator.Value(@"• info <fief_name> - Shows detailed information about the specified fief.\n");
                generator.Value(@"• projects <fief_name> <building1> <building2> ... - Sets the building projects queue and daily project for the fief.\n");
                generator.Value(@"• gold <fief_name> <amount> - Changes the building boost (gold) for the fief.\n\n");
                generator.Value(@"• explanation <building_name> - Shows the description and effect of the specified building.\n\n");              
            }
        }

        public override Type HandlerConfigType => typeof(Documentation);
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            //if (config is not Settings settings) return;
            //var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }
            if (Mission.Current != null)
            {
                onFailure("{=MPTOZqMS}You cannot manage your clan, as a mission is active!".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=B86KnTcu}You are not in a clan".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("You are not a clan leader");
                return;
            }
            if (adoptedHero.Clan.Fiefs.Count == 0)
            {
                onFailure("Your clan has no fiefs");
                return;
            }

            var splitArgs = context.Args.Split(' ');
            var command = splitArgs[0];
            var fief = adoptedHero.Clan.Fiefs.FirstOrDefault(f => f.Name.ToString().ToLower() == splitArgs[1].ToLower()) ?? adoptedHero.Clan.Fiefs.FirstOrDefault(c =>
                 c.Name.ToString().IndexOf(splitArgs[1], StringComparison.OrdinalIgnoreCase) >= 0);

            string[] args = splitArgs.Skip(2).ToArray();

            switch (command.ToLower())
            {
                case "info":
                    FiefInfo(fief, onSuccess);
                    break;
                case "projects":
                    Project(fief, args, onSuccess, onFailure);
                    break;
                case "gold":
                    ChangeGold(adoptedHero, fief, args, onSuccess, onFailure);
                    break;
                case "explanation":
                    BuildingExp(args, onSuccess, onFailure);
                    break;
                //case "governor":
                //    Governor();
                //    break;
                default:
                    onFailure("invalid mode. Use info fief, projects fief buildings, gold fief amount");
                    break;
            }
        }

        private void FiefInfo(Town town, Action<string> onSuccess)
        {
            var name = town.Name;
            var buildings = town.Buildings.ToList();
            var active = town.BuildingsInProgress.ToList();
            int wall = town.GetWallLevel();
            var governor = town.Governor;
            int gold = town.BoostBuildingProcess;
            double bricks = Math.Round(town.Construction, 2);

            var buildingList = new StringBuilder();
            var dailyList = new StringBuilder();
            foreach (var build in buildings)
            {
                bool isActive = active.Contains(build);
                bool isdaily = build.BuildingType.IsDailyProject;

                if (isdaily)
                {

                    if (build.IsCurrentlyDefault)
                    {
                        int prio = active.Count + 1;
                        dailyList.Append($"(🔨{prio}){build.Name} - ");
                    }
                    else
                    {
                        dailyList.Append($"{build.Name} - ");
                    }
                }
                else
                {
                    int percent = (int)(build.BuildingProgress / build.GetConstructionCost()*100);
                    if (isActive)
                    {
                        int prio = active.IndexOf(build) + 1;
                        buildingList.Append($"(🔨{prio})");
                    }
                    buildingList.Append($"{build.Name}:LV{build.CurrentLevel},");
                    if (build.CurrentLevel == BuildingType.MaxLevel)
                        buildingList.Append("%100  • ");
                    else buildingList.Append($"%{percent} • ");

                }
            }

            string info = $@"{name} Info: Wall Level: {wall} - Governor: {(governor != null ? governor.Name.ToString() : "None")} - Bricks: {bricks}
            - Building boost: {gold}{Naming.Gold} - Buildings:{buildingList} - Daily projects:{dailyList}";


            onSuccess(info);
            return;
        }

        private void Project(Town town, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (args.Length == 0)
            {
                onFailure("Specify buildings");
                return;
            }
            var builds = town.Buildings;
            var queue = town.BuildingsInProgress;
            var oldDaily = queue.FirstOrDefault(b => b.BuildingType.IsDailyProject);

            List<Building> projects = new();
            Building newDaily = null;
            bool hasDaily = false;

            for (int i = 0; i < args.Length; i++)
            {
                Building match = null;

                // try 2-arg match first
                if (i + 1 < args.Length)
                {
                    string twoArg = args[i] + " " + args[i + 1];

                    match = GetBestMatch(twoArg, town);
                    if (match != null)
                    {
                        if (match.BuildingType.IsDailyProject && !hasDaily)
                        {
                            hasDaily = true;
                            newDaily = match;
                        }
                        else
                        {
                            projects.Add(match);
                            i++;
                            continue;
                        }
                    }
                }
                // single arg
                match = GetBestMatch(args[i], town);
                if (match != null)
                {
                    if (match.BuildingType.IsDailyProject && !hasDaily)
                    {
                        hasDaily = true;
                        newDaily = match;
                    }
                    else
                    {
                        projects.Add(match);
                    }
                }
            }

            if (projects.Count == 0 && newDaily == null)
            {
                onFailure("");
                return;
            }
            else
            {
                BuildingHelper.ChangeCurrentBuildingQueue(projects, town);
                if (newDaily != null)
                    BuildingHelper.ChangeDefaultBuilding(newDaily, town);

                onSuccess("Changed building projects");
                return;
            }          
        }
        Building GetBestMatch(string input, Town town)
        {
            input = input.ToLower();
            var candidates = town.Buildings.Where(b => b.Name.ToString().ToLower().StartsWith(input));           
            Building bestMatch = null;
            double bestScore = -1;

            foreach (var b in candidates)
            {
                if (b.CurrentLevel == BuildingType.MaxLevel)
                    continue;
                string name = b.Name.ToString().ToLower();

                // calculate simple score: length of input that matches at start or anywhere
                double score = 0;

                if (name.StartsWith(input))
                {
                    score = (double)input.Length / name.Length; // higher score if input covers more
                }
                else if (name.Contains(input))
                {
                    score = (double)input.Length / name.Length * 0.5; // lower score if it's inside but not start
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = b;
                }
            }

            return bestMatch;
        }

        private void ChangeGold(Hero hero, Town town, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (args.Length == 0)
            {
                onFailure("No amount specified");
                return;
            }
            if (!int.TryParse(args[0], out int amount))
            {
                onFailure("Invalid number format");
                return;
            }
            if (amount < 0)
            {
                onFailure("Cannot assign negative number");
                return;
            }
            int townGold = town.BoostBuildingProcess;
            int change = amount - townGold;
            if (hero.Gold < change)
            {
                onFailure("Not enough gold");
                return;
            }
            town.BoostBuildingProcess = amount;
            hero.Gold -= change;
            onSuccess($"Building boost for {town.Name} set to {amount}{Naming.Gold}. Hero {(change >= 0 ? "spent" : "received")} {Math.Abs(change)}{Naming.Gold}.");
        }

        private void Governor()
        {

        }

        private void BuildingExp(string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            string build = string.Join(" ", args);
            var buildingType = BuildingType.All.FirstOrDefault(b => b.Name.ToString().IndexOf(build, StringComparison.OrdinalIgnoreCase) >= 0);
            if (buildingType == null)
            {
                onFailure("Couldnt find building");
                return;
            }
            var explanation = buildingType.Explanation;
            onSuccess($"{buildingType.Name}: {explanation}");
        }
    }
}
