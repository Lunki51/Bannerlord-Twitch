using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

public class BLTAllianceOfferBehavior : CampaignBehaviorBase
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

    public static BLTAllianceOfferBehavior Current { get; private set; }

    public BLTAllianceOfferBehavior()
    {
        Current = this;
    }
}