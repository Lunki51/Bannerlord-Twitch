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
    [LocDisplayName("Assign Heir"),
     LocDescription("Assigns a hero as heir. Optional: specify a name. Inherits gold, custom items, and can override age or skills."),
     UsedImplicitly]
    public class HeirCommand : ICommandHandler, IRewardHandler
    {
        private class Settings : IDocumentable
        {
            [LocDisplayName("Override Age"),
             LocDescription("Override the heir's age"),
             PropertyOrder(1)]
            public bool OverrideAge { get; set; }

            [LocDisplayName("Starting Age Range"),
             LocDescription("Random range of age when overriding it"),
             PropertyOrder(2)]
            public RangeFloat StartingAgeRange { get; set; } = new(18, 35);

            [LocDisplayName("Starting Skills"),
             LocDescription("Starting skills, if empty defaults are kept"),
             Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor)),
             PropertyOrder(3)]
            public ObservableCollection<SkillRangeDef> StartingSkills { get; set; } = new();

            [LocDisplayName("Starting Gold"),
             LocDescription("Gold the heir starts with"),
             PropertyOrder(4)]
            public int StartingGold { get; set; }

            [LocDisplayName("Inheritance Percentage"),
             LocDescription("Fraction of assets inherited (0-1)"),
             PropertyOrder(5)]
            public float Inheritance { get; set; } = 0.25f;

            [LocDisplayName("Maximum Inherited Custom Items"),
             LocDescription("Max custom items inherited"),
             PropertyOrder(6)]
            public int MaxInheritedCustomItems { get; set; } = 2;

            [LocDisplayName("Starting Equipment Tier"),
             LocDescription("Optional starting equipment tier"),
             PropertyOrder(7)]
            public int? StartingEquipmentTier { get; set; }

            [LocDisplayName("Show Notifications"),
             LocDescription("Display feed notifications when assigning heir"),
             PropertyOrder(8)]
            public bool Notifications { get; set; } = true;

            [YamlIgnore, Browsable(false)]
            public IEnumerable<SkillRangeDef> ValidStartingSkills
                => StartingSkills?.Where(s => s.Skill != SkillsEnum.None);

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.P("HeirCommand settings for age, skills, gold, inheritance, equipment, and notifications.");
            }
        }

        Type IRewardHandler.RewardConfigType => typeof(Settings);
        Type ICommandHandler.HandlerConfigType => typeof(Settings);

        void IRewardHandler.Enqueue(ReplyContext context, object config)
        {
            (_, string message) = ExecuteInternal(context.UserName, (Settings)config, context.Args);
            if (message != null)
                ActionManager.NotifyComplete(context, message);
        }

        void ICommandHandler.Execute(ReplyContext context, object config)
        {
            (_, string message) = ExecuteInternal(context.UserName, (Settings)config, context.Args);
            if (message != null)
                ActionManager.SendReply(context, message);
        }

        private static (bool, string) ExecuteInternal(string userName, Settings settings, string contextArgs)
        {
            Hero adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(userName);

            Hero heirHero = BLTAdoptAHeroCampaignBehavior.Current.GetHeirHero(userName); 

            // Determine heir logic
            if (adoptedHero != null && heirHero == null)
            {
                if (string.IsNullOrWhiteSpace(contextArgs))
                {
                    heirHero = adoptedHero.Clan.Heroes
                    .Where(h => h.IsAlive && h.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge && (h.Father == adoptedHero || h.Mother == adoptedHero) && !h.IsAdopted())
                    .SelectRandom();
                    if (heirHero == null)
                        return (false, "No suitable heir found in adopted hero's clan.");

                    string oldName = heirHero.Name.ToString();
                    heirHero.SetName(new(oldName + " Heir"), new(oldName));
                    return (true, $"Assigned heir to {heirHero.FirstName}");
                }
                else
                {
                    heirHero = adoptedHero.Clan.Heroes
                    .Where(h => h.IsAlive && h.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge && (h.Father == adoptedHero || h.Mother == adoptedHero) && !h.IsAdopted())
                    .FirstOrDefault(h => string.Equals(h.FirstName.ToString(), contextArgs, StringComparison.CurrentCultureIgnoreCase));
                    if (heirHero == null)
                        return (false, $"No hero named '{contextArgs}' found to adopt as heir.");

                    string oldName = heirHero.Name.ToString();                    
                    heirHero.SetName(new(oldName + " Heir"), new(oldName));
                    return (true, $"Assigned heir to {heirHero.FirstName}");
                }
            }
            else if (adoptedHero != null && heirHero != null)
            {
                if (!string.IsNullOrWhiteSpace(contextArgs) && heirHero.FirstName.ToString() != (contextArgs))
                {
                    var oldHeirName = heirHero.FirstName;
                    var oldHeir = heirHero;
                    

                    heirHero = adoptedHero.Clan.Heroes
                    .Where(h => h.IsAlive && h.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge && (h.Father == adoptedHero || h.Mother == adoptedHero) && !h.IsAdopted())
                    .FirstOrDefault(h => string.Equals(h.Name.ToString(), contextArgs, StringComparison.CurrentCultureIgnoreCase));
                    if (heirHero == null)
                        return (false, $"No hero named '{contextArgs}' found to adopt as heir.");

                    oldHeir.SetName(oldHeirName, oldHeirName);
                    string oldName = heirHero.Name.ToString();
                    heirHero.SetName(new(oldName + " Heir"), new(oldName));
                    return (true, $"Changed heir {oldHeir.FirstName}=>{heirHero.FirstName}");
                }
                else
                {
                    return (true, $"Heir is {heirHero.FirstName}");
                }
            }
            else if (adoptedHero == null && heirHero != null)
            {
                Hero newHero = heirHero;
                if (newHero == null)
                {
                    return (false, "{=E7wqQ2kg}You can't adopt a hero: no available hero matching the requirements was found!".Translate());
                }
                heirHero = null;
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
            else { return (false, "No heir to adopt"); }           
        }
    }
}