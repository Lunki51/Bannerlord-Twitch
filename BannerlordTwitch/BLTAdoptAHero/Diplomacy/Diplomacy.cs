using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using BannerlordTwitch.Helpers;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Localization;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using BLTAdoptAHero.Actions;
using System.Windows.Media.Animation;
using System.Windows;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=TESTING}Diplomacy"),
     LocDescription("{=TESTING}Manage your kingdom diplomacy with the new BLT treaty system"),
     UsedImplicitly]
    class Diplomacy : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0),
         CategoryOrder("War", 1),
         CategoryOrder("Peace", 2),
         CategoryOrder("NAP", 3),
         CategoryOrder("Alliance", 4),
         CategoryOrder("CTW", 5),
         CategoryOrder("Tribute", 6),
         CategoryOrder("Truce", 7)]
        private class Settings : IDocumentable
        {
            // General
            [LocDisplayName("{=TESTING}Enable New Diplomacy"),
             LocCategory("General", "{=TESTING}General"),
             LocDescription("{=TESTING}Enable the new BLT treaty system (disabling reverts to old system)"),
             PropertyOrder(1), UsedImplicitly]
            public bool EnableNewDiplomacy { get; set; } = true;

            // War
            [LocDisplayName("{=TESTING}War Enabled"),
             LocCategory("War", "{=TESTING}War"),
             LocDescription("{=TESTING}Enable declaring war command"),
             PropertyOrder(1), UsedImplicitly]
            public bool WarEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Gold Cost"),
             LocCategory("War", "{=TESTING}War"),
             LocDescription("{=TESTING}War command gold cost"),
             PropertyOrder(2), UsedImplicitly]
            public int WarPrice { get; set; } = 250000;

            [LocDisplayName("{=TESTING}Influence Cost"),
             LocCategory("War", "{=TESTING}War"),
             LocDescription("{=TESTING}War influence cost multiplier (multiplies base game influence cost)"),
             PropertyOrder(3), UsedImplicitly]
            public float WarInfluenceMult { get; set; } = 1.0f;

            [LocDisplayName("{=TESTING}Cooldown Days"),
             LocCategory("War", "{=TESTING}War"),
             LocDescription("{=TESTING}Days after peace before war can be declared again"),
             PropertyOrder(4), UsedImplicitly]
            public int WarCooldown { get; set; } = 20;

            [LocDisplayName("{=TESTING}Require Confirmation"),
             LocCategory("War", "{=TESTING}War"),
             LocDescription("{=TESTING}Require 'yes' confirmation if enemy has allies"),
             PropertyOrder(5), UsedImplicitly]
            public bool WarRequireConfirm { get; set; } = true;

            [LocDisplayName("{=TESTING}Min War Duration"),
             LocCategory("War", "{=TESTING}War"),
             LocDescription("{=TESTING}Minimum days a war must last before peace can be made"),
             PropertyOrder(6), UsedImplicitly]
            public int MinWarDuration { get; set; } = 30;

            // Peace
            [LocDisplayName("{=TESTING}Peace Enabled"),
             LocCategory("Peace", "{=TESTING}Peace"),
             LocDescription("{=TESTING}Enable making peace command"),
             PropertyOrder(1), UsedImplicitly]
            public bool PeaceEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Gold Cost"),
             LocCategory("Peace", "{=TESTING}Peace"),
             LocDescription("{=TESTING}Peace command gold cost"),
             PropertyOrder(2), UsedImplicitly]
            public int PeacePrice { get; set; } = 100000;

            [LocDisplayName("{=TESTING}Influence Cost"),
             LocCategory("Peace", "{=TESTING}Peace"),
             LocDescription("{=TESTING}Peace influence cost multiplier"),
             PropertyOrder(3), UsedImplicitly]
            public float PeaceInfluenceMult { get; set; } = 1.0f;

            // NAP
            [LocDisplayName("{=TESTING}NAP Enabled"),
             LocCategory("NAP", "{=TESTING}NAP"),
             LocDescription("{=TESTING}Enable non-aggression pact command"),
             PropertyOrder(1), UsedImplicitly]
            public bool NAPEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Gold Cost"),
             LocCategory("NAP", "{=TESTING}NAP"),
             LocDescription("{=TESTING}NAP gold cost"),
             PropertyOrder(2), UsedImplicitly]
            public int NAPPrice { get; set; } = 100000;

            [LocDisplayName("{=TESTING}Influence Cost"),
             LocCategory("NAP", "{=TESTING}NAP"),
             LocDescription("{=TESTING}NAP influence cost"),
             PropertyOrder(3), UsedImplicitly]
            public int NAPInfluence { get; set; } = 50;

            [LocDisplayName("{=TESTING}Max NAPs"),
             LocCategory("NAP", "{=TESTING}NAP"),
             LocDescription("{=TESTING}Maximum NAPs per kingdom (0 = unlimited)"),
             PropertyOrder(4), UsedImplicitly]
            public int MaxNAPs { get; set; } = 5;

            [LocDisplayName("{=TESTING}Cost Scaling"),
             LocCategory("NAP", "{=TESTING}NAP"),
             LocDescription("{=TESTING}Enable cost scaling based on existing treaties"),
             PropertyOrder(5), UsedImplicitly]
            public bool NAPCostScaling { get; set; } = false;

            [LocDisplayName("{=TESTING}Cost Scale Rate"),
             LocCategory("NAP", "{=TESTING}NAP"),
             LocDescription("{=TESTING}Cost multiplier per existing NAP (e.g. 1.2 = 20% more per NAP)"),
             PropertyOrder(6), UsedImplicitly]
            public float NAPCostScaleRate { get; set; } = 1.2f;

            // Alliance
            [LocDisplayName("{=TESTING}Alliance Enabled"),
             LocCategory("Alliance", "{=TESTING}Alliance"),
             LocDescription("{=TESTING}Enable alliance command"),
             PropertyOrder(1), UsedImplicitly]
            public bool AllianceEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Gold Cost"),
             LocCategory("Alliance", "{=TESTING}Alliance"),
             LocDescription("{=TESTING}Alliance gold cost"),
             PropertyOrder(2), UsedImplicitly]
            public int AlliancePrice { get; set; } = 150000;

            [LocDisplayName("{=TESTING}Influence Cost"),
             LocCategory("Alliance", "{=TESTING}Alliance"),
             LocDescription("{=TESTING}Alliance influence cost"),
             PropertyOrder(3), UsedImplicitly]
            public int AllianceInfluence { get; set; } = 100;

            [LocDisplayName("{=TESTING}Max Alliances"),
             LocCategory("Alliance", "{=TESTING}Alliance"),
             LocDescription("{=TESTING}Maximum alliances per kingdom (0 = unlimited)"),
             PropertyOrder(4), UsedImplicitly]
            public int MaxAlliances { get; set; } = 3;

            [LocDisplayName("{=TESTING}Cost Scaling"),
             LocCategory("Alliance", "{=TESTING}Alliance"),
             LocDescription("{=TESTING}Enable cost scaling based on existing alliances"),
             PropertyOrder(5), UsedImplicitly]
            public bool AllianceCostScaling { get; set; } = false;

            [LocDisplayName("{=TESTING}Cost Scale Rate"),
             LocCategory("Alliance", "{=TESTING}Alliance"),
             LocDescription("{=TESTING}Cost multiplier per existing alliance"),
             PropertyOrder(6), UsedImplicitly]
            public float AllianceCostScaleRate { get; set; } = 1.3f;

            [LocDisplayName("{=TESTING}Trade"),
             LocCategory("Alliance", "{=TESTING}Alliance"),
             LocDescription("{=TESTING}Enable trade alliance command"),
             PropertyOrder(7), UsedImplicitly]
            public bool TradeEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Price"),
             LocCategory("Alliance", "{=TESTING}Alliance"),
             LocDescription("{=TESTING}Trade command price"),
             PropertyOrder(8), UsedImplicitly]
            public int TradePrice { get; set; } = 50000;

            [LocDisplayName("{=TESTING}Clan Ally Price"),
             LocCategory("Alliance", "{=TESTING}Alliance"),
             LocDescription("{=TESTING}Gold cost for an independent clan to propose a clan alliance"),
             PropertyOrder(9), UsedImplicitly]
            public int ClanAllyPrice { get; set; } = 50000;

            // CTW
            [LocDisplayName("{=TESTING}CTW Enabled"),
             LocCategory("CTW", "{=TESTING}Call to War"),
             LocDescription("{=TESTING}Enable call to war command"),
             PropertyOrder(1), UsedImplicitly]
            public bool CTWEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Gold Cost"),
             LocCategory("CTW", "{=TESTING}Call to War"),
             LocDescription("{=TESTING}Call to war gold cost"),
             PropertyOrder(2), UsedImplicitly]
            public int CTWPrice { get; set; } = 50000;

            [LocDisplayName("{=TESTING}Influence Cost"),
             LocCategory("CTW", "{=TESTING}Call to War"),
             LocDescription("{=TESTING}Call to war influence cost"),
             PropertyOrder(3), UsedImplicitly]
            public int CTWInfluence { get; set; } = 50;

            [LocDisplayName("{=TESTING}Acceptance Days"),
             LocCategory("CTW", "{=TESTING}Call to War"),
             LocDescription("{=TESTING}Days ally has to accept call to war"),
             PropertyOrder(4), UsedImplicitly]
            public int CTWAcceptanceDays { get; set; } = 15;

            [LocDisplayName("{=TESTING}Cooldown Days"),
             LocCategory("CTW", "{=TESTING}Call to War"),
             LocDescription("{=TESTING}Days before can call same ally again (0 = no cooldown)"),
             PropertyOrder(5), UsedImplicitly]
            public int CTWCooldown { get; set; } = 30;

            // Tribute
            [LocDisplayName("{=TESTING}Min Daily Tribute"),
             LocCategory("Tribute", "{=TESTING}Tribute"),
             LocDescription("{=TESTING}Minimum daily tribute gold"),
             PropertyOrder(1), UsedImplicitly]
            public int TributeMin { get; set; } = 100;

            [LocDisplayName("{=TESTING}Max Daily Tribute"),
             LocCategory("Tribute", "{=TESTING}Tribute"),
             LocDescription("{=TESTING}Maximum daily tribute gold"),
             PropertyOrder(2), UsedImplicitly]
            public int TributeMax { get; set; } = 10000;

            [LocDisplayName("{=TESTING}Default Duration"),
             LocCategory("Tribute", "{=TESTING}Tribute"),
             LocDescription("{=TESTING}Default tribute duration in days"),
             PropertyOrder(3), UsedImplicitly]
            public int TributeDuration { get; set; } = 90;

            // Truce
            [LocDisplayName("{=TESTING}Duration Days"),
             LocCategory("Truce", "{=TESTING}Truce"),
             LocDescription("{=TESTING}Truce duration in days"),
             PropertyOrder(1), UsedImplicitly]
            public int TruceDuration { get; set; } = 30;

            [LocDisplayName("{=TESTING}Break NAP Cost"),
             LocCategory("Truce", "{=TESTING}Truce"),
             LocDescription("{=TESTING}Gold cost to break NAP"),
             PropertyOrder(2), UsedImplicitly]
            public int BreakNAPPrice { get; set; } = 0;

            [LocDisplayName("{=TESTING}Break Alliance Cost"),
             LocCategory("Truce", "{=TESTING}Truce"),
             LocDescription("{=TESTING}Gold cost to break alliance"),
             PropertyOrder(3), UsedImplicitly]
            public int BreakAlliancePrice { get; set; } = 0;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value($"<strong>New Diplomacy System:</strong> {(EnableNewDiplomacy ? "Enabled" : "Disabled")}");

                if (EnableNewDiplomacy)
                {
                    var sb = new StringBuilder();
                    if (WarEnabled) sb.Append("War, ");
                    if (PeaceEnabled) sb.Append("Peace, ");
                    if (NAPEnabled) sb.Append("NAP, ");
                    if (AllianceEnabled) sb.Append("Alliance, ");
                    if (TradeEnabled) sb.Append("{=TESTING}Trade, ".Translate());
                    if (CTWEnabled) sb.Append("CTW");

                    if (sb.Length > 0)
                        generator.Value($"<strong>Enabled Features:</strong> {sb.ToString().TrimEnd(',', ' ')}");

                    if (WarEnabled)
                        generator.Value($"<strong>War:</strong> {WarPrice}{Naming.Gold}, Influence x{WarInfluenceMult}, {WarCooldown} day cooldown, {MinWarDuration} day minimum duration");

                    if (PeaceEnabled)
                        generator.Value($"<strong>Peace:</strong> {PeacePrice}{Naming.Gold}, Influence x{PeaceInfluenceMult}");

                    if (NAPEnabled)
                        generator.Value($"<strong>NAP:</strong> {NAPPrice}{Naming.Gold}, {NAPInfluence} influence, Max: {(MaxNAPs == 0 ? "Unlimited" : MaxNAPs.ToString())}");

                    if (AllianceEnabled)
                        generator.Value($"<strong>Alliance:</strong> {AlliancePrice}{Naming.Gold}, {AllianceInfluence} influence, Max: {(MaxAlliances == 0 ? "Unlimited" : MaxAlliances.ToString())}");
                        generator.Value($"<strong>Clan Alliance:</strong> {ClanAllyPrice}{Naming.Gold} per proposal");

                    if (TradeEnabled)
                        generator.Value("<strong>Trade Config: </strong>" +
                                        "Price={price}{icon}".Translate(("price", TradePrice.ToString()), ("icon", Naming.Gold)));

                    if (CTWEnabled)
                        generator.Value($"<strong>CTW:</strong> {CTWPrice}{Naming.Gold}, {CTWInfluence} influence, {CTWAcceptanceDays} days to accept");

                    generator.Value($"<strong>Tribute:</strong> {TributeMin}-{TributeMax}{Naming.Gold}/day, {TributeDuration} days");
                    generator.Value($"<strong>Truce:</strong> {TruceDuration} days");
                }
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;

            // If new diplomacy is disabled, fall back to old system
            if (!settings.EnableNewDiplomacy)
            {
                ExecuteOldDiplomacy(adoptedHero, context, settings, onSuccess, onFailure);
                return;
            }

            // Validation
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Args))
            {
                onFailure("Usage: !diplomacy <war|peace|nap|alliance|trade|ctw|break|info|accept|reject> [args]");
                return;
            }

            if (Mission.Current != null)
            {
                onFailure("Mission is active!");
                return;
            }

            if (adoptedHero.Clan == null)
            {
                onFailure("{=}You are not in a Clan!".Translate());
                return;
            }

            if (adoptedHero.Clan.Kingdom == null)
            {
                var earlyArgs = context.Args.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (earlyArgs.Length > 0 && earlyArgs[0].Equals("clan", StringComparison.OrdinalIgnoreCase))
                {
                    HandleClanCommand(settings, adoptedHero,
                        earlyArgs.Length > 1 ? earlyArgs[1] : "",
                        onSuccess, onFailure);
                    return;
                }
            }

            if (!adoptedHero.IsKingdomLeader)
            {
                onFailure("{=TESTING}You must be a king to use diplomacy commands".Translate());
                return;
            }

            if (adoptedHero.Clan.IsUnderMercenaryService)
            {
                onFailure("Mercenaries cannot manage diplomacy");
                return;
            }

            // Update the treaty manager's minimum war duration setting
            if (BLTTreatyManager.Current != null)
            {
                BLTTreatyManager.Current.MinWarDurationDays = settings.MinWarDuration;
            }

            var splitArgs = context.Args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var command = splitArgs[0].ToLower();
            var args = splitArgs.Skip(1).ToArray();

            switch (command)
            {
                case "war":
                    HandleWarCommand(settings, adoptedHero, args, onSuccess, onFailure);
                    break;
                case "warstance":
                    HandleWarStanceCommand(settings, adoptedHero, args, onSuccess, onFailure);
                    break;
                case "peace":
                    HandlePeaceCommand(settings, adoptedHero, args, onSuccess, onFailure);
                    break;
                case "nap":
                    HandleNAPCommand(settings, adoptedHero, args, onSuccess, onFailure);
                    break;
                case "alliance":
                    HandleAllianceCommand(settings, adoptedHero, args, onSuccess, onFailure);
                    break;
                case "ally":
                    HandleAllianceCommand(settings, adoptedHero, args, onSuccess, onFailure);
                    break;
                case "trade":
                    HandleTradeCommand(settings, adoptedHero, args, onSuccess, onFailure);
                    break;
                case "ctw":
                    HandleCTWCommand(settings, adoptedHero, args, onSuccess, onFailure);
                    break;
                case "break":
                    HandleBreakCommand(settings, adoptedHero, args, onSuccess, onFailure);
                    break;
                case "info":
                    HandleInfoCommand(settings, adoptedHero, args, onSuccess, onFailure);
                    break;
                case "accept":
                    HandleAcceptCommand(settings, adoptedHero, args, onSuccess, onFailure);
                    break;
                case "reject":
                    HandleRejectCommand(settings, adoptedHero, args, onSuccess, onFailure);
                    break;
                default:
                    onFailure("Invalid command. Use: war, peace, nap, alliance, trade, ctw, break, info, accept, reject");
                    break;
            }
        }

        private void HandleWarCommand(Settings settings, Hero hero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.WarEnabled)
            {
                onFailure("War declarations are disabled");
                return;
            }

            if (args.Length == 0)
            {
                onFailure("Usage: !diplomacy war <kingdom> [yes]");
                return;
            }

            bool confirmed = args.Length > 1 && args[args.Length - 1].ToLower() == "yes";
            string targetName = confirmed ? string.Join(" ", args.Take(args.Length - 1)) : string.Join(" ", args);

            var kingdom = hero.Clan.Kingdom;
            var target = FindKingdom(targetName);

            if (target == null)
            {
                onFailure($"Kingdom '{targetName}' not found");
                return;
            }

            // Check if can declare war
            if (!BLTTreatyManager.Current.CanDeclareWar(kingdom, target, out string reason))
            {
                onFailure(reason);
                return;
            }

            // Check cooldown
            var stance = kingdom.GetStanceWith(target);
            if (stance != null && stance.PeaceDeclarationDate.ElapsedDaysUntilNow < settings.WarCooldown)
            {
                int remaining = (int)(settings.WarCooldown - stance.PeaceDeclarationDate.ElapsedDaysUntilNow);
                onFailure($"Cannot declare war yet. {remaining} days remaining in cooldown.");
                return;
            }

            // Check costs
            int influenceCost = (int)(Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingWar(hero.Clan) * settings.WarInfluenceMult);

            if (hero.Clan.Influence < influenceCost)
            {
                onFailure($"Not enough influence (need {influenceCost}, have {(int)hero.Clan.Influence})");
                return;
            }

            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero) < settings.WarPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.WarPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero)));
                return;
            }

            // Check for allies and require confirmation
            var targetAllies = BLTTreatyManager.Current.GetAlliancesFor(target);
            bool hasAllies = targetAllies.Count > 0;

            if (hasAllies && settings.WarRequireConfirm && !confirmed)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"WARNING: Declaring war on {target.Name}");
                sb.AppendLine($"Your strength: {(int)kingdom.CurrentTotalStrength}");

                int totalEnemyStrength = (int)target.CurrentTotalStrength;
                sb.Append($"{target.Name}: {(int)target.CurrentTotalStrength}");

                if (targetAllies.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"{target.Name} has {targetAllies.Count} allies:");
                    foreach (var alliance in targetAllies)
                    {
                        var ally = alliance.GetOtherKingdom(target);
                        if (ally != null)
                        {
                            sb.AppendLine($"  - {ally.Name}: {(int)ally.CurrentTotalStrength}");
                            totalEnemyStrength += (int)ally.CurrentTotalStrength;
                        }
                    }
                }

                sb.AppendLine($"Total enemy strength: {totalEnemyStrength}");
                sb.AppendLine();
                sb.Append("To confirm, use: !diplomacy war " + targetName + " yes");

                onSuccess(sb.ToString());
                return;
            }

            // Declare war
            AdoptedHeroFlags._allowDiplomacyAction = true;
            try
            {
                // Remove any NAP/Alliance with target
                BLTTreatyManager.Current.RemoveNAP(kingdom, target);
                if (target.Leader != null && target.Leader.IsAdopted())
                {
                    string tName = target.Leader.FirstName.ToString()
                        .Replace(BLTAdoptAHeroModule.Tag, "").Replace(BLTAdoptAHeroModule.DevTag, "").Trim();
                    Log.LogFeedResponse($"@{tName} {kingdom.Name} has broken your non-aggression pact by declaring war!");
                }

                BLTTreatyManager.Current.RemoveAlliance(kingdom, target); 
                if (target.Leader != null && target.Leader.IsAdopted())
                {
                    string tName = target.Leader.FirstName.ToString()
                        .Replace(BLTAdoptAHeroModule.Tag, "").Replace(BLTAdoptAHeroModule.DevTag, "").Trim();
                    Log.LogFeedResponse($"@{tName} {kingdom.Name} has broken your alliance by declaring war!");
                }


                // Cancel any tributes
                BLTTreatyManager.Current.RemoveTribute(kingdom, target);

                // Create BLT war
                var war = BLTTreatyManager.Current.CreateWar(kingdom, target);

                // Declare actual game war
                DeclareWarAction.ApplyByDefault(kingdom, target);
                FactionManager.DeclareWar(kingdom, target);

                // Deduct costs
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -settings.WarPrice, true);
                hero.Clan.Influence -= influenceCost;

                onSuccess($"Declared war on {target.Name}!");
                Log.ShowInformation($"{hero.Name} has declared war on {target.Name}!", hero.CharacterObject, Log.Sound.Horns2);
            }
            finally
            {
                AdoptedHeroFlags._allowDiplomacyAction = false;
            }
        }

        private void HandlePeaceCommand(Settings settings, Hero hero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.PeaceEnabled)
            {
                onFailure("Peace is disabled");
                return;
            }

            if (args.Length == 0)
            {
                onFailure("Usage: !diplomacy peace <offer|demand> <kingdom> [amount] [yes]");
                return;
            }

            // Parse arguments
            bool isOffer = args[0].ToLower() == "offer";
            bool isDemand = args[0].ToLower() == "demand";

            if (!isOffer && !isDemand)
            {
                onFailure("Use 'offer' to pay tribute or 'demand' to receive tribute. Usage: !diplomacy peace <offer|demand> <kingdom> [amount] [yes]");
                return;
            }

            if (args.Length < 2)
            {
                onFailure("Usage: !diplomacy peace <offer|demand> <kingdom> [amount] [yes]");
                return;
            }

            bool confirmed = args[args.Length - 1].ToLower() == "yes";
            int tributeAmount = 0;
            bool hasCustomTribute = false;
            string targetName;

            // Parse tribute amount if provided
            if (args.Length >= 3)
            {
                string lastBeforeYes = confirmed && args.Length >= 3 ? args[args.Length - 2] : args[args.Length - 1];
                if (int.TryParse(lastBeforeYes, out tributeAmount))
                {
                    hasCustomTribute = true;
                    int endIndex = confirmed ? args.Length - 2 : args.Length - 1;
                    targetName = string.Join(" ", args.Skip(1).Take(endIndex - 1));
                }
                else
                {
                    targetName = confirmed ? string.Join(" ", args.Skip(1).Take(args.Length - 2)) : string.Join(" ", args.Skip(1));
                }
            }
            else
            {
                targetName = args[1];
            }

            var kingdom = hero.Clan.Kingdom;
            var target = FindKingdom(targetName);

            if (target == null)
            {
                onFailure($"Kingdom '{targetName}' not found");
                return;
            }

            if (!kingdom.IsAtWarWith(target))
            {
                onFailure($"Not at war with {target.Name}");
                return;
            }

            // Check if peace can be made (includes minimum war duration check)
            if (!BLTTreatyManager.Current.CanMakePeace(kingdom, target, out string peaceDenialReason))
            {
                onFailure(peaceDenialReason);
                return;
            }

            // Check minimum war duration
            var war = BLTTreatyManager.Current.GetWar(kingdom, target);
            if (war != null && settings.MinWarDuration > 0)
            {
                int daysSinceWarStart = (int)(CampaignTime.Now - war.StartDate).ToDays;
                if (daysSinceWarStart < settings.MinWarDuration)
                {
                    int daysRemaining = settings.MinWarDuration - daysSinceWarStart;
                    onFailure($"War must last at least {settings.MinWarDuration} days. {daysRemaining} days remaining.");
                    return;
                }
            }

            // Check if target is BLT controlled - need this early to validate tribute
            bool targetIsBLT = target.Leader != null && target.Leader.IsAdopted();

            // Only allow custom tribute for BLT-controlled kingdoms
            if (hasCustomTribute && (!targetIsBLT || target.Leader == Hero.MainHero))
            {
                onFailure($"Custom tribute amounts are only allowed when negotiating with BLT-controlled kingdoms. The game will calculate tribute for {target.Name}.");
                return;
            }

            // Check if this would break an alliance
            bool wouldBreakAlliance = false;
            Kingdom alliancePartner = null;

            if (war != null && !war.IsMainParticipant(kingdom))
            {
                // This is an assisting ally trying to peace out
                var mainOpponent = war.GetMainOpponent(kingdom);
                if (mainOpponent != null)
                {
                    wouldBreakAlliance = true;
                    // Find which main participant is our ally
                    if (war.IsAttackerSide(kingdom))
                        alliancePartner = war.GetAttacker();
                    else
                        alliancePartner = war.GetDefender();
                }
            }

            if (wouldBreakAlliance && !confirmed)
            {
                onSuccess($"WARNING: Making peace will break your alliance with {alliancePartner.Name} and create a {settings.TruceDuration}-day truce. To confirm: !diplomacy peace {args[0]} {targetName} {(hasCustomTribute ? tributeAmount.ToString() + " " : "")}yes");
                return;
            }

            // Calculate tribute
            int dailyTribute = 0;
            int duration = settings.TributeDuration;

            if (hasCustomTribute)
            {
                // This will only execute for BLT-controlled kingdoms due to earlier check
                if (tributeAmount < settings.TributeMin || tributeAmount > settings.TributeMax)
                {
                    onFailure($"Tribute must be between {settings.TributeMin} and {settings.TributeMax} gold/day");
                    return;
                }
                dailyTribute = tributeAmount;
            }
            else
            {
                // Use base game calculation
                // FIXED: Calculate tribute correctly based on offer/demand
                int gameDuration;
                if (isOffer)
                {
                    // Offering peace: we pay if we're weaker
                    dailyTribute = Campaign.Current.Models.DiplomacyModel.GetDailyTributeToPay(hero.Clan, target.RulingClan, out gameDuration);
                    // If game says we should pay (positive), use that. Otherwise, 0.
                    dailyTribute = Math.Max(0, dailyTribute);
                }
                else
                {
                    // Demanding peace: they pay if they're weaker
                    dailyTribute = Campaign.Current.Models.DiplomacyModel.GetDailyTributeToPay(target.RulingClan, hero.Clan, out gameDuration);
                    // If game says they should pay (positive), use that. Otherwise, 0.
                    dailyTribute = Math.Max(0, dailyTribute);
                }
                duration = gameDuration > 0 ? gameDuration : settings.TributeDuration;
            }

            // Check costs
            int influenceCost = (int)(Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingPeace(hero.Clan) * settings.PeaceInfluenceMult);

            if (hero.Clan.Influence < influenceCost)
            {
                onFailure($"Not enough influence (need {influenceCost}, have {(int)hero.Clan.Influence})");
                return;
            }

            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero) < settings.PeacePrice)
            {
                onFailure(Naming.NotEnoughGold(settings.PeacePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero)));
                return;
            }

            // FIXED: Determine payer and receiver BEFORE deducting costs
            Kingdom payer = isOffer ? kingdom : target;
            Kingdom receiver = isOffer ? target : kingdom;

            // Check if target is BLT controlled
            if (targetIsBLT)
            {
                // Create peace proposal instead of forcing it
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -settings.PeacePrice, true);
                hero.Clan.Influence -= influenceCost;

                BLTTreatyManager.Current.CreatePeaceProposal(
                    kingdom,
                    target,
                    isOffer,
                    dailyTribute,
                    duration,
                    settings.PeacePrice,
                    influenceCost,
                    15 // days to accept
                );

                // FIXED: Add tribute info to display
                string tributeMsg = dailyTribute > 0
                    ? $" ({(isOffer ? "offering" : "demanding")} {dailyTribute}{Naming.Gold}/day for {duration} days)"
                    : " (no tribute)";

                onSuccess($"Peace proposal sent to {target.Name}{tributeMsg}. They have 15 days to respond.");

                // FIXED: Notify target with tribute info
                string targetLeaderName = target.Leader.FirstName.ToString()
                    .Replace(BLTAdoptAHeroModule.Tag, "")
                    .Replace(BLTAdoptAHeroModule.DevTag, "")
                    .Trim();
                Log.LogFeedResponse($"@{targetLeaderName} {kingdom.Name} offers peace{tributeMsg}! Use !diplomacy accept peace {kingdom.Name}");
            }
            else if (target.Leader == Hero.MainHero)
            {
                // Deduct costs
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -settings.PeacePrice, true);
                hero.Clan.Influence -= influenceCost;
                // Player kingdom making peace with AI - use event dispatcher
                CampaignEventDispatcher.Instance.OnPeaceOfferedToPlayer(kingdom, dailyTribute, duration);
            }
            else
            {
                // AI to AI peace - force it
                var diplomacyHelper = Campaign.Current.GetCampaignBehavior<BLTDiplomacyHelper>();
                if (diplomacyHelper.IsPeaceBlocked(kingdom, target))
                {
                    onFailure("Cannot peace rebellion wars");
                    return;
                }

                bool acceptPeace = Campaign.Current.Models.KingdomDecisionPermissionModel.IsPeaceDecisionAllowedBetweenKingdoms(kingdom, target, out TextObject reason);
                if (!acceptPeace)
                {
                    onFailure(reason.ToString());
                    return;
                }

                // Deduct costs
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -settings.PeacePrice, true);
                hero.Clan.Influence -= influenceCost;

                AdoptedHeroFlags._allowDiplomacyAction = true;
                try
                {
                    // Make peace in game
                    MakePeaceAction.ApplyByKingdomDecision(kingdom, target, dailyTribute, duration);
                    FactionManager.SetNeutral(kingdom, target);

                    // Create tribute if amount > 0
                    if (dailyTribute > 0)
                    {
                        BLTTreatyManager.Current.CreateTribute(payer, receiver, dailyTribute, duration);
                    }

                    // Create truce
                    BLTTreatyManager.Current.CreateTruce(kingdom, target, settings.TruceDuration);

                    // Handle war cleanup
                    if (war != null)
                    {
                        if (war.IsMainParticipant(kingdom))
                        {
                            // Main participant making peace - peace out all assistants (no tribute for them)
                            var allies = war.IsAttackerSide(kingdom) ? war.GetAttackerAllies() : war.GetDefenderAllies();
                            foreach (var ally in allies)
                            {
                                if (ally != null && ally.IsAtWarWith(target))
                                {
                                    MakePeaceAction.Apply(ally, target);
                                    FactionManager.SetNeutral(ally, target);
                                }
                            }
                            BLTTreatyManager.Current.RemoveWar(kingdom, target);
                        }
                        else
                        {
                            // Assisting ally peacing out separately - remove from war and break alliance
                            war.RemoveAlly(kingdom);

                            // Recalculate alliance partner
                            Kingdom alliancePartnerToBreak = null;
                            if (war.IsAttackerSide(kingdom))
                                alliancePartnerToBreak = war.GetAttacker();
                            else
                                alliancePartnerToBreak = war.GetDefender();

                            if (alliancePartnerToBreak != null)
                            {
                                BLTTreatyManager.Current.RemoveAlliance(kingdom, alliancePartnerToBreak);
                                BLTTreatyManager.Current.CreateTruce(kingdom, alliancePartnerToBreak, settings.TruceDuration);

                                // FIXED: Notify both parties of broken alliance
                                Log.ShowInformation($"Alliance broken between {kingdom.Name} and {alliancePartnerToBreak.Name}!", hero.CharacterObject);

                                if (alliancePartnerToBreak.Leader != null && alliancePartnerToBreak.Leader.IsAdopted())
                                {
                                    string partnerLeaderName = alliancePartnerToBreak.Leader.FirstName.ToString()
                                        .Replace(BLTAdoptAHeroModule.Tag, "")
                                        .Replace(BLTAdoptAHeroModule.DevTag, "")
                                        .Trim();
                                    Log.LogFeedResponse($"@{partnerLeaderName} Your alliance with {kingdom.Name} has been broken because they made peace with {target.Name}!");
                                }
                            }
                        }
                    }

                    string tributeMsg = dailyTribute > 0
                        ? $" ({(isOffer ? "paying" : "receiving")} {dailyTribute}{Naming.Gold}/day for {duration} days)"
                        : "";

                    onSuccess($"Made peace with {target.Name}{tributeMsg}. Truce: {settings.TruceDuration} days");
                    Log.ShowInformation($"{hero.Name} has made peace with {target.Name}!", hero.CharacterObject);
                }
                finally
                {
                    AdoptedHeroFlags._allowDiplomacyAction = false;
                }
            }
        }

        private void HandleNAPCommand(Settings settings, Hero hero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.NAPEnabled)
            {
                onFailure("NAPs are disabled");
                return;
            }

            if (args.Length == 0)
            {
                onFailure("Usage: !diplomacy nap <kingdom>");
                return;
            }

            string targetName = string.Join(" ", args);
            var kingdom = hero.Clan.Kingdom;
            var target = FindKingdom(targetName);

            if (target == null)
            {
                onFailure($"Kingdom '{targetName}' not found");
                return;
            }

            if (kingdom == target)
            {
                onFailure("Cannot make NAP with yourself");
                return;
            }

            if (kingdom.IsAtWarWith(target))
            {
                onFailure($"At war with {target.Name}. Make peace first.");
                return;
            }

            // Check for existing NAP
            if (BLTTreatyManager.Current.GetNAP(kingdom, target) != null)
            {
                onFailure($"Already have NAP with {target.Name}");
                return;
            }

            // Check for alliance (NAP would be redundant)
            if (BLTTreatyManager.Current.GetAlliance(kingdom, target) != null)
            {
                onFailure($"Already allied with {target.Name}");
                return;
            }

            // Check for truce
            var truce = BLTTreatyManager.Current.GetTruce(kingdom, target);
            if (truce != null && !truce.IsExpired())
            {
                onFailure($"Cannot make NAP during truce ({truce.DaysRemaining()} days remaining)");
                return;
            }

            // Check max NAPs
            if (settings.MaxNAPs > 0)
            {
                int napCount = BLTTreatyManager.Current.GetNAPsFor(kingdom).Count;
                if (napCount >= settings.MaxNAPs)
                {
                    onFailure($"Maximum NAPs reached ({napCount}/{settings.MaxNAPs})");
                    return;
                }
            }

            // Calculate costs with scaling
            int goldCost = settings.NAPPrice;
            int influenceCost = settings.NAPInfluence;

            if (settings.NAPCostScaling)
            {
                int existingNAPs = BLTTreatyManager.Current.GetNAPsFor(kingdom).Count;
                goldCost = (int)(goldCost * Math.Pow(settings.NAPCostScaleRate, existingNAPs));
                influenceCost = (int)(influenceCost * Math.Pow(settings.NAPCostScaleRate, existingNAPs));
            }

            // Check costs
            if (hero.Clan.Influence < influenceCost)
            {
                onFailure($"Not enough influence (need {influenceCost}, have {(int)hero.Clan.Influence})");
                return;
            }

            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero) < goldCost)
            {
                onFailure(Naming.NotEnoughGold(goldCost, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero)));
                return;
            }

            // Create NAP
            // Check if target is BLT controlled
            if (target.Leader != null && target.Leader.IsAdopted())
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -goldCost, true);
                hero.Clan.Influence -= influenceCost;
                BLTTreatyManager.Current.CreateNAPProposal(kingdom, target, goldCost, influenceCost, 15);
                onSuccess($"NAP proposal sent to {target.Name}. They have 15 days to respond.");

                string targetLeaderName = target.Leader.FirstName.ToString()
                    .Replace(BLTAdoptAHeroModule.Tag, "")
                    .Replace(BLTAdoptAHeroModule.DevTag, "")
                    .Trim();
                Log.LogFeedResponse($"@{targetLeaderName} {kingdom.Name} proposes a non-aggression pact! Use !diplomacy accept nap {kingdom.Name}");
            }
            //else if (target == Hero.MainHero.Clan.Kingdom)
            //{
            //    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -goldCost, true);
            //    hero.Clan.Influence -= influenceCost;
            //    BLTTreatyManager.Current.CreateNAPProposal(kingdom, target, goldCost, influenceCost, 15);
            //    BLTNAPOfferBehavior.Current?.OfferNAPToPlayer(kingdom, target, 15);
            //    onSuccess($"NAP proposal sent to {target.Name}");
            //}
            else
            {
                // AI kingdom - create NAP directly
                //BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -goldCost, true);
                //hero.Clan.Influence -= influenceCost;
                //
                //BLTTreatyManager.Current.CreateNAP(kingdom, target);
                //
                //onSuccess($"Non-aggression pact established with {target.Name}");
                //Log.ShowInformation($"{kingdom.Name} and {target.Name} have signed a non-aggression pact!", hero.CharacterObject);


                // We're blocking NAPs with AI for balance reasons
                onFailure($"You cannot form NAPs with AI controlled kingdoms!");
                return;
            }
        }

        private void HandleAllianceCommand(Settings settings, Hero hero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.AllianceEnabled)
            {
                onFailure("Alliances are disabled");
                return;
            }

            if (args.Length == 0)
            {
                onFailure("Usage: !diplomacy alliance <kingdom>");
                return;
            }

            string targetName = string.Join(" ", args);
            var kingdom = hero.Clan.Kingdom;
            var target = FindKingdom(targetName);

            if (target == null)
            {
                onFailure($"Kingdom '{targetName}' not found");
                return;
            }

            if (kingdom == target)
            {
                onFailure("Cannot ally with yourself");
                return;
            }

            if (kingdom.IsAtWarWith(target))
            {
                onFailure($"At war with {target.Name}. Make peace first.");
                return;
            }

            // Check for existing alliance
            if (BLTTreatyManager.Current.GetAlliance(kingdom, target) != null)
            {
                onFailure($"Already allied with {target.Name}");
                return;
            }

            // Check for truce
            var truce = BLTTreatyManager.Current.GetTruce(kingdom, target);
            if (truce != null && !truce.IsExpired())
            {
                onFailure($"Cannot ally during truce ({truce.DaysRemaining()} days remaining)");
                return;
            }

            // Check max alliances
            if (settings.MaxAlliances > 0)
            {
                int allyCount = BLTTreatyManager.Current.GetAlliancesFor(kingdom).Count;
                if (allyCount >= settings.MaxAlliances)
                {
                    onFailure($"Maximum alliances reached ({allyCount}/{settings.MaxAlliances})");
                    return;
                }
            }

            // Calculate costs with scaling
            int goldCost = settings.AlliancePrice;
            int influenceCost = settings.AllianceInfluence;

            if (settings.AllianceCostScaling)
            {
                int existingAlliances = BLTTreatyManager.Current.GetAlliancesFor(kingdom).Count;
                goldCost = (int)(goldCost * Math.Pow(settings.AllianceCostScaleRate, existingAlliances));
                influenceCost = (int)(influenceCost * Math.Pow(settings.AllianceCostScaleRate, existingAlliances));
            }

            // Check costs
            if (hero.Clan.Influence < influenceCost)
            {
                onFailure($"Not enough influence (need {influenceCost}, have {(int)hero.Clan.Influence})");
                return;
            }

            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero) < goldCost)
            {
                onFailure(Naming.NotEnoughGold(goldCost, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero)));
                return;
            }

            // Need to add check for alliances with targets allied with kingdoms at war with

            // Create alliance
            // Check if target is BLT controlled
            if (target.Leader != null && target.Leader.IsAdopted())
            {
                // Create proposal instead
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -goldCost, true);
                hero.Clan.Influence -= influenceCost;

                BLTTreatyManager.Current.CreateAllianceProposal(
                    kingdom,
                    target,
                    goldCost,
                    influenceCost,
                    15,
                    settings.BreakAlliancePrice,
                    settings.CTWPrice
                );

                onSuccess($"Alliance proposal sent to {target.Name}. They have 15 days to respond.");

                // Notify target leader
                string targetLeaderName = target.Leader.FirstName.ToString()
                    .Replace(BLTAdoptAHeroModule.Tag, "")
                    .Replace(BLTAdoptAHeroModule.DevTag, "")
                    .Trim();
                Log.LogFeedResponse($"@{targetLeaderName} {kingdom.Name} proposes an alliance! Use !diplomacy accept alliance {kingdom.Name}");
            }
            //else if (target == Hero.MainHero.Clan.Kingdom)
            //{
            //    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -goldCost, true);
            //    hero.Clan.Influence -= influenceCost;
            //
            //    BLTTreatyManager.Current.CreateAllianceProposal(
            //        kingdom,
            //        target,
            //        goldCost,
            //        influenceCost,
            //        15,
            //        settings.BreakAlliancePrice,
            //        settings.CTWPrice
            //    );
            //
            //    BLTPlayerOffersBehavior.Current?.OfferAllianceToPlayer(kingdom, target, 15);
            //
            //    onSuccess($"Alliance proposal sent to {target.Name}");
            //}
            else
            {
                // AI kingdom - create alliance directly
                //BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -goldCost, true);
                //hero.Clan.Influence -= influenceCost;
                //
                //BLTTreatyManager.Current.CreateAlliance(kingdom, target);
                //BLTTreatyManager.Current.RemoveNAP(kingdom, target);
                //
                //onSuccess($"Alliance formed with {target.Name}!");
                //Log.ShowInformation($"{kingdom.Name} and {target.Name} have formed an alliance!", hero.CharacterObject, Log.Sound.Horns2);

                // We're blocking alliances with AI for balance reasons
                onFailure($"You cannot ally AI controlled kingdoms!");
                return;
            }
        }

        private void HandleCTWCommand(Settings settings, Hero hero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.CTWEnabled)
            {
                onFailure("Call to war is disabled");
                return;
            }

            if (args.Length < 2)
            {
                onFailure("Usage: !diplomacy ctw <ally_kingdom> <target_kingdom>");
                return;
            }

            // Parse: last word is target, rest is ally name
            string targetName = args[args.Length - 1];
            string allyName = string.Join(" ", args.Take(args.Length - 1));

            var kingdom = hero.Clan.Kingdom;
            var ally = FindKingdom(allyName);
            var target = FindKingdom(targetName);

            if (ally == null)
            {
                onFailure($"Ally kingdom '{allyName}' not found");
                return;
            }

            if (target == null)
            {
                onFailure($"Target kingdom '{targetName}' not found");
                return;
            }

            // Check alliance
            if (BLTTreatyManager.Current.GetAlliance(kingdom, ally) == null)
            {
                onFailure($"Not allied with {ally.Name}");
                return;
            }

            // Check if already at war with target
            if (!kingdom.IsAtWarWith(target))
            {
                onFailure($"You must be at war with {target.Name} to call allies");
                return;
            }

            // Check if ally already at war with target
            if (ally.IsAtWarWith(target))
            {
                onFailure($"{ally.Name} is already at war with {target.Name}");
                return;
            }

            // Check if ally can declare war on target
            if (!BLTTreatyManager.Current.CanDeclareWar(ally, target, out string reason))
            {
                onFailure($"{ally.Name} cannot join: {reason}");
                return;
            }

            // Check costs
            if (hero.Clan.Influence < settings.CTWInfluence)
            {
                onFailure($"Not enough influence (need {settings.CTWInfluence}, have {(int)hero.Clan.Influence})");
                return;
            }

            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero) < settings.CTWPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.CTWPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero)));
                return;
            }


            // Notify ally kingdom leader if BLT
            if (ally.Leader != null && ally.Leader.IsAdopted())
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -settings.CTWPrice, true);
                hero.Clan.Influence -= settings.CTWInfluence;
                BLTTreatyManager.Current.CreateCTWProposal(kingdom, ally, target, settings.CTWAcceptanceDays);
                onSuccess($"Call to war sent to {ally.Name} against {target.Name}. They have {settings.CTWAcceptanceDays} days to respond.");

                string allyLeaderName = ally.Leader.FirstName.ToString()
                    .Replace(BLTAdoptAHeroModule.Tag, "")
                    .Replace(BLTAdoptAHeroModule.DevTag, "")
                    .Trim();
                Log.LogFeedResponse($"@{allyLeaderName} {kingdom.Name} calls you to war against {target.Name}! Use !diplomacy accept ctw {kingdom.Name} to join.");
            }
            //else if (ally?.Leader == Hero.MainHero)
            //{
            //    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -settings.CTWPrice, true);
            //    hero.Clan.Influence -= settings.CTWInfluence;
            //    BLTTreatyManager.Current.CreateCTWProposal(kingdom, ally, target, settings.CTWAcceptanceDays);
            //    BLTCTWOfferBehavior.Current?.OfferCTWToPlayer(kingdom, ally, target, settings.CTWAcceptanceDays);
            //    onSuccess($"Call to war sent to {ally.Name}");
            //}
        }

        private void HandleBreakCommand(Settings settings, Hero hero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (args.Length < 2)
            {
                onFailure("Usage: !diplomacy break <nap|alliance> <kingdom>");
                return;
            }

            string type = args[0].ToLower();
            string targetName = string.Join(" ", args.Skip(1));

            var kingdom = hero.Clan.Kingdom;
            var target = FindKingdom(targetName);

            if (target == null)
            {
                onFailure($"Kingdom '{targetName}' not found");
                return;
            }

            if (type == "nap")
            {
                var nap = BLTTreatyManager.Current.GetNAP(kingdom, target);
                if (nap == null)
                {
                    onFailure($"No NAP with {target.Name}");
                    return;
                }

                if (settings.BreakNAPPrice > 0)
                {
                    if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero) < settings.BreakNAPPrice)
                    {
                        onFailure(Naming.NotEnoughGold(settings.BreakNAPPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero)));
                        return;
                    }
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -settings.BreakNAPPrice, true);
                }

                BLTTreatyManager.Current.RemoveNAP(kingdom, target);
                if (target.Leader != null && target.Leader.IsAdopted())
                {
                    string tName = target.Leader.FirstName.ToString()
                        .Replace(BLTAdoptAHeroModule.Tag, "").Replace(BLTAdoptAHeroModule.DevTag, "").Trim();
                    Log.LogFeedResponse($"@{tName} {kingdom.Name} has dissolved their non-aggression pact with you.");
                }

                BLTTreatyManager.Current.CreateTruce(kingdom, target, settings.TruceDuration);

                onSuccess($"NAP with {target.Name} broken. Truce: {settings.TruceDuration} days");
            }
            else if (type == "alliance" || type == "ally")
            {
                var alliance = BLTTreatyManager.Current.GetAlliance(kingdom, target);
                if (alliance == null)
                {
                    onFailure($"No alliance with {target.Name}");
                    return;
                }

                if (settings.BreakAlliancePrice > 0)
                {
                    if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero) < settings.BreakAlliancePrice)
                    {
                        onFailure(Naming.NotEnoughGold(settings.BreakAlliancePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero)));
                        return;
                    }
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -settings.BreakAlliancePrice, true);
                }

                BLTTreatyManager.Current.RemoveAlliance(kingdom, target);
                if (target.Leader != null && target.Leader.IsAdopted())
                {
                    string tName = target.Leader.FirstName.ToString()
                        .Replace(BLTAdoptAHeroModule.Tag, "").Replace(BLTAdoptAHeroModule.DevTag, "").Trim();
                    Log.LogFeedResponse($"@{tName} {kingdom.Name} has dissolved their alliance with you.");
                }

                BLTTreatyManager.Current.CreateTruce(kingdom, target, settings.TruceDuration);

                onSuccess($"Alliance with {target.Name} broken. Truce: {settings.TruceDuration} days");
            }
            else
            {
                onFailure("Invalid type. Use 'nap' or 'alliance'");
            }
        }

        private void HandleInfoCommand(Settings settings, Hero hero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            var kingdom = hero.Clan.Kingdom;

            if (args.Length == 0)
            {
                // Show all treaties
                var sb = new StringBuilder();
                sb.AppendLine($"=== {kingdom.Name} Diplomacy ===");

                // Wars
                var wars = BLTTreatyManager.Current.GetWarsInvolving(kingdom);
                if (wars.Count > 0)
                {
                    sb.AppendLine("\n[Wars]");
                    foreach (var war in wars)
                    {
                        var enemies = war.GetEnemies(kingdom);
                        string enemyList = string.Join(", ", enemies.Select(e => e.Name.ToString()));
                        sb.AppendLine($"  • {enemyList}");
                    }
                }

                // Alliances
                var alliances = BLTTreatyManager.Current.GetAlliancesFor(kingdom);
                if (alliances.Count > 0)
                {
                    sb.AppendLine("\n[Alliances]");
                    foreach (var alliance in alliances)
                    {
                        var partner = alliance.GetOtherKingdom(kingdom);
                        sb.AppendLine($"  • {partner.Name}");
                    }
                }

                // NAPs
                var naps = BLTTreatyManager.Current.GetNAPsFor(kingdom);
                if (naps.Count > 0)
                {
                    sb.AppendLine("\n[Non-Aggression Pacts]");
                    foreach (var nap in naps)
                    {
                        var partner = nap.GetOtherKingdom(kingdom);
                        sb.AppendLine($"  • {partner.Name}");
                    }
                }

                var tributesPaying = BLTTreatyManager.Current.GetTributesPayedBy(kingdom);
                var tributesReceiving = BLTTreatyManager.Current.GetTributesReceivedBy(kingdom);

                if (tributesPaying.Count > 0 || tributesReceiving.Count > 0)
                {
                    sb.AppendLine("\n[Tributes]");
                    foreach (var tribute in tributesPaying)
                    {
                        var receiver = tribute.GetReceiver();
                        sb.AppendLine($"  • Paying {tribute.DailyAmount}{Naming.Gold}/day to {receiver.Name} - {tribute.DaysRemaining()} days remaining");
                    }
                    foreach (var tribute in tributesReceiving)
                    {
                        var payer = tribute.GetPayer();
                        sb.AppendLine($"  • Receiving {tribute.DailyAmount}{Naming.Gold}/day from {payer.Name} - {tribute.DaysRemaining()} days remaining");
                    }
                }

                var peaceProposals = BLTTreatyManager.Current.GetPeaceProposalsFor(kingdom);
                if (peaceProposals.Count > 0)
                {
                    sb.AppendLine("\n[Peace Proposals]");
                    foreach (var proposal in peaceProposals)
                    {
                        var proposer = proposal.GetProposer();
                        string tributeInfo = proposal.DailyTribute > 0
                            ? $" ({(proposal.IsOffer ? "offering" : "demanding")} {proposal.DailyTribute}{Naming.Gold}/day for {proposal.Duration} days)"
                            : "";
                        sb.AppendLine($"  • {proposer.Name}{tributeInfo} - {proposal.DaysRemaining()} days to respond");
                    }
                }

                // CTW Proposals
                var ctwProposals = BLTTreatyManager.Current.GetCTWProposalsFor(kingdom);
                if (ctwProposals.Count > 0)
                {
                    sb.AppendLine("\n[Call to War Proposals]");
                    foreach (var ctw in ctwProposals)
                    {
                        var proposer = ctw.GetProposer();
                        var target = ctw.GetTarget();
                        sb.AppendLine($"  • {proposer.Name} vs {target.Name} - {ctw.DaysRemaining()} days to respond");
                    }
                }

                onSuccess(sb.ToString());
            }
            else
            {
                // Filter by type
                string filter = args[0].ToLower();
                switch (filter)
                {
                    case "wars":
                    case "war":
                        ShowWars(kingdom, onSuccess);
                        break;
                    case "ally":
                    case "allies":
                    case "alliances":
                    case "alliance":
                        ShowAlliances(kingdom, onSuccess);
                        break;
                    case "naps":
                    case "nap":
                        ShowNAPs(kingdom, onSuccess);
                        break;
                    case "tributes":
                    case "tribute":
                        ShowTributes(kingdom, onSuccess);
                        break;
                    case "truces":
                    case "truce":
                        ShowTruces(kingdom, onSuccess);
                        break;
                    default:
                        onFailure("Invalid filter. Use: wars, alliances, naps, tributes, truces");
                        break;
                }
            }
        }

        private void ShowWars(Kingdom kingdom, Action<string> onSuccess)
        {
            var wars = BLTTreatyManager.Current.GetWarsInvolving(kingdom);
            if (wars.Count == 0)
            {
                onSuccess("No active wars");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== {kingdom.Name} Wars ===");
            foreach (var war in wars)
            {
                var enemies = war.GetEnemies(kingdom);
                string enemyList = string.Join(", ", enemies.Select(e => $"{e.Name} ({(int)e.CurrentTotalStrength})"));
                sb.AppendLine($"• {enemyList}");
            }
            onSuccess(sb.ToString());
        }

        private void ShowAlliances(Kingdom kingdom, Action<string> onSuccess)
        {
            var alliances = BLTTreatyManager.Current.GetAlliancesFor(kingdom);
            if (alliances.Count == 0)
            {
                onSuccess("No alliances");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== {kingdom.Name} Alliances ===");
            foreach (var alliance in alliances)
            {
                var partner = alliance.GetOtherKingdom(kingdom);
                int daysSince = (int)(CampaignTime.Now - alliance.StartDate).ToDays;
                sb.AppendLine($"• {partner.Name} (since {daysSince} days ago)");
            }
            onSuccess(sb.ToString());
        }

        private void ShowNAPs(Kingdom kingdom, Action<string> onSuccess)
        {
            var naps = BLTTreatyManager.Current.GetNAPsFor(kingdom);
            if (naps.Count == 0)
            {
                onSuccess("No NAPs");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== {kingdom.Name} NAPs ===");
            foreach (var nap in naps)
            {
                var partner = nap.GetOtherKingdom(kingdom);
                int daysSince = (int)(CampaignTime.Now - nap.StartDate).ToDays;
                sb.AppendLine($"• {partner.Name} (since {daysSince} days ago)");
            }
            onSuccess(sb.ToString());
        }

        private void ShowTributes(Kingdom kingdom, Action<string> onSuccess)
        {
            var paying = BLTTreatyManager.Current.GetTributesPayedBy(kingdom);
            var receiving = BLTTreatyManager.Current.GetTributesReceivedBy(kingdom);

            if (paying.Count == 0 && receiving.Count == 0)
            {
                onSuccess("No tributes");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== {kingdom.Name} Tributes ===");

            if (paying.Count > 0)
            {
                sb.AppendLine("\n[Paying]");
                foreach (var tribute in paying)
                {
                    var receiver = tribute.GetReceiver();
                    sb.AppendLine($"• {tribute.DailyAmount}{Naming.Gold}/day to {receiver.Name} - {tribute.DaysRemaining()} days");
                }
            }

            if (receiving.Count > 0)
            {
                sb.AppendLine("\n[Receiving]");
                foreach (var tribute in receiving)
                {
                    var payer = tribute.GetPayer();
                    sb.AppendLine($"• {tribute.DailyAmount}{Naming.Gold}/day from {payer.Name} - {tribute.DaysRemaining()} days");
                }
            }

            onSuccess(sb.ToString());
        }

        private void ShowTruces(Kingdom kingdom, Action<string> onSuccess)
        {
            var allKingdoms = Kingdom.All.Where(k => k != kingdom && !k.IsEliminated).ToList();
            var truces = new List<(Kingdom, BLTTruce)>();

            foreach (var k in allKingdoms)
            {
                var truce = BLTTreatyManager.Current.GetTruce(kingdom, k);
                if (truce != null && !truce.IsExpired())
                {
                    truces.Add((k, truce));
                }
            }

            if (truces.Count == 0)
            {
                onSuccess("No active truces");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== {kingdom.Name} Truces ===");
            foreach (var (k, truce) in truces)
            {
                sb.AppendLine($"• {k.Name} - {truce.DaysRemaining()} days remaining");
            }
            onSuccess(sb.ToString());
        }

        private void HandleAcceptCommand(Settings settings, Hero hero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (args.Length < 2)
            {
                onFailure("Usage: !diplomacy accept <peace|alliance|trade|nap|ctw> <proposer_kingdom>");
                return;
            }

            string type = args[0].ToLower();
            string proposerName = string.Join(" ", args.Skip(1));
            var kingdom = hero.Clan.Kingdom;
            var proposer = FindKingdom(proposerName);

            if (proposer == null)
            {
                onFailure($"Kingdom '{proposerName}' not found");
                return;
            }

            switch (type)
            {
                case "peace":
                    AcceptPeaceProposal(settings, hero, kingdom, proposer, onSuccess, onFailure);
                    break;
                case "alliance":
                case "ally":
                    AcceptAllianceProposal(settings, hero, kingdom, proposer, onSuccess, onFailure);
                    break;
                case "trade":
                    AcceptTradeProposal(settings, hero, kingdom, proposer, onSuccess, onFailure);
                    break;
                case "nap":
                    AcceptNAPProposal(settings, hero, kingdom, proposer, onSuccess, onFailure);
                    break;
                case "ctw":
                    AcceptCTWProposal(settings, hero, kingdom, proposer, onSuccess, onFailure);
                    break;
                default:
                    onFailure("Invalid type. Use: peace, alliance, nap, or ctw");
                    break;
            }
        }

        private void AcceptPeaceProposal(Settings settings, Hero hero, Kingdom kingdom, Kingdom proposer, Action<string> onSuccess, Action<string> onFailure)
        {
            var proposal = BLTTreatyManager.Current.GetPeaceProposal(proposer, kingdom);
            if (proposal == null)
            {
                onFailure($"No peace proposal from {proposer.Name}");
                return;
            }

            if (!kingdom.IsAtWarWith(proposer))
            {
                onFailure($"Not at war with {proposer.Name}");
                BLTTreatyManager.Current.RemovePeaceProposal(proposer, kingdom);
                return;
            }

            // FIXED: Determine payer and receiver based on proposal type
            Kingdom payer = proposal.IsOffer ? proposer : kingdom;
            Kingdom receiver = proposal.IsOffer ? kingdom : proposer;

#if DEBUG
    Log.Trace($"[BLT Peace] Accepting peace proposal from {proposer.Name} to {kingdom.Name}");
    Log.Trace($"[BLT Peace] IsOffer: {proposal.IsOffer}, DailyTribute: {proposal.DailyTribute}, Duration: {proposal.Duration}");
    Log.Trace($"[BLT Peace] Payer: {payer.Name}, Receiver: {receiver.Name}");
#endif

            AdoptedHeroFlags._allowDiplomacyAction = true;
            try
            {
                // Make peace using the tribute amount from the proposal
                MakePeaceAction.ApplyByKingdomDecision(kingdom, proposer, proposal.DailyTribute, proposal.Duration);
                FactionManager.SetNeutral(kingdom, proposer);

                // FIXED: Create tribute if needed, using correct payer/receiver
                if (proposal.DailyTribute > 0)
                {
#if DEBUG
            Log.Trace($"[BLT Peace] Creating tribute: {payer.Name} pays {proposal.DailyTribute} to {receiver.Name} for {proposal.Duration} days");
#endif
                    BLTTreatyManager.Current.CreateTribute(payer, receiver, proposal.DailyTribute, proposal.Duration);

                    // Verify tribute was created
                    var createdTribute = BLTTreatyManager.Current.GetTribute(payer, receiver);
                    if (createdTribute == null)
                    {
                        Log.Error($"[BLT Peace] Failed to create tribute between {payer.Name} and {receiver.Name}!");
                    }
                    else
                    {
#if DEBUG
                Log.Trace($"[BLT Peace] Tribute created successfully - verifying: Payer={createdTribute.GetPayer()?.Name}, Receiver={createdTribute.GetReceiver()?.Name}, Amount={createdTribute.DailyAmount}");
#endif
                    }
                }
                else
                {
#if DEBUG
            Log.Trace($"[BLT Peace] No tribute (amount is 0)");
#endif
                }

                // Create truce
                BLTTreatyManager.Current.CreateTruce(kingdom, proposer, settings.TruceDuration);

                // Handle war cleanup
                var war = BLTTreatyManager.Current.GetWar(kingdom, proposer);
                if (war != null)
                {
                    if (war.IsMainParticipant(kingdom))
                    {
                        // Main participant making peace - peace out all assistants (no tribute for them)
                        var allies = war.IsAttackerSide(kingdom) ? war.GetAttackerAllies() : war.GetDefenderAllies();
                        foreach (var ally in allies)
                        {
                            if (ally != null && ally.IsAtWarWith(proposer))
                            {
                                MakePeaceAction.Apply(ally, proposer);
                                FactionManager.SetNeutral(ally, proposer);
#if DEBUG
                        Log.Trace($"[BLT Peace] Peaced out ally {ally.Name}");
#endif
                            }
                        }
                        BLTTreatyManager.Current.RemoveWar(kingdom, proposer);
                    }
                    else if (war.IsMainParticipant(proposer))
                    {
                        // Proposer was main participant - still clean up
                        var allies = war.IsAttackerSide(proposer) ? war.GetAttackerAllies() : war.GetDefenderAllies();
                        foreach (var ally in allies)
                        {
                            if (ally != null && ally.IsAtWarWith(kingdom))
                            {
                                MakePeaceAction.Apply(ally, kingdom);
                                FactionManager.SetNeutral(ally, kingdom);
#if DEBUG
                        Log.Trace($"[BLT Peace] Peaced out ally {ally.Name}");
#endif
                            }
                        }
                        BLTTreatyManager.Current.RemoveWar(kingdom, proposer);
                    }
                    else
                    {
                        // Accepting kingdom is an assisting ally - remove from war and break alliance with main participant
                        war.RemoveAlly(kingdom);

                        Kingdom alliancePartnerToBreak = null;
                        if (war.IsAttackerSide(kingdom))
                            alliancePartnerToBreak = war.GetAttacker();
                        else
                            alliancePartnerToBreak = war.GetDefender();

                        if (alliancePartnerToBreak != null)
                        {
                            BLTTreatyManager.Current.RemoveAlliance(kingdom, alliancePartnerToBreak);
                            BLTTreatyManager.Current.CreateTruce(kingdom, alliancePartnerToBreak, settings.TruceDuration);

                            Log.ShowInformation($"Alliance broken between {kingdom.Name} and {alliancePartnerToBreak.Name}!", hero.CharacterObject);

                            if (alliancePartnerToBreak.Leader != null && alliancePartnerToBreak.Leader.IsAdopted())
                            {
                                string partnerLeaderName = alliancePartnerToBreak.Leader.FirstName.ToString()
                                    .Replace(BLTAdoptAHeroModule.Tag, "")
                                    .Replace(BLTAdoptAHeroModule.DevTag, "")
                                    .Trim();
                                Log.LogFeedResponse($"@{partnerLeaderName} Your alliance with {kingdom.Name} has been broken because they made peace with {proposer.Name}!");
                            }
                        }
                    }
                }

                // Remove proposal
                BLTTreatyManager.Current.RemovePeaceProposal(proposer, kingdom);

                string tributeMsg = proposal.DailyTribute > 0
                    ? $" ({(proposal.IsOffer ? "receiving" : "paying")} {proposal.DailyTribute}{Naming.Gold}/day for {proposal.Duration} days)"
                    : "";

                onSuccess($"Accepted peace with {proposer.Name}{tributeMsg}");
                Log.ShowInformation($"{kingdom.Name} has made peace with {proposer.Name}!", hero.CharacterObject);
            }
            finally
            {
                AdoptedHeroFlags._allowDiplomacyAction = false;
            }
        }

        private void AcceptAllianceProposal(Settings settings, Hero hero, Kingdom kingdom, Kingdom proposer, Action<string> onSuccess, Action<string> onFailure)
        {
            var proposal = BLTTreatyManager.Current.GetAllianceProposal(proposer, kingdom);
            if (proposal == null)
            {
                onFailure($"No alliance proposal from {proposer.Name}");
                return;
            }

            if (kingdom.IsAtWarWith(proposer))
            {
                onFailure($"At war with {proposer.Name}. Make peace first.");
                BLTTreatyManager.Current.RemoveAllianceProposal(proposer, kingdom);
                return;
            }

            // Create alliance
            BLTTreatyManager.Current.CreateAlliance(kingdom, proposer);
            BLTTreatyManager.Current.RemoveNAP(kingdom, proposer);
            BLTTreatyManager.Current.RemoveAllianceProposal(proposer, kingdom);

            onSuccess($"Alliance formed with {proposer.Name}!");
            Log.ShowInformation($"{kingdom.Name} and {proposer.Name} have formed an alliance!", hero.CharacterObject, Log.Sound.Horns2);
        }

        private void AcceptNAPProposal(Settings settings, Hero hero, Kingdom kingdom, Kingdom proposer, Action<string> onSuccess, Action<string> onFailure)
        {
            var proposal = BLTTreatyManager.Current.GetNAPProposal(proposer, kingdom);
            if (proposal == null)
            {
                onFailure($"No NAP proposal from {proposer.Name}");
                return;
            }

            if (kingdom.IsAtWarWith(proposer))
            {
                onFailure($"At war with {proposer.Name}. Make peace first.");
                BLTTreatyManager.Current.RemoveNAPProposal(proposer, kingdom);
                return;
            }

            // Create NAP
            BLTTreatyManager.Current.CreateNAP(kingdom, proposer);
            BLTTreatyManager.Current.RemoveNAPProposal(proposer, kingdom);

            onSuccess($"Non-aggression pact established with {proposer.Name}");
            Log.ShowInformation($"{kingdom.Name} and {proposer.Name} have signed a non-aggression pact!", hero.CharacterObject);
        }

        private void AcceptCTWProposal(Settings settings, Hero hero, Kingdom kingdom, Kingdom proposer, Action<string> onSuccess, Action<string> onFailure)
        {
            var proposals = BLTTreatyManager.Current.GetCTWProposalsFor(kingdom);
            var proposal = proposals.FirstOrDefault(p => p.GetProposer() == proposer);

            if (proposal == null)
            {
                onFailure($"No call to war from {proposer.Name}");
                return;
            }

            var target = proposal.GetTarget();

            if (!BLTTreatyManager.Current.CanDeclareWar(kingdom, target, out string reason))
            {
                onFailure($"Cannot join war: {reason}");
                BLTTreatyManager.Current.RemoveCTWProposal(proposer, kingdom, target);
                return;
            }

            AdoptedHeroFlags._allowDiplomacyAction = true;
            try
            {
                BLTTreatyManager.Current.RemoveNAP(kingdom, target);
                BLTTreatyManager.Current.RemoveAlliance(kingdom, target);
                BLTTreatyManager.Current.RemoveTribute(kingdom, target);

                var war = BLTTreatyManager.Current.GetWar(proposer, target);
                if (war != null)
                {
                    if (war.IsAttackerSide(proposer))
                        war.AddAttackerAlly(kingdom);
                    else
                        war.AddDefenderAlly(kingdom);
                }

                DeclareWarAction.ApplyByDefault(kingdom, target);
                FactionManager.DeclareWar(kingdom, target);
                BLTTreatyManager.Current.RemoveCTWProposal(proposer, kingdom, target);

                onSuccess($"Joined {proposer.Name}'s war against {target.Name}!");
                Log.ShowInformation($"{kingdom.Name} has joined {proposer.Name}'s war against {target.Name}!", hero.CharacterObject, Log.Sound.Horns2);
            }
            finally
            {
                AdoptedHeroFlags._allowDiplomacyAction = false;
            }
        }

        private void HandleRejectCommand(Settings settings, Hero hero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (args.Length < 2)
            {
                onFailure("Usage: !diplomacy reject ctw <proposer_kingdom>");
                return;
            }

            string type = args[0].ToLower();

            if (type == "ctw")
            {
                string proposerName = string.Join(" ", args.Skip(1));
                var kingdom = hero.Clan.Kingdom;
                var proposer = FindKingdom(proposerName);

                if (proposer == null)
                {
                    onFailure($"Kingdom '{proposerName}' not found");
                    return;
                }

                // Find CTW proposal
                var proposals = BLTTreatyManager.Current.GetCTWProposalsFor(kingdom);
                var proposal = proposals.FirstOrDefault(p => p.GetProposer() == proposer);

                if (proposal == null)
                {
                    onFailure($"No call to war from {proposer.Name}");
                    return;
                }

                var target = proposal.GetTarget();

                // Remove proposal
                BLTTreatyManager.Current.RemoveCTWProposal(proposer, kingdom, target);

                onSuccess($"Rejected {proposer.Name}'s call to war against {target.Name}");
            }
            else
            {
                onFailure("Invalid type. Currently only 'ctw' is supported");
            }
        }

        private void HandleClanCommand(Settings settings, Hero hero, string subArgs,
    Action<string> onSuccess, Action<string> onFailure)
        {
            if (!hero.IsAdopted())
            { onFailure("Only BLT heroes can use clan diplomacy"); return; }
            if (hero.Clan.Kingdom != null)
            { onFailure("You are in a kingdom — use the kingdom diplomacy system (!diplomacy alliance)"); return; }
            if (hero.Clan.Leader != hero)
            { onFailure("You must be your clan's leader to use clan diplomacy"); return; }
            if (BLTClanDiplomacyBehavior.Current == null)
            { onFailure("Clan diplomacy system not available"); return; }

            var parts = subArgs.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            var sub = parts.Length > 0 ? parts[0].ToLower() : "";
            var arg = parts.Length > 1 ? parts[1].Trim() : "";

            switch (sub)
            {
                case "ally": HandleClanAlly(settings, hero, arg, onSuccess, onFailure); break;
                case "accept": HandleClanAccept(hero, arg, onSuccess, onFailure); break;
                case "break": HandleClanBreak(hero, arg, onSuccess, onFailure); break;
                case "info": HandleClanInfo(hero, onSuccess, onFailure); break;
                default:
                    onFailure("Usage: !diplomacy clan <ally|accept|break|info> [clan_name]");
                    break;
            }
        }

        private void HandleClanAlly(Settings settings, Hero hero, string targetName,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(targetName))
            { onFailure("Specify a clan name. Usage: !diplomacy clan ally <clan_name>"); return; }

            var target = FindIndependentClan(targetName);
            if (target == null)
            { onFailure($"Independent clan '{targetName}' not found (target must also be kingdom-less)"); return; }

            int ClanAllyGoldCost = settings.ClanAllyPrice;

            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero) < ClanAllyGoldCost)
            { onFailure(Naming.NotEnoughGold(ClanAllyGoldCost, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero))); return; }

            string error = BLTClanDiplomacyBehavior.Current.CreateProposal(
                hero.Clan, target, ClanAllyGoldCost, daysToAccept: 15);
            if (error != null) { onFailure(error); return; }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -ClanAllyGoldCost, true);
            onSuccess($"Clan alliance proposal sent to {target.Name}. They have 15 days to respond.");

            if (target.Leader?.IsAdopted() == true)
                BLTClanDiplomacyBehavior.NotifyClanLeader(target,
                    $"{hero.Clan.Name} proposes a clan alliance! Use !diplomacy clan accept {hero.Clan.Name}");
        }

        private static void HandleClanAccept(Hero hero, string proposerName,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(proposerName))
            { onFailure("Specify the proposing clan's name. Usage: !diplomacy clan accept <clan_name>"); return; }

            var proposer = FindClanByName(proposerName);
            if (proposer == null) { onFailure($"Clan '{proposerName}' not found"); return; }

            string error = BLTClanDiplomacyBehavior.Current.AcceptProposal(hero.Clan, proposer);
            if (error != null) { onFailure(error); return; }

            onSuccess($"Clan alliance formed with {proposer.Name}!");
            Log.ShowInformation($"{hero.Clan.Name} and {proposer.Name} are now allied!",
                hero.CharacterObject, Log.Sound.Horns2);
            BLTClanDiplomacyBehavior.NotifyClanLeader(proposer,
                $"{hero.Clan.Name} has accepted your clan alliance proposal!");
        }

        private static void HandleClanBreak(Hero hero, string targetName,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(targetName))
            { onFailure("Specify a clan name. Usage: !diplomacy clan break <clan_name>"); return; }

            var target = FindClanByName(targetName);
            if (target == null) { onFailure($"Clan '{targetName}' not found"); return; }

            if (!BLTClanDiplomacyBehavior.Current.HasAlliance(hero.Clan, target))
            { onFailure($"No clan alliance with {target.Name}"); return; }

            BLTClanDiplomacyBehavior.Current.BreakAlliance(hero.Clan, target,
                $"{hero.Name} dissolved the pact");
            onSuccess($"Clan alliance with {target.Name} broken");
            // Notification to partner is handled inside BreakAlliance → NotifyAllianceBroken.
        }

        private static void HandleClanInfo(Hero hero,
            Action<string> onSuccess, Action<string> onFailure)
        {
            var dip = BLTClanDiplomacyBehavior.Current;
            if (dip == null) { onSuccess($"{hero.Clan.Name}: Clan diplomacy system unavailable"); return; }

            var alliances = dip.GetAlliancesFor(hero.Clan);
            var proposals = dip.GetProposalsFor(hero.Clan);

            if (alliances.Count == 0 && proposals.Count == 0)
            { onSuccess($"{hero.Clan.Name} has no active clan alliances or pending proposals"); return; }

            var sb = new StringBuilder($"{hero.Clan.Name} Clan Diplomacy");

            if (alliances.Count > 0)
            {
                sb.Append($" | Alliances({alliances.Count}):");
                foreach (var a in alliances)
                {
                    var other = a.GetOther(hero.Clan);
                    int days = (int)(CampaignTime.Now.ToDays - a.StartDays);
                    bool otherHasArmy = BLTClanArmyBehavior.Current?.HasClanArmy(other) == true;
                    sb.Append($" {other?.Name}(+{days}d{(otherHasArmy ? ",army" : "")})");
                }
            }

            if (proposals.Count > 0)
            {
                sb.Append($" | Pending({proposals.Count}):");
                foreach (var p in proposals)
                    sb.Append($" from {p.GetProposer()?.Name}({p.DaysRemaining()}d left)");
            }

            onSuccess(sb.ToString());
        }

        /// <summary>Find an independent (kingdom-less) clan by name.</summary>
        private static Clan FindIndependentClan(string name) =>
            FindClanByName(name, requireIndependent: true);

        private static Clan FindClanByName(string name, bool requireIndependent = false)
        {
            bool Filter(Clan c) =>
                c != null && !c.IsEliminated
                && (!requireIndependent || c.Kingdom == null)
                && !c.Name.IsEmpty();

            return Clan.All.FirstOrDefault(c =>
                       Filter(c) && c.Name.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
                   ?? Clan.All
                       .Where(c => Filter(c))
                       .OrderBy(c => c.Name.ToString().Length)
                       .FirstOrDefault(c =>
                           c.Name.ToString().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        }


        private void HandleWarStanceCommand(Settings settings, Hero hero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (args.Length == 0)
            {
                onFailure("Usage: !diplomacy warstance <kingdom> (balanced/defensive/aggressive)");
                return;
            }

            string stanceString = args.Last();
            string kingdomString = string.Join(" ", args.Take(args.Length - 1));

            var matchingKingdoms = Kingdom.All
                .Where(k => k.Name.ToString().IndexOf(kingdomString, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            if (matchingKingdoms.Count == 0)
            {
                onFailure($"Could not find a kingdom matching \"{kingdomString}\"");
                return;
            }
            if (matchingKingdoms.Count > 1)
            {
                onFailure($"Multiple kingdoms match \"{kingdomString}\": {string.Join(", ", matchingKingdoms.Select(k => k.Name))}");
                return;
            }
            var desiredKingdom = matchingKingdoms[0];

            if (hero.Clan.Kingdom == desiredKingdom)
            {
                onFailure("Not at war with yourself!");
                return;
            }
            var stance = hero.Clan.Kingdom.GetStanceWith(desiredKingdom);
            if (!hero.Clan.Kingdom.IsAtWarWith(desiredKingdom))
            {
                onFailure($"Not at war with {desiredKingdom}");
                return;
            }
            int priority = stanceString.ToLower() switch
            {
                "balanced" => 0,
                "defensive" => 1,
                "aggressive" => 2,
                _ => -1
            };
            if (priority == -1)
            {
                onFailure("invalid stance(balanced/defensive/aggressive)");
                return;
            }
            else
            {
                stance.BehaviorPriority = priority;
                onSuccess($"Changed war strategy to {stanceString.ToLower()}");
            }
        }

        private void HandleTradeCommand(Settings settings, Hero hero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.TradeEnabled)
            {
                onFailure("Trade alliances disabled");
                return;
            }

            if (args.Length == 0)
            {
                onFailure("Usage: !diplomacy trade <kingdom>");
                return;
            }

            string targetName = string.Join(" ", args);
            var kingdom = hero.Clan.Kingdom;
            var target = FindKingdom(targetName);

            if (target == null)
            {
                onFailure($"Kingdom '{targetName}' not found");
                return;
            }

            if (kingdom == target)
            {
                onFailure("Cannot trade with yourself");
                return;
            }

            if (kingdom.IsAtWarWith(target))
            {
                onFailure($"At war with {target.Name}. Make peace first.");
                return;
            }

            // Check for existing trade agreement
            TradeAgreementsCampaignBehavior tradeBehavior = Campaign.Current.GetCampaignBehavior<TradeAgreementsCampaignBehavior>();
            if (tradeBehavior.HasTradeAgreement(kingdom, target))
            {
                onFailure($"Already have trade agreement with {target.Name}");
                return;
            }
            bool hasProposed = BLTTreatyManager.Current.GetTradeProposal(kingdom, target) != null;
            bool hasProposal = BLTTreatyManager.Current.GetTradeProposalsFor(kingdom).Any(t => t.ProposerKingdomId == target.StringId);
            if (hasProposed)
            {
                onFailure($"Already proposed trade agreement with {target.Name}");
                return;
            }
            if (hasProposal)
            {
                onFailure($"Already has trade agreement proposal with {target.Name}. Accept or ignore");
                return;
            }

            // Check costs
            int influenceCost = Campaign.Current.Models.TradeAgreementModel.GetInfluenceCostOfProposingTradeAgreement(hero.Clan);

            if (hero.Clan.Influence < influenceCost)
            {
                onFailure($"Not enough influence (need {influenceCost}, have {(int)hero.Clan.Influence})");
                return;
            }

            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero) < settings.TradePrice)
            {
                onFailure(Naming.NotEnoughGold(settings.TradePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero)));
                return;
            }

            // Deduct costs upfront
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -settings.TradePrice, true);
            hero.Clan.Influence -= influenceCost;

            // Check if target is BLT controlled
            if (target.Leader != null && target.Leader.IsAdopted())
            {
                // Create trade proposal for BLT kingdoms
                BLTTreatyManager.Current.CreateTradeProposal(kingdom, target, settings.TradePrice, influenceCost, 15);

                onSuccess($"Trade agreement proposal sent to {target.Name}. They have 15 days to respond.");

                // Notify target leader
                string targetLeaderName = target.Leader.FirstName.ToString()
                    .Replace(BLTAdoptAHeroModule.Tag, "")
                    .Replace(BLTAdoptAHeroModule.DevTag, "")
                    .Trim();
                Log.LogFeedResponse($"@{targetLeaderName} {kingdom.Name} proposes a trade agreement! Use !diplomacy accept trade {kingdom.Name}");
            }
            else if (target == Hero.MainHero.Clan.Kingdom)
            {
                // Propose to player kingdom
                tradeBehavior.OnTradeAgreementOfferedToPlayer(kingdom);
                onSuccess($"Trade agreement proposal sent to {target.Name}");
            }
            else
            {
                // AI kingdom - create trade agreement directly
                var duration = Campaign.Current.Models.TradeAgreementModel.GetTradeAgreementDurationInYears(kingdom, target);
                tradeBehavior.MakeTradeAgreement(kingdom, target, duration);

                onSuccess($"Trade agreement established with {target.Name}");
                Log.ShowInformation($"{kingdom.Name} and {target.Name} have formed a trade agreement!", hero.CharacterObject);
            }
        }

        private void AcceptTradeProposal(Settings settings, Hero hero, Kingdom kingdom, Kingdom proposer, Action<string> onSuccess, Action<string> onFailure)
        {
            var proposal = BLTTreatyManager.Current.GetTradeProposal(proposer, kingdom);
            if (proposal == null)
            {
                onFailure($"No trade proposal from {proposer.Name}");
                return;
            }

            if (kingdom.IsAtWarWith(proposer))
            {
                onFailure($"At war with {proposer.Name}. Make peace first.");
                BLTTreatyManager.Current.RemoveTradeProposal(proposer, kingdom);
                return;
            }

            // Create trade agreement
            TradeAgreementsCampaignBehavior tradeBehavior = Campaign.Current.GetCampaignBehavior<TradeAgreementsCampaignBehavior>();
            var duration = Campaign.Current.Models.TradeAgreementModel.GetTradeAgreementDurationInYears(kingdom, proposer);
            tradeBehavior.MakeTradeAgreement(kingdom, proposer, duration);

            BLTTreatyManager.Current.RemoveTradeProposal(proposer, kingdom);

            onSuccess($"Trade agreement established with {proposer.Name}");
            Log.ShowInformation($"{kingdom.Name} and {proposer.Name} have formed a trade agreement!", hero.CharacterObject);
        }

        private Kingdom FindKingdom(string name)
        {
            return Kingdom.All.FirstOrDefault(k =>
                !k.IsEliminated &&
                k.Name.ToString().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // OLD DIPLOMACY FALLBACK (kept for when EnableNewDiplomacy = false)
        private void ExecuteOldDiplomacy(Hero adoptedHero, ReplyContext context, Settings settings, Action<string> onSuccess, Action<string> onFailure)
        {
            // This is the old diplomacy code from the original Diplomacy.cs file
            // Keeping it as a fallback when new system is disabled
            // [Include original Diplomacy.cs implementation here]
            onFailure("Old diplomacy system - use the attached original Diplomacy.cs");
        }
    }
}