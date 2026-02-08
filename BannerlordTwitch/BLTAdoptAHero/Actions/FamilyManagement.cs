using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BannerlordTwitch.Helpers;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=FamilyMgmt}Family Management"),
     LocDescription("{=FamilyMgmtDesc}Manage and view your hero's family members"),
     UsedImplicitly]
    public class FamilyManagement : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0)]
        private class Settings : IDocumentable
        {
            // General
            [LocDisplayName("{=TESTING}Baby Command Limit"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=TESTING}Maximum number of kids before the baby command is blocked."),
             PropertyOrder(1), UsedImplicitly]
            public int MakeKidsLimit { get; set; } = 3;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value($"Max Kids From Baby Command: {MakeKidsLimit}");
            }

        }
        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
        Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;

            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (Mission.Current != null)
            {
                onFailure("{=FamilyMissionActive}You cannot manage your family, as a mission is active!".Translate());
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ShowFamilyOverview(adoptedHero, onSuccess);
                return;
            }

            var splitArgs = context.Args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var command = splitArgs[0].ToLower();

            switch (command)
            {
                case "spouse":
                    HandleSpouseCommand(adoptedHero, splitArgs, onSuccess, onFailure, settings);
                    break;
                case "children":
                    HandleChildListCommand(adoptedHero, onSuccess, onFailure);
                    break;
                case "parents":
                    break;
                default:
                    HandleNamedMemberCommand(adoptedHero, splitArgs, onSuccess, onFailure);
                    break;
            }
        }

        private void ShowFamilyOverview(Hero adoptedHero, Action<string> onSuccess)
        {
            int spouseCount = (adoptedHero.Spouse != null ? 1 : 0) + adoptedHero.ExSpouses.Count(h=> !h.IsDead);
            int childrenCount = adoptedHero.Children.Count(h => !h.IsDead);
            int grandchildrenCount = adoptedHero.Children.Sum(c => c.Children.Count(h => !h.IsDead));
            int parentCount = ((adoptedHero.Father != null && !adoptedHero.Father.IsDead) ? 1 : 0) + ((adoptedHero.Mother != null && !adoptedHero.Mother.IsDead) ? 1 : 0);
            int siblingCount = adoptedHero.Siblings.Count(h => !h.IsDead);
            int totalFamily = spouseCount + childrenCount + grandchildrenCount + parentCount + siblingCount;

            var sb = new StringBuilder();
            sb.Append("{=FamilyOverview}Family Overview: ".Translate());
            sb.Append("{=SpouseCount}Spouses: {count} | ".Translate(("count", spouseCount)));
            sb.Append("{=ChildCount}Children: {count} | ".Translate(("count", childrenCount)));
            if (grandchildrenCount > 0)
                sb.Append("{=GrandchildCount}Grandchildren: {count} | ".Translate(("count", grandchildrenCount)));
            if (parentCount > 0)
                sb.Append("{=GrandchildCount}Parents: {count} | ".Translate(("count", parentCount)));
            if (siblingCount > 0)
                sb.Append("{=GrandchildCount}Siblings: {count} | ".Translate(("count", siblingCount)));
            sb.Append("{=TotalFamily}Total Family: {count}".Translate(("count", totalFamily)));

            onSuccess(sb.ToString());
        }

        private void HandleSpouseCommand(Hero adoptedHero, string[] args, Action<string> onSuccess, Action<string> onFailure, Settings settings)
        {
            if (args.Length > 1 && args[1].ToLower() == "looks")
            {
                if (adoptedHero.Spouse == null)
                {
                    onFailure("{=NoSpouse}You have no spouse".Translate());
                    return;
                }

                if (args.Length < 3)
                {
                    onFailure("{=ProvideLooks}Please provide an appearance string".Translate());
                    return;
                }

                string appearanceArg = string.Join(" ", args.Skip(2));
                ApplyLooks(adoptedHero.Spouse, appearanceArg, onSuccess, onFailure);
                return;
            }

            if (args.Length > 1 && args[1].ToLower() == "rename")
            {
                if (adoptedHero.Spouse == null)
                {
                    onFailure("{=NoSpouse}You have no spouse".Translate());
                    return;
                }

                if (args.Length < 3)
                {
                    onFailure("{=ProvideNewName}Please provide a new name".Translate());
                    return;
                }
                string newName = string.Join(" ", args.Skip(2));
                RenameHero(adoptedHero.Spouse, newName, onSuccess, onFailure);
                return;
            }

            if (args.Length > 1 && args[1].ToLower() == "baby")
            {
                if (adoptedHero.Spouse == null)
                {
                    onFailure("{=NoSpouse}You have no spouse".Translate());
                    return;
                }

                MakeBaby(adoptedHero, onSuccess, onFailure, settings);
                return;
            }

            if (args.Length > 1 && args[1].ToLower() == "skills")
            {
                if (adoptedHero.Spouse == null)
                {
                    onFailure("{=NoSpouse}You have no spouse".Translate());
                    return;
                }
                string skills = ShowSkills(adoptedHero.Spouse);
                onSuccess(skills);
                return;
            }

            if (adoptedHero.Spouse == null)
            {
                if (adoptedHero.ExSpouses.Count > 0)
                {
                    var sB = new StringBuilder();
                    sB.Append("{=ChildrenList}Ex-spouses: ".Translate());

                    var spouses = adoptedHero.ExSpouses.OrderByDescending(c => c.Age).ToList();
                    for (int i = 0; i < spouses.Count; i++)
                    {
                        var exSpouse = spouses[i];
                        sB.Append(CleanName(exSpouse.Name.ToString()));
                        sB.Append($" ({(int)exSpouse.Age}, ");
                        sB.Append(exSpouse.IsFemale ? "{=F}F".Translate() : "{=M}M".Translate());
                        if (exSpouse.Spouse != null)
                            sB.Append(", 💍");
                        if (exSpouse.IsDead)
                            sB.Append(", 💀");
                        if (exSpouse.Children.Count > 0)
                            sB.Append($", 👪:{exSpouse.Children.Count}");
                        sB.Append(")");

                        if (i < spouses.Count - 1)
                        {
                            sB.Append(", ");
                        }
                    }
                }
                else
                {
                    onFailure("{=NoSpouse}You have no spouse".Translate());
                }
                return;
            }

            var spouse = adoptedHero.Spouse;
            var sb = new StringBuilder();

            sb.Append("{=SpouseInfo}Spouse: ".Translate());
            sb.Append(CleanName(spouse.Name.ToString()));
            sb.Append(" | ");
            sb.Append("{=Age}Age: {age}".Translate(("age", (int)spouse.Age)));
            sb.Append(" | ");
            sb.Append(spouse.IsFemale ? "{=Female}Female".Translate() : "{=Male}Male".Translate());

            if (adoptedHero.IsFemale && adoptedHero.IsPregnant)
            {
                sb.Append(" | {=YouPregnant}You are pregnant".Translate());
            }
            else if (!adoptedHero.IsFemale && spouse.IsPregnant)
            {
                sb.Append(" | {=SpousePregnant}Your spouse is pregnant".Translate());
            }
            var highestSkill = CampaignHelpers.AllSkillObjects
                .OrderByDescending(s => spouse.GetSkillValue(s))
                .FirstOrDefault();
            sb.Append($" | TopSkill:{SkillXP.GetShortSkillName(highestSkill)} {spouse.GetSkillValue(highestSkill)}");

            onSuccess(sb.ToString());
        }

        private void HandleChildListCommand(Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (adoptedHero.Children.Count == 0)
            {
                onFailure("{=NoChildren}You have no children".Translate());
                return;
            }

            var sb = new StringBuilder();
            sb.Append("{=ChildrenList}Children: ".Translate());

            var children = adoptedHero.Children.OrderByDescending(c => c.Age).ToList();
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                sb.Append(CleanName(child.Name.ToString()));
                sb.Append($" ({(int)child.Age}, ");
                sb.Append(child.IsFemale ? "{=F}F".Translate() : "{=M}M".Translate());
                if (child.Spouse != null)
                    sb.Append(", 💍");
                if (child.IsDead)
                    sb.Append(", 💀");
                if (child.Children.Count > 0)
                    sb.Append($", 👪:{child.Children.Count}");
                sb.Append(")");

                if (i < children.Count - 1)
                {
                    sb.Append(", ");
                }
            }

            onSuccess(sb.ToString());
        }

        private void HandleNamedMemberCommand(Hero adoptedHero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            string memberName = args[0];
            int? index = null;

            // Check if name ends with a number (e.g., "John2")
            var match = Regex.Match(memberName, @"^(.+?)(\d+)$");
            if (match.Success)
            {
                memberName = match.Groups[1].Value;
                index = int.Parse(match.Groups[2].Value);
            }

            // Find matching children
            var matchingChildren = adoptedHero.Children
                .Where(c => CleanName(c.Name.ToString()).IndexOf(memberName, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (matchingChildren.Count == 0)
            {
                onFailure("{=NoChildFound}No child found with name '{name}'".Translate(("name", memberName)));
                return;
            }

            if (matchingChildren.Count > 1 && !index.HasValue)
            {
                var sb = new StringBuilder();
                sb.Append("{=MultipleChildren}Multiple children found: ".Translate());
                for (int i = 0; i < matchingChildren.Count; i++)
                {
                    sb.Append($"{CleanName(matchingChildren[i].Name.ToString())}{i + 1}");
                    sb.Append($" ({(int)matchingChildren[i].Age}, ");
                    sb.Append(matchingChildren[i].IsFemale ? "{=F}F".Translate() : "{=M}M".Translate());
                    if (matchingChildren[i].Spouse != null)
                        sb.Append(", 💍");
                    if (matchingChildren[i].IsDead)
                        sb.Append(", 💀");
                    if (matchingChildren[i].Children.Count > 0)
                        sb.Append($", 👪:{matchingChildren[i].Children.Count}");
                    if (i < matchingChildren.Count - 1) sb.Append(", ");
                }
                onFailure(sb.ToString());
                return;
            }

            Hero child = index.HasValue && index.Value > 0 && index.Value <= matchingChildren.Count
                ? matchingChildren[index.Value - 1]
                : matchingChildren[0];

            // Check for subcommands
            if (args.Length > 1)
            {
                var subCommand = args[1].ToLower();

                switch (subCommand)
                {
                    case "looks":
                        if (args.Length < 3)
                        {
                            onFailure("{=ProvideLooks}Please provide an appearance string".Translate());
                            return;
                        }
                        string appearanceArg = string.Join(" ", args.Skip(2));
                        ApplyLooks(child, appearanceArg, onSuccess, onFailure);
                        break;

                    case "rename":
                        if (args.Length < 3)
                        {
                            onFailure("{=ProvideNewName}Please provide a new name".Translate());
                            return;
                        }
                        string newName = string.Join(" ", args.Skip(2));
                        RenameHero(child, newName, onSuccess, onFailure);
                        break;
                    case "marry":
                        {
                            string[] targets = args.Skip(2).ToArray();
                            MarryHero(adoptedHero, child, targets, onSuccess, onFailure);
                        }
                        break;                      
                    case "skills":
                        { 
                            string skills = ShowSkills(child);
                            onSuccess(skills);
                            return;
                        }

                    default:
                        // Check if it's a grandchild name
                        HandleGrandchildCommand(child, args.Skip(1).ToArray(), onSuccess, onFailure);
                        break;
                }
            }
            else
            {
                ShowChildInfo(child, onSuccess);
            }
        }

        private void HandleGrandchildCommand(Hero parent, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (parent.Children.Count == 0)
            {
                onFailure("{=NoGrandchildren}{parent} has no children".Translate(("parent", CleanName(parent.Name.ToString()))));
                return;
            }

            string grandchildName = args[0];
            int? index = null;

            var match = Regex.Match(grandchildName, @"^(.+?)(\d+)$");
            if (match.Success)
            {
                grandchildName = match.Groups[1].Value;
                index = int.Parse(match.Groups[2].Value);
            }

            var matchingGrandchildren = parent.Children
                .Where(c => CleanName(c.Name.ToString()).IndexOf(grandchildName, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (matchingGrandchildren.Count == 0)
            {
                onFailure("{=NoGrandchildFound}No grandchild found with name '{name}'".Translate(("name", grandchildName)));
                return;
            }

            if (matchingGrandchildren.Count > 1 && !index.HasValue)
            {
                var sb = new StringBuilder();
                sb.Append("{=MultipleGrandchildren}Multiple grandchildren found: ".Translate());
                for (int i = 0; i < matchingGrandchildren.Count; i++)
                {
                    sb.Append($"{CleanName(matchingGrandchildren[i].Name.ToString())}{i + 1}");
                    if (i < matchingGrandchildren.Count - 1) sb.Append(", ");
                }
                onFailure(sb.ToString());
                return;
            }

            Hero grandchild = index.HasValue && index.Value > 0 && index.Value <= matchingGrandchildren.Count
                ? matchingGrandchildren[index.Value - 1]
                : matchingGrandchildren[0];

            ShowChildInfo(grandchild, onSuccess);
        }

        private void ShowChildInfo(Hero child, Action<string> onSuccess)
        {
            var sb = new StringBuilder();

            sb.Append("{=ChildInfo}Name: {name}".Translate(("name", CleanName(child.Name.ToString()))));
            sb.Append(" | ");
            sb.Append("{=Age}Age: {age}".Translate(("age", (int)child.Age)));
            sb.Append(" | ");
            sb.Append(child.IsFemale ? "{=Female}Female".Translate() : "{=Male}Male".Translate());

            if (child.Clan != null)
            {
                sb.Append(" | ");
                sb.Append("{=Clan}Clan: {clan}".Translate(("clan", child.Clan.Name.ToString())));
            }

            if (child.IsDead)
            {
                sb.Append(" | {=Deceased}DECEASED".Translate());
            }
            if (child.Spouse != null)
            {
                sb.Append(" | ");
                sb.Append("{=Spouse}Spouse: {spouse}".Translate(("spouse", CleanName(child.Spouse.Name.ToString()))));
            }
            var highestSkill = CampaignHelpers.AllSkillObjects
                .OrderByDescending(s => child.GetSkillValue(s))
                .FirstOrDefault();

            sb.Append($" | TopSkill:{SkillXP.GetShortSkillName(highestSkill)} {child.GetSkillValue(highestSkill)}");

            if (child.Children.Count > 0)
            {
                sb.Append(" | ");
                sb.Append("{=Children}Children: ".Translate());
                var childNames = child.Children.Select(c => CleanName(c.Name.ToString())).ToList();
                sb.Append(string.Join(", ", childNames));
            }

            onSuccess(sb.ToString());
        }

        private void ApplyLooks(Hero hero, string appearanceArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrEmpty(appearanceArg))
            {
                onFailure("{=ProvideLooks}Please provide an appearance string".Translate());
                return;
            }

            bool IsValidBodyProperties(string input)
            {
                var pattern = @"^<BodyProperties\s+" +
                              @"version=""4""\s+" +
                              @"age=""[^""]+""\s+" +
                              @"weight=""(0\.(0*[1-9]\d*|[1-9]\d*)|1(\.0*)?)""\s+" +
                              @"build=""(0\.(0*[1-9]\d*|[1-9]\d*)|1(\.0*)?)""\s+" +
                              @"key=""[0-9A-Fa-f]+""\s*/>$";

                return Regex.IsMatch(input.Trim(), pattern);
            }

            if (!IsValidBodyProperties(appearanceArg))
            {
                onFailure("{=InvalidAppearance}Invalid appearance string format".Translate());
                return;
            }

            string ReplaceAge(string input, float age)
            {
                return Regex.Replace(
                    input,
                    @"age=""[^""]+""",
                    $"age=\"{age.ToString(System.Globalization.CultureInfo.InvariantCulture)}\""
                );
            }

            string updatedAppearance = ReplaceAge(appearanceArg, hero.Age);

            BodyProperties updatedBodyProperties = BodyProperties.Default;
            BodyProperties.FromString(updatedAppearance, out updatedBodyProperties);

            bool isFemale = hero.IsFemale;
            int race = hero.CharacterObject?.Race ?? 0;

            hero.CharacterObject.UpdatePlayerCharacterBodyProperties(updatedBodyProperties, race, isFemale);

            onSuccess("{=AppearanceUpdated}Appearance updated for {name}!".Translate(("name", CleanName(hero.Name.ToString()))));
        }

        private void RenameHero(Hero hero, string newName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                onFailure("{=ProvideNewName}Please provide a new name".Translate());
                return;
            }

            string oldName = CleanName(hero.Name.ToString());
            var newNameObj = new TextObject(newName);
            hero.SetName(newNameObj, newNameObj);

            onSuccess("{=HeroRenamed}{oldName} has been renamed to {newName}!".Translate(
                ("oldName", oldName),
                ("newName", newName)));

            Log.ShowInformation("{=HeroRenamed}{oldName} has been renamed to {newName}!".Translate(
                ("oldName", oldName),
                ("newName", newName)), hero.CharacterObject);
        }

        private string ShowSkills(Hero hero)
        {
            var stats = new StringBuilder();
            stats.Append($"{"{=fRwyY6ms}[LVL]".Translate()} {hero.Level}");

            var skillsList = CampaignHelpers.AllSkillObjects         
                .OrderByDescending(s => hero.GetSkillValue(s))
                .Select(skill =>
                    $"{SkillXP.GetShortSkillName(skill)} {hero.GetSkillValue(skill)} " +
                    $"[" +
                    $"{"{=lHRDKsUT}f".Translate()}" +
                    $"{hero.HeroDeveloper.GetFocus(skill)}]");

            stats.Append($"{"{=rTId8pBy}[SKILLS]".Translate()} {string.Join(Naming.Sep2, skillsList)}");
            return stats.ToString();
        }

        private Dictionary<(Hero proposer, Hero receiver), (Hero hero1, Hero hero2)> _marriageProposals = new();

        private void MarryHero(Hero adoptedHero, Hero hero, string[] targets, Action<string> onSuccess, Action<string> onFailure)
        {
            if (targets.Length < 2)
            {
                onFailure("marry (username) (kid first name) | Whoever makes proposal marries off");
                return;
            }

            Hero adoptedHero1 = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString().IndexOf(targets[0], StringComparison.OrdinalIgnoreCase) >= 0);

            if (adoptedHero1 == null)
            {
                onFailure($"Couldnt find hero named {targets[0]}");
                return;
            }

            if (targets[1].Equals("reject", StringComparison.OrdinalIgnoreCase))
            {
                if (_marriageProposals.Remove((adoptedHero1, adoptedHero)))
                    onSuccess($"Rejected {adoptedHero1.Name}'s proposal");
                else
                    onFailure("No proposal to reject");
                return;
            }

            Hero target = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString().IndexOf(targets[1], StringComparison.OrdinalIgnoreCase) >= 0 && (h.Father == adoptedHero1 || h.Mother == adoptedHero1));
            
            if (target == null)
            {
                onFailure($"Couldnt find hero named {targets[1]}");
                return;
            }
            if (hero.Age < 18 || target.Age < 18)
            {
                onFailure("Too young");
                return;
            }
            if (hero.IsAdopted()|| target.IsAdopted())
            {
                onFailure("Cannot marry blts");
                return;
            }
            if (hero.IsClanLeader || target.IsClanLeader)
            {
                onFailure("Cannot  marry clan leaders");
                return;
            }

            if (_marriageProposals.TryGetValue((adoptedHero1, adoptedHero), out var pair))
            {
                var h1 = pair.hero1;
                var h2 = pair.hero2;

                if (h1.Spouse != null || h2.Spouse != null)
                {
                    onFailure("One hero is already married");
                    _marriageProposals.Remove((adoptedHero1, adoptedHero));
                    return;
                }

                var oldClan = h1.Clan;

                h1.Spouse = h2;
                h2.Spouse = h1;
                if (h1.GovernorOf != null)
                {
                    ChangeGovernorAction.RemoveGovernorOf(h1);
                }
                if (h1.PartyBelongedTo != null)
                {
                    var oldParty = h1.PartyBelongedTo;
                    bool wasLeader = oldParty.LeaderHero == h1;
                    oldParty.MemberRoster.RemoveTroop(h1.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
                    MakeHeroFugitiveAction.Apply(h1, false);
                    if (wasLeader && oldParty.IsLordParty)
                        DisbandPartyAction.StartDisband(oldParty);
                }
                h1.Clan = h2.Clan;
                _marriageProposals.Remove((adoptedHero1, adoptedHero));

                var marriageModel = Campaign.Current.Models.MarriageModel;
                h2.UpdateHomeSettlement();
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(h1, h2, Campaign.Current.Models.MarriageModel.GetEffectiveRelationIncrease(h1, h2), false);

                onSuccess($"{h1.Name} of {oldClan.Name} married {h2.Name} of {h2.Clan.Name}");
                return;
            }
            if (hero.Spouse != null || target.Spouse != null)
            {
                onFailure($"invalid hero");
                return;
            }
            else
            {
                _marriageProposals[(adoptedHero, adoptedHero1)] = (hero, target);
                onSuccess($"Sent proposal to {adoptedHero1.Name}");
                return;
            }
        }

        private void MakeBaby(Hero hero, Action<string> onSuccess, Action<string> onFailure, Settings settings)
        {
            int childCount = hero.Children.Where(c => !c.IsDead && c.Clan == hero.Clan).Count();
            if (childCount >= settings.MakeKidsLimit)
            {
                onFailure($"You already have {childCount} alive children in your clan, baby command limit is {settings.MakeKidsLimit}");
                return;
            }
            bool isTarget = hero.IsFemale;
            if (isTarget)
            {
                if (hero.IsPregnant)
                {
                    onFailure($"{hero.Name} is already pregnant.");
                    return;
                }
                else
                {
                    MakePregnantAction.Apply(hero);
                    onSuccess($"{hero.Name} is now pregnant.");
                }
            }
            else
            {
                if (hero.Spouse.IsPregnant)
                {
                    onFailure($"{hero.Spouse.Name} is already pregnant.");
                    return;
                }
                else
                {
                    MakePregnantAction.Apply(hero.Spouse);
                    onSuccess($"{hero.Spouse.Name} is now pregnant.");
                }
            }
        }

        private string CleanName(string name)
        {
            return name.StartsWith("{=") ? name.Substring(name.IndexOf("}") + 1) : name;
        }
    }
}