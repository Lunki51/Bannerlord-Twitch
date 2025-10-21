using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Settlements;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;


namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=OoGyH5cw}Hero Features"),
     LocDescription("{=Ia7ACrTK}Allow viewer to adjust characteristics about their Hero"),
     UsedImplicitly]
    public class HeroFeatures : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=KjuNxda1}Change Hero Gender Enabled"),
             LocCategory("Gender", "{=1lZ8Vcbc}Gender"),
             LocDescription("{=puULf6Ca}Enable ability to change gender"),
             PropertyOrder(1), UsedImplicitly]
            public bool GenderEnabled { get; set; } = true;

            [LocDisplayName("{=Tt3LPI6w}Change Hero Gender Gold Cost"),
             LocCategory("Gender", "{=1lZ8Vcbc}Gender"),
             LocDescription("{=oyxJVLCx}Cost of changing gender"),
             PropertyOrder(2), UsedImplicitly]
            public int GenderCost { get; set; } = 50000;

            [LocDisplayName("{=jW4WABm2}Only on created heroes?"),
             LocCategory("Gender", "{=1lZ8Vcbc}Gender"),
             LocDescription("{=guSdSDEy}Only allow changing gender for heroes that are created, instead of adopted"),
             PropertyOrder(3), UsedImplicitly]
            public bool GenderDisabledonNative { get; set; } = true;

            [LocDisplayName("{=tlrdxhlh}Hero appearance enabled"),
             LocCategory("Appearance", "{=rnaya1kT}Appearance"),
             LocDescription("{=f1kdzuzz}Allow applying bodyproperty string to your character"),
             PropertyOrder(4), UsedImplicitly]
            public bool AppearanceEnabled { get; set; } = true;

            [LocDisplayName("{=BKial3sf}Hero marriage enabled"),
             LocCategory("Marriage", "{=PUP4VDH3}Marriage"),
             LocDescription("{=SSJOUTxk}Enable ability for heroes to marry"),
             PropertyOrder(5), UsedImplicitly]
            public bool MarriageEnabled { get; set; } = true;

            [LocDisplayName("{=R0Nn6K7Q}Hero marriage gold cost"),
             LocCategory("Marriage", "{=PUP4VDH3}Marriage"),
             LocDescription("{=ySzfRTTr}Cost of marry action"),
             PropertyOrder(6), UsedImplicitly]
            public int MarriageCost { get; set; } = 50000;

            [LocDisplayName("{=AHxka3EX}Only create spouse"),
             LocCategory("Marriage", "{=PUP4VDH3}Marriage"),
             LocDescription("{=n7fZ5jrr}Spawn spouse instead of choosing existing hero"),
             PropertyOrder(7), UsedImplicitly]
            public bool OnlySpawnSpouse { get; set; } = true;

            [LocDisplayName("{=gMbchUTO}Allow clan or name selection"),
             LocCategory("Marriage", "{=PUP4VDH3}Marriage"),
             LocDescription("{=GYzk7nZB}Allow selecting by clan or hero name"),
             PropertyOrder(8), UsedImplicitly]
            public bool ClanorName { get; set; } = false;

            //[locdisplayname("{=testing}allow viewer marriage"),
            // loccategory("marriage", "{=testing}marriage"),
            // locdescription("{=testing}allow marriage between viewers"),
            // propertyorder(9), usedimplicitly]
            //public bool ViewerAllowed { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                var EnabledCommands = new StringBuilder();
                if (GenderEnabled)
                    EnabledCommands.Append("Change Hero Gender, ");
                if (AppearanceEnabled)
                    EnabledCommands.Append("Change Hero Appearance");
                if (MarriageEnabled)
                    EnabledCommands.Append("Marriage, ");
                if (EnabledCommands.Length > 0)
                    generator.Value("<strong>Enabled Commands:</strong> {commands}".Translate(("commands", EnabledCommands.ToString().Substring(0, EnabledCommands.Length - 2))));

                if (GenderEnabled)
                    generator.Value("<strong>" +
                                    "Gender Change Config: " +
                                    "</strong>" +
                                    "Price={price}{icon}, ".Translate(("price", GenderCost.ToString()), ("icon", Naming.Gold)) +
                                    "Only on created heroes?={DisabledonNative}".Translate(("DisabledonNative", GenderDisabledonNative.ToString())));
                if (MarriageEnabled)
                    generator.Value("<strong>" +
                                    "Marriage Config: " +
                                    "</strong>" +
                                    "Price={price}{icon}, ".Translate(("price", MarriageCost.ToString()), ("icon", Naming.Gold)) +
                                    "Only create spouse?={Create Only}, ".Translate(("Create Only", OnlySpawnSpouse.ToString())) +
                                    "Allow choose by clan or name?={Clan or name}".Translate(("Clan or name", ClanorName.ToString())));
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
                onFailure("{=EyBgfdPz}You cannot manage your hero, as a mission is active!".Translate());
                return;
            }
            if (adoptedHero.HeroState == Hero.CharacterStates.Prisoner)
            {
                onFailure("{=KIaeC6OH}You cannot manage your hero, as you are a prisoner!".Translate());
                return;
            }
            var splitArgs = context.Args.Split(' ');
            var command = splitArgs[0];
            switch (command.ToLower())
            {
                case ("gender"):
                    if (!settings.GenderEnabled)
                    {
                        onFailure("{=rS4Ykysf}Changing heroes gender is not enabled".Translate());
                        return;
                    }
                    if (splitArgs.Length < 2 || string.IsNullOrWhiteSpace(splitArgs[1]))
                    {
                        onFailure("{=rPqyzuoG}Invalid entry (male/female)".Translate());
                        return;
                    }
                    if (!BLTAdoptAHeroCampaignBehavior.Current.GetIsCreatedHero(adoptedHero) && settings.GenderDisabledonNative)
                    {
                        onFailure("{=XfKeCtCR}Changing heroes gender is only enabled for created heroes".Translate());
                        return;
                    }
                    if (settings.GenderCost > BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero))
                    {
                        onFailure(Naming.NotEnoughGold(settings.GenderCost, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                        return;
                    }
                    if (string.Equals(splitArgs[1].ToLower(), "female", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (adoptedHero.IsFemale)
                        {
                            onFailure("{=BE1uGwVi}Your hero is already female".Translate());
                            return;
                        }
                        onSuccess("{=kANu9D6d}Your hero has changed their gender to female".Translate());
                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.GenderCost);
                        adoptedHero.UpdatePlayerGender(true);
                        Log.ShowInformation(
                            "{=byvm3h6C}{Name} has changed their gender to female!".Translate(("Name", adoptedHero.Name)),
                            adoptedHero.CharacterObject);
                        return;
                    }
                    else if (string.Equals(splitArgs[1].ToLower(), "male", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (!adoptedHero.IsFemale)
                        {
                            onFailure("{=aG7rIjnV}Your hero is already male".Translate());
                            return;
                        }
                        onSuccess("{=FlGjts5K}Your hero has changed their gender to male".Translate());
                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.GenderCost);
                        adoptedHero.UpdatePlayerGender(false);
                        Log.ShowInformation(
                            "{=MgcrSo56}{Name} has changed their gender to male!".Translate(("Name", adoptedHero.Name)),
                            adoptedHero.CharacterObject);
                        return;
                    }
                    onFailure("{=rPqyzuoG}Invalid entry (male/female)".Translate());
                    return;

                case ("looks"):
                    {
                        if (!settings.AppearanceEnabled)
                        {
                            onFailure("{=uaiHAfGa}Changing appearance is disabled".Translate());
                            return;
                        }

                        string appearanceArg = context.Args.Length > command.Length
                            ? context.Args.Substring(command.Length).Trim()
                            : null;

                        if (string.IsNullOrEmpty(appearanceArg))
                        {
                            onFailure("{=VuhqWvIK}Please provide an appearance string".Translate());
                            return;
                        }
                        // Validate general format (allow any age, as we'll override it)
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
                            onFailure("{=CW9BdhhR}Invalid appearance string format".Translate());
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

                        string updatedAppearance = ReplaceAge(appearanceArg, adoptedHero.Age);

                        BodyProperties updatedBodyProperties = BodyProperties.Default;
                        BodyProperties.FromString(updatedAppearance, out updatedBodyProperties);

                        bool isFemale = adoptedHero.IsFemale;
                        int race = adoptedHero.CharacterObject?.Race ?? 0;

                        adoptedHero.CharacterObject.UpdatePlayerCharacterBodyProperties(updatedBodyProperties, race, isFemale);

                        onSuccess("{=GjKCaf8c}Appearance updated successfully!".Translate());
                        return;
                    }

                case ("marry"):
                    {
                        if (!settings.MarriageEnabled)
                        {
                            onFailure("{=5LxKguGE}Hero marriage is not enabled".Translate());
                            return;
                        }
                        if (adoptedHero.Occupation != Occupation.Lord)
                        {
                            onFailure("{=TESTING}Not a noble".Translate());
                            return;
                        }
                        if (adoptedHero.Spouse != null)
                        {
                            onFailure("{=42LsY2qb}You are already married".Translate());
                            return;
                        }
                        if (adoptedHero.Clan == null)
                        {
                            onFailure("{=zXegzi8c}You are not in a clan".Translate());
                            return;
                        }
                        if (settings.MarriageCost > BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero))
                        {
                            onFailure("{=avHpPxTN}You do not have enough gold ({price}) to marry".Translate(("price", settings.MarriageCost.ToString())));
                            return;
                        }

                        string spouseArg = context.Args.Length > 1 ? context.Args.Substring(command.Length).Trim() : "";

                        string CleanName(string name)
                        {
                            return name.StartsWith("{=") ? name.Substring(name.IndexOf("}") + 1) : name;
                        }

                        Hero SpawnSpouse(string cultureArg = null)
                        {
                            bool fallback = false;
                            var cultureToSpawn = !string.IsNullOrEmpty(cultureArg)
                                ? CampaignHelpers.MainCultures.FirstOrDefault(c => c.Name.ToString().StartsWith(cultureArg, StringComparison.CurrentCultureIgnoreCase))
                                : adoptedHero.Culture;

                            if (cultureToSpawn == null)
                                cultureToSpawn = CampaignHelpers.MainCultures.FirstOrDefault();

                            var character = CampaignHelpers.GetWandererTemplates(cultureToSpawn).SelectRandom();
                            if (character == null)
                            {
                                character = CampaignHelpers.AllWandererTemplates.SelectRandom();
                                fallback = true;
                            }

                            if (character == null)
                            {
                                onFailure("{=cfkeeEB0}Failed to find a character template to spawn".Translate());
                                return null;
                            }

                            var newHero = HeroCreator.CreateSpecialHero(character);
                            newHero.ChangeState(Hero.CharacterStates.Active);
                            BLTAdoptAHeroCampaignBehavior.Current.SetIsCreatedHero(newHero, true);

                            var towns = Settlement.All.Where(s => s.IsTown).ToList();

                            Settlement heroSettlement = adoptedHero.LastKnownClosestSettlement;
                            Settlement targetSettlement = null;

                            if (heroSettlement != null)
                            {
                                var heroPos = heroSettlement.Position2D;
                                targetSettlement = towns.OrderBy(town => town.Position2D.DistanceSquared(heroPos)).FirstOrDefault();
                            }
                            targetSettlement ??= towns.SelectRandom();

                            if (targetSettlement != null)
                                EnterSettlementAction.ApplyForCharacterOnly(newHero, targetSettlement);
                            else
                                Log.Error("No suitable settlement found to place new hero");

                            newHero.SetNewOccupation(Occupation.Lord);
                            newHero.Clan = adoptedHero.Clan;

                            var randAge = new Random();
                            newHero.SetBirthDay(CampaignTime.YearsFromNow(-Math.Max(Campaign.Current.Models.AgeModel.HeroComesOfAge, adoptedHero.Age + randAge.Next(-3, +3))));

                            if (adoptedHero.IsFemale == newHero.IsFemale)
                            {
                                newHero.UpdatePlayerGender(!newHero.IsFemale);
                            }
                            // Now randomize and assign the name based on new gender
                            bool isFemale = newHero.IsFemale;

                            TextObject objectName = isFemale
                            ? cultureToSpawn.FemaleNameList.SelectRandom()
                            : cultureToSpawn.MaleNameList.SelectRandom();

                            string rawName = objectName.ToString();
                            string oldName = newHero.Name.ToString();

                            CampaignHelpers.SetHeroName(newHero, objectName, objectName);
                            if (fallback)
                                newHero.Culture = cultureToSpawn;
                            return newHero;
                        }

                        if (settings.OnlySpawnSpouse)
                        {
                            if (string.IsNullOrEmpty(spouseArg))
                            {
                                onFailure("{=OwlH8hKI}Please specify a culture to spawn spouse from".Translate());
                                return;
                            }

                            var newHero = SpawnSpouse(spouseArg);
                            if (newHero == null)
                                return;

                            adoptedHero.Spouse = newHero;
                            newHero.Spouse = adoptedHero;

                            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.MarriageCost);
                            onSuccess("{=JW5L4lvt}Marriage successful with spawned spouse".Translate());
                            Log.ShowInformation("{=h6AHfoVx}{heroName} has married {spouseName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("spouseName", CleanName(newHero.Name.ToString()))),
                                adoptedHero.Spouse.CharacterObject, Log.Sound.Horns2);
                            return;
                        }
                        else
                        {
                            string StripTranslationKey(string str)
                            {
                                if (string.IsNullOrEmpty(str)) return str;

                                var match = System.Text.RegularExpressions.Regex.Match(str, @"^\{=.*?\}(.*)$");
                                if (match.Success)
                                    return match.Groups[1].Value.Trim();

                                return str;
                            }

                            IEnumerable<Hero> candidates = CampaignHelpers.AliveHeroes.Where(n =>
                                (n.Name != null && !StripTranslationKey(n.Name.ToString()).Contains(BLTAdoptAHeroModule.Tag)) &&
                                (n.Spouse == null) &&
                                (adoptedHero.IsFemale != n.IsFemale));

                            Func<Hero, bool> universalFilters = n =>
                                n.Occupation == Occupation.Lord &&
                                !n.IsHumanPlayerCharacter &&
                                !n.IsPlayerCompanion &&
                                n.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge &&
                                !n.Clan.Name.ToString().Contains("[BLT Clan]");

                            candidates = candidates.Where(universalFilters);
                            bool ClanorName = false;

                            if (!string.IsNullOrEmpty(spouseArg))
                            {
                                string argLower = spouseArg.ToLowerInvariant().Trim();


                                if (settings.ClanorName)
                                {
                                    var clanOrNameMatches = candidates.Where(n =>
                                    {
                                        string clanName = n.Clan?.Name.ToString() ?? "";
                                        string heroName = StripTranslationKey(n.Name.ToString());

                                        bool clanMatch = clanName.StartsWith(spouseArg, StringComparison.CurrentCultureIgnoreCase) && !clanName.Contains("[BLT Clan]");
                                        bool nameMatch = heroName.StartsWith(spouseArg, StringComparison.CurrentCultureIgnoreCase) && (n.Clan != null || !n.Clan.Name.ToString().Contains("[BLT Clan]"));

                                        ClanorName = true;
                                        return clanMatch || nameMatch;
                                    });

                                    if (!clanOrNameMatches.Any())
                                    {
                                        var cultureMatch = CampaignHelpers.MainCultures.FirstOrDefault(c =>
                                            c.Name.ToString().StartsWith(spouseArg, StringComparison.CurrentCultureIgnoreCase));
                                        if (cultureMatch == null)
                                        {
                                            onFailure("{=Hh87fxvK}No clan, name or culture starting with '{Text}' found".Translate(("Text", spouseArg)));
                                            return;
                                        }

                                        candidates = candidates.Where(n => n.Culture == cultureMatch);

                                        if (adoptedHero.Clan?.Kingdom != null)
                                        {
                                            var sameKingdom = candidates.Where(n => n.Clan?.Kingdom == adoptedHero.Clan.Kingdom);
                                            candidates = sameKingdom.Any() ? sameKingdom : candidates;
                                        }
                                    }
                                    else
                                    {
                                        candidates = clanOrNameMatches;
                                    }
                                }
                                else
                                {
                                    var spouseCulture = CampaignHelpers.MainCultures.FirstOrDefault(c =>
                                        c.Name.ToString().StartsWith(spouseArg, StringComparison.CurrentCultureIgnoreCase));
                                    if (spouseCulture == null)
                                    {
                                        onFailure("{=CCYS1Sb0}No culture starting with '{Text}' found".Translate(("Text", spouseArg)));
                                        return;
                                    }
                                    candidates = candidates.Where(n => n.Culture == spouseCulture);

                                    if (adoptedHero.Clan?.Kingdom != null)
                                    {
                                        var sameKingdom = candidates.Where(n => n.Clan?.Kingdom == adoptedHero.Clan.Kingdom);
                                        candidates = sameKingdom.Any() ? sameKingdom : candidates;
                                    }
                                }
                            }

                            var spouse = candidates.SelectRandom();

                            if (spouse == null && ClanorName == false)
                            {
                                var newHero = SpawnSpouse(spouseArg);
                                if (newHero == null) return;

                                adoptedHero.Spouse = newHero;
                                newHero.Spouse = adoptedHero;

                                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.MarriageCost);
                                onSuccess("{=AMBu22hn}No valid spouse found, spawned fallback spouse successfully".Translate());
                                Log.ShowInformation("{=h6AHfoVx}{heroName} has married {spouseName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("spouseName", CleanName(newHero.Name.ToString()))),
                                    adoptedHero.CharacterObject, Log.Sound.Horns2);
                                return;
                            }
                            if (spouse == null && ClanorName == true)
                            {
                                onFailure("{=GlvwEhly}No available hero with '{Text}' clan or name found".Translate(("Text", spouseArg)));
                                return;
                            }

                            adoptedHero.Spouse = spouse;
                            spouse.Spouse = adoptedHero;
                            spouse.Clan = adoptedHero.Clan;
                            var randAge2 = new Random();
                            spouse.SetBirthDay(CampaignTime.YearsFromNow(-Math.Max(Campaign.Current.Models.AgeModel.HeroComesOfAge, adoptedHero.Age + randAge2.Next(-3, +3))));

                            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.MarriageCost);
                            onSuccess("{=asmoKHI6}Marriage successful".Translate());
                            Log.ShowInformation("{=h6AHfoVx}{heroName} has married {spouseName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("spouseName", CleanName(spouse.Name.ToString()))),
                                adoptedHero.Spouse.CharacterObject, Log.Sound.Horns2);

                            return;
                        }
                    }
                //case "race":
                //    {
                //        if (splitArgs.Length < 2 || !int.TryParse(splitArgs[1], out int race))
                //        {
                //            onFailure("Usage: race <int>".Translate());
                //            return;
                //        }

                //        BodyProperties newBody = BodyProperties.GetRandomBodyProperties(
                //            race,
                //            adoptedHero.IsFemale,
                //            BodyProperties.Default,
                //            BodyProperties.Default,
                //            0,
                //            MBRandom.RandomInt(),
                //            "",
                //            "",
                //            ""
                //        );

                //        adoptedHero.UpdatePlayerCharacterBodyProperties(newBody, race, adoptedHero.IsFemale);
                //        onSuccess($"Hero race set to {race}".Translate());
                //        return;
                //    }
                default:
                    onFailure("{=6t9UWDR2}Invalid action".Translate());
                    return;
            }
        }
    }
}