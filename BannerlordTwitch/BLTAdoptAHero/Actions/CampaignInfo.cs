using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using BannerlordTwitch.Helpers;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;


namespace BLTAdoptAHero
{
    [LocDisplayName("{=TESTING}CampaignInfo"),
     LocDescription("{=TESTING}Shows kingdom list, culture list, wars list and specific kingdom/war info"),
     UsedImplicitly]
    public class CampaignInfo : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context,
                    context.ArgsErrorMessage("{=TESTING}invalid mode (use kingdomlist, culturelist, warlist, kingdom (kingdom), war (kingdom)".Translate()));
                return;
            }

            var splitArgs = context.Args.Split(' ');
            var mode = splitArgs[0];
            var desiredName = string.Join(" ", splitArgs.Skip(1)).Trim();

            switch (mode)
            {
                case "kingdomlist":
                    {
                        string kingdomList = string.Join(", ", CampaignHelpers.MainFactions.Select(c => c.Name.ToString()));
                        onSuccess(kingdomList);
                        break;
                    }

                case "culturelist":
                    {
                        string cultureList = string.Join(", ", CampaignHelpers.MainCultures.Select(c => c.Name.ToString()));
                        onSuccess(cultureList);
                        break;
                    }

                case "kingdom":
                    {
                        if (string.IsNullOrWhiteSpace(desiredName))
                        {
                            onFailure("{=TESTING}Need kingdom name".Translate());
                        }
                        var desiredKingdom = CampaignHelpers.AllHeroes.Select(h => h?.Clan?.Kingdom).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);
                        if (desiredKingdom == null)
                        {
                            onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                            return;
                        }
                        else
                        {
                            bool war = false;
                            TextObject warList = new TextObject();
                            foreach (Kingdom k in Kingdom.All)
                            {
                                if ((desiredKingdom != k) && (desiredKingdom.IsAtWarWith(k)))
                                {
                                    war = true;
                                    warList.Value = warList.Value + k.Name.Value + ":" + ((int)k.TotalStrength).ToString() + ", ";
                                }
                            }
                            warList.Value = warList.Value.TrimEnd(',', ' ');

                            var clanStats = new StringBuilder();
                            clanStats.Append("{=SVlrGgol}Kingdom Name: {name} | ".Translate(("name", desiredKingdom.Name.ToString())));
                            clanStats.Append("{=Ss588M9l}Ruling Clan: {rulingClan} | ".Translate(("rulingClan", desiredKingdom.RulingClan.Name.ToString())));
                            clanStats.Append("{=T1FhhCH9}Clan Count: {clanCount} | ".Translate(("clanCount", desiredKingdom.Clans.Count.ToString())));
                            clanStats.Append("{=TUOmh7NY}Strength: {strength} | ".Translate(("strength", Math.Round(desiredKingdom.TotalStrength).ToString())));                            
                            if (war)
                                clanStats.Append("{=QadZnUKh}Wars: {wars} | ".Translate(("wars", warList.ToString())));
                            if (desiredKingdom.RulingClan.HomeSettlement.Name != null)
                                clanStats.Append("{=EXKsUpaU}Capital: {capital} | ".Translate(("capital", desiredKingdom.RulingClan.HomeSettlement.Name.ToString())));
                            if (desiredKingdom.Fiefs.Count >= 1)
                            {
                                int townCount = 0;
                                int castleCount = 0;
                                foreach (var settlement in desiredKingdom.Fiefs)
                                {
                                    if (!settlement.IsCastle)
                                    {
                                        townCount++;
                                    }
                                    if (settlement.IsCastle)
                                    {
                                        castleCount++;
                                    }
                                }
                                clanStats.Append("{=BwuFSJU1}Towns: {towns} | ".Translate(("towns", (object)townCount)));
                                clanStats.Append("{=0rMNNQ7R}Castles: {castles}".Translate(("castles", (object)castleCount)));
                            }
                            onSuccess("{stats}".Translate(("stats", clanStats.ToString())));
                        }

                        break;
                    }

                case "warlist":
                    {
                        List<string> kingdomId = new List<string>();
                        var warList = new StringBuilder();
                        for (int i = 0; i < Kingdom.All.Count; i++)
                        {
                            for (int j = 0; j < Kingdom.All.Count; j++)
                            {
                                if ((j != i) && !kingdomId.Contains(Kingdom.All[j].StringId))
                                {
                                    if (Kingdom.All[i].IsAtWarWith(Kingdom.All[j]))
                                    {
                                        warList.Append($"{Kingdom.All[i].Name}({Math.Round(Kingdom.All[i].TotalStrength)}) VS {Kingdom.All[j].Name}({Math.Round(Kingdom.All[j].TotalStrength)}) | ");
                                    }

                                }

                            }
                            kingdomId.Add(Kingdom.All[i].StringId);
                        }
                        warList.ToString().TrimEnd('|', ' ');
                        onSuccess($"{warList}");

                        break;
                    }

                case "war":
                    {
                        if (string.IsNullOrWhiteSpace(desiredName))
                        {
                            onFailure("{=TESTING}Need kingdom name".Translate());
                        }
                        var desiredKingdom = CampaignHelpers.AllHeroes.Select(h => h?.Clan?.Kingdom).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);
                        if (desiredKingdom == null)
                        {
                            onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                            return;
                        }
                        if (!Kingdom.All.Any(k => k.IsAtWarWith(desiredKingdom)))
                        {
                            onFailure("{=TESTING}{kingdom} is not at war".Translate(("kingdom", desiredKingdom.Name.ToString())));
                            return;
                        }
                        else
                        {
                            var warStats = new StringBuilder();

                            foreach (Kingdom k in Kingdom.All)
                            {
                                if (desiredKingdom != k && desiredKingdom.IsAtWarWith(k))
                                {
                                    StanceLink stance = desiredKingdom.GetStanceWith(k);

                                    int ourCasualties = stance.GetCasualties(desiredKingdom);
                                    int enemyCasualties = stance.GetCasualties(k);
                                    int ourRaids = stance.GetSuccessfulRaids(desiredKingdom);
                                    int enemyRaids = stance.GetSuccessfulRaids(k);
                                    int ourSieges = stance.GetSuccessfulSieges(desiredKingdom);
                                    int enemySieges = stance.GetSuccessfulSieges(k);

                                    warStats.Append("{=TESTING}{kingdom1} VS {kingdom2} = Casualties: {c1}/{c2} - Raids: {r1}/{r2} - Sieges: {s1}/{s2} - Started: {date}"
                                        .Translate(
                                            ("kingdom1", desiredKingdom.Name.ToString()),
                                            ("kingdom2", k.Name.ToString()),
                                            ("c1", ourCasualties.ToString()),
                                            ("c2", enemyCasualties.ToString()),
                                            ("r1", ourRaids.ToString()),
                                            ("r2", enemyRaids.ToString()),
                                            ("s1", ourSieges.ToString()),
                                            ("s2", enemySieges.ToString()),
                                            ("date", stance.WarStartDate.ToString())
                                        ));

                                    warStats.Append(" | "); // separator between wars
                                }
                            }

                            string result = warStats.ToString().TrimEnd('|', ' ');
                            onSuccess(result);
                        }

                       break;
                    }
                default:
                    {
                        onFailure("{=TESTING}invalid mode (use kingdomlist, culturelist, warlist, kingdom (kingdom), war (kingdom)".Translate());
                        break; 
                    }
            }
        }
    }
}