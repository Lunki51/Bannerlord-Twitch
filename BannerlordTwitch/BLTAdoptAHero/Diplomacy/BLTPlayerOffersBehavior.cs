using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;

namespace BLTAdoptAHero
{
    public class BLTPlayerOffersBehavior : CampaignBehaviorBase
    {
        private List<(Kingdom proposer, int goldCost, int influenceCost, CampaignTime expiration)> _pendingPlayerOffers
            = new List<(Kingdom, int, int, CampaignTime)>();
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }
    
        public override void SyncData(IDataStore dataStore)
        {
            // No persistence needed - proposals are in BLTTreatyManager
        }
    
        private void OnDailyTick()
        {
            // Check for expired offers
            _pendingPlayerOffers.RemoveAll(offer => CampaignTime.Now >= offer.expiration);
        }
    
        public void OfferAllianceToPlayer(Kingdom proposer, Kingdom playerKingdom, int daysToAccept)
        {
            if (Hero.MainHero.Clan.Kingdom == null) return;
    
            var proposal = BLTTreatyManager.Current.GetAllianceProposal(proposer, playerKingdom);
            if (proposal == null) return;
    
            // Show the inquiry using data from the proposal
            ShowAllianceOfferInquiry(proposal);
        }
    
        private void ShowAllianceOfferInquiry(BLTAllianceProposal proposal)
        {
            var proposer = proposal.GetProposer();
            var playerKingdom = proposal.GetTarget();
    
            InformationManager.ShowInquiry(
                new InquiryData(
                    titleText: "Alliance Proposal",
                    text: $"{proposer.Name} proposes a defensive alliance!\n\n" +
                          $"Benefits:\n" +
                          $"• Mutual defense: Both kingdoms join defensive wars\n" +
                          $"• Can call {proposer.Name} to war (costs {proposal.CTWCost}{Naming.Gold})\n\n" +
                          $"Obligations:\n" +
                          $"• Auto-join when {proposer.Name} is attacked\n" +
                          $"• Breaking costs {proposal.BreakAllianceCost}{Naming.Gold}\n\n" +
                          $"You have {proposal.DaysRemaining()} days to decide.",
                    isAffirmativeOptionShown: true,
                    isNegativeOptionShown: true,
                    affirmativeText: "Accept Alliance",
                    negativeText: "Decline",
                    affirmativeAction: () => AcceptPlayerAlliance(proposer, playerKingdom),
                    negativeAction: () => DeclinePlayerAlliance(proposer, playerKingdom)
                ),
                pauseGameActiveState: true
            );
        }
    
        private void AcceptPlayerAlliance(Kingdom proposer, Kingdom playerKingdom)
        {
            if (BLTTreatyManager.Current == null) return;
    
            var proposal = BLTTreatyManager.Current.GetAllianceProposal(proposer, playerKingdom);
            if (proposal == null || playerKingdom.IsAtWarWith(proposer))
            {
                InformationManager.DisplayMessage(
                    new InformationMessage("Alliance proposal is no longer valid", Colors.Red)
                );
                return;
            }
    
            // Create alliance
            BLTTreatyManager.Current.CreateAlliance(proposer, playerKingdom);
            BLTTreatyManager.Current.RemoveNAP(proposer, playerKingdom);
            BLTTreatyManager.Current.RemoveAllianceProposal(proposer, playerKingdom);
    
            // Remove from pending
            _pendingPlayerOffers.RemoveAll(o => o.proposer == proposer);
    
            InformationManager.DisplayMessage(
                new InformationMessage($"Alliance formed with {proposer.Name}!", Colors.Green)
            );
    
            Log.ShowInformation(
                $"{playerKingdom.Name} and {proposer.Name} have formed an alliance!",
                Hero.MainHero.CharacterObject,
                Log.Sound.Horns2
            );
        }
    
        private void DeclinePlayerAlliance(Kingdom proposer, Kingdom playerKingdom)
        {
            if (BLTTreatyManager.Current == null) return;
    
            BLTTreatyManager.Current.RemoveAllianceProposal(proposer, playerKingdom);
            _pendingPlayerOffers.RemoveAll(o => o.proposer == proposer);
    
            InformationManager.DisplayMessage(
                new InformationMessage($"Declined alliance with {proposer.Name}", Colors.Black)
            );
        }
    
        public static BLTPlayerOffersBehavior Current { get; private set; }
    
