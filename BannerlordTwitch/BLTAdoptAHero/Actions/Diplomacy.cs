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
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=TESTING}Diplomacy"),
     LocDescription("{=TESTING}Manage your kingdom diplomacy and other actions."),
     UsedImplicitly]
    class Diplomacy : HeroCommandHandlerBase
    {
        [CategoryOrder("War", 0),
         CategoryOrder("Peace", 1),
         CategoryOrder("Alliance", 2),
         CategoryOrder("Trade", 3),
         CategoryOrder("Army", 4),
         CategoryOrder("Policy", 5)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=TESTING}War"),
             LocCategory("War", "{=TESTING}War"),
             LocDescription("{=TESTING}Enable declaring war command"),
             PropertyOrder(1), UsedImplicitly]
            public bool WarEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Price"),
             LocCategory("War", "{=TESTING}War"),
             LocDescription("{=TESTING}War command price"),
             PropertyOrder(2), UsedImplicitly]
            public int WarPrice { get; set; } = 250000;

            [LocDisplayName("{=TESTING}Cooldown"),
             LocCategory("War", "{=TESTING}War"),
             LocDescription("{=TESTING}War cooldown"),
             PropertyOrder(3), UsedImplicitly]
            public int WarCooldown { get; set; } = 21;

            [LocDisplayName("{=TESTING}Peace"),
             LocCategory("Peace", "{=TESTING}Peace"),
             LocDescription("{=TESTING}Enable declaring war command"),
             PropertyOrder(1), UsedImplicitly]
            public bool PeaceEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Price"),
             LocCategory("Peace", "{=TESTING}Peace"),
             LocDescription("{=TESTING}Peace command price"),
             PropertyOrder(2), UsedImplicitly]
            public int PeacePrice { get; set; } = 100000;

            [LocDisplayName("{=TESTING}Cooldown"),
             LocCategory("War", "{=TESTING}Peace"),
             LocDescription("{=TESTING}Peace cooldown"),
             PropertyOrder(3), UsedImplicitly]
            public int PeaceCooldown { get; set; } = 21;

            [LocDisplayName("{=TESTING}Cooldown"),
             LocCategory("War", "{=TESTING}Peace"),
             LocDescription("{=TESTING}Peace cooldown"),
             PropertyOrder(3), UsedImplicitly]
            public bool AllowPeaceOutPlayer { get; set; } = false;

            [LocDisplayName("{=TESTING}Ally"),
             LocCategory("Alliance", "{=TESTING}Alliance"),
             LocDescription("{=TESTING}Enable alliance command"),
             PropertyOrder(1), UsedImplicitly]
            public bool AllyEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Price"),
             LocCategory("Alliance", "{=TESTING}Alliance"),
             LocDescription("{=TESTING}Ally command price"),
             PropertyOrder(2), UsedImplicitly]
            public int AllyPrice { get; set; } = 100000;

            [LocDisplayName("{=TESTING}Trade"),
             LocCategory("Trade", "{=TESTING}Trade"),
             LocDescription("{=TESTING}Enable trade alliance command"),
             PropertyOrder(1), UsedImplicitly]
            public bool TradeEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Price"),
             LocCategory("Trade", "{=TESTING}Trade"),
             LocDescription("{=TESTING}Trade command price"),
             PropertyOrder(2), UsedImplicitly]
            public int TradePrice { get; set; } = 50000;

            [LocDisplayName("{=TESTING}Army"),
             LocCategory("Army", "{=TESTING}Army"),
             LocDescription("{=TESTING}Enable creating army command"),
             PropertyOrder(1), UsedImplicitly]
            public bool ArmyEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Price"),
             LocCategory("Army", "{=TESTING}Army"),
             LocDescription("{=TESTING}Army command price"),
             PropertyOrder(2), UsedImplicitly]
            public int ArmyPrice { get; set; } = 150000;

            [LocDisplayName("{=TESTING}Policy"),
             LocCategory("Policy", "{=TESTING}Policy"),
             LocDescription("{=TESTING}Enable viewing,adding and removing policies"),
             PropertyOrder(1), UsedImplicitly]
            public bool PolicyEnabled { get; set; } = true;

            [LocDisplayName("{=TESTING}Price"),
             LocCategory("Policy", "{=TESTING}Policy"),
             LocDescription("{=TESTING}Policy command price"),
             PropertyOrder(2), UsedImplicitly]
            public int PolicyPrice { get; set; } = 50000;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                var sb = new StringBuilder();
                if (WarEnabled) sb.Append("{=TESTING}War, ".Translate());
                if (PeaceEnabled) sb.Append("{=TESTING}Peace, ".Translate());
                if (AllyEnabled) sb.Append("{=TESTING}Alliance, ".Translate());
                if (TradeEnabled) sb.Append("{=TESTING}Trade, ".Translate());
                if (ArmyEnabled) sb.Append("{=TESTING}Army, ".Translate());
                if (PolicyEnabled) sb.Append("{=TESTING}Policy".Translate());
                if (sb.Length > 0)
                    generator.Value("<strong>Enabled Commands:</strong> {commands}".Translate(
                        ("commands", sb.ToString(0, sb.Length - 2))));

                if (WarEnabled)
                    generator.Value("<strong>War Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", WarPrice.ToString()), ("icon", Naming.Gold)) +
                                    "Cooldown={cd}".Translate(("cd", WarCooldown.ToString())));
                if (PeaceEnabled)
                    generator.Value("<strong>Peace Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", PeacePrice.ToString()), ("icon", Naming.Gold)) +
                                    "Cooldown={cd}".Translate(("cd", PeaceCooldown.ToString())));
                if (AllyEnabled)
                    generator.Value("<strong>Alliance Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", AllyPrice.ToString()), ("icon", Naming.Gold)));
                if (TradeEnabled)
                    generator.Value("<strong>Trade Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", TradePrice.ToString()), ("icon", Naming.Gold)));
                if (ArmyEnabled)
                    generator.Value("<strong>Army Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", ArmyEnabled.ToString()), ("icon", Naming.Gold)));
                if (PolicyEnabled)
                    generator.Value("<strong>Policy Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", PolicyPrice.ToString()), ("icon", Naming.Gold)));
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context,
                    context.ArgsErrorMessage("{=TESTING}invalid mode (use war (kingdom), peace (kingdom), alliance (kingdom), trade (kingdom), army (defend/siege/patrol)".Translate()));
                return;
            }
            if (Mission.Current != null)
            {
                onFailure("Mission is active!");
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=B86KnTcu}You are not in a clan".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=EJ4Pd2Lg}Your clan is not in a Kingdom".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=HS14GdUa}You cannot manage your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (adoptedHero.Clan.IsUnderMercenaryService)
            {
                onFailure("Mercenary");
                return;
            }
            //if (adoptedHero.Clan == Clan.PlayerClan)
            //{
            //    onFailure("")
            //}

            var splitArgs = context.Args.Split(' ');
            var mode = splitArgs[0];
            var desiredName = string.Join(" ", splitArgs.Skip(1)).Trim();
            var kingdom = adoptedHero.Clan.Kingdom;
            bool atWar = Kingdom.All.Any(k => k.IsAtWarWith(kingdom));
            AllianceCampaignBehavior allianceBehavior = Campaign.Current.GetCampaignBehavior<AllianceCampaignBehavior>();
            TradeAgreementsCampaignBehavior tradeBehavior = Campaign.Current.GetCampaignBehavior<TradeAgreementsCampaignBehavior>();

            var desiredKingdom = Kingdom.All.FirstOrDefault(c => c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);

            switch (mode)
            {
                case "war":
                    {
                        if (!settings.WarEnabled)
                        {
                            onFailure("War disabled".Translate());
                            return;
                        }
                        if (desiredKingdom == null)
                        {
                            onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                            return;
                        }
                        if (!adoptedHero.IsKingdomLeader)
                        {
                            onFailure("{=TESTING}Not a king.".Translate());
                            return;
                        }
                        int influenceCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingWar(adoptedHero.Clan);
                        if (adoptedHero.Clan.Influence < influenceCost)
                        {
                            onFailure($"Not enough influence:{influenceCost}");
                            return;
                        }
                        if (kingdom.IsAtWarWith(desiredKingdom))
                        {
                            onFailure($"Already at war with {desiredKingdom}");
                            return;
                        }
                        if (kingdom == desiredKingdom)
                        {
                            onFailure("Cant declare war on yourself!");
                            return;
                        }
                        var stance = kingdom.GetStanceWith(desiredKingdom);
                        if ((stance.PeaceDeclarationDate != CampaignTime.Zero || stance.PeaceDeclarationDate != null) && (CampaignTime.Now - stance.PeaceDeclarationDate).ToDays < settings.WarCooldown)
                        {
                            onFailure($"Cant war yet. {(int)(20 - (CampaignTime.Now - stance.PeaceDeclarationDate).ToDays)} days remaining.");
                            return;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.WarPrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.WarPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        //BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.WarPrice, true);
                        //if (kingdom == Hero.MainHero.Clan.Kingdom)
                        //{
                        //    bool isAlreadyProposed = kingdom.UnresolvedDecisions
                        //    .Any(d => d is DeclareWarDecision warDecision &&
                        //              warDecision.FactionToDeclareWarOn == desiredKingdom);
                        //    if (isAlreadyProposed)
                        //    {
                        //        Log.LogFeedMessage($"Vote already ongoing.");
                        //        return;
                        //    }

                        //    DeclareWarDecision newWarProposal = new DeclareWarDecision(adoptedHero.Clan, desiredKingdom);
                        //    adoptedHero.Clan.Kingdom.AddDecision(newWarProposal);
                        //    onSuccess("Proposed war decision.");
                        //}
                        else
                        {
                            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.WarPrice, true);
                            if (allianceBehavior.IsAllyWithKingdom(kingdom, desiredKingdom))
                            {
                                allianceBehavior.EndAlliance(kingdom, desiredKingdom);
                            }
                            if (tradeBehavior.HasTradeAgreement(kingdom, desiredKingdom))
                            {
                                tradeBehavior.EndTradeAgreement(kingdom, desiredKingdom);
                            }
                            DeclareWarAction.ApplyByDefault(kingdom, desiredKingdom);
                            adoptedHero.Clan.Influence -= influenceCost;
                            onSuccess($"Declared war on {desiredKingdom}");

                        }
                        break;
                    }
                case "peace":
                    {
                        if (!settings.PeaceEnabled)
                        {
                            onFailure("Peace disabled".Translate());
                            return;
                        }
                        if (!settings.AllowPeaceOutPlayer && (desiredKingdom == Hero.MainHero.Clan.Kingdom && Hero.MainHero.IsKingdomLeader))
                        { 
                            onFailure("Peacing out the Streamer is disabled. Good Luck".Translate());
                            return;
                        }
                        if (!adoptedHero.IsKingdomLeader)
                        {
                            onFailure("{=TESTING}Not a king.".Translate());
                            return;
                        }
                        if (desiredKingdom == null)
                        {
                            onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                            return;
                        }
                        if (kingdom == desiredKingdom)
                        {
                            onFailure("Cant peace yourself!");
                            return;
                        }
                        int influenceCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingPeace(adoptedHero.Clan);
                        if (adoptedHero.Clan.Influence < influenceCost)
                        {
                            onFailure($"Not enough influence:{influenceCost}");
                            return;
                        }

                        var stance = kingdom.GetStanceWith(desiredKingdom);
                        if (!kingdom.IsAtWarWith(desiredKingdom))
                        {
                            onFailure($"Already at peace with {desiredKingdom}");
                            return;
                        }
                        if ((stance.WarStartDate != CampaignTime.Zero || stance.WarStartDate != null) && (CampaignTime.Now - stance.WarStartDate).ToDays < settings.PeaceCooldown)
                        {
                            onFailure($"Cant peace yet. {(int)(20 - (CampaignTime.Now - stance.WarStartDate).ToDays)} days remaining.");
                            return;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.PeacePrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.PeacePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.PeacePrice, true);

                        Clan proposer = adoptedHero.Clan;
                        var diplomacy = Campaign.Current.Models.DiplomacyModel;
                        //var barter = new PeaceBarterable(kingdom, desiredKingdom, CampaignTime.Years(1f));
                        //float valueForFaction = barter.GetValueForFaction(proposer);
                        //float wealthFactor = (proposer.Leader.Gold < 50000f)
                        //? (1f + 0.5f * ((50000f - proposer.Leader.Gold) / 50000f))
                        //: ((proposer.Leader.Gold > 200000f)
                        //? (float)Math.Max(0.66, Math.Pow(200000.0 / proposer.Leader.Gold, 0.4))
                        //: 1f);
                        //int generosityLevel = proposer.Leader.GetTraitLevel(DefaultTraits.Generosity);
                        //float generosityFactor = (true) // tribute payer
                        //? (1f - 0.1f * Math.Max(-2, Math.Min(2, generosityLevel)))
                        //: 1f;
                        Clan recipient = desiredKingdom.RulingClan;
                        // Get daily tribute and the duration in days automatically
                        int dailyTribute = Campaign.Current.Models.DiplomacyModel.GetDailyTributeToPay(proposer, recipient, out int tributeDurationInDays);

                        if (kingdom == Hero.MainHero.Clan.Kingdom)
                        {
                            bool isAlreadyProposed = kingdom.UnresolvedDecisions
                            .Any(d => d is MakePeaceKingdomDecision peaceDecision &&
                                      peaceDecision.FactionToMakePeaceWith == desiredKingdom);
                            if (isAlreadyProposed)
                            {
                                Log.LogFeedMessage($"Vote already ongoing.");
                                return;
                            }

                            MakePeaceKingdomDecision newPeaceProposal = new MakePeaceKingdomDecision(adoptedHero.Clan, desiredKingdom, dailyTribute);
                            adoptedHero.Clan.Kingdom.AddDecision(newPeaceProposal);
                            onSuccess($"Proposed peace decision");
                        }
                        //else if (desiredKingdom == Hero.MainHero.Clan.Kingdom && Hero.MainHero.IsKingdomLeader)
                        //{
                        //    PeaceOfferCampaignBehavior (GL Kanboru)
                        //}
                        else
                        {
                            // Apply peace with the calculated tribute
                            MakePeaceAction.ApplyByKingdomDecision(kingdom, desiredKingdom, dailyTribute, tributeDurationInDays);

                            adoptedHero.Clan.Influence -= influenceCost;
                            influenceCost *= -1;
                            onSuccess("{=BLTTribute}Peace applied between {Proposer} and {Recipient}. Daily tribute: {DailyTribute}, Duration: {Days} days."
                                .Translate(
                                    ("Proposer", proposer.Name),
                                    ("Recipient", recipient.Name),
                                    ("DailyTribute", dailyTribute),
                                    ("Days", tributeDurationInDays)
                                ));
                        }

                        break;
                    }
                case "policy":
                    {
                        var desiredPolicy = PolicyObject.All.FirstOrDefault(c => c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
                        int policyCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfPolicyProposalAndDisavowal(adoptedHero.Clan);
                        if (!settings.PolicyEnabled)
                        {
                            onFailure("Policy disabled".Translate());
                            return;
                        }
                        if (!adoptedHero.IsKingdomLeader)
                        {
                            onFailure("{=TESTING}Not a king.".Translate());
                            return;
                        }

                        if (desiredName == "list")
                        {
                            var listString = string.Join(", ", PolicyObject.All.Select(k => k.Name.ToString()));
                            onSuccess(listString);
                            return;
                        }
                        if (string.IsNullOrEmpty(desiredName) || splitArgs.Count() <= 2)
                        {
                            var listString = string.Join(", ", kingdom.ActivePolicies.Select(p => p.ToString()));
                            onSuccess(listString);
                            return;
                        }
                        if (desiredPolicy != null)
                        {
                            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.PolicyPrice)
                            {
                                onFailure(Naming.NotEnoughGold(settings.PeacePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                                return;
                            }
                            if (adoptedHero.Clan.Influence < policyCost)
                            {
                                onFailure($"Not enough influence:{policyCost}");
                                return;
                            }
                            if (kingdom.ActivePolicies.Contains(desiredPolicy))
                            {
                                kingdom.RemovePolicy(desiredPolicy);
                                onSuccess($"Removed {desiredPolicy}");
                                return;
                            }
                            else
                            {
                                kingdom.AddPolicy(desiredPolicy);
                                onSuccess($"Added {desiredPolicy}");
                                return;
                            }
                        }
                        else { onFailure("Invalid action"); }
                        break;
                    }
                case "army":
                    {
                        if (!settings.ArmyEnabled)
                        {
                            onFailure("Army disabled");
                            return;
                        }
                        if (!atWar)
                        {
                            onFailure("No wars");
                            return;
                        }
                        if (adoptedHero.IsPrisoner)
                        {
                            onFailure("{=TESTING}You are prisoner!".Translate());
                            return;
                        }
                        if (!adoptedHero.IsPartyLeader)
                        {
                            onFailure("{=LVFh1Pd5}Your hero is not leading a party".Translate());
                            return;
                        }
                        if (adoptedHero.PartyBelongedTo.Army != null && adoptedHero.PartyBelongedTo.Army.LeaderParty != adoptedHero.PartyBelongedTo)
                        {
                            onFailure("Already in an army!");
                            return;
                        }
                        if (adoptedHero.PartyBelongedTo.MapEvent != null)
                        {
                            onFailure("Your party is busy.");
                            return;
                        }
                        Army.ArmyTypes armyType;
                        if (splitArgs.Length < 2)
                        {
                            onFailure("Specify an army type: defend/siege/raid/patrol");
                            return;
                        }
                        switch (desiredName)
                        {
                            case "siege":
                                armyType = Army.ArmyTypes.Besieger;
                                break;
                            //case "raid":
                            //    armyType = Army.ArmyTypes.Raider;
                            //    break;
                            case "defend":
                                armyType = Army.ArmyTypes.Defender;
                                break;
                            case "patrol":
                                armyType = Army.ArmyTypes.Patrolling;
                                break;
                            default:
                                onFailure($"Invalid army type: {desiredName}");
                                return;
                        }
                        if (adoptedHero.PartyBelongedTo.Army != null && adoptedHero.PartyBelongedTo.Army.LeaderParty == adoptedHero.PartyBelongedTo)
                        {
                            adoptedHero.PartyBelongedTo.Army.ArmyType = armyType;
                            onSuccess($"Changed army type to {armyType}");
                            return;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.ArmyPrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.ArmyPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        var pos = adoptedHero.LastKnownClosestSettlement ?? adoptedHero.HomeSettlement;
                        var sameClanParties = kingdom.AllParties
                            .Where(p => p?.ActualClan == adoptedHero.Clan && p != adoptedHero.PartyBelongedTo && p.Army == null && p.AttachedTo == null && p.LeaderHero != null)
                            .ToList();

                        adoptedHero.Clan.Kingdom.CreateArmy(adoptedHero, pos, armyType);
                        Army army = adoptedHero.PartyBelongedTo.Army;

                        //int addedPartiesCount = 0;
                        foreach (var party in sameClanParties)
                        {
                            party.Army = army;
                        }

                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.ArmyPrice, true);

                        onSuccess($"Gathering {armyType} army({army.Parties.Count}) at {pos}");
                        break;
                    }
                case "alliance":
                    {

                        if (!settings.AllyEnabled)
                        {
                            onFailure("Alliance disabled".Translate());
                            return;
                        }
                        if (desiredKingdom == null)
                        {
                            onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                            return;
                        }
                        if (!adoptedHero.IsKingdomLeader)
                        {
                            onFailure("{=TESTING}Not a king.".Translate());
                            return;
                        }

                        if (kingdom.IsAtWarWith(desiredKingdom))
                        {
                            onFailure($"At war with {desiredKingdom}");
                            return;
                        }
                        if (allianceBehavior.IsAllyWithKingdom(kingdom, desiredKingdom))
                        {
                            onFailure($"Already allied with {desiredKingdom}");
                            return;
                        }
                        if (kingdom == desiredKingdom)
                        {
                            onFailure("Cant ally on yourself!");
                            return;
                        }
                        int influenceCost = Campaign.Current.Models.AllianceModel.GetInfluenceCostOfProposingStartingAlliance(adoptedHero.Clan);
                        if (adoptedHero.Clan.Influence < influenceCost)
                        {
                            onFailure($"Not enough influence:{influenceCost}");
                            return;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.AllyPrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.AllyPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        else
                        {
                            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.AllyPrice, true);
                            allianceBehavior.StartAlliance(kingdom, desiredKingdom);
                            adoptedHero.Clan.Influence -= influenceCost;
                            onSuccess($"Allied with {desiredKingdom}");
                        }
                        break;
                    }
                case "trade":
                    {

                        if (!settings.TradeEnabled)
                        {
                            onFailure("Trade alliances disabled".Translate());
                            return;
                        }
                        if (desiredKingdom == null)
                        {
                            onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                            return;
                        }
                        if (!adoptedHero.IsKingdomLeader)
                        {
                            onFailure("{=TESTING}Not a king.".Translate());
                            return;
                        }

                        if (kingdom.IsAtWarWith(desiredKingdom))
                        {
                            onFailure($"At war with {desiredKingdom}");
                            return;
                        }
                        if (tradeBehavior.HasTradeAgreement(kingdom, desiredKingdom))
                        {
                            onFailure($"Already trading with {desiredKingdom}");
                            return;
                        }
                        if (kingdom == desiredKingdom)
                        {
                            onFailure("Cant trade with yourself!");
                            return;
                        }
                        int influenceCost = Campaign.Current.Models.TradeAgreementModel.GetInfluenceCostOfProposingTradeAgreement(adoptedHero.Clan);
                        if (adoptedHero.Clan.Influence < influenceCost)
                        {
                            onFailure($"Not enough influence:{influenceCost}");
                            return;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.TradePrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.TradePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        else
                        {
                            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.TradePrice, true);
                            tradeBehavior.MakeTradeAgreement(kingdom, desiredKingdom, CampaignTime.Years(1));
                            adoptedHero.Clan.Influence -= influenceCost;
                            onSuccess($"Allied with {desiredKingdom}");
                        }
                        break;
                    }
                default:
                    {
                        ActionManager.SendReply(context,
                        context.ArgsErrorMessage("{=TESTING}invalid mode (use war (kingdom), peace (kingdom), alliance (kingdom), trade (kingdom), army (defend/siege/patrol)".Translate()));
                        break;
                    }
            }
        }
    }
}