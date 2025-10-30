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
            CategoryOrder("Army", 2)]
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

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                var sb = new StringBuilder();
                if (WarEnabled) sb.Append("{=TESTING}War, ".Translate());
                if (PeaceEnabled) sb.Append("{=TESTING}Peace, ".Translate());
                if (ArmyEnabled) sb.Append("{=TESTING}Army, ".Translate());
                if (sb.Length > 0)
                    generator.Value("<strong>Enabled Commands:</strong> {commands}".Translate(
                        ("commands", sb.ToString(0, sb.Length - 2))));

                if (WarEnabled)
                    generator.Value("<strong>War Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", WarPrice.ToString()), ("icon", Naming.Gold)));
                if (PeaceEnabled)
                    generator.Value("<strong>Peace Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", PeacePrice.ToString()), ("icon", Naming.Gold)));
                if (ArmyEnabled)
                    generator.Value("<strong>Army Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", ArmyEnabled.ToString()), ("icon", Naming.Gold)));
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
                    context.ArgsErrorMessage("{=TESTING}invalid mode (use war (kingdom), peace (kingdom), army (defend/siege/raid/patrol)".Translate()));
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



            var desiredKingdom = CampaignHelpers.AllHeroes.Select(h => h?.Clan?.Kingdom).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);

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
                            onFailure("Not enough influence.");
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
                        if ((CampaignTime.Now - stance.PeaceDeclarationDate).ToDays < 20)
                        {
                            onFailure($"Cant war yet. {(int)(20 - (CampaignTime.Now - stance.PeaceDeclarationDate).ToDays)} days remaining.");
                            return;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.WarPrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.WarPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.WarPrice, true);
                        if (kingdom == Hero.MainHero.Clan.Kingdom)
                        {
                            bool isAlreadyProposed = kingdom.UnresolvedDecisions
                            .Any(d => d is DeclareWarDecision warDecision &&
                                      warDecision.FactionToDeclareWarOn == desiredKingdom);
                            if (isAlreadyProposed)
                            {
                                Log.LogFeedMessage($"Vote already ongoing.");
                                return;
                            }

                            DeclareWarDecision newWarProposal = new DeclareWarDecision(adoptedHero.Clan, desiredKingdom);
                            adoptedHero.Clan.Kingdom.AddDecision(newWarProposal);
                            onSuccess("Proposed war decision.");
                        }
                        else
                        {
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
                            onFailure("Not enough influence");
                            return;
                        }

                        var stance = kingdom.GetStanceWith(desiredKingdom);
                        if (!kingdom.IsAtWarWith(desiredKingdom))
                        {
                            onFailure($"Already at peace with {desiredKingdom}");
                            return;
                        }
                        if ((stance.WarStartDate - CampaignTime.Now).ToDays < 20)
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
                        var barter = new PeaceBarterable(kingdom, desiredKingdom, CampaignTime.Years(1f));
                        float valueForFaction = barter.GetValueForFaction(proposer);
                        float wealthFactor = (proposer.Leader.Gold < 50000f)
                        ? (1f + 0.5f * ((50000f - proposer.Leader.Gold) / 50000f))
                        : ((proposer.Leader.Gold > 200000f)
                        ? (float)Math.Max(0.66, Math.Pow(200000.0 / proposer.Leader.Gold, 0.4))
                        : 1f);
                        int generosityLevel = proposer.Leader.GetTraitLevel(DefaultTraits.Generosity);
                        float generosityFactor = (true) // tribute payer
                        ? (1f - 0.1f * Math.Max(-2, Math.Min(2, generosityLevel)))
                        : 1f;
                        int tributeValue = diplomacy.GetDailyTributeForValue((int)(valueForFaction / (wealthFactor * generosityFactor)));

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

                            MakePeaceKingdomDecision newPeaceProposal = new MakePeaceKingdomDecision(adoptedHero.Clan, desiredKingdom, tributeValue);
                            adoptedHero.Clan.Kingdom.AddDecision(newPeaceProposal);
                            onSuccess($"Proposed peace decision");
                        }
                        //else if (desiredKingdom == Hero.MainHero.Clan.Kingdom && Hero.MainHero.IsKingdomLeader)
                        //{
                        //    PeaceOfferCampaignBehavior
                        //}
                        else
                        {
                            MakePeaceAction.Apply(kingdom, desiredKingdom, tributeValue);

                            adoptedHero.Clan.Influence -= influenceCost;
                            tributeValue *= -1;
                            influenceCost *= -1;
                            onSuccess($"Made peace with {desiredKingdom}. Tribute:{tributeValue}, Influence:{influenceCost}");
                        }

                        break;
                    }
                //case "policy":
                //    {

                //        break;
                //    }
                case "army":
                    {
                        if (!settings.ArmyEnabled)
                        {
                            onFailure("Army disabled");
                            return;
                        }
                        if (!kingdom.Stances.Any(s => s.IsAtWar && (s.Faction1 is Kingdom && s.Faction2 is Kingdom) && (s.Faction1 == kingdom || s.Faction2 == kingdom)))
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
                            case "raid":
                                armyType = Army.ArmyTypes.Raider;
                                break;
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

                        int armyLimit = Math.Max(2, (int)Math.Floor(adoptedHero.Clan.Influence / 50f));
                        //int addedPartiesCount = 0;
                        foreach (var party in sameClanParties)
                        {
                            party.Army = army;
                        }

                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.ArmyPrice, true);

                        onSuccess($"Gathering {armyType} army({army.Parties.Count}) at {pos}");
                        break;
                    }
                default:
                    {
                        ActionManager.SendReply(context,
                        context.ArgsErrorMessage("{=TESTING}invalid mode (use war (kingdom), peace (kingdom), army (defend/siege/raid/patrol)".Translate()));
                        break;
                    }
            }
        }

    }
}