        public BLTPlayerOffersBehavior()
        {
            Current = this;
        }
    }

    public class BLTNAPOfferBehavior : CampaignBehaviorBase
    {
        private List<(Kingdom proposer, int goldCost, int influenceCost, CampaignTime expiration)> _pendingPlayerOffers
            = new List<(Kingdom, int, int, CampaignTime)>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistence needed - proposals are in BLTTreatyManager
        }

        private void OnDailyTick()
        {
            // Check for expired offers
            _pendingPlayerOffers.RemoveAll(offer => CampaignTime.Now >= offer.expiration);
        }

        public void OfferNAPToPlayer(Kingdom proposer, Kingdom playerKingdom, int daysToAccept)
        {
            if (Hero.MainHero.Clan.Kingdom == null) return;

            var proposal = BLTTreatyManager.Current.GetNAPProposal(proposer, playerKingdom);
            if (proposal == null) return;

            // Show the inquiry using data from the proposal
            ShowNAPOfferInquiry(proposal);
        }

        private void ShowNAPOfferInquiry(BLTNAPProposal proposal)
        {
            var proposer = proposal.GetProposer();
            var playerKingdom = proposal.GetTarget();

            InformationManager.ShowInquiry(
                new InquiryData(
                    titleText: "Non-Aggression Pact Proposal",
                    text: $"{proposer.Name} proposes a non-aggression pact!\n\n" +
                          $"Benefits:\n" +
                          $"• Mutual peace: Neither kingdom can declare war\n" +
                          $"• Can be broken at any time\n\n" +
                          $"Note:\n" +
                          $"• Does not provide military assistance\n" +
                          $"• Less binding than an alliance\n\n" +
                          $"You have {proposal.DaysRemaining()} days to decide.",
                    isAffirmativeOptionShown: true,
                    isNegativeOptionShown: true,
                    affirmativeText: "Accept NAP",
                    negativeText: "Decline",
                    affirmativeAction: () => AcceptPlayerNAP(proposer, playerKingdom),
                    negativeAction: () => DeclinePlayerNAP(proposer, playerKingdom)
                ),
                pauseGameActiveState: true
            );
        }

        private void AcceptPlayerNAP(Kingdom proposer, Kingdom playerKingdom)
        {
            if (BLTTreatyManager.Current == null) return;

            var proposal = BLTTreatyManager.Current.GetNAPProposal(proposer, playerKingdom);
            if (proposal == null || playerKingdom.IsAtWarWith(proposer))
            {
                InformationManager.DisplayMessage(
                    new InformationMessage("NAP proposal is no longer valid", Colors.Red)
                );
                return;
            }

            // Create NAP
            BLTTreatyManager.Current.CreateNAP(proposer, playerKingdom);
            BLTTreatyManager.Current.RemoveNAPProposal(proposer, playerKingdom);

            // Remove from pending
            _pendingPlayerOffers.RemoveAll(o => o.proposer == proposer);

            InformationManager.DisplayMessage(
                new InformationMessage($"Non-aggression pact formed with {proposer.Name}!", Colors.Green)
            );

            Log.ShowInformation(
                $"{playerKingdom.Name} and {proposer.Name} have formed a non-aggression pact!",
                Hero.MainHero.CharacterObject,
                Log.Sound.Horns2
            );
        }

        private void DeclinePlayerNAP(Kingdom proposer, Kingdom playerKingdom)
        {
            if (BLTTreatyManager.Current == null) return;

            BLTTreatyManager.Current.RemoveNAPProposal(proposer, playerKingdom);
            _pendingPlayerOffers.RemoveAll(o => o.proposer == proposer);

            InformationManager.DisplayMessage(
                new InformationMessage($"Declined NAP with {proposer.Name}", Colors.Black)
            );
        }

        public static BLTNAPOfferBehavior Current { get; private set; }

        public BLTNAPOfferBehavior()
        {
            Current = this;
        }
    }

    public class BLTCTWOfferBehavior : CampaignBehaviorBase
    {
        private List<(Kingdom caller, Kingdom target, CampaignTime expiration)> _pendingPlayerOffers
            = new List<(Kingdom, Kingdom, CampaignTime)>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistence needed - proposals are in BLTTreatyManager
        }

        private void OnDailyTick()
        {
            // Check for expired offers
            _pendingPlayerOffers.RemoveAll(offer => CampaignTime.Now >= offer.expiration);
        }

        public void OfferCTWToPlayer(Kingdom caller, Kingdom playerKingdom, Kingdom target, int daysToAccept)
        {
            if (Hero.MainHero.Clan.Kingdom == null || playerKingdom == null) return;

            var proposal = BLTTreatyManager.Current.GetCTWProposalsFor(playerKingdom).Where(p => p.ProposerKingdomId == caller.StringId).FirstOrDefault();
            if (proposal == null) return;

            // Show the inquiry using data from the proposal
            ShowCTWOfferInquiry(proposal);
        }

        private void ShowCTWOfferInquiry(BLTCTWProposal proposal)
        {
            var caller = proposal.GetProposer();
            var playerKingdom = proposal.GetCalled();
            var target = proposal.GetTarget();

            InformationManager.ShowInquiry(
                new InquiryData(
                    titleText: "Call to War",
                    text: $"{caller.Name} calls you to war against {target.Name}!\n\n" +
                          $"Alliance obligation:\n" +
                          $"• Your ally {caller.Name} is at war with {target.Name}\n" +
                          $"• You are expected to join this war\n\n" +
                          $"Consequences:\n" +
                          $"• Accept: Join the war against {target.Name}\n" +
                          $"• Decline: May damage relations with {caller.Name}\n\n" +
                          $"You have {proposal.DaysRemaining()} days to decide.",
                    isAffirmativeOptionShown: true,
                    isNegativeOptionShown: true,
                    affirmativeText: "Join the War",
                    negativeText: "Decline",
                    affirmativeAction: () => AcceptPlayerCTW(caller, playerKingdom, target),
                    negativeAction: () => DeclinePlayerCTW(caller, playerKingdom, target)
                ),
                pauseGameActiveState: true
            );
        }

        private void AcceptPlayerCTW(Kingdom caller, Kingdom playerKingdom, Kingdom target)
        {
            if (BLTTreatyManager.Current == null) return;

            var proposal = BLTTreatyManager.Current.GetCTWProposalsFor(playerKingdom).Where(p => p.ProposerKingdomId == caller.StringId).FirstOrDefault();
            if (proposal == null)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage("Call to war is no longer valid", Colors.Red)
                );
                return;
            }

            // Check if still valid
            if (!caller.IsAtWarWith(target))
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"{caller.Name} is no longer at war with {target.Name}", Colors.Red)
                );
                BLTTreatyManager.Current.RemoveCTWProposal(caller, playerKingdom, target);
                return;
            }

            if (playerKingdom.IsAtWarWith(target))
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"You are already at war with {target.Name}", Colors.Red)
                );
                BLTTreatyManager.Current.RemoveCTWProposal(caller, playerKingdom, target);
                return;
            }

            // Check if can declare war
            if (!BLTTreatyManager.Current.CanDeclareWar(playerKingdom, target, out string reason))
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"Cannot join war: {reason}", Colors.Red)
                );
                return;
            }

            // Join the war
            AdoptedHeroFlags._allowDiplomacyAction = true;
            try
            {
                DeclareWarAction.ApplyByKingdomDecision(playerKingdom, target);

                // Join existing war as ally
                var war = BLTTreatyManager.Current.GetWar(caller, target);
                if (war != null)
                {
                    var att = war.GetAttacker();
                    var def = war.GetDefender();
                    if (att == caller)
                    {
                        war.AddAttackerAlly(playerKingdom);
                    }
                    else if (def == caller)
                    {
                        war.AddDefenderAlly(playerKingdom);
                    }
                }
                BLTTreatyManager.Current.RemoveCTWProposal(caller, playerKingdom, target);
                _pendingPlayerOffers.RemoveAll(o => o.caller == caller && o.target == target);

                InformationManager.DisplayMessage(new InformationMessage($"Joined {caller.Name}'s war against {target.Name}!", Colors.Green));

                Log.ShowInformation(
                    $"{playerKingdom.Name} has joined {caller.Name}'s war against {target.Name}!",
                    Hero.MainHero.CharacterObject,
                    Log.Sound.Horns2
                );
            }
            finally
            {
                AdoptedHeroFlags._allowDiplomacyAction = false;
            }
        }

        private void DeclinePlayerCTW(Kingdom caller, Kingdom playerKingdom, Kingdom target)
        {
            if (BLTTreatyManager.Current == null) return;

            BLTTreatyManager.Current.RemoveCTWProposal(caller, playerKingdom, target);
            _pendingPlayerOffers.RemoveAll(o => o.caller == caller && o.target == target);

            InformationManager.DisplayMessage(
                new InformationMessage($"Declined call to war from {caller.Name}", Colors.Black)
            );
        }

        public static BLTCTWOfferBehavior Current { get; private set; }

        public BLTCTWOfferBehavior()
        {
            Current = this;
        }
    }
}