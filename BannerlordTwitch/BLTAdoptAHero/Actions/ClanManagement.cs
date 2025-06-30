using System;
using System.Linq;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
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
        [CategoryOrder("Join", 0),
         CategoryOrder("Create", 1),
         CategoryOrder("Lead", 2),
         CategoryOrder("Rename", 3),
         CategoryOrder("Stats", 4)]
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

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Stats", "{=mlayrmHr}Stats"),
             LocDescription("{=vNGlBZUB}Enable stats command"),
             PropertyOrder(1), UsedImplicitly]
            public bool StatsEnabled { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                var EnabledCommands = new StringBuilder();
                if (JoinEnabled)
                    EnabledCommands = EnabledCommands.Append("{=}Join, ".Translate());
                if (CreateEnabled)
                    EnabledCommands = EnabledCommands.Append("Create, ");
                if (LeadEnabled)
                    EnabledCommands = EnabledCommands.Append("Lead, ");
                if (RenameEnabled)
                    EnabledCommands = EnabledCommands.Append("Rename, ");
                if (StatsEnabled)
                    EnabledCommands = EnabledCommands.Append("Stats, ");
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
                                    "Price={price}{icon}".Translate(("price", CreatePrice.ToString()), ("icon", Naming.Gold)));
                if (LeadEnabled)
                    generator.Value("<strong>" +
                                    "Lead Config: " +
                                    "</strong>" +
                                    "Price={price}{icon}, ".Translate(("price", LeadPrice.ToString()),("icon",Naming.Gold)) +
                                    "Challenge Heroes?={challengeHeroes}".Translate(("challengeHeroes", LeadChallengeHeroes.ToString())));
                if (RenameEnabled)
                    generator.Value("<strong>" +
                                    "Rename Config: " +
                                    "</strong>" +
                                    "Price={price}{icon}".Translate(("price", RenamePrice.ToString()), ("icon", Naming.Gold)));
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
                onFailure("{=MPTOZqMS}You cannot manage your clan, as a mission is active!".Translate());
                return;
            }
            if (adoptedHero.HeroState == Hero.CharacterStates.Prisoner)
            {
                onFailure("{=oxNqBy4k}You cannot manage your clan, as you are a prisoner!".Translate());
                return;
            }

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

            switch (command.ToLower())
            {
                case "{=I2jEHyAY}join".Translate():
                    HandleJoinCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "{=ymJh4yMY}create".Translate():
                    HandleCreateCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "{=pumBg7sU}lead".Translate():
                    HandleLeadCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case "{=ek75vkTT}rename".Translate():
                    HandleRenameCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "{=VB2W7FoL}stats".Translate():
                    HandleStatsCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                default:
                    onFailure("{=pkzDqw18}Invalid or empty clan action, try (join/create/lead/rename/stats)".Translate());
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
            adoptedHero.Clan = newClan;
            newClan.SetLeader(adoptedHero);
            if (!CampaignHelpers.IsEncyclopediaBookmarked(newClan))
                CampaignHelpers.AddEncyclopediaBookmarkToItem(newClan);
            onSuccess("{=omDrEeDx}Created and leading clan {name}".Translate(("name", fullClanName)));
            Log.ShowInformation("{=TsmDfvuz}{heroName} has created and is leading clan {clanName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("clanName", adoptedHero.Clan.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
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
            clanStats.Append("{=Sg11nEUe}Tier: {tier} | ".Translate(("tier", adoptedHero.Clan.Tier.ToString())));
            clanStats.Append("{=ZFGikYn8}Strength: {strength} | ".Translate(("strength", Math.Round(adoptedHero.Clan.TotalStrength).ToString())));
            clanStats.Append("{=eHJYAZha}Members: {members}".Translate(("members", adoptedHero.Clan.Heroes.Count.ToString())));
            onSuccess("{stats}".Translate(("stats", clanStats.ToString())));
        }
    }
}
