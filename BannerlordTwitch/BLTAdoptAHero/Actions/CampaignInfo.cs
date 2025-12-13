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
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=OpptAAU9}CampaignInfo"),
     LocDescription("{=5FynPtK8}Shows kingdom list, culture list, wars list and specific kingdom/war info"),
     UsedImplicitly]
    public class CampaignInfo : ICommandHandler
    {
        public Type HandlerConfigType => null;

        public void Execute(ReplyContext context, object config)
        {
            ExecuteInternal(context, config);
        }

        private void ExecuteInternal(ReplyContext context, object config)
        {
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context, "{=tk7R3uwg}invalid mode (use kingdomlist, culturelist, warlist, kingdom (kingdom), war (kingdom), fief (town/castle/village))".Translate());
                return;
            }

            var splitArgs = context.Args.Split(' ');
            var mode = splitArgs[0];
            var desiredName = string.Join(" ", splitArgs.Skip(1)).Trim();

            switch (mode)
            {
                case "kingdomlist":
                    ActionManager.SendReply(context,
                        string.Join(", ", CampaignHelpers.MainFactions.Select(c => c.Name.ToString())));
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

                case "war":
                    ShowWar(desiredName, context);
                    break;

                case "fief":
                    ShowFief(desiredName, context);
                    break;

                default:
                    ActionManager.SendReply(context,
                        "{=tk7R3uwg}invalid mode (use kingdomlist, culturelist, warlist, kingdom (kingdom), war (kingdom), fief (town/castle/village))".Translate());
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

            var desiredKingdom = Kingdom.All.FirstOrDefault(c =>
                c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (desiredKingdom == null)
            {
                ActionManager.SendReply(context,
                    "{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                return;
            }

            bool war = false;
            var warList = new StringBuilder();
            foreach (Kingdom k in Kingdom.All)
            {
                if (desiredKingdom != k && desiredKingdom.IsAtWarWith(k))
                {
                    war = true;
                    warList.Append($"{k.Name}:{(int)k.CurrentTotalStrength}, ");
                }
            }
            if (war) warList.Length -= 2;

            var sb = new StringBuilder();
            sb.Append("{=SVlrGgol}Kingdom Name: {name} | ".Translate(("name", desiredKingdom.Name.ToString())));
            sb.Append("{=Ss588M9l}Ruling Clan: {rulingClan} | ".Translate(("rulingClan", desiredKingdom.RulingClan.Name.ToString())));
            sb.Append("{=T1FhhCH9}Clan Count: {count} | ".Translate(("count", desiredKingdom.Clans.Count)));
            sb.Append("{=TUOmh7NY}Strength: {strength} | ".Translate(("strength", Math.Round(desiredKingdom.CurrentTotalStrength).ToString())));
            if (war)
                sb.Append("{=QadZnUKh}Wars: {wars} | ".Translate(("wars", warList.ToString())));
            if (desiredKingdom.RulingClan.HomeSettlement != null)
                sb.Append("{=EXKsUpaU}Capital: {capital} | ".Translate(("capital", desiredKingdom.RulingClan.HomeSettlement.Name.ToString())));

            int towns = desiredKingdom.Fiefs.Count(f => !f.IsCastle);
            int castles = desiredKingdom.Fiefs.Count(f => f.IsCastle);
            sb.Append("{=BwuFSJU1}Towns: {towns} | ".Translate(("towns", towns)));
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
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                ActionManager.SendReply(context, "{=DSNx7CFT}Need kingdom name".Translate());
                return;
            }

            var desiredKingdom = Kingdom.All.FirstOrDefault(c =>
                c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);

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
                int p1 = 0;
                int p2 = 0;
                foreach (var prisoner in desiredKingdom.Heroes)
                {
                    if (prisoner.IsPrisoner && prisoner.PartyBelongedToAsPrisoner?.MapFaction == k)
                        p1++;
                }
                foreach (var prisoner in k.Heroes)
                {
                    if (prisoner.IsPrisoner && prisoner.PartyBelongedToAsPrisoner?.MapFaction == desiredKingdom)
                        p2++;
                }
                if (desiredKingdom != k && desiredKingdom.IsAtWarWith(k))
                {
                    var stance = desiredKingdom.GetStanceWith(k);
                    sb.Append("{=N9b5k8FR}{k1} VS {k2} = Casualties: {c1}/{c2} - Prisoners: {p1}/{p2} - Raids: {r1}/{r2} - Sieges: {s1}/{s2} - Progress: {p1%}/{p2%} Started: {date}"
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
                            ("p1%", diplo.GetWarProgressScore(desiredKingdom, k)),
                            ("p2%", diplo.GetWarProgressScore(k, desiredKingdom)),
                            ("date", stance.WarStartDate.ToString())
                        ));
                    sb.Append(" | ");
                }
            }

            ActionManager.SendReply(context, sb.ToString().TrimEnd('|', ' '));
        }
        private void ShowFief(string desiredName, ReplyContext context)
        {
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                ActionManager.SendReply(context, "{=TESTING}Need fief name".Translate());
                return;
            }
            var desiredFief = Settlement.All.FirstOrDefault(c =>
                c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
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
                if (desiredFief.IsUnderRaid)
                    sb.Append("⚔️");
                if (desiredFief.IsRaided)
                    sb.Append("🔥");
                sb.Append(" | ");
                sb.Append("{=TESTING}Village | ".Translate());
                sb.Append("{=TESTING}Culture: {culture} | ".Translate(("culture", desiredFief.Culture.ToString())));
                sb.Append("{=TESTING}Hearths: {hearths}({change}) | ".Translate(("hearths", (int)vill.Hearth), ("change", (vill.HearthChange >= 0 ? "+" : "") + Math.Round(vill.HearthChange, 2))));
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
                    town.Settlement.BoundVillages.Sum(v => Campaign.Current.Models.ClanFinanceModel.CalculateVillageIncome(town.OwnerClan, v, false)) -
                    (town.GarrisonParty?.TotalWage ?? 0)
                    );
                sb.Append("{=TESTING}{Name} ".Translate(("Name", town.Name)));
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
                sb.Append("{=TESTING}Prosperity:{pros}({change}) | ".Translate(("pros", (int)town.Prosperity), ("change", (town.ProsperityChange > 0 ? "+" : "") + Math.Round(town.ProsperityChange, 2))));
                sb.Append("{=TESTING}Loyalty:{loy}({change}) | ".Translate(("loy", (int)town.Loyalty), ("change", (town.LoyaltyChange > 0 ? "+" : "") + Math.Round(town.LoyaltyChange, 2))));
                sb.Append("{=TESTING}Security:{sec}({change}) | ".Translate(("sec", (int)town.Security), ("change", (town.SecurityChange > 0 ? "+" : "") + Math.Round(town.SecurityChange, 2))));
                sb.Append("{=TESTING}Food:{food}({change}) | ".Translate(("food", (int)town.FoodStocks), ("change", (town.FoodChange > 0 ? "+" : "") + Math.Round(town.FoodChange, 2))));
                sb.Append("{=TESTING}💰Daily income:{profit} | ".Translate(("profit", profit)));
                sb.Append("{=TESTING}Militia:{mil}({change}) | ".Translate(("mil", (int)town.Militia), ("change", (town.MilitiaChange > 0 ? "+" : "") + Math.Round(town.MilitiaChange, 2))));
                sb.Append("{=TESTING}Garrison:{gar} | ".Translate(("gar", (int)town.GarrisonParty.MemberRoster.TotalHealthyCount)));
                var villList = town.Settlement.BoundVillages.Select(v => v.Name.ToString()).ToList();
                var villNames = string.Join(", ", villList);
                sb.Append("{=TESTING}Villages:{villNames}".Translate(("villNames", villNames)));

                ActionManager.SendReply(context, sb.ToString());
            }
        }
    }
}