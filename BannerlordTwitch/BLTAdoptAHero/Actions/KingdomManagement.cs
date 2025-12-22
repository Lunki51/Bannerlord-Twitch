using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using BLTAdoptAHero.Actions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
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
         CategoryOrder("Create", 3),
         CategoryOrder("Vassal", 4),
         CategoryOrder("Stats", 5)]
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
            public int JoinMaxClans { get; set; } = 30;

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

            [LocDisplayName("{=TESTING}Player Mercenary Cost"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=TESTING}Player kingdom mercenary contract cost"),
             PropertyOrder(6), UsedImplicitly]
            public int PlayerMercPrice { get; set; } = 50000;

            [LocDisplayName("{=7KEOBexC}Players Kingdom?"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=7ivCO9JL}Allow viewers to join the players kingdom"),
             PropertyOrder(7), UsedImplicitly]
            public bool JoinAllowPlayer { get; set; } = true;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=6fkIuAEC}Cost of joining the player's kingdom"),
             PropertyOrder(8), UsedImplicitly]
            public int PlayerJoinPrice { get; set; } = 150000;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=88BqaM2k}Enable viewer clan rebelling against their kingdom"),
             PropertyOrder(1), UsedImplicitly]
            public bool RebelEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}BLT Rebel"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=TESTING}Enable viewer clan rebelling against BLT kingdoms"),
             PropertyOrder(2), UsedImplicitly]
            public bool BLTRebelEnabled { get; set; } = true;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=97hqQyTG}Cost of starting a rebellion"),
             PropertyOrder(3), UsedImplicitly]
            public int RebelPrice { get; set; } = 500000;

            [LocDisplayName("{=TESTING}BLT Gold Cost"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=TESTING}Cost of rebelling against BLT kingoms"),
             PropertyOrder(4), UsedImplicitly]
            public int BLTRebelPrice { get; set; } = 1000000;

            [LocDisplayName("{=9rmGjERc}Minimum Clan Tier"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=ANLOgDZU}Minimum clan tier to start a rebellion"),
             PropertyOrder(5), UsedImplicitly]
            public int RebelClanTierMinimum { get; set; } = 2;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Leave", "{=zG5I9PwG}Leave"),
             LocDescription("{=H0TsFPbu}Enable viewer clan leaving their kingdom"),
             PropertyOrder(1), UsedImplicitly]
            public bool LeaveEnabled { get; set; } = true;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Create", "{=TESTING}Create"),
             LocDescription("{=TESTING}Enable viewer clan to create a kingdom"),
             PropertyOrder(1), UsedImplicitly]
            public bool CreateKEnabled { get; set; } = true;

            [LocDisplayName("{=9rmGjERc}Minimum Clan Tier"),
             LocCategory("Create", "{=TESTING}Create"),
             LocDescription("{=TESTING}Minimum clan tier to create a kingdom"),
             PropertyOrder(2), UsedImplicitly]
            public int CreateKTierMinimum { get; set; } = 3;

            [LocDisplayName("{=TESTING}Minimum Clan Fiefs"),
             LocCategory("Create", "{=TESTING}Create"),
             LocDescription("{=TESTING}Minimum clan fiefs to create a kingdom"),
             PropertyOrder(3), UsedImplicitly]
            public int CreateKFiefMinimum { get; set; } = 2;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Create", "{=TESTING}Create"),
             LocDescription("{=TESTING}Cost of creating a kingdom"),
             PropertyOrder(4), UsedImplicitly]
            public int CreateKPrice { get; set; } = 20000000;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Enable viewer create vassal"),
             PropertyOrder(1), UsedImplicitly]
            public bool VassalEnabled { get; set; } = true;

            [LocDisplayName("{=6PUxQuLg}Max vassal"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Max vassal clans"),
             PropertyOrder(4), UsedImplicitly]
            public int VassalAmount { get; set; } = 3;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Cost of creating a vassal clan"),
             PropertyOrder(4), UsedImplicitly]
            public int VassalPrice { get; set; } = 250000;

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
                if (CreateKEnabled)
                    EnabledCommands.Append("Create, ");
                if (VassalEnabled)
                    EnabledCommands.Append("Vassa, ");
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
                                    "Allow Join Players Kingdom?={allowPlayer}, ".Translate(("allowPlayer", JoinAllowPlayer.ToString())) +
                                    "Player Kingdom Price={price}{icon}".Translate(("price", PlayerJoinPrice.ToString()), ("icon", Naming.Gold)));
                if (MercenaryEnabled)
                    generator.Value("<strong>" +
                                    "Mercenary: " +
                                    "</strong>" +
                                    "Price={price}{icon}, ".Translate(("price", MercPrice.ToString()), ("icon", Naming.Gold)) +
                                    "Player Kingdom Price={price}{icon}, ".Translate(("price", PlayerMercPrice.ToString()), ("icon", Naming.Gold)));

                if (RebelEnabled)
                    generator.Value("<strong>" +
                                    "Rebel Config: " +
                                    "</strong>" +
                                    "Price={price}{icon}, ".Translate(("price", RebelPrice.ToString()), ("icon", Naming.Gold)) +
                                    "Allow Rebelling from BLT Kingdom?={allowBLT}, ".Translate(("allowBLT", BLTRebelEnabled.ToString())) +
                                    "From BLT Kingdom Price={price}{icon}, ".Translate(("price", BLTRebelPrice.ToString()), ("icon", Naming.Gold)) +
                                    "Minimum Clan Tier={tier}".Translate(("tier", RebelClanTierMinimum.ToString())));
                if (CreateKEnabled)
                    generator.Value("<strong>" +
                                    "Create Config: " +
                                    "</strong>" +
                                    "Price={price}{icon}, ".Translate(("price", CreateKPrice.ToString()), ("icon", Naming.Gold)) +
                                    "Minimum Clan Tier={tier}, ".Translate(("tier", CreateKTierMinimum.ToString())) +
                                    "Minimum fiefs amount={count}".Translate(("count", CreateKFiefMinimum.ToString())));
                if (VassalEnabled)
                    generator.Value("<strong>Vassal: </strong>" +
                                    $"Max vassals:{VassalAmount}, " +
                                    $"Price={VassalPrice.ToString()}{Naming.Gold}");
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
                case "create":
                    HandleKCreateCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "vassal":
                    VassalCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
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
            bool joiningPlayer = false;
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
            else if (desiredKingdom == Hero.MainHero.Clan.Kingdom && settings.JoinAllowPlayer)
            {
                joiningPlayer = true;
            }
            if (!joiningPlayer && BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.JoinPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.JoinPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            else if (joiningPlayer && BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.PlayerJoinPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.PlayerJoinPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            AdoptedHeroFlags._allowKingdomMove = true;
            if (joiningPlayer)
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.PlayerJoinPrice, true);
            }
            else
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.JoinPrice, true);
            }
            ChangeKingdomAction.ApplyByJoinToKingdom(adoptedHero.Clan, desiredKingdom);
            if (adoptedHero.Clan.Kingdom == null)
                adoptedHero.Clan.Kingdom = desiredKingdom;
            if (adoptedHero.Clan.Fiefs.Count == 0)
                adoptedHero.Clan.SetInitialHomeSettlement(desiredKingdom.InitialHomeSettlement);

            onSuccess("{=LSea9bms}Your clan {clanName} has joined the kingom {kingdomName}".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())));
            Log.ShowInformation("{=Lid1aV3k}{clanName} has joined kingdom {kingdomName}!".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
            AdoptedHeroFlags._allowKingdomMove = false;
        }

        private void HandleRebelCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            bool BLTRebellion = false;
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
                onFailure("{=Nzm5bI4I}You cannot lead a rebellion against your kingdom, as you are not your clans leader!".Translate());
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
            if (adoptedHero.Clan.Kingdom.RulingClan.Leader.IsAdopted())
            {
                BLTRebellion = true;
            }
            if (BLTRebellion && !settings.BLTRebelEnabled)
            {
                onFailure("{=Ok94bnhi}Rebelling from BLT-owned kingdoms is disabled!".Translate());
                return;
            }
            
            if (!BLTRebellion)
            {
                if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.RebelPrice)
                {
                    onFailure(Naming.NotEnoughGold(settings.RebelPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                    return;
                }
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.RebelPrice, true);
            }
            else
            {
                if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.BLTRebelPrice)
                {
                    onFailure(Naming.NotEnoughGold(settings.BLTRebelPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                    return;
                }
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.BLTRebelPrice, true);
            }
            AdoptedHeroFlags._allowKingdomMove = true;
            IFaction oldBoss = adoptedHero.Clan.Kingdom;
            adoptedHero.Clan.ClanLeaveKingdom();
            DeclareWarAction.ApplyByRebellion(oldBoss, adoptedHero.Clan);
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
            TradeAgreementsCampaignBehavior tradeBehavior = Campaign.Current.GetCampaignBehavior<TradeAgreementsCampaignBehavior>();
            bool war = false;
            bool ally = adoptedHero.Clan.Kingdom.AlliedKingdoms.Count > 0;
            bool trade = false;
            bool tribute = false;
            TextObject warList = new TextObject("");
            TextObject tributeList = new TextObject("");
            TextObject tradeList = new TextObject("");
            foreach (Kingdom k in Kingdom.All)
            {
                if (adoptedHero.Clan.Kingdom == k)
                    continue;

                StanceLink stance = adoptedHero.Clan.Kingdom.GetStanceWith(k);
                if (tradeBehavior.HasTradeAgreement(adoptedHero.Clan.Kingdom, k))
                {
                    var tradeDate = tradeBehavior.GetTradeAgreementEndDate(adoptedHero.Clan.Kingdom, k);
                    int tradeDays = (int)(tradeDate - CampaignTime.Now).ToDays;
                    trade = true;
                    tradeList.Value += k.Name.Value + $"({tradeDays}), ";
                }
                if (adoptedHero.Clan.Kingdom.IsAtWarWith(k))
                {
                    war = true;
                    warList.Value += k.Name.Value + ":" + ((int)k.CurrentTotalStrength).ToString() + ", ";
                }
                else
                {
                    int dailyTributeFromUs = stance.GetDailyTributeToPay(adoptedHero.Clan.Kingdom);
                    int dailyTributeFromThem = stance.GetDailyTributeToPay(k);
                    int daysUs = k.GetStanceWith(adoptedHero.Clan.Kingdom).GetRemainingTributePaymentCount();
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
            var allyList = string.Join(", ", adoptedHero.Clan.Kingdom.AlliedKingdoms.Select(k => k.Name.ToString()));
            tradeList.Value = tradeList.Value.TrimEnd(',', ' ');
            tributeList.Value = tributeList.Value.TrimEnd(',', ' ');

            var clanStats = new StringBuilder();
            clanStats.Append("{=SVlrGgol}Kingdom Name: {name} | ".Translate(("name", adoptedHero.Clan.Kingdom.Name.ToString())));
            clanStats.Append("{=Ss588M9l}Ruling Clan: {rulingClan} | ".Translate(("rulingClan", adoptedHero.Clan.Kingdom.RulingClan.Name.ToString())));
            clanStats.Append("{=T1FhhCH9}Clan Count: {clanCount} | ".Translate(("clanCount", adoptedHero.Clan.Kingdom.Clans.Count.ToString())));
            clanStats.Append("{=TUOmh7NY}Strength: {strength} | ".Translate(("strength", Math.Round(adoptedHero.Clan.Kingdom.CurrentTotalStrength).ToString())));
            clanStats.Append("{=6VFGXqRe}Influence: {influence} | ".Translate(("influence", Math.Round(adoptedHero.Clan.Influence).ToString())));
            if (adoptedHero.Clan.IsUnderMercenaryService)
            {
                string mercGold = (adoptedHero.Clan.MercenaryAwardMultiplier * (Math.Round(adoptedHero.Clan.Influence / 5f) + 1)).ToString() + "/" + adoptedHero.Clan.MercenaryAwardMultiplier.ToString();
                clanStats.Append("{=PbxexPi9}Mercenary💰: {mercenary} | ".Translate(("mercenary", mercGold)));
            }
            if (war)
                clanStats.Append("{=QadZnUKh}Wars: {wars} | ".Translate(("wars", warList.ToString())));
            if (ally)
                clanStats.Append("{=TESTING}Alliances: {allies} | ".Translate(("allies", allyList)));
            if (trade)
                clanStats.Append("{=TESTING}Trades: {trade} | ".Translate(("trade", tradeList.ToString())));
            if (tribute)
                clanStats.Append("{=0GhTvF3K}Tribute: {tribute} | ".Translate(("tribute", tributeList.ToString())));
            if (adoptedHero.Clan.Kingdom.RulingClan.HomeSettlement.Name != null)
                clanStats.Append("{=EXKsUpaU}Capital: {capital} ".Translate(("capital", adoptedHero.Clan.Kingdom.RulingClan.HomeSettlement.Name.ToString())));
            if (adoptedHero.Clan.Kingdom.Armies.Count >= 1)
            {
                clanStats.Append($"| Armies: {adoptedHero.Clan.Kingdom.Armies.Count} ");
            }
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
                clanStats.Append("{=BwuFSJU1}| Towns: {towns} | ".Translate(("towns", (object)townCount)));
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
            foreach (var fief in adoptedHero.Clan.Settlements.ToList())
            {
                Hero ruler = adoptedHero.Clan.Kingdom?.RulingClan?.Leader;
                if (ruler != null && ruler != adoptedHero)
                {
                    ChangeOwnerOfSettlementAction.ApplyByDefault(ruler, fief);
                }
            }
            onSuccess("{=sc77IxCW}Your clan has left {oldBoss}".Translate(("oldBoss", oldBoss)));
            adoptedHero.Clan.ClanLeaveKingdom();
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

            bool mercforPlayer = false;
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
            else if (desiredKingdom == Hero.MainHero.Clan.Kingdom && Hero.MainHero.Clan == Hero.MainHero.Clan.Kingdom.RulingClan)
            {
                mercforPlayer = true;
            }
            if (desiredKingdom.Clans.Count >= settings.JoinMaxClans)
            {
                onFailure("{=KFzBPUry}The kingdom {name} is full".Translate(("name", desiredName)));
                return;
            }
            if (!mercforPlayer && BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.MercPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.MercPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            else if (!mercforPlayer && BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.PlayerMercPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.PlayerMercPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            
            if (!mercforPlayer)
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.MercPrice, true);
            }
            else
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.PlayerMercPrice, true);
            }
            ChangeKingdomAction.ApplyByJoinFactionAsMercenary(adoptedHero.Clan, desiredKingdom);
            Log.ShowInformation("{=tpwW6Ix8}{clanName} is now under contract with {kingdomName}!".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
        }
        private void HandleKCreateCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.CreateKEnabled)
            {
                onFailure("Kingdom creation is disabled");
                return;
            }
            if (adoptedHero.Clan.Fiefs.Count < settings.CreateKFiefMinimum)
            {
                onFailure($"Not enough fiefs{adoptedHero.Clan.Fiefs.Count}/{settings.CreateKFiefMinimum}");
                return;
            }
            if (adoptedHero.Clan.Tier < settings.CreateKTierMinimum)
            {
                onFailure("Your clan is not high enough tier to create a kingdom");
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
                onFailure("{=ETfJQatX}(create) (kingdom name)".Translate());
                return;
            }
            var existingKingdom = CampaignHelpers.AllHeroes.Select(h => h?.Clan?.Kingdom).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);
            if (existingKingdom != null)
            {
                onFailure("{=TESTING}A kingdom with the name {name} already exists".Translate(("name", desiredName)));
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.CreateKPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.CreateKPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.CreateKPrice, true);
            var creator = Campaign.Current.KingdomManager;

            //var newKingdom = Kingdom.CreateKingdom(desiredName);
            var culture = adoptedHero.Culture;
            //var banner = adoptedHero.Clan.Banner;
            //var color1 = adoptedHero.Clan.Banner.GetPrimaryColor();
            //var color2 = adoptedHero.Clan.Banner.GetSecondaryColor();
            //var home = adoptedHero.Clan.HomeSettlement;
            //string title = adoptedHero.IsFemale ? "Queen" : "King";
            //string descText = adoptedHero.Name.ToString();
            //Keep clan wars HERE
            //var warTargets = adoptedHero.Clan.FactionsAtWarWith;

            creator.CreateKingdom(new TextObject(desiredName), new TextObject(desiredName), culture, adoptedHero.Clan, null, null, null, null);
            var newKingdom = adoptedHero.Clan.Kingdom;
            newKingdom.KingdomBudgetWallet = 2000000;
            adoptedHero.Clan.Influence = 2000;
            //foreach(Kingdom target in warTargets)
            //{
            //    DeclareWarAction.ApplyByRebellion(newKingdom, target);
            //}
            onSuccess("{=TESTING}Created kingdom {name}".Translate(("name", desiredName)));
            Log.ShowInformation("{=TESTING}{heroName} has founded kingdom {kingdom}!".Translate(("heroName", adoptedHero.Name.ToString()), ("kingdom", adoptedHero.Clan.Kingdom.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
        }

        private void VassalCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.VassalEnabled)
            {
                onFailure("Vassal creation is disabled");
                return;
            }

            if (adoptedHero.Clan.Kingdom == null || adoptedHero.Clan.Kingdom.Leader != adoptedHero)
            {
                onFailure("{=GEGrsLPm}Your must be king to vassal".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=HS14GdUa}You cannot manage your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                onFailure("{=ETfJQatX}(vassal) (hero name)".Translate());
                return;
            }
            //var existingClan = Clan.All.FirstOrDefault(c => c.Name.ToString() == desiredName);
            //if (existingClan != null)
            //{
            //    onFailure("{=TESTING}A clan with the name {name} already exists".Translate(("name", desiredName)));
            //    return;
            //}
            if (adoptedHero.Clan.Kingdom.Clans.FindAll(c => c.Name.ToString().IndexOf("[Vassal]", StringComparison.OrdinalIgnoreCase) >= 0).Count >= settings.VassalAmount)
            {
                onFailure($"Max vassals{settings.VassalAmount}");
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.CreateKPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.VassalPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }


            Hero vassal = adoptedHero.Clan.Heroes.Find(h => h.FirstName.ToString() == desiredName);
            if (vassal == null)
            {
                onFailure($"No hero named {desiredName}");
                return;
            }
            if (vassal.Spouse == null)
            {
                HeroFeatures.SpawnSpouse(vassal, vassal.Culture);
            }

            var fullClanName = vassal.Culture.ClanNameList.SelectRandom().ToString()+ " [Vassal]";
            var newClan = Clan.CreateClan(fullClanName);
            newClan.ChangeClanName(new TextObject(fullClanName), new TextObject(fullClanName));
            newClan.Culture = vassal.Culture;
            newClan.Banner = Banner.CreateOneColoredBannerWithOneIcon(adoptedHero.Clan.Banner.GetPrimaryColor(), adoptedHero.Clan.Banner.GetFirstIconColor(), -1);
            newClan.Kingdom = adoptedHero.Clan.Kingdom;
            newClan.SetInitialHomeSettlement(Settlement.All.SelectRandom());
            vassal.Clan = newClan;
            if (vassal.Spouse != null)
            {
                vassal.Spouse.Clan = newClan;
            }
            if (vassal.Children.Count > 0)
            {
                foreach (Hero child in vassal.Children)
                {
                    child.Clan = newClan;
                }
            }
            var tierModel = Campaign.Current.Models.ClanTierModel;
            newClan.AddRenown(tierModel.GetRequiredRenownForTier(tierModel.CompanionToLordClanStartingTier));
            newClan.SetLeader(vassal);
            newClan.IsNoble = true;
            CampaignEventDispatcher.Instance.OnClanCreated(newClan, false);
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(adoptedHero, vassal, 100, false);
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.VassalPrice, true);
            Log.ShowInformation("Vassal created");
        }
    }
}