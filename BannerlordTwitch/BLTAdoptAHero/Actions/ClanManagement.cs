using System;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using HarmonyLib;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=NGEyUHsh}Clan Management"),
     LocDescription("{=hz2tRydD}Allow viewer to change their clan or make leader decisions"),
     UsedImplicitly]
    public class ClanManagement : HeroCommandHandlerBase
    {
        private static Harmony harmonyInstance = null;
        static ClanManagement()
        {
            InitializeHarmony();
        }

        private static void InitializeHarmony()
        {
            if (harmonyInstance == null)
            {
                harmonyInstance = new Harmony("BLTClanManagement");
                harmonyInstance.PatchAll();
            }
        }
        [CategoryOrder("Join", 0),
         CategoryOrder("Create", 1),
         CategoryOrder("Lead", 2),
         CategoryOrder("Rename", 3),
         CategoryOrder("Stats", 4),
         CategoryOrder("Party", 5),
         CategoryOrder("Leave", 6),
         //CategoryOrder("Disband", 6),
         CategoryOrder("Buy Noble Title", 7),
         CategoryOrder("Edit Banner", 8)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=zeD9NYrA}Enable joining clans command"),
             PropertyOrder(1), UsedImplicitly]
            public bool JoinEnabled { get; set; } = true;

            [LocDisplayName("{=jwSrIS8n}Max Heroes"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=nC2MvvB6}Maximum heroes (includes NPC's) before join is disallowed"),
             PropertyOrder(2), UsedImplicitly]
            public int JoinMaxHeroes { get; set; } = 50;

            [LocDisplayName("{=d5WMYSvO}Gold Cost"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=bxuW8r3J}Cost of joining a clan"),
             PropertyOrder(3), UsedImplicitly]
            public int JoinPrice { get; set; } = 150000;

            [LocDisplayName("{=k3ihPbMl}Players Clan?"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=KA3w5CSP}Allow viewers to join the players clan"),
             PropertyOrder(4), UsedImplicitly]
            public bool JoinAllowPlayer { get; set; } = false;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Create", "{=9lAIycwE}Create"),
             LocDescription("{=x5aY2Ryn}Enable creating clans command"),
             PropertyOrder(1), UsedImplicitly]
            public bool CreateEnabled { get; set; } = true;

            [LocDisplayName("{=d5WMYSvO}Gold Cost"),
             LocCategory("Create", "{=9lAIycwE}Create"),
             LocDescription("{=KvYA5eAy}Cost of creating a clan"),
             PropertyOrder(2), UsedImplicitly]
            public int CreatePrice { get; set; } = 2500000;

            [LocDisplayName("{=d5WMYSvO}Starting renown"),
             LocCategory("Create", "{=9lAIycwE}Create"),
             LocDescription("{=KvYA5eAy}Starting renown(T1:50, T2:150, T3:350, T4:900, T5:2350, T6:6150)"),
             PropertyOrder(3), UsedImplicitly]
            public int Renown { get; set; } = 50;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Lead", "{=TrSSHcbH}Lead"),
             LocDescription("{=KQWPJonx}Enable leading clans command"),
             PropertyOrder(1), UsedImplicitly]
            public bool LeadEnabled { get; set; } = true;

            [LocDisplayName("{=d5WMYSvO}Gold Cost"),
             LocCategory("Lead", "{=TrSSHcbH}Lead"),
             LocDescription("{=7Zqi5Ehg}Cost of leading a clan"),
             PropertyOrder(2), UsedImplicitly]
            public int LeadPrice { get; set; } = 1000000;

            [LocDisplayName("{=xcThCwjr}Challenge Heroes"),
             LocCategory("Lead", "{=TrSSHcbH}Lead"),
             LocDescription("{=LWj6LPyH}Toggle whether or not trying to lead a clan already led by a BLT hero is possible - random chance they win based on skill difference"),
             PropertyOrder(3), UsedImplicitly]
            public bool LeadChallengeHeroes { get; set; } = true;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Rename", "{=ugFdRADy}Rename"),
             LocDescription("{=NhJk9hgu}Enable renaming clans command"),
             PropertyOrder(1), UsedImplicitly]
            public bool RenameEnabled { get; set; } = true;

            [LocDisplayName("{=d5WMYSvO}Gold Cost"),
             LocCategory("Rename", "{=ugFdRADy}Rename"),
             LocDescription("{=d2H2BrIG}Cost of renaming a clan"),
             PropertyOrder(2), UsedImplicitly]
            public int RenamePrice { get; set; } = 1000000;

            [LocDisplayName("{=mlayrmHr}Stats"),
             LocCategory("Stats", "{=mlayrmHr}Stats"),
             LocDescription("{=vNGlBZUB}Enable stats command"),
             PropertyOrder(1), UsedImplicitly]
            public bool StatsEnabled { get; set; } = true;

            [LocDisplayName("{=7XUApUQM}Fiefs"),
             LocCategory("Stats", "{=mlayrmHr}Stats"),
             LocDescription("{=KC7IE9Bt}Enable fiefs command"),
             PropertyOrder(2), UsedImplicitly]
            public bool FiefsEnabled { get; set; } = true;

            [LocDisplayName("{=dvp0XkiR}Leave"),
             LocCategory("Leave", "{=dvp0XkiR}Leave"),
             LocDescription("Allow BLTs to leave their clan.   WARNING: Leaving will turn their character into a wanderer, and they may have to buy their Nobility back!"),
             PropertyOrder(1), UsedImplicitly]
            public bool LeaveEnabled { get; set; } = true;

            //[LocDisplayName("{=pYjIUlTE}Enabled"),
            // LocCategory("Disband", "{=TESTING}Disband"),
            // LocDescription("Enable BLTs disbanding an empty clan.   WARNING: Disbanding will turn their character into a wanderer, and they may have to buy their Nobility back!"),
            // PropertyOrder(1), UsedImplicitly]
            //public bool DisbandEnabled { get; set; } = true;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Buy Noble Title", "{=moApZJvC}Buy Noble Title"),
             LocDescription("Allow non-noble BLTs to buy their way into being a Lord, allowing their Hero's AI many more clan and kingdom actions.   NOTE: Buying title is needed when joining.Disabling this will simply make BLT's into Lords when joining a clan."),
             PropertyOrder(1), UsedImplicitly]
            public bool BuyTitleEnabled { get; set; } = true;

            [LocDisplayName("{=d5WMYSvO}Gold Cost"),
             LocCategory("Buy Noble Title", "{=moApZJvC}Buy Noble Title"),
             LocDescription("Cost of Becoming a Noble"),
             PropertyOrder(2), UsedImplicitly]
            public int TitlePrice { get; set; } = 200000;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Edit Banner", "{=UnFfiM9h}Edit Banner"),
             LocDescription("Edit your banner with a code. Make your banner at https://bannerlord.party/banner"),
             PropertyOrder(1), UsedImplicitly]
            public bool EditBannerEnabled { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                var EnabledCommands = new StringBuilder();
                if (JoinEnabled)
                    EnabledCommands = EnabledCommands.Append("{=q5JhpNMF}Join, ".Translate());
                if (CreateEnabled)
                    EnabledCommands = EnabledCommands.Append("{=9lAIycwE}Create, ".Translate());
                if (LeadEnabled)
                    EnabledCommands = EnabledCommands.Append("{=TrSSHcbH}Lead, ".Translate());
                if (RenameEnabled)
                    EnabledCommands = EnabledCommands.Append("{=ugFdRADy}Rename, ".Translate());
                if (StatsEnabled)
                    EnabledCommands = EnabledCommands.Append("{=mlayrmHr}Stats, ".Translate());
                if (FiefsEnabled)
                    EnabledCommands = EnabledCommands.Append("{=7XUApUQM}Fiefs, ".Translate());
                if (LeaveEnabled)
                    EnabledCommands = EnabledCommands.Append("{=dvp0XkiR}Leave, ".Translate());
                //if (DisbandEnabled)
                //    EnabledCommands = EnabledCommands.Append("{=TESTING}Disband, ".Translate());
                //    Log.ShowInformation("disband");
                if (BuyTitleEnabled)
                    EnabledCommands = EnabledCommands.Append("{=moApZJvC}Buy Noble Title, ".Translate());
                if (EditBannerEnabled)
                    EnabledCommands = EnabledCommands.Append("{=UnFfiM9h}Edit Banner, ".Translate());
                if (EnabledCommands != null)
                    generator.Value("<strong>Enabled Commands:</strong> {commands}".Translate(("commands", EnabledCommands.ToString().Substring(0, EnabledCommands.ToString().Length - 2))));

                if (JoinEnabled)
                    generator.Value("<strong>" +
                                    "Join Config: " +
                                    "</strong>" +
                                    "Max Heroes={maxHeroes}, ".Translate(("maxHeroes", JoinMaxHeroes)) +
                                    "Price={price}{icon}, ".Translate(("price", JoinPrice.ToString()), ("icon", Naming.Gold)) +
                                    "Allow Join Players Clan?={allowPlayer}".Translate(("allowPlayer", JoinAllowPlayer.ToString())));
                if (CreateEnabled)
                    generator.Value("<strong>" +
                                    "Create Config: " +
                                    "</strong>" +
                                    "Price={price}{icon}, ".Translate(("price", CreatePrice.ToString()), ("icon", Naming.Gold)) +
                                    "Renown={renown}, ".Translate(("renown", Renown)));
                if (LeadEnabled)
                    generator.Value("<strong>" +
                                    "Lead Config: " +
                                    "</strong>" +
                                    "Price={price}{icon}, ".Translate(("price", LeadPrice.ToString()), ("icon", Naming.Gold)) +
                                    "Challenge Heroes?={challengeHeroes}".Translate(("challengeHeroes", LeadChallengeHeroes.ToString())));
                if (RenameEnabled)
                    generator.Value("<strong>" +
                                    "Rename Config: " +
                                    "</strong>" +
                                    "Price={price}{icon}".Translate(("price", RenamePrice.ToString()), ("icon", Naming.Gold)));
                if (BuyTitleEnabled)
                    generator.Value("<strong>" +
                                    "Buy Noble Title Config: " +
                                    "</strong>" +
                                    "Price={price}{icon}".Translate(("price", TitlePrice.ToString()), ("icon", Naming.Gold)));
            }
        }
        public override Type HandlerConfigType => typeof(Settings);
        //private string ConvertPastebinUrlToRaw(string url)
        //{
        //    if (url.Contains("pastes.io/"))
        //    {
        //        return url.Replace("pastes.io/", "pastes.io/raw/");
        //    }
        //    if (url.Contains("pastesio/"))
        //    {
        //        return url.Replace("pastesio/", "pastes.io/raw/");
        //    }

        //    return url;
        //}


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
                onFailure("{=MPTOZqMS}You cannot manage your clan, as a mission is active!".Translate());
                return;
            }
            //This is deemed annoying and unnecessary in the community, especially since you can't effect being captured or released at all through clan commands, so there are very few possible conflicts or errors from this.
            //if (adoptedHero.HeroState == Hero.CharacterStates.Prisoner)
            //{
            //    onFailure("{=oxNqBy4k}You cannot manage your clan, as you are a prisoner!".Translate());
            //    return;
            //}

            if (context.Args.IsEmpty())
            {
                if (adoptedHero.Clan == null)
                {
                    onFailure("{=B86KnTcu}You are not in a clan".Translate());
                    return;
                }
                onSuccess("{=xMSAI7HK}Your clan is {clanName}".Translate(("clanName", adoptedHero.Clan.Name.ToString())));
                return;
            }

            var splitArgs = context.Args.Split(' ');
            var command = splitArgs[0];
            var desiredName = string.Join(" ", splitArgs.Skip(1)).Trim();
            // Special case: !clan buy title
            if (command.Equals("buy", StringComparison.OrdinalIgnoreCase) &&
                splitArgs.Length > 1 &&
                splitArgs[1].Equals("title", StringComparison.OrdinalIgnoreCase))
            {
                command = "buy title";
            }
            var bannerCodeOrUrl = desiredName;

            string joinCommand = "{=I2jEHyAY}join".Translate();
            string createCommand = "{=ymJh4yMY}create".Translate();
            string leadCommand = "{=pumBg7sU}lead".Translate();
            string renameCommand = "{=ek75vkTT}rename".Translate();
            string statsCommand = "{=VB2W7FoL}stats".Translate();
            string partyCommand = "{=iXrUl79z}party".Translate();
            string fiefsCommand = "{=D909bAhX}fiefs".Translate();
            string leaveCommand = "{=0oxt9iXm}leave".Translate();
            //string disbandCommand = "{=TESTING}disband".Translate();
            string buytitleCommand = "{=jk3WfmjK}buy title".Translate();
            string bannerCommand = "{=15vWZKaM}banner".Translate();

            switch (command.ToLower())
            {
                case var _ when command.ToLower() == joinCommand:
                    HandleJoinCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case var _ when command.ToLower() == createCommand:
                    HandleCreateCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case var _ when command.ToLower() == leadCommand:
                    HandleLeadCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case var _ when command.ToLower() == renameCommand:
                    HandleRenameCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case var _ when command.ToLower() == statsCommand:
                    HandleStatsCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case var _ when command.ToLower() == partyCommand:
                    HandlePartyCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case var _ when command.ToLower() == fiefsCommand:
                    HandleFiefsCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case var _ when command.ToLower() == leaveCommand:
                    HandleLeaveCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                //case var _ when command.ToLower() == disbandCommand:
                //    HandleDisbandCommand(settings, adoptedHero, onSuccess, onFailure);
                //    break;
                case var _ when command.ToLower() == buytitleCommand:
                    HandleBuyTitleCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case var _ when command.ToLower() == bannerCommand:
                    {
                        string bannerCode = bannerCodeOrUrl;
                        //if (bannerCodeOrUrl.StartsWith("https://pastes.io/", StringComparison.OrdinalIgnoreCase) || bannerCodeOrUrl.StartsWith("https://pastesio/", StringComparison.OrdinalIgnoreCase))
                        //{
                        //    bannerCodeOrUrl = ConvertPastebinUrlToRaw(bannerCodeOrUrl);
                        //    try
                        //    {
                        //        using var client = new HttpClient();
                        //        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                        //        bannerCode = client.GetStringAsync(bannerCodeOrUrl).GetAwaiter().GetResult().Trim();
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        onFailure($"Failed to fetch banner code from URL: {ex.Message}");
                        //        return;
                        //    }
                        //}

                        HandleBannerCommand(settings, adoptedHero, bannerCode, onSuccess, onFailure);
                        break;
                    }
                default:
                    onFailure("{=pkzDqw18}Invalid or empty clan action, try (join/create/lead/rename/stats/party/fiefs/leave/buy title/banner)".Translate());
                    break;
            }
        }

        private void HandleJoinCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.JoinEnabled)
            {
                onFailure("{=VupTnRNX}Joining clans is disabled".Translate());
                return;
            }
            if (adoptedHero.IsClanLeader)
            {
                onFailure("{=OrBEbanC}You cannot join another clan as you are the leader of your clan".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                onFailure("{=3ktTpCyC}(join) (clan name)".Translate());
                return;
            }

            var desiredClan = CampaignHelpers.AllHeroes.Select(h => h.Clan).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);
            if (desiredClan == null)
            {
                onFailure("{=xylTvKyE}Could not find the clan with the name {name}".Translate(("name", desiredName)));
                return;
            }
            if (desiredClan.Heroes.Count >= settings.JoinMaxHeroes)
            {
                onFailure("{=aoxW7fmn}The clan {name} is full".Translate(("name", desiredName)));
                return;
            }
            if (desiredClan == Hero.MainHero.Clan && !settings.JoinAllowPlayer)
            {
                onFailure("{=jptOPf36}Joining the players clan is disabled".Translate());
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.JoinPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.JoinPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.JoinPrice, true);
            adoptedHero.Clan = desiredClan;
            onSuccess("{=T7U1Piwx}Joined clan {name}".Translate(("name", desiredName)));
            Log.ShowInformation("{=WseRTV8W}{heroName} has joined clan {clanName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("clanName", adoptedHero.Clan.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
            if ((!settings.BuyTitleEnabled) && ((adoptedHero.Occupation != Occupation.Lord) && (adoptedHero.Clan != null)))
            {
                onFailure("{=6yQUu78N}{heroName} has become a noble!".Translate(("heroName", adoptedHero.Name.ToString())));
                adoptedHero.SetNewOccupation(Occupation.Lord);
            }
        }

        private void HandleCreateCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.CreateEnabled)
            {
                onFailure("{=drmqcbE2}Creating clans is disabled".Translate());
                return;
            }
            if (adoptedHero.IsClanLeader)
            {
                onFailure("{=zZYHVBZ6}You cannot create another clan as you are the leader of your clan".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                onFailure("{=6vTxAMVx}(create) (clan name)".Translate());
                return;
            }

            var fullClanName = $"[BLT Clan] {desiredName}";
            var existingClan = CampaignHelpers.AllHeroes.Select(h => h.Clan).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(fullClanName, StringComparison.OrdinalIgnoreCase) == true);
            if (existingClan != null)
            {
                onFailure("{=Aae45bKp}A clan with the name {name} already exists".Translate(("name", desiredName)));
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.CreatePrice)
            {
                onFailure(Naming.NotEnoughGold(settings.CreatePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.CreatePrice, true);
            var newClan = Clan.CreateClan(fullClanName);
            var clanCulture = adoptedHero.Culture;
            var clanBanner = Banner.CreateRandomBanner();
            newClan.InitializeClan(new TextObject(fullClanName), new TextObject(fullClanName), clanCulture, clanBanner);
            newClan.UpdateHomeSettlement(Settlement.All.SelectRandom());
            //BLTClanBehavior.Current?.RegisterBLTClan(newClan);
            adoptedHero.Clan = newClan;
            newClan.AddRenown(settings.Renown, false);
            adoptedHero.Gold = 50000;
            newClan.SetLeader(adoptedHero);
            if (!CampaignHelpers.IsEncyclopediaBookmarked(newClan))
                CampaignHelpers.AddEncyclopediaBookmarkToItem(newClan);
            onSuccess("{=omDrEeDx}Created and leading clan {name}".Translate(("name", fullClanName)));
            Log.ShowInformation("{=TsmDfvuz}{heroName} has created and is leading clan {clanName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("clanName", adoptedHero.Clan.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
            if ((adoptedHero.Occupation != Occupation.Lord) && (adoptedHero.Clan != null))
            {
                onSuccess("{=vBmuM0Hn}{heroName} has become a noble!".Translate(("heroName", adoptedHero.Name.ToString())));
                adoptedHero.SetNewOccupation(Occupation.Lord);
            }
        }

        private void HandleLeadCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.LeadEnabled)
            {
                onFailure("{=OVxeTkYW}Leading clans is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=xH2geJ28}You are not in a clan".Translate());
                return;
            }
            if (adoptedHero.Clan == Hero.MainHero.Clan)
            {
                onFailure("{=jGg5q1JD}You cannot lead the players clan".Translate());
                return;
            }
            if (adoptedHero.IsClanLeader)
            {
                onFailure("{=cRpqnI3B}You are already the leader of your clan".Translate());
                return;
            }
            if ((adoptedHero.Occupation != Occupation.Lord) && (!settings.BuyTitleEnabled))
            {
                onFailure("{=xjAujM6b}You must be a noble to usurp a clan!".Translate());
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.LeadPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.LeadPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            if (adoptedHero.Clan.Leader.Name.Contains(BLTAdoptAHeroModule.Tag))
            {
                if (!settings.LeadEnabled)
                {
                    onFailure("{=L7PFIoD6}Leading clans led by other BLT Heroes is disabled".Translate());
                    return;
                }
                Hero oldLeader = adoptedHero.Clan.Leader;
                if (MBRandom.RandomInt(0, 10) < MBMath.ClampInt(oldLeader.Level - adoptedHero.Level, 0, 10))
                {
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.LeadPrice, true);
                    onFailure("{=PlG3BI1y}You have been bested in battle by {oldLeader} and failed to lead your clan".Translate(("oldLeader", oldLeader.Name.ToString())));
                    return;
                }
                else
                {
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.LeadPrice, true);
                    adoptedHero.Clan.SetLeader(adoptedHero);
                    Log.ShowInformation("{=ZCYqf89T}{heroName} has usurped {oldLeader} and is now leading clan {clanName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("oldLeader", oldLeader.Name.ToString()), ("clanName", adoptedHero.Clan.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
                    onSuccess("{=nDZKenCx}You have successfully taken over the leadership of your clan".Translate());
                    return;
                }
            }
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.LeadPrice, true);
            adoptedHero.Clan.SetLeader(adoptedHero);
            onSuccess("{=MbMibbNm}You are now the leader of your clan".Translate());
            Log.ShowInformation("{=Zc5EPvQU}{heroName} is now leading clan {clanName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("clanName", adoptedHero.Clan.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
        }

        private void HandleRenameCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.RenameEnabled)
            {
                onFailure("{=4pDk2rNm}Renaming clans is disabled".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                onFailure("{=vjHYEbRR}(rename) (clan name)".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=jQZ93EID}You are not the leader of your clan".Translate());
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.RenamePrice)
            {
                onFailure(Naming.NotEnoughGold(settings.RenamePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.RenamePrice, true);
            var fullClanName = $"[BLT Clan] {desiredName}";
            var oldName = adoptedHero.Clan.Name.ToString();
            adoptedHero.Clan.ChangeClanName(new TextObject(fullClanName), new TextObject(fullClanName));
            onSuccess("{=hNtBu8rx}Renamed clan to {name}".Translate(("name", fullClanName)));
            Log.ShowInformation("{=d3tUyvv3}{heroName} has renamed clan {oldName} to {clanName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("oldName", oldName), ("clanName", adoptedHero.Clan.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
        }

        private void HandleStatsCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.StatsEnabled)
            {
                onFailure("{=9XKKVMKf}Clan stats is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }

            var clanStats = new StringBuilder();
            clanStats.Append("{=Ki8jvwkw}Clan Name: {name} | ".Translate(("name", adoptedHero.Clan.Name.ToString())));
            clanStats.Append("{=sZcYhSOL}Leader: {leader} | ".Translate(("leader", adoptedHero.Clan.Leader.Name.ToString())));
            if (adoptedHero.Clan.Kingdom != null)
                clanStats.Append("{=ch83d8zT}Kingdom: {kingdom} | ".Translate(("kingdom", adoptedHero.Clan.Kingdom.Name.ToString())));
            clanStats.Append("{=Sg11nEUe}Tier: {tier}({renown}) | ".Translate(("tier", adoptedHero.Clan.Tier.ToString()), ("renown", Math.Round(adoptedHero.Clan.Renown).ToString())));
            clanStats.Append("{=ZFGikYn8}Strength: {strength} | ".Translate(("strength", Math.Round(adoptedHero.Clan.TotalStrength).ToString())));
            if (adoptedHero.IsPrisoner && adoptedHero.PartyBelongedToAsPrisoner.IsMobile)
                clanStats.Append("{=zVDODxiN}Prisoner: {prisoner} | ".Translate(("prisoner", adoptedHero.PartyBelongedToAsPrisoner.Name.ToString())));
            if (adoptedHero.IsPrisoner && adoptedHero.PartyBelongedToAsPrisoner.IsSettlement)
                clanStats.Append("{=zVDODxiN}Prisoner: {prisoner} | ".Translate(("prisoner", adoptedHero.PartyBelongedToAsPrisoner.Settlement.Name.ToString())));
            clanStats.Append("{=SDVLj0nw}Wealth: {wealth} | ".Translate(("wealth", adoptedHero.Clan.Leader.Gold.ToString())));
            clanStats.Append("{=eHJYAZha}Members: {members} | ".Translate(("members", adoptedHero.Clan.Heroes.Count.ToString())));
            clanStats.Append("{=Ib213Hp9}Parties: {cparties}/{mparties} |".Translate(("cparties", adoptedHero.Clan.WarPartyComponents.Count), ("mparties", adoptedHero.Clan.CommanderLimit)));
            if (adoptedHero.Clan.Fiefs.Count >= 1)
            {
                int townCount = 0;
                int castleCount = 0;
                foreach (var settlement in adoptedHero.Clan.Fiefs)
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
            onSuccess("{=TESTING}{stats}".Translate(("stats", clanStats.ToString())));
        }

        private void HandlePartyCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.StatsEnabled)
            {
                onFailure("{=9XKKVMKf}Clan stats is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }
            var partyStats = new StringBuilder();
            void partyCreate(Hero adoptedHero)
            {
                if (adoptedHero.PartyBelongedTo == null && !adoptedHero.IsPrisoner && adoptedHero.GovernorOf == null && !adoptedHero.Clan.Leader.IsHumanPlayerCharacter)
                {
                    if (adoptedHero.Clan.Kingdom != null && !Kingdom.All.Any(k => k != adoptedHero.Clan.Kingdom && adoptedHero.Clan.Kingdom.IsAtWarWith(k)))
                    {
                        partyStats.Append("{=TESTING} | No wars".Translate());
                        return;
                    }
                    if (!adoptedHero.IsClanLeader && adoptedHero.Clan.WarPartyComponents.Count >= adoptedHero.Clan.CommanderLimit)
                        partyStats.Append("{=sKbTZzds} | Party limit reached".Translate());
                    else
                    {
                        try
                        {
                            //adoptedHero.ChangeState(Hero.CharacterStates.Active);
                            adoptedHero.Clan.CreateNewMobileParty(adoptedHero);
                            partyStats.Append("{=wNXoOa5K} | Create party".Translate());
                        }
                        catch
                        {
                            partyStats.Append("{=rFBSpayQ} | Create party failed".Translate());
                            var clan = adoptedHero.Clan;
                            bool leader = adoptedHero.IsClanLeader;

                            adoptedHero.Clan = null;
                            adoptedHero.Clan = clan;
                            adoptedHero.Clan.SetLeader(adoptedHero);
                            if (leader)
                                adoptedHero.Clan.SetLeader(adoptedHero);
                        }
                    }
                }
            }

            if (adoptedHero.HeroState == Hero.CharacterStates.Released)
                partyStats.Append("{=r1nJTiSA}Your hero has just been released".Translate());
            else if (adoptedHero.IsPrisoner && adoptedHero.PartyBelongedToAsPrisoner.IsMobile)
            {
                partyStats.Append("{=zVDODxiN}Prisoner: {prisoner}".Translate(("prisoner", adoptedHero.PartyBelongedToAsPrisoner.Name.ToString())));
                partyStats.Append(" | ");
                var place = adoptedHero.PartyBelongedToAsPrisoner?.LeaderHero?.LastKnownClosestSettlement?.Name?.ToString() ?? "Unknown";
                partyStats.Append("{=B2xDasDx}Last seen near {Place}".Translate(("Place", place)));

            }
            else if (adoptedHero.IsPrisoner && adoptedHero.PartyBelongedToAsPrisoner.IsSettlement)
                partyStats.Append("{=zVDODxiN}Prisoner: {prisoner}".Translate(("prisoner", adoptedHero.PartyBelongedToAsPrisoner.Settlement.Name.ToString())));
            else if (adoptedHero.GovernorOf != null)
            {
                var govFief = Settlement.FindFirst(s => s.Town != null && s.Town.Governor == adoptedHero);
                partyStats.Append("{=ocrxKWUF}Governor: {governor}".Translate(("governor", govFief.Name.ToString())));
            }
            else if (adoptedHero.IsPartyLeader)
            {
                partyStats.Append("{=sN2NzoA7}Party(Strength: {party_strength} - ".Translate(("party_strength", Math.Round(adoptedHero.PartyBelongedTo.Party.TotalStrength).ToString())));
                string partySizeStr = $"{adoptedHero.PartyBelongedTo.MemberRoster.TotalHealthyCount}/{adoptedHero.PartyBelongedTo.Party.PartySizeLimit}";
                if (adoptedHero.PartyBelongedTo.PrisonRoster.Count > 0)
                {
                    partyStats.Append("{=4HDBsO9U}Size: {size} - ".Translate(("size", partySizeStr)));
                    partyStats.Append("{=jrBszDI8}Prisoners: {prisoners}) | ".Translate(("prisoners", adoptedHero.PartyBelongedTo.PrisonRoster.Count)));
                }
                else partyStats.Append("{=Sunm7EKS}Size: {size}) | ".Translate(("size", partySizeStr)));

                if (adoptedHero.PartyBelongedTo.Army == null)
                {
                    var party = adoptedHero.PartyBelongedTo;
                    if (adoptedHero.PartyBelongedTo.MapEvent != null)
                    {
                        var mapEvent = adoptedHero.PartyBelongedTo.MapEvent;
                        var partySide = adoptedHero.PartyBelongedTo.MapEventSide;
                        var otherSide = partySide.OtherSide;

                        if (partySide != null && otherSide != null)
                        {
                            string battleSide = partySide == mapEvent.DefenderSide
                                ? "{=c3CZCj6p}(Defending)".Translate()
                                : "{=83Uwa9xi}(Attacking)".Translate();

                            string enemyName = otherSide.LeaderParty.Name.ToString();
                            int remainTroops = otherSide.TroopCount;
                            //int enemyTroops = otherSide.Parties.;

                            string enemy = $"{enemyName}:{remainTroops}";

                            if (mapEvent.IsFieldBattle)
                            {
                                partyStats.Append("{=QV6KWiVt}Field Battle {battleside} [{enemy}] | "
                                    .Translate(("battleside", battleSide), ("enemy", enemy)));
                            }
                            else if (mapEvent.IsRaid)
                            {
                                partyStats.Append("{=U3NJo32u}Raid {battleside} [{enemy}] | "
                                    .Translate(("battleside", battleSide), ("enemy", enemy)));
                            }
                            else if (mapEvent.IsSiegeAssault || mapEvent.IsSallyOut || mapEvent.IsSiegeOutside)
                            {
                                partyStats.Append("{=FbhijpQL}Siege {battleside} [{enemy}] | "
                                    .Translate(("battleside", battleSide), ("enemy", enemy)));
                            }
                        }
                    }

                    else
                    {
                        if (party.ShortTermTargetParty != null && party.ShortTermBehavior == AiBehavior.EngageParty)
                        {
                            partyStats.Append("{=9aFoBcPY}Target: {target} - ".Translate(("target", party.ShortTermTargetParty.Name.ToString())));
                            partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", party.ShortTermTargetParty.MemberRoster.TotalManCount)));
                        }

                        if (party.TargetSettlement != null && party.IsCurrentlyGoingToSettlement)
                        {
                            partyStats.Append("{=SER2eRHo}Travelling: {travelling} | ".Translate(("travelling", party.TargetSettlement.Name.ToString())));
                        }
                        else if (party.ShortTermTargetSettlement != null && party.ShortTermBehavior == AiBehavior.DefendSettlement)
                        {
                            partyStats.Append("{=n225F4tj}Defending: {defending} | ".Translate(("defending", party.ShortTermTargetSettlement.Name.ToString())));
                        }
                        else if (party.TargetSettlement != null && party.DefaultBehavior == AiBehavior.DefendSettlement)
                        {
                            partyStats.Append("{=n225F4tj}Defending: {defending} | ".Translate(("defending", party.TargetSettlement.Name.ToString())));
                        }
                        else if (party.ShortTermBehavior == AiBehavior.RaidSettlement && party.ShortTermTargetSettlement?.IsVillage == true)
                        {
                            partyStats.Append("{=tHVQ8nsh}Raiding: {raiding} | ".Translate(("raiding", party.ShortTermTargetSettlement.Name.ToString())));
                        }
                        else if (party.DefaultBehavior == AiBehavior.RaidSettlement && party.TargetSettlement?.IsVillage == true)
                        {
                            partyStats.Append("{=tHVQ8nsh}Raiding: {raiding} | ".Translate(("raiding", party.TargetSettlement.Name.ToString())));
                        }
                        else if (party.ShortTermBehavior == AiBehavior.BesiegeSettlement && party.ShortTermTargetSettlement != null)
                        {
                            partyStats.Append("{=TUfgsPaj}Besieging: {besieging} | ".Translate(("besieging", party.ShortTermTargetSettlement.Name.ToString())));
                        }
                        else if (party.DefaultBehavior == AiBehavior.BesiegeSettlement && party.TargetSettlement != null)
                        {
                            partyStats.Append("{=TUfgsPaj}Besieging: {besieging} | ".Translate(("besieging", party.TargetSettlement.Name.ToString())));
                        }
                        else if ((party.ShortTermBehavior == AiBehavior.FleeToGate || party.ShortTermBehavior == AiBehavior.FleeToParty || party.ShortTermBehavior == AiBehavior.FleeToPoint) && party.ShortTermTargetParty != null)
                        {
                            partyStats.Append("{=pAhTQCii}Fleeing: {fleeing} - ".Translate(("fleeing", party.ShortTermTargetParty.Name)));
                            partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", party.ShortTermTargetParty.MemberRoster.TotalManCount)));
                        }
                        else if ((party.DefaultBehavior == AiBehavior.FleeToGate || party.DefaultBehavior == AiBehavior.FleeToParty || party.DefaultBehavior == AiBehavior.FleeToPoint) && party.ShortTermTargetParty != null)
                        {
                            partyStats.Append("{=pAhTQCii}Fleeing: {fleeing} - ".Translate(("fleeing", party.ShortTermTargetParty.Name)));
                            partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", party.ShortTermTargetParty.MemberRoster.TotalManCount)));
                        }

                        else if (party.TargetSettlement != null)
                        {
                            partyStats.Append("{=QmPqTDMX}Patrolling: {patrolling} | ".Translate(("patrolling", party.TargetSettlement.Name.ToString())));
                        }
                        else if (party.DefaultBehavior == AiBehavior.Hold || party.ShortTermBehavior == AiBehavior.Hold)
                            partyStats.Append("{=fBShVu8p}Holding | ".Translate());
                    }
                }

                else
                {
                    partyStats.Append("{=CVzSgXhT}Army: {army}".Translate(("army", adoptedHero.PartyBelongedTo.Army.Name.ToString())));
                    partyStats.Append("{=d76wc5iS}[Strength: {strength} | ".Translate(("strength", Math.Round(adoptedHero.PartyBelongedTo.Army.TotalStrength).ToString())));
                    partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", adoptedHero.PartyBelongedTo.Army.TotalHealthyMembers.ToString())));
                    partyStats.Append("{=7p5j5Mlx}Party nº: {count}] ".Translate(("count", adoptedHero.PartyBelongedTo.Army.LeaderPartyAndAttachedPartiesCount.ToString())));
                    //if ((int)adoptedHero.PartyBelongedTo.Army.AIBehavior > 0 && (int)adoptedHero.PartyBelongedTo.Army.AIBehavior < 11)
                    //{

                    //}
                    var party = adoptedHero.PartyBelongedTo.Army.LeaderParty;

                    if (adoptedHero.PartyBelongedTo.MapEvent != null)
                    {
                        var mapEvent = adoptedHero.PartyBelongedTo.MapEvent;
                        var partySide = adoptedHero.PartyBelongedTo.MapEventSide;
                        var otherSide = partySide.OtherSide;

                        if (partySide != null && otherSide != null)
                        {
                            string battleSide = partySide == mapEvent.DefenderSide
                                ? "{=c3CZCj6p}(Defending)".Translate()
                                : "{=83Uwa9xi}(Attacking)".Translate();

                            string enemyName = otherSide.LeaderParty.Name.ToString();
                            int remainTroops = otherSide.TroopCount;
                            //int enemyTroops = otherSide.Parties.;

                            string enemy = $"{enemyName}:{remainTroops}";

                            if (mapEvent.IsFieldBattle)
                            {
                                partyStats.Append("{=QV6KWiVt}Field Battle {battleside} [{enemy}] | "
                                    .Translate(("battleside", battleSide), ("enemy", enemy)));
                            }
                            else if (mapEvent.IsRaid)
                            {
                                partyStats.Append("{=U3NJo32u}Raid {battleside} [{enemy}] | "
                                    .Translate(("battleside", battleSide), ("enemy", enemy)));
                            }
                            else if (mapEvent.IsSiegeAssault || mapEvent.IsSallyOut || mapEvent.IsSiegeOutside)
                            {
                                partyStats.Append("{=FbhijpQL}Siege {battleside} [{enemy}] | "
                                    .Translate(("battleside", battleSide), ("enemy", enemy)));
                            }
                        }
                    }

                    else
                    {
                        if (party.ShortTermTargetParty != null && party.ShortTermBehavior == AiBehavior.EngageParty)
                        {
                            partyStats.Append("{=9aFoBcPY}Target: {target} - ".Translate(("target", party.ShortTermTargetParty.Name.ToString())));
                            partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", party.ShortTermTargetParty.MemberRoster.TotalManCount)));
                        }

                        if (party.TargetSettlement != null && party.IsCurrentlyGoingToSettlement)
                        {
                            partyStats.Append("{=SER2eRHo}Travelling: {travelling} | ".Translate(("travelling", party.TargetSettlement.Name.ToString())));
                        }
                        else if (party.ShortTermTargetSettlement != null && party.ShortTermBehavior == AiBehavior.DefendSettlement)
                        {
                            partyStats.Append("{=n225F4tj}Defending: {defending} | ".Translate(("defending", party.ShortTermTargetSettlement.Name.ToString())));
                        }
                        else if (party.TargetSettlement != null && party.DefaultBehavior == AiBehavior.DefendSettlement)
                        {
                            partyStats.Append("{=n225F4tj}Defending: {defending} | ".Translate(("defending", party.TargetSettlement.Name.ToString())));
                        }
                        else if (party.ShortTermBehavior == AiBehavior.RaidSettlement && party.ShortTermTargetSettlement?.IsVillage == true)
                        {
                            partyStats.Append("{=tHVQ8nsh}Raiding: {raiding} | ".Translate(("raiding", party.ShortTermTargetSettlement.Name.ToString())));
                        }
                        else if (party.DefaultBehavior == AiBehavior.RaidSettlement && party.TargetSettlement?.IsVillage == true)
                        {
                            partyStats.Append("{=tHVQ8nsh}Raiding: {raiding} | ".Translate(("raiding", party.TargetSettlement.Name.ToString())));
                        }
                        else if (party.ShortTermBehavior == AiBehavior.BesiegeSettlement && party.ShortTermTargetSettlement != null)
                        {
                            partyStats.Append("{=TUfgsPaj}Besieging: {besieging} | ".Translate(("besieging", party.ShortTermTargetSettlement.Name.ToString())));
                        }
                        else if (party.DefaultBehavior == AiBehavior.BesiegeSettlement && party.TargetSettlement != null)
                        {
                            partyStats.Append("{=TUfgsPaj}Besieging: {besieging} | ".Translate(("besieging", party.TargetSettlement.Name.ToString())));
                        }
                        else if ((party.ShortTermBehavior == AiBehavior.FleeToGate || party.ShortTermBehavior == AiBehavior.FleeToParty || party.ShortTermBehavior == AiBehavior.FleeToPoint) && party.ShortTermTargetParty != null)
                        {
                            partyStats.Append("{=pAhTQCii}Fleeing: {fleeing} - ".Translate(("fleeing", party.ShortTermTargetParty.Name)));
                            partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", party.ShortTermTargetParty.MemberRoster.TotalManCount)));
                        }
                        else if ((party.DefaultBehavior == AiBehavior.FleeToGate || party.DefaultBehavior == AiBehavior.FleeToParty || party.DefaultBehavior == AiBehavior.FleeToPoint) && party.ShortTermTargetParty != null)
                        {
                            partyStats.Append("{=pAhTQCii}Fleeing: {fleeing} - ".Translate(("fleeing", party.ShortTermTargetParty.Name)));
                            partyStats.Append("{=D3dcUxuj}Size: {size} | ".Translate(("size", party.ShortTermTargetParty.MemberRoster.TotalManCount)));
                        }

                        else if (party.TargetSettlement != null)
                        {
                            partyStats.Append("{=QmPqTDMX}Patrolling: {patrolling} | ".Translate(("patrolling", party.TargetSettlement.Name.ToString())));
                        }
                        else if (party.DefaultBehavior == AiBehavior.Hold || party.ShortTermBehavior == AiBehavior.Hold)
                            partyStats.Append("{=fBShVu8p}Holding | ".Translate());
                    }
                }
            }
            else if (adoptedHero.StayingInSettlement != null)
            {
                partyStats.Append("{=dMOlobea}Your hero is staying at {place}".Translate(("place", adoptedHero.StayingInSettlement.Name.ToString())));
                partyCreate(adoptedHero);
            }
            else
            {
                partyStats.Append("{=LVFh1Pd5}Your hero is not leading a party".Translate());
                partyCreate(adoptedHero);
            }

            onSuccess("{=TESTING}{party}".Translate(("party", partyStats.ToString())));

        }

        private void HandleFiefsCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.FiefsEnabled)
            {
                onFailure("{=6q8NKGam}Clan fiefs is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }
            if (adoptedHero.Clan.Fiefs.Count == 0)
            {
                onFailure("{=ssqRB9Ye}You have no fiefs".Translate());
                return;
            }
            var fiefList = new StringBuilder();
            string townInfo = "";
            string castleInfo = "";

            foreach (Town f in adoptedHero.Clan.Fiefs)
            {

                if (f.IsCastle)
                {
                    int profit = (int)(
                    Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(f, false).ResultNumber +
                    Campaign.Current.Models.ClanFinanceModel.CalculateTownIncomeFromTariffs(adoptedHero.Clan, f, false).ResultNumber +
                    Campaign.Current.Models.ClanFinanceModel.CalculateTownIncomeFromProjects(f) +
                    f.Settlement.BoundVillages.Sum(v => Campaign.Current.Models.ClanFinanceModel.CalculateVillageIncome(adoptedHero.Clan, v, false)) -
                    (f.GarrisonParty?.TotalWage ?? 0)
                    );

                    castleInfo = castleInfo + f.Name.ToString() + "[";
                    castleInfo = castleInfo + "P📈:" + ((int)f.Prosperity).ToString();
                    castleInfo = castleInfo + ", L🤝:" + ((int)f.Loyalty).ToString();
                    castleInfo = castleInfo + ", G💰:" + profit.ToString();
                    castleInfo = castleInfo + ", M/G⚔:" + ((int)f.Militia).ToString() + "/" + (f.GarrisonParty?.LimitedPartySize.ToString() ?? "0");
                    castleInfo = castleInfo + ", F🌾:" + ((int)f.FoodStocks).ToString();
                    if (f.IsUnderSiege && f.Settlement.SiegeEvent != null)
                    {
                        castleInfo += ", UnderSiege] ";
                    }
                    else castleInfo += "] ";
                }

                if (!f.IsCastle)
                {
                    int profit = (int)(
                    Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(f, false).ResultNumber +
                    Campaign.Current.Models.ClanFinanceModel.CalculateTownIncomeFromTariffs(adoptedHero.Clan, f, false).ResultNumber +
                    Campaign.Current.Models.ClanFinanceModel.CalculateTownIncomeFromProjects(f) +
                    f.Settlement.BoundVillages.Sum(v => Campaign.Current.Models.ClanFinanceModel.CalculateVillageIncome(adoptedHero.Clan, v, false)) -
                    (f.GarrisonParty?.TotalWage ?? 0)
                    );
                    townInfo = townInfo + f.Name.ToString() + "[";
                    townInfo = townInfo + "P📈:" + ((int)f.Prosperity).ToString();
                    townInfo = townInfo + ", L🤝:" + ((int)f.Loyalty).ToString();
                    townInfo = townInfo + ", G💰:" + profit.ToString();
                    townInfo = townInfo + ", M/G⚔:" + ((int)f.Militia).ToString() + "/" + (f.GarrisonParty?.LimitedPartySize.ToString() ?? "0");
                    townInfo = townInfo + ", F🌾:" + ((int)f.FoodStocks).ToString();
                    if (f.IsUnderSiege && f.Settlement.SiegeEvent != null)
                    {
                        townInfo += ", UnderSiege] ";
                    }
                    else townInfo += "] ";
                }

            }
            fiefList.Append("{=BwuFSJU1}Towns: {towns} | ".Translate(("towns", (object)townInfo)));
            fiefList.Append("{=0rMNNQ7R}Castles: {castles}".Translate(("castles", (object)castleInfo)));
            onSuccess("{=TESTING}{fiefs}".Translate(("fiefs", fiefList.ToString())));
        }

        private void HandleLeaveCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.LeaveEnabled)
            {
                onFailure("{=5hayZV51}Leaving clans is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }
            if (adoptedHero.IsClanLeader)
            {
                onFailure("{=cRpqnI3B}You are already the leader of your clan".Translate());
                return;
            }

            //var mobileParty = MobileParty.All.ToList().Where(p => p.LeaderHero?.CharacterObject == adoptedHero.CharacterObject).FirstOrDefault();
            //if (mobileParty != null)
            //{
            //    var newLead = adoptedHero.Clan.Heroes.ToList().Where(h => h.IsPartyLeader == false && h.CanLeadParty()).FirstOrDefault();
            //    if (newLead == null)
            //        mobileParty.RemoveParty();
            //    else
            //    mobileParty.ChangePartyLeader(newLead);
            //}
            //adoptedHero.Clan = null;
            //adoptedHero.SetNewOccupation(Occupation.Wanderer);
            //if (adoptedHero.IsPartyLeader)
            //    adoptedHero.PartyBelongedTo.RemoveParty();
            //var targetSettlement = Settlement.All.Where(s => s.IsTown).SelectRandom();
            //EnterSettlementAction.ApplyForCharacterOnly(adoptedHero, targetSettlement);
            onSuccess("Clan leave doesnt work"); //attempt FactionDiscontinuationCampaignBehavior.FinalizeMapEvents!!
        }

        //private void HandleDisbandCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        //{
        //    if (!settings.LeaveEnabled)
        //    {
        //        onFailure("Leaving clans is disabled");
        //        return;
        //    }
        //    if (adoptedHero.Clan == null)
        //    {
        //        onFailure("{=yPeUCq8t}You are not in a clan".Translate());
        //        return;
        //    }

        //    var mobileParty = MobileParty.All.ToList().Where(p => p.LeaderHero?.CharacterObject == adoptedHero.CharacterObject).FirstOrDefault();
        //    if (mobileParty != null)
        //    {
        //        mobileParty.RemoveParty();
        //    }
        //    adoptedHero.Clan = null;
        //    adoptedHero.Clan.Kingdom = null;
        //    adoptedHero.SetNewOccupation(Occupation.Wanderer);
        //    var targetSettlement = Settlement.All.Where(s => s.IsTown).SelectRandom();
        //    EnterSettlementAction.ApplyForCharacterOnly(adoptedHero, targetSettlement);
        //}

        private void HandleBuyTitleCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if ((adoptedHero.Occupation == Occupation.Lord) && (!settings.BuyTitleEnabled))
            {
                onFailure("{=nes7s2UR}Buying Noble Titles is disabled, and you are already a noble!".Translate());
                return;
            }
            if (!settings.BuyTitleEnabled)
            {
                onFailure("{=fHkLWTE4}Buying Noble Titles is disabled".Translate());
                return;
            }
            if (adoptedHero.Occupation == Occupation.Lord)
            {
                onFailure("{=z1XfuFHU}You are already a noble!".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.TitlePrice)
            {
                onFailure(Naming.NotEnoughGold(settings.TitlePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.TitlePrice, true);
            adoptedHero.SetNewOccupation(Occupation.Lord);
            onSuccess("{=6yQUu78N}{heroName} has become a noble!".Translate(("heroName", adoptedHero.Name.ToString())));
        }

        private void HandleBannerCommand(Settings settings, Hero adoptedHero, string bannerCode, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.EditBannerEnabled)
            {
                onFailure("{=sJdd2kgr}Editing banners is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=jQZ93EID}You are not the leader of your clan".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(bannerCode))
            {
                onFailure("{=PSDbhv3a}Make your banner at https://bannerlord.party/banner and paste it directly".Translate());
                return;
            }
            if (bannerCode == "clear")
            {
                try
                {
                    var behavior = Campaign.Current?.GetCampaignBehavior<BLTClanBannerSaveBehavior>();
                    if (behavior != null && adoptedHero?.Clan != null)
                    {
                        behavior._banners.Remove(adoptedHero.Clan.StringId);
                        onSuccess("Clan banner cleared from saved data.");
                    }
                    else
                    {
                        onFailure("No clan or behavior found to clear.");
                    }
                }
                catch (Exception ex)
                {
                    onFailure($"Failed to clear saved banner: {ex.Message}");
                }
                return;
            }
            try
            {
                var newData = Banner.GetBannerDataFromBannerCode(bannerCode);
                if (newData == null || newData.Count == 0)
                {
                    onFailure("Invalid banner code.");
                    return;
                }

                Banner clanBanner = adoptedHero.Clan.Banner;
                clanBanner.Deserialize(bannerCode);

                clanBanner.SetBannerVisual(null);

                IBannerVisual visual = clanBanner.BannerVisual;

                if (visual != null)
                {
                    var convert = visual.GetType().GetMethod("ConvertToMultiMesh",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    convert?.Invoke(visual, null);
                }
                var behavior = Campaign.Current?.GetCampaignBehavior<BLTClanBannerSaveBehavior>();
                behavior?.SaveBanner(adoptedHero.Clan.StringId, bannerCode);

                onSuccess("{=BiiO7KQx}Banner updated successfully!".Translate());
            }
            catch (Exception ex)
            {
                onFailure($"Failed to update banner: {ex.Message}");
            }
        }
    }
}