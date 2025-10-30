using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using YamlDotNet.Serialization;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=NkZXnSQI}Adopt A Hero"),
     LocDescription("{=fd7G5N0Q}Allows viewer to 'adopt' or create a hero in game -- the hero name will change to the viewers name, and they can control it with further commands"),
     UsedImplicitly]
    public class AdoptAHero : IRewardHandler, ICommandHandler
    {
        public static string NoHeroMessage => "{=r8nXZgcY}Couldn't find your hero, did you adopt one yet?".Translate();

        [CategoryOrder("General", 0),
         CategoryOrder("Random Selection (if CreateNew is false)", 1),
         CategoryOrder("Subscribers", 2),
         CategoryOrder("Initialization", 3),
         CategoryOrder("Inheretance", 4)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=TLrDxhlh}Create New Hero?"),
             LocCategory("General", "{=C5T5nnix}General"),
             LocDescription("{=F1KDzuZZ}Create a new hero instead of adopting an existing one (they will be a wanderer at a random tavern)"),
             PropertyOrder(1), UsedImplicitly]
            public bool CreateNew { get; set; }
            [LocDisplayName("{=EWlUcw9W}In-game Notification?"),
             LocCategory("General", "{=C5T5nnix}General"),
             LocDescription("{=F1KDzuZZ}Enable/Disable the ingame adoption notification)"),
             PropertyOrder(2), UsedImplicitly]
            public bool Notifications { get; set; }

            public enum ViewerSelect
            {
                Nothing,
                Name,
                Clan,
                Culture,
                Faction,
            }

            [LocDisplayName("{=NoKO59t1}Viewer Selects"),
             LocCategory("Random Selection  (if CreateNew is false)", "{=1lHWj3nT}Random Selection (if CreateNew is false)"),
             LocDescription("{=VSYokFLH}What criteria the viewer selects via text input (make sure to enable 'Is User Input " +
                            "Required' it in the Reward Specification > Misc section if you set this to something other than None). "),
             PropertyOrder(1), UsedImplicitly]
            public ViewerSelect ViewerSelects { get; set; }

            [LocDisplayName("{=nPIcT2s7}Allow Noble"),
             LocCategory("Random Selection (if CreateNew is false)", "{=1lHWj3nT}Random Selection (if CreateNew is false)"),
             LocDescription("{=XvZN7OOY}Allow noble heroes"),
             PropertyOrder(2), UsedImplicitly]
            public bool AllowNoble { get; set; } = true;

            [LocDisplayName("{=VVFsa8LQ}Allow Wanderer"),
             LocCategory("Random Selection (if CreateNew is false)", "{=1lHWj3nT}Random Selection (if CreateNew is false)"),
             LocDescription("{=9lE2KSvC}Allow wanderer heroes"),
             PropertyOrder(3), UsedImplicitly]
            public bool AllowWanderer { get; set; } = true;

            [LocDisplayName("{=FUsqiXdp}Allow Party Leader"),
             LocCategory("Random Selection (if CreateNew is false)", "{=1lHWj3nT}Random Selection (if CreateNew is false)"),
             LocDescription("{=L4cLwv9E}Allow heroes that lead parties"),
             PropertyOrder(4), UsedImplicitly]
            public bool AllowPartyLeader { get; set; } = false;

            [LocDisplayName("{=9DdalaHK}Allow Minor Faction Hero"),
             LocCategory("Random Selection (if CreateNew is false)", "{=1lHWj3nT}Random Selection (if CreateNew is false)"),
             LocDescription("{=NCrSbTGc}Allow heroes that lead minor factions"),
             PropertyOrder(5), UsedImplicitly]
            public bool AllowMinorFactionHero { get; set; } = false;

            [LocDisplayName("{=A8G9ctbn}Allow Player Companion"),
             LocCategory("Random Selection (if CreateNew is false)", "{=1lHWj3nT}Random Selection (if CreateNew is false)"),
             LocDescription("{=6EjGRMkt}Allow companions"),
             PropertyOrder(6), UsedImplicitly]
            public bool AllowPlayerCompanion { get; set; }

            [LocDisplayName("{=O4DGlP9Z}Subscriber Only"),
             LocCategory("Subscribers", "{=26cIeQzp}Subscribers"),
             LocDescription("{=TBNkHsLC}Only subscribers can adopt"),
             PropertyOrder(1), UsedImplicitly]
            public bool SubscriberOnly { get; set; }

            [LocDisplayName("{=dO41CKIU}Minimum Subscribed Months"),
             LocCategory("Subscribers", "{=26cIeQzp}Subscribers"),
             LocDescription("{=BVZwDqR0}Only viewers who have been subscribers for at least this many months can adopt, ignored if not specified"),
             PropertyOrder(2), UsedImplicitly]
            public int? MinSubscribedMonths { get; set; }

            [LocDisplayName("{=iOmYBC7I}Starting Gold"),
             LocCategory("Initialization", "{=DRNO9OAl}Initialization"),
             LocDescription("{=pZMkJLix}Gold the adopted hero will start with"),
             PropertyOrder(1), UsedImplicitly,
             Document]
            public int StartingGold { get; set; }

            [LocDisplayName("{=ZXwbvbbq}Override Age"),
             LocCategory("Initialization", "{=DRNO9OAl}Initialization"),
             LocDescription("{=gxQgrAey}Override the heroes age"),
             PropertyOrder(2), UsedImplicitly]
            public bool OverrideAge { get; set; }

            [LocDisplayName("{=NEBQgHiX}Starting Age Range"),
             LocCategory("Initialization", "{=DRNO9OAl}Initialization"),
             LocDescription("{=TYqEBuLW}Random range of age when overriding it"),
             PropertyOrder(3), UsedImplicitly]
            public RangeFloat StartingAgeRange { get; set; } = new(18, 35);

            [LocDisplayName("{=9IOFHQjS}Starting Skills"),
             LocCategory("Initialization", "{=DRNO9OAl}Initialization"),
             LocDescription("{=C4rV4f2F}Starting skills, if empty then default skills of the adopted hero will be left in tact"),
             Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor)),
             PropertyOrder(4), UsedImplicitly]
            public ObservableCollection<SkillRangeDef> StartingSkills { get; set; } = new();

            [YamlIgnore, Browsable(false)]
            public IEnumerable<SkillRangeDef> ValidStartingSkills
                => StartingSkills?.Where(s => s.Skill != SkillsEnum.None);

            [LocDisplayName("{=IAKQCRa1}Starting Equipment Tier"),
             LocCategory("Initialization", "{=DRNO9OAl}Initialization"),
             LocDescription("{=mQwjHXfC}Equipment tier the adopted hero will start with, if you don't specify then they get the heroes existing equipment"),
             Range(0, 6),
             PropertyOrder(5), UsedImplicitly]
            public int? StartingEquipmentTier { get; set; }

            [LocDisplayName("{=0vGFJdO1}Starting Class"),
             LocCategory("Initialization", "{=DRNO9OAl}Initialization"),
             LocDescription("{=zgjyFL6i}Starting class of the hero"),
             PropertyOrder(6), ItemsSource(typeof(HeroClassDef.ItemSource)), UsedImplicitly]
            public Guid StartingClass { get; set; }

            [LocDisplayName("{=dvbkxJQz}Inheritance Percentage"),
             LocCategory("Inheritance", "{=biICJtC2}Inheritance"),
             LocDescription("{=KLJtpEjg}What fraction of assets will be inherited when a new character is adopted after an old one died (0 to 1)"),
             UIRangeAttribute(0, 1, 0.05f),
             Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
             PropertyOrder(1), UsedImplicitly]
            public float Inheritance { get; set; } = 0.25f;

            [LocDisplayName("{=Bi19tTPj}Maximum Inherited Custom Items"),
             LocCategory("Inheritance", "{=biICJtC2}Inheritance"),
             LocDescription("{=tFolfAOn}How many custom items can be inherited"),
             Range(0, Int32.MaxValue),
             PropertyOrder(2), UsedImplicitly]
            public int MaxInheritedCustomItems { get; set; } = 2;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                if (SubscriberOnly)
                {
                    generator.Value("<strong>" +
                                    "{=4zAUTiSP}Subscriber Only".Translate() +
                                    "</strong>");
                }

                //if (CreateClan) generator.Value("{=xjmF7XjL}Create the selected clan if it does not exist".Translate());
                if (ViewerSelects == ViewerSelect.Culture) generator.Value("{=Lg6V3rzn}Viewer selects hero by culture".Translate());
                if (ViewerSelects == ViewerSelect.Faction) generator.Value("{=kps5JINU}Viewer selects hero by faction".Translate());
                if (ViewerSelects == ViewerSelect.Name) generator.Value("{=EUJZfjFj}Viewer selects hero by name".Translate());
                if (ViewerSelects == ViewerSelect.Clan) generator.Value("{=y2U378lu}Viewer selects hero by clan".Translate());

                if (CreateNew)
                {
                    generator.Value("{=cJQCU33B}Newly created wanderer".Translate());
                }
                else
                {
                    var allowed = new List<string>();
                    if (AllowNoble) allowed.Add("{=fP84ES0X}Noble".Translate());
                    if (AllowWanderer) allowed.Add("{=ozRaAx6L}Wanderer".Translate());
                    if (AllowPlayerCompanion) allowed.Add("{=YucejFfO}Companions".Translate());
                    generator.PropertyValuePair("{=UNtHwNhx}Allowed".Translate(), string.Join(", ", allowed));
                }

                if (OverrideAge)
                {
                    generator.PropertyValuePair("{=pDP8b5HR}Starting Age Range".Translate(),
                        StartingAgeRange.IsFixed
                            ? $"{StartingAgeRange.Min}"
                            : $"{StartingAgeRange.Min} to {StartingAgeRange.Max}"
                        );
                }

                generator.PropertyValuePair("{=FvhsCSd3}Starting Gold".Translate(), $"{StartingGold}");

                if (Inheritance != 0.25f || MaxInheritedCustomItems != 2)
                {
                    generator.PropertyValuePair("{=wP0lfTf3}Inheritance".Translate(),
                        "{=mDK67efh}{Inheritance}% of gold spent on equipment and retinue"
                            .Translate(("Inheritance", (int)(Inheritance * 100))) +
                        ", " +
                        (MaxInheritedCustomItems == 0
                            ? "{=76NtQIGB}no custom items".Translate()
                            : "{=sEDQrZCp}up to {MaxInheritedCustomItems} custom items"
                                .Translate(("MaxInheritedCustomItems", MaxInheritedCustomItems))
                        ));
                }

                if (StartingEquipmentTier.HasValue)
                {
                    generator.PropertyValuePair("{=IAKQCRa1}Starting Equipment Tier".Translate(), $"{StartingEquipmentTier.Value}");
                }

                if (StartingClass != Guid.Empty)
                {
                    var classDef = BLTAdoptAHeroModule.HeroClassConfig.GetClass(StartingClass);
                    if (classDef != null)
                    {
                        generator.PropertyValuePair("{=0vGFJdO1}Starting Class".Translate(),
                            () => generator.LinkToAnchor(classDef.Name.ToString(), classDef.Name.ToString()));
                    }
                }

                if (ValidStartingSkills.Any())
                {
                    generator.PropertyValuePair("{=9IOFHQjS}Starting Skills".Translate(), () =>
                        generator.Table("starting-skills", () =>
                        {
                            generator.TR(() =>
                                generator.TH("{=OEMBeawy}Skill".Translate()).TH("{=iu0dtUP5}Level".Translate())
                            );
                            foreach (var s in ValidStartingSkills)
                            {
                                generator.TR(() =>
                                {
                                    generator.TD(s.Skill.GetDisplayName());
                                    generator.TD(s.IsFixed
                                        ? $"{s.MinLevel}"
                                        : "{=yVydxRHh}{From} to {To}".Translate(
                                            ("From", s.MinLevel), ("To", s.MaxLevel)));
                                });
                            }
                        }));
                }
            }
        }

        Type IRewardHandler.RewardConfigType => typeof(Settings);
        void IRewardHandler.Enqueue(ReplyContext context, object config)
        {
            var hero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            if (hero?.IsAlive == true)
            {
                ActionManager.NotifyCancelled(context, "{=mJfD7e2g}You have already adopted a hero!".Translate());
                return;
            }
            var settings = (Settings)config;
            if (settings.MinSubscribedMonths > 0 && context.SubscribedMonthCount < settings.MinSubscribedMonths)
            {
                ActionManager.NotifyCancelled(context,
                    "{=4K7Q7gR0}You must be subscribed for at least {MinSubscribedMonths} months to adopt a hero with this command!".Translate(("MinSubscribedMonths", settings.MinSubscribedMonths)));
                return;
            }
            if (!context.IsSubscriber && settings.SubscriberOnly)
            {
                ActionManager.NotifyCancelled(context, "{=0QeQPxYi}You must be subscribed to adopt a hero with this command!".Translate());
                return;
            }
            (bool success, string message) = ExecuteInternal(context.UserName, settings, context.Args);
            if (success)
            {
                ActionManager.NotifyComplete(context, message);
            }
            else
            {
                ActionManager.NotifyCancelled(context, message);
            }
        }

        Type ICommandHandler.HandlerConfigType => typeof(Settings);
        void ICommandHandler.Execute(ReplyContext context, object config)
        {
            if (BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName) != null)
            {
                ActionManager.SendReply(context, "{=mJfD7e2g}You have already adopted a hero!".Translate());
                return;
            }

            var settings = (Settings)config;
            if (settings.MinSubscribedMonths > 0 && context.SubscribedMonthCount < settings.MinSubscribedMonths)
            {
                ActionManager.SendReply(context,
                    "{=4K7Q7gR0}You must be subscribed for at least {MinSubscribedMonths} months to adopt a hero with this command!".Translate(("MinSubscribedMonths", settings.MinSubscribedMonths)));
                return;
            }
            if (!context.IsSubscriber && settings.SubscriberOnly)
            {
                ActionManager.SendReply(context, "{=0QeQPxYi}You must be subscribed to adopt a hero with this command!".Translate());
                return;
            }

            (_, string message) = ExecuteInternal(context.UserName, settings, context.Args);
            ActionManager.SendReply(context, message);
        }

        private static (bool success, string message) ExecuteInternal(string userName, Settings settings, string contextArgs)
        {
            Hero newHero = null;

            if (settings.CreateNew && (settings.ViewerSelects == Settings.ViewerSelect.Clan || settings.ViewerSelects == Settings.ViewerSelect.Faction || settings.ViewerSelects == Settings.ViewerSelect.Name))
            {
                return (false, "{=98Hl4Gnd}Config Error: Can't create new hero and have random selection filters".Translate());
            }

            CultureObject desiredCulture = null;
            IFaction desiredFaction = null;
            String desiredName = null;
            Clan desiredClan = null;

            //If viewer has filter selected, start assigning filters
            if (!(settings.ViewerSelects == Settings.ViewerSelect.Nothing))
            {
                //Set Culture filter if enabled
                if (settings.ViewerSelects == Settings.ViewerSelect.Culture)
                {
                    if (contextArgs.Trim() == "{=fQKXPB5C}list".Translate() || contextArgs.Trim() == "{=z9cGIl8j}a".Translate())
                        return (false, "{=jjUmUpia}Culture list: {Cultures}".Translate(("Cultures", string.Join(", ", CampaignHelpers.MainCultures.Select(c => c.Name.ToString())))));
                    if (contextArgs.Length > 1)
                    {
                        desiredCulture = CampaignHelpers.MainCultures.FirstOrDefault(c =>
                            c.Name.ToString().StartsWith(contextArgs, StringComparison.CurrentCultureIgnoreCase));
                        if (desiredCulture == null)
                        {
                            return (false, "{=dVVduPvy}No culture starting with '{Text}' found".Translate(("Text", contextArgs)));
                        }
                    }
                    else
                    {
                        return (false, "{=SViljL0E}Please enter the name of the culture you wish to adopt a hero from".Translate());
                    }
                }

                //Set Faction filter if enabled
                else if (settings.ViewerSelects == Settings.ViewerSelect.Faction)
                {
                    if (contextArgs.Trim() == "{=fQKXPB5C}list".Translate() || contextArgs.Trim() == "{=z9cGIl8j}a".Translate())
                        return (false, "{=7DN9p01E}Faction list: {Factions}".Translate(("Factions", string.Join(", ", CampaignHelpers.MainFactions.Select(c => c.Name.ToString())))));
                    if (contextArgs.Length > 1)
                    {
                        desiredFaction = CampaignHelpers.MainFactions.FirstOrDefault(c =>
                            c.Name.ToString().StartsWith(contextArgs, StringComparison.CurrentCultureIgnoreCase));

                        if (desiredFaction == null)
                        {
                            return (false, "{=k4Hj2rxu}No faction starting with '{Text}' found".Translate(("Text", contextArgs)));
                        }
                    }
                    else
                    {
                        return (false, "{=kAeOPuZc}Please enter the name of the faction you wish to adopt a hero from".Translate());
                    }
                }

                //Set Name filter if enabled
                else if (settings.ViewerSelects == Settings.ViewerSelect.Name)
                {
                    if (contextArgs.Length > 1)
                    {
                        desiredName = CampaignHelpers.AllHeroes.Select(h => h.Name.ToString()).Distinct().FirstOrDefault(c =>
                        {
                            if (c != null)
                            {
                                return string.Equals(c, contextArgs.Trim(), StringComparison.CurrentCultureIgnoreCase);
                            }

                            return false;

                        });
                        if (desiredName == null)
                            return (false, "{=q9m9Yp1F}Error could not find a hero with the name {desiredName}".Translate(("desiredName", desiredName)));
                    }
                    else
                    {
                        return (false, "{=lJXxLdFz}Please enter the name of the leader you wish to adopt".Translate());
                    }
                }

                //Set Clan filter if enabled
                else if (settings.ViewerSelects == Settings.ViewerSelect.Clan)
                {
                    if (contextArgs.Length > 1)
                    {
                        desiredClan = CampaignHelpers.AllHeroes.Select(h => h.Clan).Distinct().FirstOrDefault(c =>
                        {
                            if (c != null)
                            {
                                return string.Equals(c.Name.ToString(), contextArgs, StringComparison.CurrentCultureIgnoreCase);
                            }

                            return false;
                        });
                        if (desiredClan == null)
                            return (false, "{=bzsT7oy0}Error could not find a clan with the name {clanName}".Translate(("clanName", contextArgs)));
                    }
                    else
                    {
                        return (false, "{=sN23nt5M}Please enter the name of the clan you wish to adopt a hero from".Translate());
                    }
                }
            }
            
            //Stop filtering and select or create hero
            if (settings.CreateNew)
            {
                var character = desiredCulture != null
                    ? CampaignHelpers.GetWandererTemplates(desiredCulture).SelectRandom()
                    : CampaignHelpers.AllWandererTemplates.SelectRandom();

                if (character != null)
                {
                    newHero = HeroCreator.CreateSpecialHero(character);
                    newHero.ChangeState(Hero.CharacterStates.Active);
                    BLTAdoptAHeroCampaignBehavior.Current.SetIsCreatedHero(newHero, true);
                    var targetSettlement = Settlement.All.Where(s => s.IsTown).SelectRandom();
                    EnterSettlementAction.ApplyForCharacterOnly(newHero, targetSettlement);
                    Log.Info($"Created and placed new hero {newHero.Name} at {targetSettlement.Name}");
                }
                else { Log.Error($"No wanderer template for {desiredCulture}"); }
            }
            else
            {
                newHero = BLTAdoptAHeroCampaignBehavior.GetAvailableHeroes(h =>
                        // Filter by allowed types
                        (settings.AllowNoble || !h.IsLord)
                        && (settings.AllowWanderer || !h.IsWanderer)
                        && (settings.AllowPartyLeader || !h.IsPartyLeader)
                        && (settings.AllowMinorFactionHero || !h.IsMinorFactionHero)
                        && (settings.AllowPlayerCompanion || !h.IsPlayerCompanion)
                        // Disallow rebel clans as they may get deleted if the rebellion fails
                        && h.Clan?.IsRebelClan != true
                        && (desiredCulture == null || desiredCulture == h.Culture)
                        && (desiredFaction == null || desiredFaction == h.MapFaction)
                        && (desiredName == null || string.Equals(desiredName, h.Name.ToString(), StringComparison.CurrentCultureIgnoreCase))
                        && (desiredClan == null || (h.Clan != null && desiredClan == h.Clan))
                    ).SelectRandom();
            }
            if (newHero == null)
            {
                return (false, "{=E7wqQ2kg}You can't adopt a hero: no available hero matching the requirements was found!".Translate());
            }
            if (settings.OverrideAge)
            {
                newHero.SetBirthDay(CampaignTime.YearsFromNow(-Math.Max(Campaign.Current.Models.AgeModel.HeroComesOfAge, settings.StartingAgeRange.RandomInRange())));
            }
            if (settings.ValidStartingSkills?.Any() == true)
            {
                newHero.HeroDeveloper.ClearHero();

                foreach (var skill in settings.ValidStartingSkills)
                {
                    var actualSkills = SkillGroup.GetSkills(skill.Skill);
                    newHero.HeroDeveloper.SetInitialSkillLevel(actualSkills.SelectRandom(),
                        MBMath.ClampInt(
                            MBRandom.RandomInt(
                                Math.Min(skill.MinLevel, skill.MaxLevel),
                                Math.Max(skill.MinLevel, skill.MaxLevel)
                                ), 0, 300)
                        );
                }
                newHero.HeroDeveloper.InitializeHeroDeveloper();
            }

            // A wanderer MUST have at least 1 skill point, or they get killed on load 
            if (newHero.GetSkillValue(CampaignHelpers.AllSkillObjects.First()) == 0)
            {
                newHero.HeroDeveloper.SetInitialSkillLevel(CampaignHelpers.AllSkillObjects.First(), 1);
            }

            HeroClassDef classDef = null;
            if (settings.StartingClass != default)
            {
                classDef = BLTAdoptAHeroModule.HeroClassConfig.GetClass(settings.StartingClass);
                if (classDef == null)
                {
                    Log.Error($"AdoptAHero: StartingClass not found, please re-select it in settings");
                }
                else
                {
                    BLTAdoptAHeroCampaignBehavior.Current.SetClass(newHero, classDef);
                }
            }

            // Setup skills first, THEN name, as skill changes can generate feed messages for adopted characters
            string oldName = newHero.Name.ToString();
            BLTAdoptAHeroCampaignBehavior.Current.InitAdoptedHero(newHero, userName);

            // Inherit items before equipping, so we can use them DURING equipping
            var inheritedItems = BLTAdoptAHeroCampaignBehavior.Current.InheritCustomItems(newHero, settings.MaxInheritedCustomItems);
            if (settings.StartingEquipmentTier.HasValue)
            {
                EquipHero.RemoveAllEquipment(newHero);
                if (settings.StartingEquipmentTier.Value > 0)
                {
                    EquipHero.UpgradeEquipment(newHero, settings.StartingEquipmentTier.Value - 1,
                        classDef, replaceSameTier: false);
                }
                BLTAdoptAHeroCampaignBehavior.Current.SetEquipmentTier(newHero, settings.StartingEquipmentTier.Value - 1);
                BLTAdoptAHeroCampaignBehavior.Current.SetEquipmentClass(newHero, classDef);
            }

            if (!CampaignHelpers.IsEncyclopediaBookmarked(newHero))
            {
                CampaignHelpers.AddEncyclopediaBookmarkToItem(newHero);
            }

            BLTAdoptAHeroCampaignBehavior.Current.SetHeroGold(newHero, settings.StartingGold);

            int inheritedGold = BLTAdoptAHeroCampaignBehavior.Current.InheritGold(newHero, settings.Inheritance);
            int newGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(newHero);

            var inherited = inheritedItems.Select(i => i.GetModifiedItemName().ToString()).ToList();
            if (inheritedGold != 0)
            {
                inherited.Add($"{inheritedGold}{Naming.Gold}");
            }
            if (settings.Notifications)
                Log.ShowInformation(
                    "{=K7nuJVCN}{OldName} is now known as {NewName}!".Translate(("OldName", oldName), ("NewName", newHero.Name)),
                    newHero.CharacterObject, Log.Sound.Horns2);
            else
                Log.Info("{=K7nuJVCN}{OldName} is now known as {NewName}!".Translate(("OldName", oldName), ("NewName", newHero.Name)));

            return inherited.Any()
                ? (true, "{=PAc5S0GY}{OldName} is now known as {NewName}, they have {NewGold} (inheriting {Inherited})!"
                    .Translate(
                        ("OldName", oldName),
                        ("NewName", newHero.Name),
                        ("NewGold", newGold + Naming.Gold),
                        ("Inherited", string.Join(", ", inherited))))
                : (true, "{=lANBKEFN}{OldName} is now known as {NewName}, they have {NewGold}!".Translate(
                    ("OldName", oldName),
                    ("NewName", newHero.Name),
                    ("NewGold", newGold + Naming.Gold)));
        }
    }
}
