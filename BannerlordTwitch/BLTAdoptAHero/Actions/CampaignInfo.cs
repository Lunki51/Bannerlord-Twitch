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
    [LocDisplayName("{=TESTING}CampaignInfo"),
     LocDescription("{=TESTING}Shows kingdom list, culture list, wars list and specific kingdom/war info"),
     UsedImplicitly]
    public class CampaignInfo : ICommandHandler
    {
        public Type HandlerConfigType => null;

        public void Execute(ReplyContext context, object config)
        {
            // hero not required
            ExecuteInternal(context, config);
        }

        private void ExecuteInternal(ReplyContext context, object config)
        {
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context, "{=TESTING}invalid mode (use kingdomlist, culturelist, warlist, kingdom (kingdom), war (kingdom))".Translate());
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

                default:
                    ActionManager.SendReply(context,
                        "{=TESTING}invalid mode (use kingdomlist, culturelist, warlist, kingdom (kingdom), war (kingdom))".Translate());
                    break;
            }
        }

        private void ShowKingdom(string desiredName, ReplyContext context)
        {
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                ActionManager.SendReply(context, "{=TESTING}Need kingdom name".Translate());
                return;
            }

            var desiredKingdom = Kingdom.All.FirstOrDefault(c =>
                c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);

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
                    warList.Append($"{k.Name}:{(int)k.TotalStrength}, ");
                }
            }
            if (war) warList.Length -= 2;

            var sb = new StringBuilder();
            sb.Append("{=SVlrGgol}Kingdom Name: {name} | ".Translate(("name", desiredKingdom.Name.ToString())));
            sb.Append("{=Ss588M9l}Ruling Clan: {rulingClan} | ".Translate(("rulingClan", desiredKingdom.RulingClan.Name.ToString())));
            sb.Append("{=T1FhhCH9}Clan Count: {count} | ".Translate(("count", desiredKingdom.Clans.Count)));
            sb.Append("{=TUOmh7NY}Strength: {strength} | ".Translate(("strength", Math.Round(desiredKingdom.TotalStrength))));
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
                        sb.Append($"{k1.Name}({Math.Round(k1.TotalStrength)}) VS {k2.Name}({Math.Round(k2.TotalStrength)}) | ");
                }
                seen.Add(k1.StringId);
            }
            ActionManager.SendReply(context, sb.ToString().TrimEnd('|', ' '));
        }

        private void ShowWar(string desiredName, ReplyContext context)
        {
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                ActionManager.SendReply(context, "{=TESTING}Need kingdom name".Translate());
                return;
            }

            var desiredKingdom = Kingdom.All.FirstOrDefault(c =>
                c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);

            if (desiredKingdom == null)
            {
                ActionManager.SendReply(context,
                    "{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                return;
            }

            if (!Kingdom.All.Any(k => k.IsAtWarWith(desiredKingdom)))
            {
                ActionManager.SendReply(context,
                    "{=TESTING}{kingdom} is not at war".Translate(("kingdom", desiredKingdom.Name.ToString())));
                return;
            }

            var sb = new StringBuilder();
            foreach (var k in Kingdom.All)
            {
                if (desiredKingdom != k && desiredKingdom.IsAtWarWith(k))
                {
                    var stance = desiredKingdom.GetStanceWith(k);
                    sb.Append("{=TESTING}{k1} VS {k2} = Casualties: {c1}/{c2} - Raids: {r1}/{r2} - Sieges: {s1}/{s2} - Started: {date}"
                        .Translate(
                            ("k1", desiredKingdom.Name.ToString()),
                            ("k2", k.Name.ToString()),
                            ("c1", stance.GetCasualties(desiredKingdom)),
                            ("c2", stance.GetCasualties(k)),
                            ("r1", stance.GetSuccessfulRaids(desiredKingdom)),
                            ("r2", stance.GetSuccessfulRaids(k)),
                            ("s1", stance.GetSuccessfulSieges(desiredKingdom)),
                            ("s2", stance.GetSuccessfulSieges(k)),
                            ("date", stance.WarStartDate.ToString())
                        ));
                    sb.Append(" | ");
                }
            }

            ActionManager.SendReply(context, sb.ToString().TrimEnd('|', ' '));
        }
    }
}