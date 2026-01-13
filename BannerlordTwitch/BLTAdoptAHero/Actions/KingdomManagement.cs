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
using System.ComponentModel.DataAnnotations;
using BLTAdoptAHero.Behaviors;

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
         CategoryOrder("Stats", 5),
         CategoryOrder("Release", 6),
         CategoryOrder("Expel", 7),
         CategoryOrder("Tax", 8)]
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
             LocDescription("{=TESTING}Cost of rebelling against BLT kingdoms"),
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

            [LocDisplayName("{=BLT_MaxVassals}Max vassal"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=BLT_MaxVassalsDesc}Max vassal clans"),
             PropertyOrder(2), UsedImplicitly]
            public int VassalAmount { get; set; } = 3;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Cost of creating a vassal clan"),
             PropertyOrder(3), UsedImplicitly]
            public int VassalPrice { get; set; } = 250000;

            [LocDisplayName("{=TESTING}Vassal Merc Income Share %"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Percentage of vassal mercenary income shared with master (0.0 - 2.0, 0.25 = 25%)"),
             PropertyOrder(4), UsedImplicitly,
             Range(0f, 2f)]
            public float VassalMercIncomeShare { get; set; } = 0.25f; // 25% default

            [LocDisplayName("{=TESTING}Vassal Fief Income Share %"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Percentage of vassal fief income shared with master (0.0 - 2.0, 0.25 = 25%)"),
             PropertyOrder(5), UsedImplicitly,
             Range(0f, 2f)]
            public float VassalFiefIncomeShare { get; set; } = 0.25f; // 25% default

            [LocDisplayName("{=TESTING}King Vassals Only"),
             LocCategory("Vassal", "{=TESTING}Vassal"),
             LocDescription("{=TESTING}Prevents anyone except kings to create vassal clans"),
             PropertyOrder(6), UsedImplicitly]
            public bool KingVassalsOnly { get; set; } = false;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Stats", "{=rTee27gM}Stats"),
             LocDescription("{=CFBJIpux}Enable stats command"),
             PropertyOrder(1), UsedImplicitly]
            public bool StatsEnabled { get; set; } = true;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Release", "{=TESTING}Release"),
             LocDescription("{=TESTING}Enable king to release clans from kingdom (with their land)"),
             PropertyOrder(1), UsedImplicitly]
            public bool ReleaseEnabled { get; set; } = true;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Release", "{=TESTING}Release"),
             LocDescription("{=TESTING}Cost for king to release a clan"),
             PropertyOrder(2), UsedImplicitly]
            public int ReleasePrice { get; set; } = 50000;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Expel", "{=TESTING}Expel"),
             LocDescription("{=TESTING}Enable king to expel clans from kingdom (takes their land first)"),
             PropertyOrder(1), UsedImplicitly]
            public bool ExpelEnabled { get; set; } = true;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Expel", "{=TESTING}Expel"),
             LocDescription("{=TESTING}Cost for king to expel a clan"),
             PropertyOrder(2), UsedImplicitly]
            public int ExpelPrice { get; set; } = 100000;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Tax", "{=TESTING}Tax"),
             LocDescription("{=TESTING}Enable kingdom taxation system"),
             PropertyOrder(1), UsedImplicitly]
            public bool TaxEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Minimum Tax Rate %"),
             LocCategory("Tax", "{=TESTING}Tax"),
             LocDescription("{=TESTING}Minimum tax rate kings can set (0-100)"),
             PropertyOrder(2), UsedImplicitly,
             Range(0f, 100f)]
            public float MinTaxRate { get; set; } = 0f;

            [LocDisplayName("{=TESTING}Maximum Tax Rate %"),
             LocCategory("Tax", "{=TESTING}Tax"),
             LocDescription("{=TESTING}Maximum tax rate kings can set (0-100)"),
             PropertyOrder(3), UsedImplicitly,
             Range(0f, 100f)]
            public float MaxTaxRate { get; set; } = 50f;

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
                    EnabledCommands.Append("Vassal, ");
                if (StatsEnabled)
                    EnabledCommands.Append("Stats, ");
                if (ReleaseEnabled)
                    EnabledCommands.Append("Release, ");
                if (ExpelEnabled)
                    EnabledCommands.Append("Expel, ");
                if (TaxEnabled)
                    EnabledCommands.Append("Tax, ");

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
                                    "Minimum Fiefs Amount={count}".Translate(("count", CreateKFiefMinimum.ToString())));
                if (VassalEnabled)
                    generator.Value("<strong>Vassal: </strong>" +
                                    $"Only Kings can make Vassals: {KingVassalsOnly}, " +
                                    $"Max Vassals: {VassalAmount}, " +
                                    $"Price={VassalPrice.ToString()}{Naming.Gold}" +
                                    $"Percent of Vassal's Mercenary Income given to Parent: " +
                                    $"{(int)(VassalMercIncomeShare * 100)}%, " + 
                                    $"Percent of Vassal's Fief Income given to Parent: " + 
                                    $"{(int)(VassalFiefIncomeShare * 100)}%");
                if (ReleaseEnabled)
                    generator.Value("<strong>Release: </strong>" +
                                    $"Price={ReleasePrice.ToString()}{Naming.Gold}");
                if (ExpelEnabled)
                    generator.Value("<strong>Expel: </strong>" +
                                    $"Price={ExpelPrice.ToString()}{Naming.Gold}");
                if (TaxEnabled)
                    generator.Value("<strong>Tax: </strong>" +
                                    $"Min Rate={MinTaxRate}%, Max Rate={MaxTaxRate}%");
            }
        }
        public override Type HandlerConfigType => typeof(Settings);
        

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;
            // Set vassal mercenary income share percentage
            if (VassalBehavior.Current != null)
            {
                VassalBehavior.MercenaryIncomeSharePercent = settings.VassalMercIncomeShare;
                VassalBehavior.FiefIncomeSharePercent = settings.VassalFiefIncomeShare;
            }
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
                onSuccess("{=EkmpJvML}Your clan {clanName} is a member of the kingdom {kingdomName}".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())));
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
                    HandleVassalCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                        break;
                case "release":
                    HandleReleaseCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "expel":
                    HandleExpelCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "stats":
                    HandleStatsCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case "tax":
                    HandleTaxCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                default:
                    onFailure("{=FFxXuX5i}Invalid or empty kingdom action, try (join/merc/rebel/leave/create/vassal/release/expel/stats)".Translate());
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
            
            var diplomacyHelper = Campaign.Current.GetCampaignBehavior<BLTDiplomacyHelper>();
            bool hassharedwar = false;
            if (!desiredKingdom.IsAtWarWith(adoptedHero.Clan))
            {
                foreach (Kingdom k in Kingdom.All.ToList())
                {
                    if (desiredKingdom.IsAtWarWith(k) && adoptedHero.Clan.IsAtWarWith(k))
                    {
                        hassharedwar = true;
                        break;
                    }
                }
            }
            else
            {
                hassharedwar = false;
            }

            if (diplomacyHelper.IsPeaceBlocked(adoptedHero.Clan, desiredKingdom) && !hassharedwar)
            {
                onFailure("Rebellion block");
                return;
            }
            if (desiredKingdom.Clans.Where(c => !VassalBehavior.Current.IsVassal(c) && !c.IsUnderMercenaryService).Count() >= settings.JoinMaxClans)
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

            onSuccess("{=LSea9bms}Your clan {clanName} has joined the kingdom {kingdomName}".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())));
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
                onFailure("{=OgwKEDza}You are the ruling clan, force transfer all fiefs of your kingdom to another to disband your kingdom".Translate());
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
            if (VassalBehavior.Current != null)
            {
                VassalBehavior.Current.OnClanChangedKingdom(adoptedHero.Clan, (Kingdom)oldBoss, null, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom, false);
            }
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
            var diplomacyHelper = Campaign.Current.GetCampaignBehavior<BLTDiplomacyHelper>();
            if (diplomacyHelper.IsPeaceBlocked(adoptedHero.Clan, desiredKingdom))
            {
                onFailure("Rebellion block");
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
            //if (desiredKingdom.Clans.Where(c => !VassalBehavior.Current.IsVassal(c) && !c.IsUnderMercenaryService).Count() >= settings.JoinMaxClans)
            //{
            //    onFailure("{=KFzBPUry}The kingdom {name} is full".Translate(("name", desiredName)));
            //    return;
            //}
            if (!mercforPlayer && BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.MercPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.MercPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            else if (mercforPlayer && BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.PlayerMercPrice)
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
            var culture = adoptedHero.Culture;
            creator.CreateKingdom(new TextObject(desiredName), new TextObject(desiredName), culture, adoptedHero.Clan, null, null, null, null);
            var newKingdom = adoptedHero.Clan.Kingdom;
            newKingdom.KingdomBudgetWallet = 2000000;
            adoptedHero.Clan.Influence = 2000;

            onSuccess("{=TESTING}Created kingdom {name}".Translate(("name", desiredName)));
            Log.ShowInformation("{=TESTING}{heroName} has founded kingdom {kingdom}!".Translate(("heroName", adoptedHero.Name.ToString()), ("kingdom", adoptedHero.Clan.Kingdom.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
        }

        private void HandleVassalCommand(Settings settings, Hero adoptedHero, string args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.VassalEnabled)
            {
                onFailure("Vassal creation is disabled");
                return;
            }

            var splitargs = args.Split(' ');
            var childName = splitargs[0];
            var setname = string.Join(" ", splitargs.Skip(1)).Trim();
            if (settings.KingVassalsOnly && adoptedHero.Clan.Kingdom.Leader != adoptedHero)
            {
                onFailure("{=GEGrsLPm}You must be a king to create vassals".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=RvkJO6J9}Your clan is not in a kingdom".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=HS14GdUa}You cannot manage your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(childName) || string.IsNullOrWhiteSpace(setname))
            {
                onFailure("{=ETfJQatX}Usage: (vassal) (hero name) (clan name)".Translate());
                return;
            }
            var existingClan = Clan.All.FirstOrDefault(c => c.Name.ToString().ToLower() == setname.ToLower() || c.Name.ToString().ToLower() == $"[vassal] {setname.ToLower()}" || c.Name.ToString().ToLower() == $"[blt clan] {setname.ToLower()}");
            if (existingClan != null)
            {
                onFailure("{=TESTING}A clan with the name {name} already exists".Translate(("name", setname)));
                return;
            }
            if (VassalBehavior.Current.GetVassalClans(adoptedHero.Clan).Count >= settings.VassalAmount)
            {
                onFailure($"Max vassals{settings.VassalAmount}");
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.VassalPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.VassalPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }


            Hero vassal = adoptedHero.Clan.Heroes.Find(h => h.FirstName.ToString().ToLower() == childName.ToLower());

            if (vassal == null)
            {
                onFailure($"No hero named {childName}");
                return;
            }
            if (vassal.Age < 18)
            {
                onFailure($"{childName} is too young");
                return;
            }
            if (vassal.Spouse != null && vassal.Spouse.IsAdopted())
            {
                onFailure("Cannot vassal a blt spouse");
                return;
            }
            if (vassal.IsAdopted())
            {
                onFailure("Cannot vassal a blt");
                return;                  
            }
            if (vassal.IsPrisoner)
            {
                onFailure($"{childName} is prisoner");
                return;
            }
            if (vassal.HeroState == Hero.CharacterStates.Fugitive || vassal.HeroState == Hero.CharacterStates.Released || vassal.HeroState == Hero.CharacterStates.Traveling)
            {
                onFailure($"{childName} is busy");
                return;
            }
            if (vassal.Spouse == null)
            {
                HeroFeatures.SpawnSpouse(vassal, vassal.Culture);
            }
            if (vassal.GovernorOf != null)
            {
                ChangeGovernorAction.RemoveGovernorOf(vassal);
            }
            if (vassal.PartyBelongedTo != null)
            {
                var oldParty = vassal.PartyBelongedTo;
                bool wasLeader = oldParty.LeaderHero == vassal;
                oldParty.MemberRoster.RemoveTroop(vassal.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
                MakeHeroFugitiveAction.Apply(vassal, false);
                if (wasLeader && oldParty.IsLordParty)
                    DisbandPartyAction.StartDisband(oldParty);
            }
            var fullClanName = $"[Vassal] {setname}";
            var newClan = Clan.CreateClan(fullClanName);
            newClan.ChangeClanName(new TextObject(fullClanName), new TextObject(fullClanName));
            newClan.Culture = vassal.Culture;
            newClan.Banner = Banner.CreateOneColoredBannerWithOneIcon(adoptedHero.Clan.Banner.GetPrimaryColor(), adoptedHero.Clan.Banner.GetFirstIconColor(), -1);
            if (adoptedHero.Clan.Kingdom != null)
            {
            AdoptedHeroFlags._allowKingdomMove = true;
            if (adoptedHero.Clan.IsUnderMercenaryService)
                ChangeKingdomAction.ApplyByJoinFactionAsMercenary(newClan, adoptedHero.Clan.Kingdom);
            else
                ChangeKingdomAction.ApplyByJoinToKingdom(newClan, adoptedHero.Clan.Kingdom);
            AdoptedHeroFlags._allowKingdomMove = false;
            }
            newClan.SetInitialHomeSettlement(Settlement.All.SelectRandom());
            vassal.Clan = newClan;
            if (vassal.Spouse != null)
            {
                if (vassal.Spouse.GovernorOf != null)
                {
                    ChangeGovernorAction.RemoveGovernorOf(vassal.Spouse);
                }
                if (vassal.Spouse.PartyBelongedTo != null)
                {
                    var oldParty = vassal.Spouse.PartyBelongedTo;
                    bool wasLeader = oldParty.LeaderHero == vassal.Spouse;
                    oldParty.MemberRoster.RemoveTroop(vassal.Spouse.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
                    MakeHeroFugitiveAction.Apply(vassal.Spouse, false);
                    if (wasLeader && oldParty.IsLordParty)
                        DisbandPartyAction.StartDisband(oldParty);
                }
                vassal.Spouse.Clan = newClan;
            }
            if (vassal.Children.Count > 0)
            {
                foreach (Hero child in vassal.Children)
                {
                    if (child.GovernorOf != null)
                    {
                        ChangeGovernorAction.RemoveGovernorOf(child);
                    }
                    if (child.PartyBelongedTo != null)
                    {
                        var oldParty = child.PartyBelongedTo;
                        bool wasLeader = oldParty.LeaderHero == child;
                        oldParty.MemberRoster.RemoveTroop(child.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
                        MakeHeroFugitiveAction.Apply(child, false);
                        if (wasLeader && oldParty.IsLordParty)
                            DisbandPartyAction.StartDisband(oldParty);
                    }
                    child.Clan = newClan;
                }
            }
            var tierModel = Campaign.Current.Models.ClanTierModel;
            newClan.AddRenown(tierModel.GetRequiredRenownForTier(tierModel.CompanionToLordClanStartingTier));
            newClan.SetLeader(vassal);
            newClan.IsNoble = true;
            vassal.Gold += 50000;
            CampaignEventDispatcher.Instance.OnClanCreated(newClan, false);
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(adoptedHero, vassal, 100, false);

            // Register the vassal with the VassalBehavior
            VassalBehavior.Current?.RegisterVassal(newClan, adoptedHero.Clan);

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.VassalPrice, true);
            string response = $"Vassal created by {adoptedHero.FirstName.ToString()}: {newClan.Name.ToString()}";
            Log.LogFeedResponse(response);
        }
        private void HandleReleaseCommand(Settings settings, Hero adoptedHero, string targetName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.ReleaseEnabled)
            {
                onFailure("Release is disabled");
                return;
            }
            if (adoptedHero.Clan.Kingdom == null || adoptedHero.Clan.Kingdom.Leader != adoptedHero)
            {
                onFailure("You must be the kingdom leader to release clans");
                return;
            }
            if (string.IsNullOrWhiteSpace(targetName))
            {
                onFailure("Usage: (release) (hero name or clan name)");
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.ReleasePrice)
            {
                onFailure(Naming.NotEnoughGold(settings.ReleasePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            // Find the target clan by searching for clan name or leader name with possible prefixes/suffixes
            Clan targetClan = null;
            var possiblePrefixes = new[] { "", "[Vassal] ", "[BLT Clan] " };
            var possibleSuffixes = new[] { "", " [BLT]", " [DEV]" };

            // Search by clan name with prefixes
            foreach (var prefix in possiblePrefixes)
            {
                var searchName = prefix + targetName;
                targetClan = adoptedHero.Clan.Kingdom.Clans
                    .FirstOrDefault(c => c.Name.ToString().Equals(searchName, StringComparison.OrdinalIgnoreCase));

                if (targetClan != null) break;
            }

            // If not found, search by leader name with suffixes
            if (targetClan == null)
            {
                foreach (var suffix in possibleSuffixes)
                {
                    var searchName = targetName + suffix;
                    targetClan = adoptedHero.Clan.Kingdom.Clans
                        .FirstOrDefault(c => c.Leader?.FirstName.ToString().Equals(searchName, StringComparison.OrdinalIgnoreCase) == true);

                    if (targetClan != null) break;
                }
            }

            if (targetClan == null)
            {
                onFailure($"Could not find clan or hero named {targetName} in your kingdom");
                return;
            }

            if (targetClan == adoptedHero.Clan)
            {
                onFailure("You cannot release your own clan");
                return;
            }
            if (targetClan.Kingdom != adoptedHero.Clan.Kingdom)
            {
                onFailure($"{targetClan.Name} is not in your kingdom");
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.ReleasePrice, true);

            // Release vassals first
            if (VassalBehavior.Current != null)
            {
                var vassals = VassalBehavior.Current.GetVassalClans(targetClan).ToList();
                foreach (var vassal in vassals)
                {
                    AdoptedHeroFlags._allowKingdomMove = true;
                    vassal.ClanLeaveKingdom();
                    AdoptedHeroFlags._allowKingdomMove = false;
                    VassalBehavior.Current.OnClanChangedKingdom(vassal, adoptedHero.Clan.Kingdom, null, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom, false);
                }
            }

            // Release the main clan
            AdoptedHeroFlags._allowKingdomMove = true;
            if (targetClan.IsUnderMercenaryService)
            {
                targetClan.EndMercenaryService(true);
            }
            targetClan.ClanLeaveKingdom();
            AdoptedHeroFlags._allowKingdomMove = false;

            if (VassalBehavior.Current != null)
            {
                VassalBehavior.Current.OnClanChangedKingdom(targetClan, adoptedHero.Clan.Kingdom, null, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom, false);
            }

            onSuccess($"Released {targetClan.Name} from {adoptedHero.Clan.Kingdom.Name} with all their lands");
            Log.ShowInformation($"{adoptedHero.Name} has released {targetClan.Name} from {adoptedHero.Clan.Kingdom.Name}!", adoptedHero.CharacterObject, Log.Sound.Horns2);
        }

        private void HandleExpelCommand(Settings settings, Hero adoptedHero, string targetName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.ExpelEnabled)
            {
                onFailure("Expel is disabled");
                return;
            }
            if (adoptedHero.Clan.Kingdom == null || adoptedHero.Clan.Kingdom.Leader != adoptedHero)
            {
                onFailure("You must be the kingdom leader to expel clans");
                return;
            }
            if (string.IsNullOrWhiteSpace(targetName))
            {
                onFailure("Usage: (expel) (hero name or clan name)");
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.ExpelPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.ExpelPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            // Find the target clan by searching for clan name or leader name with possible prefixes/suffixes
            Clan targetClan = null;
            var possiblePrefixes = new[] { "", "[Vassal] ", "[BLT Clan] " };
            var possibleSuffixes = new[] { "", " [BLT]", " [DEV]" };

            // Search by clan name with prefixes
            foreach (var prefix in possiblePrefixes)
            {
                var searchName = prefix + targetName;
                targetClan = adoptedHero.Clan.Kingdom.Clans
                    .FirstOrDefault(c => c.Name.ToString().Equals(searchName, StringComparison.OrdinalIgnoreCase));

                if (targetClan != null) break;
            }

            // If not found, search by leader name with suffixes
            if (targetClan == null)
            {
                foreach (var suffix in possibleSuffixes)
                {
                    var searchName = targetName + suffix;
                    targetClan = adoptedHero.Clan.Kingdom.Clans
                        .FirstOrDefault(c => c.Leader?.FirstName.ToString().Equals(searchName, StringComparison.OrdinalIgnoreCase) == true);

                    if (targetClan != null) break;
                }
            }

            if (targetClan == null)
            {
                onFailure($"Could not find clan or hero named {targetName} in your kingdom");
                return;
            }

            if (targetClan == adoptedHero.Clan)
            {
                onFailure("You cannot expel your own clan");
                return;
            }
            if (targetClan.Kingdom != adoptedHero.Clan.Kingdom)
            {
                onFailure($"{targetClan.Name} is not in your kingdom");
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.ExpelPrice, true);

            // Transfer all fiefs from vassals first
            if (VassalBehavior.Current != null)
            {
                var vassals = VassalBehavior.Current.GetVassalClans(targetClan).ToList();
                foreach (var vassal in vassals)
                {
                    AdoptedHeroFlags._allowKingdomMove = true;
                    foreach (var fief in vassal.Settlements.ToList())
                    {
                        ChangeOwnerOfSettlementAction.ApplyByDefault(adoptedHero, fief);
                    }
                    vassal.ClanLeaveKingdom();
                    AdoptedHeroFlags._allowKingdomMove = false;
                    VassalBehavior.Current.OnClanChangedKingdom(vassal, adoptedHero.Clan.Kingdom, null, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom, false);
                }
            }

            // Transfer all fiefs from the main clan to the king
            AdoptedHeroFlags._allowKingdomMove = true;
            foreach (var fief in targetClan.Settlements.ToList())
            {
                ChangeOwnerOfSettlementAction.ApplyByDefault(adoptedHero, fief);
            }

            // Expel the clan
            if (targetClan.IsUnderMercenaryService)
            {
                targetClan.EndMercenaryService(true);
            }
            targetClan.ClanLeaveKingdom();
            AdoptedHeroFlags._allowKingdomMove = false;

            if (VassalBehavior.Current != null)
            {
                VassalBehavior.Current.OnClanChangedKingdom(targetClan, adoptedHero.Clan.Kingdom, null, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom, false);
            }

            onSuccess($"Expelled {targetClan.Name} from {adoptedHero.Clan.Kingdom.Name} and seized all their lands");
            Log.ShowInformation($"{adoptedHero.Name} has expelled {targetClan.Name} from {adoptedHero.Clan.Kingdom.Name}!", adoptedHero.CharacterObject, Log.Sound.Horns2);
        }
        private void HandleTaxCommand(Settings settings, Hero adoptedHero, string args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.TaxEnabled)
            {
                onFailure("Kingdom taxation is disabled");
                return;
            }

            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("You need to be in a kingdom to view tax rates");
                return;
            }

            if (KingdomTaxBehavior.Current == null)
            {
                onFailure("Tax system is not initialized");
                return;
            }

            bool isKing = adoptedHero.Clan.Kingdom.Leader == adoptedHero;
            float currentRate = KingdomTaxBehavior.Current.GetKingdomTaxRate(adoptedHero.Clan.Kingdom);

            // If not king, just show the tax rate
            if (!isKing)
            {
                onSuccess($"{adoptedHero.Clan.Kingdom.Name} has a tax rate of {(currentRate * 100f):F1}%");
                return;
            }

            // King functionality
            // If no args, show current tax rate and instructions
            if (string.IsNullOrWhiteSpace(args))
            {
                onSuccess($"Current tax rate: {(currentRate * 100f):F1}% | Range: {settings.MinTaxRate}%-{settings.MaxTaxRate}% | Usage: !kingdom tax <rate>");
                return;
            }

            // Parse the tax rate
            if (!float.TryParse(args, out float newRate))
            {
                onFailure("Invalid tax rate. Usage: !kingdom tax <rate> (e.g., !kingdom tax 15 for 15%)");
                return;
            }

            // Validate range
            if (newRate < settings.MinTaxRate || newRate > settings.MaxTaxRate)
            {
                onFailure($"Tax rate must be between {settings.MinTaxRate}% and {settings.MaxTaxRate}%");
                return;
            }

            // Set the new tax rate (convert percentage to decimal)
            float taxRateDecimal = newRate / 100f;
            KingdomTaxBehavior.Current.SetKingdomTaxRate(adoptedHero.Clan.Kingdom, taxRateDecimal);

            onSuccess($"Set {adoptedHero.Clan.Kingdom.Name} tax rate to {newRate:F1}%");
            Log.ShowInformation($"{adoptedHero.Name} has set {adoptedHero.Clan.Kingdom.Name} tax rate to {newRate:F1}%!", adoptedHero.CharacterObject);
        }
    }
}