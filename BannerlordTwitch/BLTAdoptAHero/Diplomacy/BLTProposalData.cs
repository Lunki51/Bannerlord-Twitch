using System;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Peace proposal - can be offered (proposer pays tribute) or demanded (proposer receives tribute)
    /// </summary>
    public class BLTPeaceProposal
    {
        public string ProposerKingdomId { get; set; }
        public string TargetKingdomId { get; set; }
        public bool IsOffer { get; set; }
        public int DailyTribute { get; set; }
        public int Duration { get; set; }
        public int GoldCost { get; set; }
        public int InfluenceCost { get; set; }

        // Absolute campaign time
        public CampaignTime ExpirationDate { get; set; }

        public BLTPeaceProposal() { }

        public BLTPeaceProposal(
            Kingdom proposer,
            Kingdom target,
            bool isOffer,
            int dailyTribute,
            int duration,
            int goldCost,
            int influenceCost,
            int daysToAccept)
        {
            ProposerKingdomId = proposer?.StringId;
            TargetKingdomId = target?.StringId;
            IsOffer = isOffer;
            DailyTribute = dailyTribute;
            Duration = duration;
            GoldCost = goldCost;
            InfluenceCost = influenceCost;
            ExpirationDate = CampaignTime.DaysFromNow(daysToAccept);
        }

        public Kingdom GetProposer() =>
            Kingdom.All.FirstOrDefault(k => k.StringId == ProposerKingdomId);

        public Kingdom GetTarget() =>
            Kingdom.All.FirstOrDefault(k => k.StringId == TargetKingdomId);

        public bool IsExpired() =>
            CampaignTime.Now >= ExpirationDate;

        public int DaysRemaining() =>
            Math.Max(0, (int)(ExpirationDate - CampaignTime.Now).ToDays);
    }

    /// <summary>
    /// Alliance proposal with costs that target must accept
    /// </summary>
    public class BLTAllianceProposal
    {
        public string ProposerKingdomId { get; set; }
        public string TargetKingdomId { get; set; }
        public int GoldCost { get; set; }
        public int InfluenceCost { get; set; }

        public CampaignTime ExpirationDate { get; set; }

        public BLTAllianceProposal() { }

        public BLTAllianceProposal(
            Kingdom proposer,
            Kingdom target,
            int goldCost,
            int influenceCost,
            int daysToAccept)
        {
            ProposerKingdomId = proposer?.StringId;
            TargetKingdomId = target?.StringId;
            GoldCost = goldCost;
            InfluenceCost = influenceCost;
            ExpirationDate = CampaignTime.DaysFromNow(daysToAccept);
        }

        public Kingdom GetProposer() =>
            Kingdom.All.FirstOrDefault(k => k.StringId == ProposerKingdomId);

        public Kingdom GetTarget() =>
            Kingdom.All.FirstOrDefault(k => k.StringId == TargetKingdomId);

        public bool IsExpired() =>
            CampaignTime.Now >= ExpirationDate;

        public int DaysRemaining() =>
            Math.Max(0, (int)(ExpirationDate - CampaignTime.Now).ToDays);
    }

    /// <summary>
    /// NAP proposal with costs that target must accept
    /// </summary>
    public class BLTNAPProposal
    {
        public string ProposerKingdomId { get; set; }
        public string TargetKingdomId { get; set; }
        public int GoldCost { get; set; }
        public int InfluenceCost { get; set; }

        public CampaignTime ExpirationDate { get; set; }

        public BLTNAPProposal() { }

        public BLTNAPProposal(
            Kingdom proposer,
            Kingdom target,
            int goldCost,
            int influenceCost,
            int daysToAccept)
        {
            ProposerKingdomId = proposer?.StringId;
            TargetKingdomId = target?.StringId;
            GoldCost = goldCost;
            InfluenceCost = influenceCost;
            ExpirationDate = CampaignTime.DaysFromNow(daysToAccept);
        }

        public Kingdom GetProposer() =>
            Kingdom.All.FirstOrDefault(k => k.StringId == ProposerKingdomId);

        public Kingdom GetTarget() =>
            Kingdom.All.FirstOrDefault(k => k.StringId == TargetKingdomId);

        public bool IsExpired() =>
            CampaignTime.Now >= ExpirationDate;

        public int DaysRemaining() =>
            Math.Max(0, (int)(ExpirationDate - CampaignTime.Now).ToDays);
    }
}
