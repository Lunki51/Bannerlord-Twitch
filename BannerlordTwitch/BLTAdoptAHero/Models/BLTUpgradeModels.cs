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
            bool includeDescriptions = false)
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
            bool includeDescriptions = false)
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

    // ---------------- TAX ----------------

    public class BLTSettlementTaxModel : SettlementTaxModel
    {
        private readonly SettlementTaxModel _previous;
        private static readonly TextObject Text =
            new TextObject("{=BLT_UpgradeTax}Upgrade bonuses");

        public override float SettlementCommissionRateTown => _previous.SettlementCommissionRateTown;

        public override float SettlementCommissionRateVillage => _previous.SettlementCommissionRateVillage;

        public override int SettlementCommissionDecreaseSecurityThreshold => _previous.SettlementCommissionDecreaseSecurityThreshold;

        public override int MaximumDecreaseBasedOnSecuritySecurity => _previous.MaximumDecreaseBasedOnSecuritySecurity;

        public BLTSettlementTaxModel(SettlementTaxModel previous)
        {
            _previous = previous;
        }

        public override ExplainedNumber CalculateTownTax(
            Town town,
            bool includeDescriptions = false)
        {
            var result = _previous.CalculateTownTax(town, includeDescriptions);

            if (town?.Settlement != null && UpgradeBehavior.Current != null)
            {
                int flat = UpgradeBehavior.Current.GetTotalTaxBonus(town.Settlement);
                if (flat != 0)
                    result.Add(flat, Text);
            }

            return result;
        }

        public override float GetTownTaxRatio(Town town)
        {
            return _previous.GetTownTaxRatio(town);
        }

        public override float GetVillageTaxRatio(Village village)
        {
            return _previous.GetVillageTaxRatio(village);
        }

        public override float GetTownCommissionChangeBasedOnSecurity(Town town, float commission)
        {
            return _previous.GetTownCommissionChangeBasedOnSecurity(town, commission);
        }

        public override int CalculateVillageTaxFromIncome(Village village, int marketIncome)
        {
            return _previous.CalculateVillageTaxFromIncome(village, marketIncome);
        }
    }

    // ---------------- LOYALTY ----------------

    public class BLTSettlementLoyaltyModel : SettlementLoyaltyModel
    {
        private readonly SettlementLoyaltyModel _previous;

        public BLTSettlementLoyaltyModel(SettlementLoyaltyModel previous)
        {
            _previous = previous;
        }

        public override int SettlementLoyaltyChangeDueToSecurityThreshold => _previous.SettlementLoyaltyChangeDueToSecurityThreshold;

        public override int MaximumLoyaltyInSettlement => _previous.MaximumLoyaltyInSettlement;

        public override int LoyaltyDriftMedium => _previous.LoyaltyDriftMedium;

        public override float HighLoyaltyProsperityEffect => _previous.HighLoyaltyProsperityEffect;

        public override int LowLoyaltyProsperityEffect => _previous.LowLoyaltyProsperityEffect;

        public override int MilitiaBoostPercentage => _previous.MilitiaBoostPercentage;

        public override float HighSecurityLoyaltyEffect => _previous.HighSecurityLoyaltyEffect;

        public override float LowSecurityLoyaltyEffect => _previous.LowSecurityLoyaltyEffect;

        public override float GovernorSameCultureLoyaltyEffect => _previous.GovernorSameCultureLoyaltyEffect;

        public override float GovernorDifferentCultureLoyaltyEffect => _previous.GovernorDifferentCultureLoyaltyEffect;

        public override float SettlementOwnerDifferentCultureLoyaltyEffect => _previous.SettlementOwnerDifferentCultureLoyaltyEffect;

        public override int ThresholdForTaxBoost => _previous.ThresholdForTaxBoost;

        public override int RebellionStartLoyaltyThreshold => _previous.RebellionStartLoyaltyThreshold;

        public override int ThresholdForTaxCorruption => _previous.ThresholdForTaxCorruption;

        public override int ThresholdForHigherTaxCorruption => _previous.ThresholdForHigherTaxCorruption;

        public override int ThresholdForProsperityBoost => _previous.ThresholdForProsperityBoost;

        public override int ThresholdForProsperityPenalty => _previous.ThresholdForProsperityPenalty;

        public override int AdditionalStarvationPenaltyStartDay => _previous.AdditionalStarvationPenaltyStartDay;

        public override int AdditionalStarvationLoyaltyEffect => _previous.AdditionalStarvationLoyaltyEffect;

        public override int RebelliousStateStartLoyaltyThreshold => _previous.RebelliousStateStartLoyaltyThreshold;

        public override int LoyaltyBoostAfterRebellionStartValue => _previous.LoyaltyBoostAfterRebellionStartValue;

        public override float ThresholdForNotableRelationBonus => _previous.ThresholdForNotableRelationBonus;

        public override int DailyNotableRelationBonus => _previous.DailyNotableRelationBonus;

        public override void CalculateGoldCutDueToLowLoyalty(Town town, ref ExplainedNumber explainedNumber)
        {
            _previous.CalculateGoldCutDueToLowLoyalty(town, ref explainedNumber);
        }

        public override void CalculateGoldGainDueToHighLoyalty(Town town, ref ExplainedNumber explainedNumber)
        {
            _previous.CalculateGoldGainDueToHighLoyalty(town, ref explainedNumber);
        }

        public override ExplainedNumber CalculateLoyaltyChange(
            Town town,
            bool includeDescriptions = false)
        {
            var result = _previous.CalculateLoyaltyChange(town, includeDescriptions);

            if (town?.Settlement != null && UpgradeBehavior.Current != null)
            {
                UpgradeModelHelper.ApplyDaily(
                    result,
                    UpgradeBehavior.Current.GetLoyaltyFlat(town.Settlement),
                    UpgradeBehavior.Current.GetLoyaltyPercent(town.Settlement));
            }

            return result;
        }
    }

    // ---------------- PROSPERITY ----------------

    public class BLTSettlementProsperityModel : SettlementProsperityModel
    {
        private readonly SettlementProsperityModel _previous;

        public BLTSettlementProsperityModel(SettlementProsperityModel previous)
        {
            _previous = previous;
        }

        public override ExplainedNumber CalculateHearthChange(Village village, bool includeDescriptions = false)
        {
            return _previous.CalculateHearthChange(village, includeDescriptions);
        }

        public override ExplainedNumber CalculateProsperityChange(
            Town town,
            bool includeDescriptions = false)
        {
            var result = _previous.CalculateProsperityChange(town, includeDescriptions);

            if (town?.Settlement != null && UpgradeBehavior.Current != null)
            {
                UpgradeModelHelper.ApplyDaily(
                    result,
                    UpgradeBehavior.Current.GetProsperityFlat(town.Settlement),
                    UpgradeBehavior.Current.GetProsperityPercent(town.Settlement));
            }

            return result;
        }
    }

    // ---------------- SECURITY ----------------

    public class BLTSettlementSecurityModel : SettlementSecurityModel
    {
        private readonly SettlementSecurityModel _previous;

        public BLTSettlementSecurityModel(SettlementSecurityModel previous)
        {
            _previous = previous;
        }

        public override int MaximumSecurityInSettlement => _previous.MaximumSecurityInSettlement;

        public override int SecurityDriftMedium => _previous.SecurityDriftMedium;

        public override float MapEventSecurityEffectRadius => _previous.MapEventSecurityEffectRadius;

        public override float HideoutClearedSecurityEffectRadius => _previous.HideoutClearedSecurityEffectRadius;

        public override int HideoutClearedSecurityGain => _previous.HideoutClearedSecurityGain;

        public override int ThresholdForTaxCorruption => _previous.ThresholdForTaxCorruption;

        public override int ThresholdForHigherTaxCorruption => _previous.ThresholdForHigherTaxCorruption;

        public override int ThresholdForTaxBoost => _previous.ThresholdForTaxBoost;

        public override int SettlementTaxBoostPercentage => _previous.SettlementTaxBoostPercentage;

        public override int SettlementTaxPenaltyPercentage => _previous.SettlementTaxPenaltyPercentage;

        public override int ThresholdForNotableRelationBonus => _previous.ThresholdForNotableRelationBonus;

        public override int ThresholdForNotableRelationPenalty => _previous.ThresholdForNotableRelationPenalty;

        public override int DailyNotableRelationBonus => _previous.DailyNotableRelationBonus;

        public override int DailyNotableRelationPenalty => _previous.DailyNotableRelationPenalty;

        public override int DailyNotablePowerBonus => _previous.DailyNotablePowerBonus;

        public override int DailyNotablePowerPenalty => _previous.DailyNotablePowerPenalty;

        public override void CalculateGoldCutDueToLowSecurity(Town town, ref ExplainedNumber explainedNumber)
        {
            _previous.CalculateGoldCutDueToLowSecurity(town, ref explainedNumber);
        }

        public override void CalculateGoldGainDueToHighSecurity(Town town, ref ExplainedNumber explainedNumber)
        {
            _previous.CalculateGoldGainDueToHighSecurity(town, ref explainedNumber);
        }

        public override ExplainedNumber CalculateSecurityChange(
            Town town,
            bool includeDescriptions = false)
        {
            var result = _previous.CalculateSecurityChange(town, includeDescriptions);

            if (town?.Settlement != null && UpgradeBehavior.Current != null)
            {
                UpgradeModelHelper.ApplyDaily(
                    result,
                    UpgradeBehavior.Current.GetSecurityFlat(town.Settlement),
                    UpgradeBehavior.Current.GetSecurityPercent(town.Settlement));
            }

            return result;
        }

        public override float GetLootedNearbyPartySecurityEffect(Town town, float sumOfAttackedPartyStrengths)
        {
            return _previous.GetLootedNearbyPartySecurityEffect(town, sumOfAttackedPartyStrengths);
        }

        public override float GetNearbyBanditPartyDefeatedSecurityEffect(Town town, float sumOfAttackedPartyStrengths)
        {
            return _previous.GetNearbyBanditPartyDefeatedSecurityEffect(town, sumOfAttackedPartyStrengths);
        }
    }


    // ---------------- MILITIA ----------------

    public class BLTSettlementMilitiaModel : SettlementMilitiaModel
    {
        private readonly SettlementMilitiaModel _previous;

        public BLTSettlementMilitiaModel(SettlementMilitiaModel previous)
        {
            _previous = previous;
        }

        public override ExplainedNumber CalculateMilitiaChange(
            Settlement settlement,
            bool includeDescriptions = false)
        {
            var result = _previous.CalculateMilitiaChange(settlement, includeDescriptions);

            if (settlement != null && UpgradeBehavior.Current != null)
            {
                UpgradeModelHelper.ApplyDaily(
                    result,
                    UpgradeBehavior.Current.GetMilitiaFlat(settlement),
                    UpgradeBehavior.Current.GetMilitiaPercent(settlement));
            }

            return result;
        }

        public override void CalculateMilitiaSpawnRate(Settlement settlement, out float meleeTroopRate, out float rangedTroopRate)
        {
            _previous.CalculateMilitiaSpawnRate(settlement, out meleeTroopRate, out rangedTroopRate);
        }

        public override ExplainedNumber CalculateVeteranMilitiaSpawnChance(Settlement settlement)
        {
            return _previous.CalculateVeteranMilitiaSpawnChance(settlement);
        }

        public override int MilitiaToSpawnAfterSiege(Town town)
        {
            return _previous.MilitiaToSpawnAfterSiege(town);
        }
    }

    // ---------------- FOOD ----------------

    public class BLTSettlementFoodModel : SettlementFoodModel
    {
        private readonly SettlementFoodModel _previous;

        public BLTSettlementFoodModel(SettlementFoodModel previous)
        {
            _previous = previous;
        }

        public override int FoodStocksUpperLimit => _previous.FoodStocksUpperLimit;

        public override int NumberOfProsperityToEatOneFood => _previous.NumberOfProsperityToEatOneFood;

        public override int NumberOfMenOnGarrisonToEatOneFood => _previous.NumberOfMenOnGarrisonToEatOneFood;

        public override int CastleFoodStockUpperLimitBonus => _previous.CastleFoodStockUpperLimitBonus;

        public override ExplainedNumber CalculateTownFoodStocksChange(
            Town town,
            bool includeMarketStocks = true,
            bool includeDescriptions = true)
        {
            var result = _previous.CalculateTownFoodStocksChange(town, includeDescriptions);

            if (town?.Settlement != null && UpgradeBehavior.Current != null)
            {
                UpgradeModelHelper.ApplyDaily(
                    result,
                    UpgradeBehavior.Current.GetFoodFlat(town.Settlement),
                    UpgradeBehavior.Current.GetFoodPercent(town.Settlement));
            }

            return result;
        }
    }
}
