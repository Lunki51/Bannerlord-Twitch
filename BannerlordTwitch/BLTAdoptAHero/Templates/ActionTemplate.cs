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
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=LOCKEY}Display Name"),
     LocDescription("{=LOCKEY}Description of what this action does"),
     UsedImplicitly]
    public class TemplateAction : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0),
         CategoryOrder("Advanced", 1)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=LOCKEY}Enabled"),
             LocCategory("General", "{=LOCKEY}General"),
             LocDescription("{=LOCKEY}Enable this action"),
             PropertyOrder(1), UsedImplicitly]
            public bool Enabled { get; set; } = true;

            [LocDisplayName("{=LOCKEY}Gold Cost"),
             LocCategory("General", "{=LOCKEY}General"),
             LocDescription("{=LOCKEY}Cost in gold to use this action"),
             PropertyOrder(2), UsedImplicitly]
            public int GoldCost { get; set; } = 1000;

            [LocDisplayName("{=LOCKEY}Cooldown Hours"),
             LocCategory("Advanced", "{=LOCKEY}Advanced"),
             LocDescription("{=LOCKEY}Hours before this action can be used again"),
             PropertyOrder(1), UsedImplicitly]
            public int CooldownHours { get; set; } = 24;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                if (Enabled)
                {
                    generator.Value("<strong>Enabled:</strong> Yes");
                    generator.Value("<strong>Gold Cost:</strong> {cost}{icon}"
                        .Translate(("cost", GoldCost.ToString()), ("icon", Naming.Gold)));
                    generator.Value("<strong>Cooldown:</strong> {hours} hours"
                        .Translate(("hours", CooldownHours)));
                }
                else
                {
                    generator.Value("<strong>Enabled:</strong> No");
                }
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(
            Hero adoptedHero,
            ReplyContext context,
            object config,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            if (config is not Settings settings) return;

            // Ensure hero exists
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            // Check if action is enabled
            if (!settings.Enabled)
            {
                onFailure("{=LOCKEY}This action is disabled".Translate());
                return;
            }

            // Check if mission is active (prevents conflicts)
            if (Mission.Current != null)
            {
                onFailure("{=LOCKEY}You cannot use this action during a mission!".Translate());
                return;
            }

            // Check if hero is prisoner (optional - remove if not needed)
            if (adoptedHero.HeroState == Hero.CharacterStates.Prisoner)
            {
                onFailure("{=LOCKEY}You cannot use this action while imprisoned!".Translate());
                return;
            }

            // Check gold cost
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.GoldCost)
            {
                onFailure(Naming.NotEnoughGold(settings.GoldCost,
                    BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            // Parse arguments if needed
            if (!context.Args.IsEmpty())
            {
                var args = context.Args.Split(' ');
                var command = args[0];
                // Handle different subcommands here
            }

            // Execute the main action logic
            try
            {
                // Deduct gold
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.GoldCost, true);

                // YOUR ACTION LOGIC HERE
                // Example: adoptedHero.AddSkillXp(DefaultSkills.Leadership, 1000);

                // Success message
                onSuccess("{=LOCKEY}Action completed successfully!".Translate());

                // Optional: Show information banner to all players
                Log.ShowInformation(
                    "{=LOCKEY}{heroName} used the template action!".Translate(("heroName", adoptedHero.Name.ToString())),
                    adoptedHero.CharacterObject,
                    Log.Sound.Horns2);
            }
            catch (Exception ex)
            {
                onFailure($"Action failed: {ex.Message}");
            }
        }

        // Helper method example - if you have a very large system, it may be worth it to create a seperate helper file with public methods, for ease of access and to prevent clutter
        private bool CanUseAction(Hero hero, Settings settings)
        {
            if (hero == null || !hero.IsAlive)
                return false;

            if (hero.IsPrisoner)
                return false;

            return true;
        }
    }
}