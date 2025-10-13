using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=1yrA4CUf}Kingdom Management"),
     LocDescription("{=4vQgxBGr}Allow viewer to change their clans Kingdom or make leader decisions"),
     UsedImplicitly]
    public class KingdomManagement : HeroCommandHandlerBase
    {
        [CategoryOrder("Join", 0),
         CategoryOrder("Rebel", 1),
         CategoryOrder("Leave", 2),
         CategoryOrder("Stats", 4)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=583Jcer2}Enable joining kingdoms command"),
             PropertyOrder(1), UsedImplicitly]
            public bool JoinEnabled { get; set; } = true;

            [LocDisplayName("{=5KY2Vdfx}Max Clans"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=5WqAw2LI}Maximum clans (includes NPC's) before join is disallowed"),
             PropertyOrder(2), UsedImplicitly]
            public int JoinMaxClans { get; set; } = 20;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=6fkIuAEC}Cost of joining a kingdom"),
             PropertyOrder(3), UsedImplicitly]
            public int JoinPrice { get; set; } = 150000;

            [LocDisplayName("{=vKsTAxDD}Mercenary"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=pEMiWgjg}!kingdom merc to enter mercenary contract"),
             PropertyOrder(4), UsedImplicitly]
            public bool MercenaryEnabled { get; set; } = true;

            [LocDisplayName("{=VTZ0Wc7R}Mercenary Cost"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=DUgSwHnD}Mercenary contract cost"),
             PropertyOrder(5), UsedImplicitly]
            public int MercPrice { get; set; } = 50000;

            [LocDisplayName("{=7KEOBexC}Players Kingdom?"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=7ivCO9JL}Allow viewers to join the players kingdom"),
             PropertyOrder(6), UsedImplicitly]
            public bool JoinAllowPlayer { get; set; } = true;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=88BqaM2k}Enable viewer clan rebelling against their kingdom"),
             PropertyOrder(1), UsedImplicitly]
            public bool RebelEnabled { get; set; } = true;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=97hqQyTG}Cost of starting a rebellion"),
             PropertyOrder(2), UsedImplicitly]
            public int RebelPrice { get; set; } = 2500000;

            [LocDisplayName("{=9rmGjERc}Minimum Clan Tier"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=ANLOgDZU}Minimum clan tier to start a rebellion"),
             PropertyOrder(3), UsedImplicitly]
            public int RebelClanTierMinimum { get; set; } = 2;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Leave", "{=zG5I9PwG}Leave"),
             LocDescription("{=H0TsFPbu}Enable viewer clan leaving their kingdom"),
             PropertyOrder(1), UsedImplicitly]
            public bool LeaveEnabled { get; set; } = true;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Stats", "{=rTee27gM}Stats"),
             LocDescription("{=CFBJIpux}Enable stats command"),
             PropertyOrder(1), UsedImplicitly]
            public bool StatsEnabled { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                var EnabledCommands = new StringBuilder();

                if (JoinEnabled)
                    EnabledCommands.Append("Join, ");
                if (MercenaryEnabled)
                    EnabledCommands.Append("Merc, ");
                if (RebelEnabled)
                    EnabledCommands.Append("Rebel, ");
                if (LeaveEnabled)
                    EnabledCommands.Append("Leave, "); 
                if (StatsEnabled)
                    EnabledCommands.Append("Stats, ");

                if (EnabledCommands.Length > 0)
                    generator.Value("<strong>Enabled Commands:</strong> {commands}".Translate(("commands", EnabledCommands.ToString(0, EnabledCommands.Length - 2))));

                if (JoinEnabled)
                    generator.Value("<strong>" +
                                    "Join Config: " +
                                    "</strong>" +
                                    "Max Clans={maxHeroes}, ".Translate(("maxClans", JoinMaxClans.ToString())) +
                                    "Price={price}{icon}, ".Translate(("price", JoinPrice.ToString()), ("icon", Naming.Gold)) +
                                    "Allow Join Players Kingdom?={allowPlayer}".Translate(("allowPlayer", JoinAllowPlayer.ToString())));
                if (MercenaryEnabled)
                    generator.Value("<strong>" +
                                    "Mercenary: " +
                                    "</strong>" +
                    "{mercenary}{icon}, ".Translate(("mercenary", MercPrice.ToString()), ("icon", Naming.Gold)));

                if (RebelEnabled)
                    generator.Value("<strong>" +
                                    "Rebel Config: " +
                                    "</strong>" +
                                    "Price={price}{icon}, ".Translate(("price", RebelPrice.ToString()), ("icon", Naming.Gold)) +
                                    "Minimum Clan Tier={tier}".Translate(("tier", RebelClanTierMinimum.ToString())));

            }
        }
        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;
            //var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }
            if (Mission.Current != null)
            {
                onFailure("{=CRCwDnag}You cannot manage your kingdom, as a mission is active!".Translate());
                return;
            }
            if (adoptedHero.HeroState == Hero.CharacterStates.Prisoner)
            {
                onFailure("{=Cjm2sCjR}You cannot manage your kingdom, as you are a prisoner!".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=DYgac2Ut}You cannot manage your kingdom, as you are not in a clan".Translate());
                return;
            }

            if (context.Args.IsEmpty())
            {
                if (adoptedHero.Clan.Kingdom == null)
                {
                    onFailure("{=EJ4Pd2Lg}Your clan is not in a Kingdom".Translate());
                    return;
                }
                onSuccess("{=EkmpJvML}Your clan {clanName} is a member of the kingom {kingdomName}".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())));
                return;
            }

            var splitArgs = context.Args.Split(' ');
            var command = splitArgs[0];
            var desiredName = string.Join(" ", splitArgs.Skip(1)).Trim();

            switch (command.ToLower())
            {
                case "join":
                    HandleJoinCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "merc":
                    HandleMercenaryCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "rebel":
                    HandleRebelCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case "leave":
                    HandleLeaveCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case "stats":
                    HandleStatsCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                default:
                    onFailure("{=FFxXuX5i}Invalid or empty kingdom action, try (join/merc/rebel/leave/stats)".Translate());
                    break;
            }

        }

        private void HandleJoinCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.JoinEnabled)
            {
                onFailure("{=FHPbdYpk}Joining kingdoms is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom != null)
            {
                onFailure("{=GEGrsLPm}Your clan is already in a kingdom, in order to leave you must rebel against them".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=HS14GdUa}You cannot manage your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                onFailure("{=IKXbDYU8}(join) (kingdom name)".Translate());
                return;
            }

            var desiredKingdom = CampaignHelpers.AllHeroes.Select(h => h?.Clan?.Kingdom).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);
            if (desiredKingdom == null)
            {
                onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                return;
            }
            if (desiredKingdom.Clans.Count >= settings.JoinMaxClans)
            {
                onFailure("{=KFzBPUry}The kingdom {name} is full".Translate(("name", desiredName)));
                return;
            }
            if (desiredKingdom == Hero.MainHero.Clan.Kingdom && !settings.JoinAllowPlayer)
            {
                onFailure("{=L4dccNIC}Joining the players kingdom is disabled".Translate());
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.JoinPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.JoinPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            AdoptedHeroFlags._allowKingdomMove = true;
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.JoinPrice, true);
            ChangeKingdomAction.ApplyByJoinToKingdom(adoptedHero.Clan, desiredKingdom);
            onSuccess("{=LSea9bms}Your clan {clanName} has joined the kingom {kingdomName}".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())));
            Log.ShowInformation("{=Lid1aV3k}{clanName} has joined kingdom {kingdomName}!".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
            AdoptedHeroFlags._allowKingdomMove = false;
        }

        private void HandleRebelCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.RebelEnabled)
            {
                onFailure("{=MRstTtQa}Clan rebellion is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=NbvwN9z3}Your clan is not in a kingdom".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=Nzm5bI4I}You cannot lead a rebellion agaisnt your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (adoptedHero.Clan == adoptedHero.Clan.Kingdom.RulingClan)
            {
                onFailure("{=OgwKEDza}You already are the ruling clan".Translate());
                return;
            }
            if (adoptedHero.Clan.IsUnderMercenaryService)
            {
                onFailure("{=Py6VMkK6}Your clan is mercenary".Translate());
                return;
            }
            if (adoptedHero.Clan.Tier < settings.RebelClanTierMinimum)
            {
                onFailure("{=Ok94bnhi}Your clan is not high enough tier to rebel".Translate());
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.RebelPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.RebelPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            AdoptedHeroFlags._allowKingdomMove = true;
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.RebelPrice, true);
            IFaction oldBoss = adoptedHero.Clan.Kingdom;
            adoptedHero.Clan.ClanLeaveKingdom();
            DeclareWarAction.ApplyByRebellion(adoptedHero.Clan, oldBoss);
            FactionManager.DeclareWar(adoptedHero.Clan, oldBoss);
            onSuccess("{=PHuBl5tJ}Your clan has rebelled against {oldBoss} and declared war".Translate(("oldBoss", oldBoss)));
            AdoptedHeroFlags._allowKingdomMove = false;
            return;
        }

        private void HandleStatsCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.StatsEnabled)
            {
                onFailure("{=RtwwHrgB}Kingdom stats is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=RvkJO6J9}Your clan is not in a kingdom".Translate());
                return;
            }
            
            bool war = false;
            bool tribute = false;
            TextObject warList = new TextObject();
            TextObject tributeList = new TextObject();
            foreach (Kingdom k in Kingdom.All)
            {
                if (adoptedHero.Clan.Kingdom == k)
                    continue;

                StanceLink stance = adoptedHero.Clan.Kingdom.GetStanceWith(k);

                if (adoptedHero.Clan.Kingdom.IsAtWarWith(k))
                {
                    war = true;
                    warList.Value += k.Name.Value + ":" + ((int)k.TotalStrength).ToString() + ", ";
                }
                else
                {
                    int dailyTributeFromUs = stance.GetDailyTributePaid(adoptedHero.Clan.Kingdom);
                    int dailyTributeFromThem = stance.GetDailyTributePaid(k);

                    if (dailyTributeFromUs > 0)
                    {
                        tribute = true;
                        tributeList.Value +=
                            $"{k.Name}:-{dailyTributeFromUs}, ";
                    }
                    else if (dailyTributeFromThem > 0)
                    {
                        tribute = true;
                        tributeList.Value +=
                            $"{k.Name}:+{dailyTributeFromThem}, ";
                    }
                }
            }
            warList.Value = warList.Value.TrimEnd(',', ' ');
            tributeList.Value = tributeList.Value.TrimEnd(',', ' ');

            var clanStats = new StringBuilder();
            clanStats.Append("{=SVlrGgol}Kingdom Name: {name} | ".Translate(("name", adoptedHero.Clan.Kingdom.Name.ToString())));
            clanStats.Append("{=Ss588M9l}Ruling Clan: {rulingClan} | ".Translate(("rulingClan", adoptedHero.Clan.Kingdom.RulingClan.Name.ToString())));
            clanStats.Append("{=T1FhhCH9}Clan Count: {clanCount} | ".Translate(("clanCount", adoptedHero.Clan.Kingdom.Clans.Count.ToString())));
            clanStats.Append("{=TUOmh7NY}Strength: {strength} | ".Translate(("strength", Math.Round(adoptedHero.Clan.Kingdom.TotalStrength).ToString())));
            clanStats.Append("{=6VFGXqRe}Influence: {influence} | ".Translate(("influence", Math.Round(adoptedHero.Clan.Influence).ToString())));
            if (adoptedHero.Clan.IsUnderMercenaryService)
            {
                string mercGold = (adoptedHero.Clan.MercenaryAwardMultiplier * (Math.Round(adoptedHero.Clan.Influence / 5f)+1)).ToString() + "/" + adoptedHero.Clan.MercenaryAwardMultiplier.ToString();
                clanStats.Append("{=PbxexPi9}Mercenary💰: {mercenary} | ".Translate(("mercenary", mercGold)));
            }
            if (war)
                clanStats.Append("{=QadZnUKh}Wars: {wars} | ".Translate(("wars", warList.ToString())));
            if (tribute)
                clanStats.Append("{=0GhTvF3K}Tribute: {tribute} | ".Translate(("tribute", tributeList.ToString())));
                if (adoptedHero.Clan.Kingdom.RulingClan.HomeSettlement.Name != null)
                clanStats.Append("{=EXKsUpaU}Capital: {capital} | ".Translate(("capital", adoptedHero.Clan.Kingdom.RulingClan.HomeSettlement.Name.ToString())));
            if (adoptedHero.Clan.Kingdom.Fiefs.Count >= 1) 
            {
                int townCount = 0;
                int castleCount = 0;
                foreach (var settlement in adoptedHero.Clan.Kingdom.Fiefs)
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

        private void HandleLeaveCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.LeaveEnabled)
            {
                onFailure("{=ozTfk7uB}Kingdom leaving is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=RvkJO6J9}Your clan is not in a kingdom".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=PSmxb52U}You cannot leave your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (adoptedHero.Clan == adoptedHero.Clan.Kingdom.RulingClan)
            {
                onFailure("{=OgwKEDza}You already are the ruling clan".Translate());
                return;
            }
            IFaction oldBoss = adoptedHero.Clan.Kingdom;
            if (adoptedHero.Clan.IsUnderMercenaryService)
            {
                adoptedHero.Clan.EndMercenaryService(true);
                adoptedHero.Clan.ClanLeaveKingdom(true);
                onSuccess("{=XWE579kx}Your clan has ended their mercenary contract".Translate());
                return;
            }
            AdoptedHeroFlags._allowKingdomMove = true;
            adoptedHero.Clan.ClanLeaveKingdom(true);
            onSuccess("{=sc77IxCW}Your clan has left {oldBoss}".Translate(("oldBoss", oldBoss)));
            AdoptedHeroFlags._allowKingdomMove = false;
            return;
        }

        private void HandleMercenaryCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.MercenaryEnabled)
            {
                onFailure("{=aSIP2AKk}Mercenary is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom != null && adoptedHero.Clan.IsUnderMercenaryService)
            {
                onFailure("{=7nEJJGzL}Already a mercenary!".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom != null)
            {
                onFailure("{=GEGrsLPm}Your clan is already in a kingdom, in order to leave you must rebel against them".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=HS14GdUa}You cannot manage your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                onFailure("{=ETfJQatX}(merc) (kingdom name)".Translate());
                return;
            }

            var desiredKingdom = CampaignHelpers.AllHeroes.Select(h => h?.Clan?.Kingdom).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);
            if (desiredKingdom == null)
            {
                onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                return;
            }
            if (desiredKingdom == Hero.MainHero.Clan.Kingdom && Hero.MainHero.Clan == Hero.MainHero.Clan.Kingdom.RulingClan && !settings.JoinAllowPlayer)
            {
                onFailure("{=L4dccNIC}Joining the players kingdom is disabled".Translate());
                return;
            }
            if (desiredKingdom.Clans.Count >= settings.JoinMaxClans)
            {
                onFailure("{=KFzBPUry}The kingdom {name} is full".Translate(("name", desiredName)));
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.MercPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.MercPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.MercPrice, true);
            ChangeKingdomAction.ApplyByJoinFactionAsMercenary(adoptedHero.Clan, desiredKingdom);
            Log.ShowInformation("{=tpwW6Ix8}{clanName} is now under contract with {kingdomName}!".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
        }
    }
}
