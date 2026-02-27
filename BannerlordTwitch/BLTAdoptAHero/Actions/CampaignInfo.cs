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
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Helpers;
using TaleWorlds.Core;
using BLTAdoptAHero.Models;
using static TaleWorlds.MountAndBlade.Launcher.Library.NativeMessageBox;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=OpptAAU9}CampaignInfo"),
     LocDescription("{=5FynPtK8}Shows kingdom list, culture list, wars list and specific kingdom/war info"),
     UsedImplicitly]
    public class CampaignInfo : ICommandHandler, IDocumentable
    {
        public Type HandlerConfigType => null;

        public void Execute(ReplyContext context, object config)
        {
            ExecuteInternal(context, config);
        }

        public void GenerateDocumentation(IDocumentationGenerator generator)
        {
            generator.Value("<strong>Modes:</strong>\n" +
                "kingdomlist, culturelist, warlist, " +
                "kingdom (kingdom), " +
                "war (kingdom), " +
                "fief (town/castle/village), " +
                "clan (kingdom/clan), " +
                "vassals (clan)"
                );
        }
        private void ExecuteInternal(ReplyContext context, object config)
        {
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context, "{=tk7R3uwg}invalid mode (use kingdomlist, culturelist, warlist, kingdom (kingdom), war (kingdom), fief (town/castle/village), clan (kingdom/clan))".Translate());
                return;
            }

            var splitArgs = context.Args.Split(' ');
            var mode = splitArgs[0];
            var desiredName = string.Join(" ", splitArgs.Skip(1)).Trim();

            switch (mode)
            {
                case "kingdomlist":
                    ActionManager.SendReply(context,
                        string.Join(", ", CampaignHelpers.MainFactions.Where(c => !c.IsEliminated).OrderByDescending(c => c.CurrentTotalStrength).Select(c => c.Name.ToString())));
                    break;

                case "culturelist":
                    ActionManager.SendReply(context,
                        string.Join(", ", CampaignHelpers.MainCultures.Select(c => c.Name.ToString())));
                    break;

                case "kingdom":
                    ShowKingdom(desiredName, context);
                    break;

                case "warlist":
                    ShowWarList(context);
                    break;
                case "wars":
                case "war":
                    ShowWar(desiredName, context);
                    break;

                case "armies":
                    ShowArmies(desiredName, context);
                    break;

                case "fief":
                    ShowFief(desiredName, context);
                    break;
                case "fiefs":
                    ShowFiefs(desiredName, context);
                    break;

                case "clan":
                    ShowClan(desiredName, context);
                    break;
                case "clans":
                    ShowClans(desiredName, context);
                    break;
                case "vassals":
                    ShowVassals(desiredName, context);
                    break;

                case "map":
                    ShowMap(desiredName, context);
                    break;

                case "time":
                case "date":
                    ShowTime(context);
                    break;

                case "player":
                    ShowPlayerSkills(context);
                    break;

                default:
                    ActionManager.SendReply(context,
                        "{=tk7R3uwg}invalid mode (use kingdomlist, culturelist, warlist, kingdom (kingdom), war (kingdom), fief (town/castle/village), fiefs (kingdom), clan (clan), clans (kingdom), vassals (clan))".Translate());
                    break;
            }
        }

        private void ShowKingdom(string desiredName, ReplyContext context)
        {
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                ActionManager.SendReply(context, "{=DSNx7CFT}Need kingdom name".Translate());
                return;
            }

            Kingdom desiredKingdom;

            // Streamer case check
            if (desiredName.ToLower() == "player")
            {
                desiredKingdom = Hero.MainHero.Clan?.Kingdom;
            }
            else 
            {
                desiredKingdom = Kingdom.All.Where(k => !k.IsEliminated).FirstOrDefault(c => c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (desiredKingdom == null)
            {
                ActionManager.SendReply(context,
                    "{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                return;
            }

            TradeAgreementsCampaignBehavior tradeBehavior = Campaign.Current.GetCampaignBehavior<TradeAgreementsCampaignBehavior>();
            bool war = false;
            bool ally = desiredKingdom.AlliedKingdoms.Count > 0;
            bool trade = false;
            bool tribute = false;
            TextObject warList = new TextObject("");
            TextObject tributeList = new TextObject("");
            TextObject tradeList = new TextObject("");
            foreach (Kingdom k in Kingdom.All)
            {
                if (desiredKingdom == k)
                    continue;

                StanceLink stance = desiredKingdom.GetStanceWith(k);
                if (tradeBehavior.HasTradeAgreement(desiredKingdom, k))
                {
                    var tradeDate = tradeBehavior.GetTradeAgreementEndDate(desiredKingdom, k);
                    int tradeDays = (int)(tradeDate - CampaignTime.Now).ToDays;
                    trade = true;
                    tradeList.Value += k.Name.Value + $"({tradeDays}), ";
                }
                if (desiredKingdom.IsAtWarWith(k))
                {
                    war = true;
                    warList.Value += k.Name.Value + ":" + ((int)k.CurrentTotalStrength).ToString() + ", ";
                }
                else
                {
                    int dailyTributeFromUs = stance.GetDailyTributeToPay(desiredKingdom);
                    int dailyTributeFromThem = stance.GetDailyTributeToPay(k);
                    int daysUs = k.GetStanceWith(desiredKingdom).GetRemainingTributePaymentCount();
                    int daysThem = stance.GetRemainingTributePaymentCount();


                    if (dailyTributeFromUs > 0)
                    {
                        tribute = true;
                        tributeList.Value +=
                            $"{k.Name}:-{dailyTributeFromUs}({daysUs}), ";
                    }
                    else if (dailyTributeFromThem > 0)
                    {
                        tribute = true;
                        tributeList.Value +=
                            $"{k.Name}:+{dailyTributeFromThem}({daysThem}), ";
                    }
                }
            }
            warList.Value = warList.Value.TrimEnd(',', ' ');
            var allyList = string.Join(", ", desiredKingdom.AlliedKingdoms.Select(k => k.Name.ToString()));
            tradeList.Value = tradeList.Value.TrimEnd(',', ' ');
            tributeList.Value = tributeList.Value.TrimEnd(',', ' ');

            var sb = new StringBuilder();
            sb.Append("{=SVlrGgol}Kingdom Name: {name} | ".Translate(("name", desiredKingdom.Name.ToString())));
            sb.Append("{=Ss588M9l}Ruling Clan: {rulingClan} | ".Translate(("rulingClan", desiredKingdom.RulingClan.Name.ToString())));
            sb.Append("{=T1FhhCH9}Clan Count: {count} | ".Translate(("count", desiredKingdom.Clans.Count)));
            sb.Append("{=TUOmh7NY}Strength: {strength} | ".Translate(("strength", Math.Round(desiredKingdom.CurrentTotalStrength).ToString())));
            if (war)
                sb.Append("{=QadZnUKh}Wars: {wars} | ".Translate(("wars", warList.ToString())));
            if (ally)
                sb.Append("{=TESTING}Alliances: {allies} | ".Translate(("allies", allyList)));
            if (trade)
                sb.Append("{=TESTING}Trades: {trade} | ".Translate(("trade", tradeList.ToString())));
            if (tribute)
                sb.Append("{=0GhTvF3K}Tribute: {tribute} | ".Translate(("tribute", tributeList.ToString())));
            if (desiredKingdom.RulingClan.HomeSettlement != null)
                sb.Append("{=EXKsUpaU}Capital: {capital}  ".Translate(("capital", desiredKingdom.RulingClan.HomeSettlement.Name.ToString())));
            if (desiredKingdom.Armies.Count >= 1)
                sb.Append($"| Armies: {desiredKingdom.Armies.Count} ");

            int towns = desiredKingdom.Fiefs.Count(f => !f.IsCastle);
            int castles = desiredKingdom.Fiefs.Count(f => f.IsCastle);
            sb.Append("{=BwuFSJU1}| Towns: {towns} | ".Translate(("towns", towns)));
            sb.Append("{=0rMNNQ7R}Castles: {castles}".Translate(("castles", castles)));

            ActionManager.SendReply(context, sb.ToString());
        }

        private void ShowWarList(ReplyContext context)
        {
            var seen = new HashSet<string>();
            var sb = new StringBuilder();
            foreach (var k1 in Kingdom.All)
            {
                foreach (var k2 in Kingdom.All)
                {
                    if (k1 == k2 || seen.Contains(k2.StringId))
                        continue;

                    if (k1.IsAtWarWith(k2))
                        sb.Append($"{k1.Name}({Math.Round(k1.CurrentTotalStrength)}) VS {k2.Name}({Math.Round(k2.CurrentTotalStrength)}) | ");
                }
                seen.Add(k1.StringId);
            }
            string warList = sb.ToString().Substring(0, sb.Length - 3);
            ActionManager.SendReply(context, warList);
        }

        private void ShowWar(string desiredName, ReplyContext context)
        {
            var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            Kingdom desiredKingdom = adoptedHero?.Clan?.Kingdom;
            if (string.IsNullOrWhiteSpace(desiredName) && desiredKingdom == null)
            {
                ActionManager.SendReply(context, "{=DSNx7CFT}Need kingdom name".Translate());
                return;
            }
            else if (!string.IsNullOrWhiteSpace(desiredName))
            {
                // Streamer case check
                if (desiredName.ToLower() == "player")
                {
                    desiredKingdom = Hero.MainHero.Clan?.Kingdom;
                }
                else
                {
                    desiredKingdom = Kingdom.All.Where(k => !k.IsEliminated).FirstOrDefault(c =>
                        c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }

            if (desiredKingdom == null)
            {
                ActionManager.SendReply(context,
                    "{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                return;
            }

            if (!Kingdom.All.Any(k => k.IsAtWarWith(desiredKingdom)))
            {
                ActionManager.SendReply(context,
                    "{=Lk1NDpuQ}{kingdom} is not at war".Translate(("kingdom", desiredKingdom.Name.ToString())));
                return;
            }
            var diplo = Campaign.Current.Models.DiplomacyModel;
            var sb = new StringBuilder();
            foreach (var k in Kingdom.All)
            {
                int p1 = Helpers.DiplomacyHelper.GetPrisonersOfWarTakenByFaction(desiredKingdom, k).Count;
                int p2 = Helpers.DiplomacyHelper.GetPrisonersOfWarTakenByFaction(k, desiredKingdom).Count;
                if (desiredKingdom != k && desiredKingdom.IsAtWarWith(k))
                {
                    var stance = desiredKingdom.GetStanceWith(k);
                    sb.Append("{=N9b5k8FR}{k1} VS {k2} = Casualties: {c1}/{c2} - Prisoners: {p1}/{p2} - Raids: {r1}/{r2} - Sieges: {s1}/{s2} - Progress: {pr1}/{pr2} Started: {date}"
                        .Translate(
                            ("k1", desiredKingdom.Name.ToString()),
                            ("k2", k.Name.ToString()),
                            ("c1", stance.GetCasualties(desiredKingdom)),
                            ("c2", stance.GetCasualties(k)),
                            ("p1", p1),
                            ("p2", p2),
                            ("r1", stance.GetSuccessfulRaids(desiredKingdom)),
                            ("r2", stance.GetSuccessfulRaids(k)),
                            ("s1", stance.GetSuccessfulSieges(desiredKingdom)),
                            ("s2", stance.GetSuccessfulSieges(k)),
                            ("pr1", diplo.GetWarProgressScore(desiredKingdom, k).RoundedResultNumber),
                            ("pr2", diplo.GetWarProgressScore(k, desiredKingdom).RoundedResultNumber),
                            ("date", stance.WarStartDate.ToString())
                        ));
                    sb.Append(" | ");
                }
            }

            ActionManager.SendReply(context, sb.ToString().TrimEnd('|', ' '));
        }

        private void ShowArmies(string desiredName, ReplyContext context)
        {
            var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            Kingdom desiredKingdom = adoptedHero?.Clan?.Kingdom;
            if (string.IsNullOrWhiteSpace(desiredName) && desiredKingdom == null)
            {
                ActionManager.SendReply(context, "{=DSNx7CFT}Need kingdom name".Translate());
                return;
            }
            else if (!string.IsNullOrWhiteSpace(desiredName))
            {
                // Streamer case check
                if (desiredName.ToLower() == "player")
                {
                    desiredKingdom = Hero.MainHero.Clan?.Kingdom;
                }
                else
                {
                    desiredKingdom = Kingdom.All.Where(k => !k.IsEliminated).FirstOrDefault(c =>
                        c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }

            if (desiredKingdom == null)
            {
                desiredKingdom = adoptedHero.Clan.Kingdom;
            }

            var armies = new StringBuilder();
            armies.Append("{=SVlrGgol}Kingdom Name: {name} | ".Translate(("name", desiredKingdom.Name.ToString())));
            armies.Append($"{desiredKingdom.Armies.Count} Armies | ");
            if (desiredKingdom.Armies.Count >= 1)
            {
                var armiesraw = desiredKingdom.Armies.ToList();
                foreach (Army army in armiesraw)
                {
                    armies.Append($"\nArmy: {army.Name.ToString()} | ");
                    armies.Append($"{(int)army.CalculateCurrentStrength()} Strength | ");
                    armies.Append($"{army.TotalHealthyMembers} Troops | ");
                    armies.Append($"{army.LeaderPartyAndAttachedPartiesCount.ToString()} Parties | ");
                    if (army?.LeaderParty?.GetBehaviorText() != null || army?.LeaderParty?.GetBehaviorText().ToString() != "")
                        armies.Append($"This army is: {army?.LeaderParty?.GetBehaviorText()?.ToString() ?? ""} | ");
                    if (army.LeaderParty.TargetParty != null || army.LeaderParty.ShortTermTargetParty != null)
                        armies.Append($"Target : {army.LeaderParty.ShortTermTargetParty ?? army.LeaderParty.TargetParty} | ");

                }
            }

            ActionManager.SendReply(context, armies.ToString().TrimEnd('|', ' '));
        }

        private void ShowFief(string desiredName, ReplyContext context)
        {
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                ActionManager.SendReply(context, "{=TESTING}Need fief name".Translate());
                return;
            }

            var desiredFief = Settlement.All.FirstOrDefault(c =>
                c.Name.ToString().ToLower() == desiredName.ToLower());
            if (desiredFief == null)
            {
                desiredFief = Settlement.All.FirstOrDefault(c =>
                c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (desiredFief == null)
            {
                ActionManager.SendReply(context,
                   "{=TESTING}Could not find a fief with the name {name}".Translate(("name", desiredName)));
                return;
            }
            var sb = new StringBuilder();
            if (desiredFief.IsVillage)
            {
                Village vill = Village.All.FirstOrDefault(v => v.Name.ToString() == desiredFief.Name.ToString());
                sb.Append("{=TESTING}{Name} ".Translate(("Name", vill.Name)));
                if (desiredFief.HasPort)
                    sb.Append("⚓");
                if (desiredFief.IsUnderRaid)
                    sb.Append("⚔️");
                if (desiredFief.IsRaided)
                    sb.Append("🔥");
                sb.Append(" | ");
                sb.Append("{=TESTING}Village | ".Translate());
                sb.Append("{=TESTING}Culture: {culture} | ".Translate(("culture", desiredFief.Culture.ToString())));
                sb.Append("{=TESTING}Hearths: {hearths}({change}) | ".Translate(("hearths", (int)vill.Hearth), ("change", (vill.HearthChange + UpgradeBehavior.Current.GetTotalHearthDaily(vill.Bound.Town.Settlement) >= 0 ? "+" : "") + Math.Round(vill.HearthChange + UpgradeBehavior.Current.GetTotalHearthDaily(vill.Bound.Town.Settlement), 2))));
                var parent = Settlement.All.FirstOrDefault(s => s.BoundVillages.Any(v => v.Name.ToString() == desiredFief.Name.ToString()));
                sb.Append("{=TESTING}Bound to {parent}".Translate(("parent", parent.Name)));
                ActionManager.SendReply(context, sb.ToString());
            }
            else if (desiredFief.IsTown || desiredFief.IsCastle)
            {
                Town town = Town.AllTowns.FirstOrDefault(t => t.Name.ToString() == desiredFief.Name.ToString())
                ?? Town.AllCastles.FirstOrDefault(c => c.Name.ToString() == desiredFief.Name.ToString());

                int profit = (int)(
                    Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(town, false).ResultNumber +
                    Campaign.Current.Models.ClanFinanceModel.CalculateTownIncomeFromTariffs(town.OwnerClan, town, false).ResultNumber +
                    Campaign.Current.Models.ClanFinanceModel.CalculateTownIncomeFromProjects(town) +
                    UpgradeBehavior.Current.GetTotalTaxBonus(town.Settlement) +
                    town.Settlement.BoundVillages.Sum(v => Campaign.Current.Models.ClanFinanceModel.CalculateVillageIncome(town.OwnerClan, v, false)) -
                    (town.GarrisonParty?.TotalWage ?? 0)
                    );
                sb.Append("{=TESTING}{Name} ".Translate(("Name", town.Name)));
                if (desiredFief.HasPort)
                    sb.Append("⚓");
                if (desiredFief.IsUnderSiege)
                    sb.Append("⚔️");
                sb.Append(" | ");
                if (!town.IsCastle)
                    sb.Append("{=TESTING}Town | ".Translate());
                else
                    sb.Append("{=TESTING}Castle | ".Translate());
                sb.Append("{=TESTING}Culture: {culture} | ".Translate(("culture", desiredFief.Culture.ToString())));
                if (town.OwnerClan != null)
                    sb.Append("{=TESTING}Owner:{own} | ".Translate(("own", town.OwnerClan.Name)));
                if (town.OwnerClan.Kingdom != null)
                    sb.Append("{=TESTING}Kingdom:{kingdom} | ".Translate(("kingdom", town.OwnerClan.Kingdom.Name)));
                if (town.Governor != null)
                    sb.Append("{=TESTING}Governor:{gove} | ".Translate(("gove", town.Governor.Name)));
                sb.Append("{=TESTING}Prosperity:{pros}({change}) | ".Translate(("pros", (int)town.Prosperity), ("change", (town.ProsperityChange + UpgradeBehavior.Current.GetProsperityFlat(town.Settlement) > 0 ? "+" : "") + Math.Round(town.ProsperityChange + UpgradeBehavior.Current.GetProsperityFlat(town.Settlement), 2))));
                sb.Append("{=TESTING}Loyalty:{loy}({change}) | ".Translate(("loy", (int)town.Loyalty), ("change", (town.LoyaltyChange + UpgradeBehavior.Current.GetLoyaltyFlat(town.Settlement) > 0 ? "+" : "") + Math.Round(town.LoyaltyChange + UpgradeBehavior.Current.GetLoyaltyFlat(town.Settlement), 2))));
                sb.Append("{=TESTING}Security:{sec}({change}) | ".Translate(("sec", (int)town.Security), ("change", (town.SecurityChange + UpgradeBehavior.Current.GetSecurityFlat(town.Settlement) > 0 ? "+" : "") + Math.Round(town.SecurityChange + UpgradeBehavior.Current.GetSecurityFlat(town.Settlement), 2))));
                sb.Append("{=TESTING}Food:{food}({change}) | ".Translate(("food", (int)town.FoodStocks), ("change", (town.FoodChange + UpgradeBehavior.Current.GetFoodFlat(town.Settlement) > 0 ? "+" : "") + Math.Round(town.FoodChange + UpgradeBehavior.Current.GetFoodFlat(town.Settlement), 2))));
                sb.Append("{=TESTING}💰Daily income:{profit} | ".Translate(("profit", profit)));
                sb.Append("{=TESTING}Militia:{mil}({change}) | ".Translate(("mil", (int)town.Militia), ("change", (town.MilitiaChange + UpgradeBehavior.Current.GetMilitiaFlat(town.Settlement) > 0 ? "+" : "") + Math.Round(town.MilitiaChange + UpgradeBehavior.Current.GetMilitiaFlat(town.Settlement), 2))));
                var garmodel = Campaign.Current.Models.PartySizeLimitModel;
                sb.Append("{=TESTING}Garrison:{gar}/{garcap} | ".Translate(("gar", (int)town.GarrisonParty.MemberRoster.TotalHealthyCount), ("garcap", (int)garmodel.CalculateGarrisonPartySizeLimit(town.Settlement, false).ResultNumber))); // + BLTUpgradeBehavior.Current.GetTotalGarrisonCapacityBonus(town.Settlement)
                var villList = town.Settlement.BoundVillages.Select(v => v).ToList();
                string villNames = "";
                sb.Append($"Villages + Hearth: ");
                foreach (Village v in villList.ToList())
                {
                    sb.Append($"{v.Name} ({Math.Round(v.Hearth, 2)} {((v.HearthChange + UpgradeBehavior.Current.GetTotalHearthDaily(town.Settlement)) >= 0 ? "+" : "")}{Math.Round((v.HearthChange + UpgradeBehavior.Current.GetTotalHearthDaily(town.Settlement)), 2)}), ");
                }
                villNames.TrimEnd(' ', ',');

                ActionManager.SendReply(context, sb.ToString());
            }
        }

        private void ShowFiefs(string desiredName, ReplyContext context)
        {
            var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            Kingdom desiredKingdom = adoptedHero?.Clan?.Kingdom;
            if (string.IsNullOrWhiteSpace(desiredName) && desiredKingdom == null)
            {
                ActionManager.SendReply(context, "{=DSNx7CFT}Need kingdom name".Translate());
                return;
            }
            else if (!string.IsNullOrWhiteSpace(desiredName))
            {
                desiredKingdom = Kingdom.All.Where(k => !k.IsEliminated).FirstOrDefault(c =>
                c.Name.ToString().ToLower() == desiredName.ToLower()) ?? Kingdom.All.FirstOrDefault(c =>
                c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
            }
           

            if (desiredKingdom != null)
            {
                Dictionary<Clan, List<Town>> fiefDict = desiredKingdom.Fiefs.Where(f => f.OwnerClan != null).GroupBy(f => f.OwnerClan).ToDictionary(g => g.Key, g => g.ToList());

                string result = "Towns: " +
                                string.Join(" - ",
                                    fiefDict
                                        .Select(kvp =>
                                        {
                                            var towns = kvp.Value.Where(t => t.IsTown).ToList();
                                            if (towns.Count == 0) return null;

                                            return $"{kvp.Key.Name}:" +
                                                   $"({string.Join(", ", towns.Select(t => t.Name))})";
                                        })
                                        .Where(s => s != null)
                                )
                                + " | Castles: " +
                                string.Join(" - ",
                                    fiefDict
                                        .Select(kvp =>
                                        {
                                            var castles = kvp.Value.Where(t => t.IsCastle).ToList();
                                            if (castles.Count == 0) return null;

                                            return $"{kvp.Key.Name}:" +
                                                   $"({string.Join(", ", castles.Select(t => t.Name))})";
                                        })
                                        .Where(s => s != null)
                                );


                ActionManager.SendReply(context, result);
            }
            else
            {
                ActionManager.SendReply(context, $"Could not find a kingdom with the name {desiredName}");
            }
        }

        private void ShowClan(string desiredName, ReplyContext context)
        {
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                ActionManager.SendReply(context, "{=}Need a clan name".Translate());
                return;
            }

            Clan desiredClan;

            // Streamer case check
            if (desiredName.ToLower() == "player")
            {
                desiredClan = Hero.MainHero.Clan;
            }
            else
            {
                desiredClan = Clan.All.FirstOrDefault(c => c.Name.ToString().ToLower() == desiredName.ToLower()) 
                    ?? Clan.All.FirstOrDefault(c => c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (desiredClan != null)
            {
                var clanSb = new StringBuilder();
                clanSb.Append("{=Ki8jvwkw}Clan Name: {name} | ".Translate(("name", desiredClan.Name.ToString())));
                clanSb.Append("{=sZcYhSOL}Leader: {leader} | ".Translate(("leader", desiredClan.Leader.Name.ToString())));
                if (desiredClan.Kingdom != null)
                {
                    clanSb.Append("{=ch83d8zT}Kingdom: {kingdom} | ".Translate(("kingdom", desiredClan.Kingdom.Name.ToString())));
                    if (desiredClan.IsUnderMercenaryService)
                    {
                        string mercGold = (desiredClan.MercenaryAwardMultiplier * Math.Round(desiredClan.Influence / 5f)).ToString() + "/" + desiredClan.MercenaryAwardMultiplier.ToString();
                        clanSb.Append("{=PbxexPi9}Mercenary💰: {mercenary} | ".Translate(("mercenary", mercGold)));
                    }
                }
                clanSb.Append("{=Sg11nEUe}Tier: {tier}({renown}) | ".Translate(("tier", desiredClan.Tier.ToString()), ("renown", Math.Round(desiredClan.Renown).ToString())));
                clanSb.Append("{=ZFGikYn8}Strength: {strength} | ".Translate(("strength", Math.Round(desiredClan.CurrentTotalStrength).ToString())));
                int income = Campaign.Current.Models.ClanFinanceModel.CalculateClanGoldChange(desiredClan).RoundedResultNumber;
                clanSb.Append("{=SDVLj0nw}Wealth: {wealth}({income}) | ".Translate(("wealth", desiredClan.Leader.Gold.ToString()), ("income", income)));
                clanSb.Append("{=eHJYAZha}Members: {members} | ".Translate(("members", desiredClan.Heroes.Count.ToString())));
                int parties = 0;
                int ships = 0;
                if (desiredClan.WarPartyComponents.Count > 0)
                {
                    foreach (var partyComponent in desiredClan.WarPartyComponents)
                    {
                        MobileParty party = partyComponent.MobileParty;

                        if (party == null || party.LeaderHero == null) continue;

                        if (party.IsLordParty) parties += 1;
                        ships += party.Ships.Count;
                    }
                }
                clanSb.Append("{=Ib213Hp9}Parties: {cparties}/{mparties} | ".Translate(("cparties", parties), ("mparties", desiredClan.CommanderLimit)));
                clanSb.Append("{=TESTING}Ships: {ships} |".Translate(("ships", ships)));
                if (desiredClan.Fiefs.Count >= 1)
                {
                    int townCount = 0;
                    int castleCount = 0;
                    foreach (var settlement in desiredClan.Fiefs)
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
                    clanSb.Append("{=BwuFSJU1} Towns: {towns} | ".Translate(("towns", (object)townCount)));
                    clanSb.Append("{=0rMNNQ7R}Castles: {castles}".Translate(("castles", (object)castleCount)));
                }
                ActionManager.SendReply(context, clanSb.ToString());
                return;
            }
            else
            {
                ActionManager.SendReply(context, $"Could not find a clan with the name {desiredName}");
            }
        }

        private void ShowClans(string desiredName, ReplyContext context)
        {
            var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            Kingdom desiredKingdom = adoptedHero?.Clan?.Kingdom;
            if (string.IsNullOrWhiteSpace(desiredName) && desiredKingdom == null)
            {
                ActionManager.SendReply(context, "{=DSNx7CFT}Need kingdom name".Translate());
                return;
            }
            else if (!string.IsNullOrWhiteSpace(desiredName))
            {
                // Streamer case check
                if (desiredName.ToLower() == "player")
                {
                    desiredKingdom = Hero.MainHero.Clan?.Kingdom;
                }
                else
                {
                    desiredKingdom = Kingdom.All.Where(k => !k.IsEliminated).FirstOrDefault(c =>
                        c.Name.ToString().ToLower() == desiredName.ToLower()) ?? Kingdom.All.FirstOrDefault(c =>
                        c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }

                
            if (desiredKingdom != null)
            {
                List<Clan> clanList = desiredKingdom.Clans.OrderByDescending(c => c.CurrentTotalStrength).ToList();
                var noble = new StringBuilder();
                var merc = new StringBuilder();
                int nobleCount = 0;
                int mercCount = 0;

                foreach (var clan in clanList)
                {
                    if (!clan.IsUnderMercenaryService)
                    {
                        if (clan.Kingdom.RulingClan == clan)
                            noble.Append($"👑");
                        noble.Append($"{clan.Name}(💪{(int)clan.CurrentTotalStrength}");
                        if (clan.Fiefs.Count > 0)
                            noble.Append($", 🏰{clan.Fiefs.Count}) - ");
                        else noble.Append(") - ");
                        nobleCount++;
                    }
                    else
                    {
                        merc.Append($"{clan.Name}(💪{(int)clan.CurrentTotalStrength}) - ");
                        mercCount++;
                    }
                }
                if (merc.Length == 0)
                    merc.Append(" None");
                string nobleS = noble.ToString().TrimEnd(' ', '-');
                string mercS = merc.ToString().TrimEnd(' ', '-');

                string clansString = $"Nobles({nobleCount}):{nobleS} | Mercs({mercCount}):{mercS}";
                ActionManager.SendReply(context, clansString);
                return;
            }
            else
            {
                ActionManager.SendReply(context, $"Could not find a kingdom with the name {desiredName}");
            }
        }

        private void ShowVassals(string desiredName, ReplyContext context)
        {
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                ActionManager.SendReply(context, "{=TESTING}Need a clan name".Translate());
                return;
            }

            var desiredClan = Clan.All.FirstOrDefault(c =>
                c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (desiredClan == null)
            {
                ActionManager.SendReply(context, $"Could not find a clan with the name {desiredName}");
                return;
            }

            var vassalBehavior = VassalBehavior.Current;
            if (vassalBehavior == null)
            {
                ActionManager.SendReply(context, "Vassal system is not available");
                return;
            }

            var vassals = vassalBehavior.GetVassalClans(desiredClan);

            if (vassals == null || vassals.Count == 0)
            {
                ActionManager.SendReply(context, $"{desiredClan.Name} has no vassals");
                return;
            }

            var sb = new StringBuilder();
            sb.Append($"{desiredClan.Name} Vassals ({vassals.Count}): ");

            foreach (var vassal in vassals)
            {
                sb.Append($"{vassal.Name}");

                if (vassal.Fiefs.Count > 0)
                {
                    var fiefNames = string.Join(", ", vassal.Fiefs.Select(f => f.Name.ToString()));
                    sb.Append($" (Fiefs: {fiefNames})");
                }
                else
                {
                    sb.Append(" (No fiefs)");
                }

                sb.Append(" | ");
            }

            // Remove trailing " | "
            string result = sb.ToString().TrimEnd(' ', '|');
            ActionManager.SendReply(context, result);
        }

        private void ShowMap(string desiredName, ReplyContext context)
        {
            var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            Kingdom desiredKingdom = adoptedHero?.Clan?.Kingdom;
            if (string.IsNullOrWhiteSpace(desiredName) && desiredKingdom == null)
            {
                ActionManager.SendReply(context, "{=DSNx7CFT}Need kingdom name".Translate());
                return;
            }
            else if (!string.IsNullOrWhiteSpace(desiredName))
            {
                desiredKingdom = Kingdom.All.FirstOrDefault(c =>
                    c.Name.ToString().ToLower() == desiredName.ToLower()) ?? Kingdom.All.FirstOrDefault(c =>
                    c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
            }


            if (desiredKingdom != null)
            {
                if (desiredKingdom.Fiefs.Count == 0)
                {
                    ActionManager.SendReply(context, "No fiefs".Translate());
                    return;
                }

                float ClockwiseAngleFromNorth(CampaignVec2 from, CampaignVec2 to)
                {
                    var dx = to.X - from.X;
                    var dy = to.Y - from.Y;

                    var angle = (float)(Math.Atan2(dx, dy) * (180.0 / Math.PI));
                    if (angle < 0f)
                        angle += 360f;

                    return angle;
                }

                string DirectionFromAngle(float angle)
                {
                    if (angle < 22.5f || angle >= 337.5f) return "↑ North";
                    if (angle < 67.5f) return "↗ North-East";
                    if (angle < 112.5f) return "→ East";
                    if (angle < 157.5f) return "↘ South-East";
                    if (angle < 202.5f) return "↓ South";
                    if (angle < 247.5f) return "↙ South-West";
                    if (angle < 292.5f) return "← West";
                    return "↖ North-West";
                }

                List<(IFaction faction, float angle, string direction)> bordering = new();

                var distance = Campaign.Current.Models.MapDistanceModel;
                var kingdomCenter = desiredKingdom.FactionMidSettlement.Position;

                foreach (var fief in desiredKingdom.Fiefs)
                {
                    var neighbors = distance.GetNeighborsOfFortification(
                        fief,
                        MobileParty.NavigationType.All
                    );

                    foreach (var n in neighbors)
                    {
                        if (n.MapFaction == desiredKingdom.MapFaction)
                            continue;

                        var faction = n.MapFaction;
                        if (bordering.Any(b => b.faction == faction))
                            continue;

                        var center = faction.FactionMidSettlement?.Position;
                        if (center == null)
                            continue;

                        var angle = ClockwiseAngleFromNorth(kingdomCenter, center.Value);
                        var dir = DirectionFromAngle(angle);

                        bordering.Add((faction, angle, dir));
                    }
                }
                string result = string.Join(", ",
                    bordering
                        .OrderBy(b => b.angle)
                        .Select(b => $"{b.faction.Name} ({b.direction})"));

                ActionManager.SendReply(context, result);
            }
        }

        private void ShowTime(ReplyContext context)
        {
            CampaignTime date = CampaignTime.Now;

            int days = (int)Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
            int yearSize = CampaignTime.DaysInYear;
            int years = (int)Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedYearsUntilNow;


            string result = $"Date: {date} | {days} days since start | {years} years({yearSize} days/year)";

            ActionManager.SendReply(context, result);
        }

        private void ShowPlayerSkills(ReplyContext context)
        {
            var result = $"{"{=fRwyY6ms}[LVL]".Translate()} {Hero.MainHero.Level}";

            var skillsList = CampaignHelpers.AllSkillObjects
                .OrderByDescending(s => Hero.MainHero.GetSkillValue(s))
                .Select(skill =>
                    $"{SkillXP.GetShortSkillName(skill)} {Hero.MainHero.GetSkillValue(skill)} " +
                    $"[" +
                    $"{"{=lHRDKsUT}f".Translate()}" +
                    $"{Hero.MainHero.HeroDeveloper.GetFocus(skill)}]");

            result.Add($"{"{=rTId8pBy}[SKILLS]".Translate()} {string.Join(Naming.Sep2, skillsList)}");
            ActionManager.SendReply(context, result);
        }
    }   
}