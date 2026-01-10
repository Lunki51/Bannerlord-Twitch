using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace BLTAdoptAHero.Models
{
    internal static class UpgradeModelHelper
    {
        private static readonly TextObject Text =
            new TextObject("{=BLT_UpgradeBonus}Upgrade bonuses");

        public static void ApplyDaily(
            ExplainedNumber result,
            float flat,
            float percent)
        {
            if (flat != 0f)
                result.Add(flat, Text);

            if (percent != 0f)
                result.AddFactor(percent / 100f, Text);
        }
    }

    // ---------------- PARTY SPEED ----------------
    
    public class BLTPartySpeedModel : PartySpeedModel
    {
        private readonly PartySpeedModel _previous;
        private static readonly TextObject Text =
            new TextObject("{=BLT_UpgradePartySpeed}Upgrade bonuses");

        public BLTPartySpeedModel(PartySpeedModel previous)
        {
            _previous = previous;
        }
        public override float BaseSpeed => _previous.BaseSpeed;
        public override float MinimumSpeed => _previous.MinimumSpeed;

        public override ExplainedNumber CalculateBaseSpeed(MobileParty party, bool includeDescriptions = false, int additionalTroopOnFootCount = 0, int additionalTroopOnHorseCount = 0)
        {
            return _previous.CalculateBaseSpeed(party, includeDescriptions, additionalTroopOnFootCount, additionalTroopOnHorseCount);
        }

        public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
        {
            var result = _previous.CalculateFinalSpeed(mobileParty, finalSpeed);

            if (UpgradeBehavior.Current != null)
            {
                result.Add(UpgradeBehavior.Current.GetTotalPartySpeedBonus(mobileParty.LeaderHero), Text);
            }

            return result;
        }
    }
    
    // ---------------- PARTY SIZE ----------------

    public class BLTPartySizeLimitModel : PartySizeLimitModel
    {
        private readonly PartySizeLimitModel _previous;
        private static readonly TextObject Text =
            new TextObject("{=BLT_UpgradePartySize}Upgrade bonuses");

        public override int MinimumNumberOfVillagersAtVillagerParty => _previous.MinimumNumberOfVillagersAtVillagerParty;

        public BLTPartySizeLimitModel(PartySizeLimitModel previous)
        {
            _previous = previous;
        }

        public override ExplainedNumber GetPartyMemberSizeLimit(
            PartyBase party,
            bool includeDescriptions = true)
        {
            var result = _previous.GetPartyMemberSizeLimit(party, includeDescriptions);

            if (party?.LeaderHero != null && UpgradeBehavior.Current != null)
            {
                int bonus = UpgradeBehavior.Current.GetTotalPartySizeBonus(party.LeaderHero);
                if (bonus != 0)
                    result.Add(bonus, Text);
            }

            return result;
        }

        public override ExplainedNumber CalculateGarrisonPartySizeLimit(
            Settlement settlement,
            bool includeDescriptions = true)
        {
            var result = _previous.CalculateGarrisonPartySizeLimit(settlement, includeDescriptions);

            if (settlement != null && UpgradeBehavior.Current != null)
            {
                int bonus = UpgradeBehavior.Current.GetTotalGarrisonCapacityBonus(settlement);
                if (bonus != 0)
                    result.Add(bonus, Text);
            }

            return result;
        }

        public override ExplainedNumber GetPartyPrisonerSizeLimit(PartyBase party, bool includeDescriptions = false)
        {
            return _previous.GetPartyPrisonerSizeLimit(party, includeDescriptions);
        }

        public override int GetClanTierPartySizeEffectForHero(Hero hero)
        {
            return _previous.GetClanTierPartySizeEffectForHero(hero);
        }

        public override int GetNextClanTierPartySizeEffectChangeForHero(Hero hero)
        {
            return _previous.GetNextClanTierPartySizeEffectChangeForHero(hero);
        }

        public override int GetAssumedPartySizeForLordParty(Hero leaderHero, IFaction partyMapFaction, Clan actualClan)
        {
            return _previous.GetAssumedPartySizeForLordParty(leaderHero, partyMapFaction, actualClan);
        }

        public override int GetIdealVillagerPartySize(Village village)
        {
            return _previous.GetIdealVillagerPartySize(village);
        }

        public override TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
        {
            return _previous.FindAppropriateInitialRosterForMobileParty(party, partyTemplate);
        }

        public override List<Ship> FindAppropriateInitialShipsForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
        {
            return _previous.FindAppropriateInitialShipsForMobileParty(party, partyTemplate);
        }
    }
}
