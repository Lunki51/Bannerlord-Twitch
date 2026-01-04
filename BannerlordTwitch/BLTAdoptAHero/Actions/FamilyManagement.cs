using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=FamilyMgmt}Family Management"),
     LocDescription("{=FamilyMgmtDesc}Manage and view your hero's family members"),
     UsedImplicitly]
    public class FamilyManagement : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
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
                    HandleSpouseCommand(adoptedHero, splitArgs, onSuccess, onFailure);
                    break;
                case "child":
                    HandleChildListCommand(adoptedHero, onSuccess, onFailure);
                    break;
                default:
                    HandleNamedMemberCommand(adoptedHero, splitArgs, onSuccess, onFailure);
                    break;
            }
        }

        private void ShowFamilyOverview(Hero adoptedHero, Action<string> onSuccess)
        {
            int spouseCount = (adoptedHero.Spouse != null ? 1 : 0) + adoptedHero.ExSpouses.Count;
            int childrenCount = adoptedHero.Children.Count;
            int grandchildrenCount = adoptedHero.Children.Sum(c => c.Children.Count);
            int totalFamily = spouseCount + childrenCount + grandchildrenCount;

            var sb = new StringBuilder();
            sb.Append("{=FamilyOverview}Family Overview: ".Translate());
            sb.Append("{=SpouseCount}Spouses: {count} | ".Translate(("count", spouseCount)));
            sb.Append("{=ChildCount}Children: {count} | ".Translate(("count", childrenCount)));
            sb.Append("{=GrandchildCount}Grandchildren: {count} | ".Translate(("count", grandchildrenCount)));
            sb.Append("{=TotalFamily}Total Family: {count}".Translate(("count", totalFamily)));

            onSuccess(sb.ToString());
        }

        private void HandleSpouseCommand(Hero adoptedHero, string[] args, Action<string> onSuccess, Action<string> onFailure)
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

            if (adoptedHero.Spouse == null)
            {
                if (adoptedHero.ExSpouses.Count > 0)
                {
                    onFailure("{=SpouseDied}Your spouse has died or divorced you".Translate());
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

            if (spouse.IsDead)
            {
                sb.Append(" | {=Deceased}DECEASED".Translate());
            }

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

            var children = adoptedHero.Children.OrderBy(c => c.Age).ToList();
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                sb.Append(CleanName(child.Name.ToString()));
                sb.Append($" ({(int)child.Age}, ");
                sb.Append(child.IsFemale ? "{=F}F".Translate() : "{=M}M".Translate());
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
            else
            {
                if (child.Spouse != null)
                {
                    sb.Append(" | ");
                    sb.Append("{=Spouse}Spouse: {spouse}".Translate(("spouse", CleanName(child.Spouse.Name.ToString()))));
                }

                if (child.Children.Count > 0)
                {
                    sb.Append(" | ");
                    sb.Append("{=Children}Children: ".Translate());
                    var childNames = child.Children.Select(c => CleanName(c.Name.ToString())).ToList();
                    sb.Append(string.Join(", ", childNames));
                }
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

        private string CleanName(string name)
        {
            return name.StartsWith("{=") ? name.Substring(name.IndexOf("}") + 1) : name;
        }
    }
}