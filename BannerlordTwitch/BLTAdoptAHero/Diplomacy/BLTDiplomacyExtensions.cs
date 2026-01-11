using TaleWorlds.CampaignSystem;
using System.Linq;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Extension methods for cleaner BLT diplomacy code
    /// </summary>
    public static class BLTDiplomacyExtensions
    {
        /// <summary>
        /// Check if kingdom can declare war on target (BLT treaty system)
        /// </summary>
        public static bool CanDeclareWarBLT(this Kingdom kingdom, Kingdom target, out string reason)
        {
            if (BLTTreatyManager.Current == null)
            {
                reason = "Treaty system not initialized";
                return false;
            }
            return BLTTreatyManager.Current.CanDeclareWar(kingdom, target, out reason);
        }

        /// <summary>
        /// Get all active BLT wars involving this kingdom
        /// </summary>
        public static System.Collections.Generic.List<BLTWar> GetBLTWars(this Kingdom kingdom)
        {
            if (BLTTreatyManager.Current == null)
                return new System.Collections.Generic.List<BLTWar>();

            return BLTTreatyManager.Current.GetWarsInvolving(kingdom);
        }

        /// <summary>
        /// Get all BLT alliances for this kingdom
        /// </summary>
        public static System.Collections.Generic.List<BLTAlliance> GetBLTAlliances(this Kingdom kingdom)
        {
            if (BLTTreatyManager.Current == null)
                return new System.Collections.Generic.List<BLTAlliance>();

            return BLTTreatyManager.Current.GetAlliancesFor(kingdom);
        }

        /// <summary>
        /// Get all BLT NAPs for this kingdom
        /// </summary>
        public static System.Collections.Generic.List<BLTNAP> GetBLTNAPs(this Kingdom kingdom)
        {
            if (BLTTreatyManager.Current == null)
                return new System.Collections.Generic.List<BLTNAP>();

            return BLTTreatyManager.Current.GetNAPsFor(kingdom);
        }

        /// <summary>
        /// Check if this kingdom has an active truce with target
        /// </summary>
        public static bool HasActiveTruce(this Kingdom kingdom, Kingdom target)
        {
            if (BLTTreatyManager.Current == null)
                return false;

            var truce = BLTTreatyManager.Current.GetTruce(kingdom, target);
            return truce != null && !truce.IsExpired();
        }

        /// <summary>
        /// Check if this kingdom has a NAP with target
        /// </summary>
        public static bool HasNAP(this Kingdom kingdom, Kingdom target)
        {
            if (BLTTreatyManager.Current == null)
                return false;

            return BLTTreatyManager.Current.GetNAP(kingdom, target) != null;
        }

        /// <summary>
        /// Check if this kingdom is allied with target
        /// </summary>
        public static bool IsAlliedBLT(this Kingdom kingdom, Kingdom target)
        {
            if (BLTTreatyManager.Current == null)
                return false;

            return BLTTreatyManager.Current.GetAlliance(kingdom, target) != null;
        }

        /// <summary>
        /// Get all kingdoms this kingdom is paying tribute to
        /// </summary>
        public static System.Collections.Generic.List<Kingdom> GetTributeReceivers(this Kingdom kingdom)
        {
            if (BLTTreatyManager.Current == null)
                return new System.Collections.Generic.List<Kingdom>();

            return BLTTreatyManager.Current.GetTributesPayedBy(kingdom)
                .Select(t => t.GetReceiver())
                .Where(k => k != null)
                .ToList();
        }

        /// <summary>
        /// Get all kingdoms paying tribute to this kingdom
        /// </summary>
        public static System.Collections.Generic.List<Kingdom> GetTributePayers(this Kingdom kingdom)
        {
            if (BLTTreatyManager.Current == null)
                return new System.Collections.Generic.List<Kingdom>();

            return BLTTreatyManager.Current.GetTributesReceivedBy(kingdom)
                .Select(t => t.GetPayer())
                .Where(k => k != null)
                .ToList();
        }

        /// <summary>
        /// Get total daily tribute this kingdom is paying
        /// </summary>
        public static int GetTotalTributePaying(this Kingdom kingdom)
        {
            if (BLTTreatyManager.Current == null)
                return 0;

            return BLTTreatyManager.Current.GetTributesPayedBy(kingdom)
                .Sum(t => t.DailyAmount);
        }

        /// <summary>
        /// Get total daily tribute this kingdom is receiving
        /// </summary>
        public static int GetTotalTributeReceiving(this Kingdom kingdom)
        {
            if (BLTTreatyManager.Current == null)
                return 0;

            return BLTTreatyManager.Current.GetTributesReceivedBy(kingdom)
                .Sum(t => t.DailyAmount);
        }

        /// <summary>
        /// Check if kingdom is in a BLT war (as main participant or assisting ally)
        /// </summary>
        public static bool IsInBLTWar(this Kingdom kingdom)
        {
            if (BLTTreatyManager.Current == null)
                return false;

            return BLTTreatyManager.Current.GetWarsInvolving(kingdom).Count > 0;
        }

        /// <summary>
        /// Check if kingdom is a main participant in any BLT war
        /// </summary>
        public static bool IsMainParticipantInWar(this Kingdom kingdom)
        {
            if (BLTTreatyManager.Current == null)
                return false;

            return BLTTreatyManager.Current.GetWarsInvolving(kingdom)
                .Any(w => w.IsMainParticipant(kingdom));
        }

        /// <summary>
        /// Get all call to war proposals for this kingdom
        /// </summary>
        public static System.Collections.Generic.List<BLTCTWProposal> GetCTWProposals(this Kingdom kingdom)
        {
            if (BLTTreatyManager.Current == null)
                return new System.Collections.Generic.List<BLTCTWProposal>();

            return BLTTreatyManager.Current.GetCTWProposalsFor(kingdom);
        }

        /// <summary>
        /// Get count of pending CTW proposals
        /// </summary>
        public static int GetPendingCTWCount(this Kingdom kingdom)
        {
            if (BLTTreatyManager.Current == null)
                return 0;

            return BLTTreatyManager.Current.GetCTWProposalsFor(kingdom).Count;
        }
    }
}