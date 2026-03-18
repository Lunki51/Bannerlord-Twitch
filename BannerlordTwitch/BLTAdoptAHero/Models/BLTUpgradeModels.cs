using System;
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

            if (UpgradeBehavior.Current == null) return result;

            if (mobileParty.Army != null)
            {
                float armyBonus = UpgradeBehavior.Current
                    .GetTotalArmySpeedBonus(mobileParty.Army);
                if (armyBonus != 0f)
                    result.Add(armyBonus, Text);
            }
            else
            {
                float partyBonus = UpgradeBehavior.Current
                    .GetTotalPartySpeedBonus(mobileParty.LeaderHero);
                if (partyBonus != 0f)
                    result.Add(partyBonus, Text);
            }

            return result;
        }
    }
    
    // ---------------- PARTY SIZE ----------------

    public class BLTPartySizeLimitModel : PartySizeLimitModel
    {
        private readonly PartySizeLimitModel _previous;
        private static readonly TextObject UpgradeText =
            new TextObject("{=BLT_UpgradePartySize}Upgrade bonuses");
        private static readonly TextObject MercArmyText =
            new TextObject("{=BLT_MercArmyPartySize}Custom Mercenary Army");

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

            if (UpgradeBehavior.Current == null) return result;

            // War-party size bonus (clan/kingdom upgrades via PartySizeBonus)
            if (party?.LeaderHero != null)
            {
                int bonus = UpgradeBehavior.Current.GetTotalPartySizeBonus(party.LeaderHero);
                if (bonus != 0)
                    result.Add(bonus, UpgradeText);
            }

            // Garrison capacity bonus — must be here because the game engine enforces
            // the garrison cap via Party.PartySizeLimit → GetPartyMemberSizeLimit,
            // NOT via CalculateGarrisonPartySizeLimit, so the bonus must live here
            // to actually be enforced in-game (not just displayed in the info screen).
            if (party?.MobileParty != null && party.MobileParty.IsGarrison)
            {
                var settlement = party.MobileParty.CurrentSettlement
                              ?? party.MobileParty.HomeSettlement;
                if (settlement != null)
                {
                    int garrisonBonus = UpgradeBehavior.Current.GetTotalGarrisonCapacityBonus(settlement);
                    if (garrisonBonus != 0)
                        result.Add(garrisonBonus, UpgradeText);
                }
            }

            return result;
        }

        //public override ExplainedNumber CalculateGarrisonPartySizeLimit(
        //    Settlement settlement,
        //    bool includeDescriptions = true)
        //{
        //    var result = _previous.CalculateGarrisonPartySizeLimit(settlement, includeDescriptions);
        //
        //    if (settlement != null && UpgradeBehavior.Current != null)
        //    {
        //        int bonus = UpgradeBehavior.Current.GetTotalGarrisonCapacityBonus(settlement);
        //        if (bonus != 0)
        //            result.Add(bonus, UpgradeText);
        //    }
        //
        //    return result;
        //}

        public override ExplainedNumber CalculateGarrisonPartySizeLimit(
            Settlement settlement,
            bool includeDescriptions = true)
        {
            // Delegate entirely to _previous — the garrison capacity bonus is now
            // applied in GetPartyMemberSizeLimit to ensure the engine enforces it.
            // Previously adding it here caused it to show correctly in the info
            // screen but have no effect on actual enforcement.
            return _previous.CalculateGarrisonPartySizeLimit(settlement, includeDescriptions);
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

    public class BLTClanTierModel : ClanTierModel
    {
        private readonly ClanTierModel _previous;
        private static readonly TextObject Text =
            new TextObject("{=BLT_UpgradeClanTier}Upgrade bonuses");

        public BLTClanTierModel(ClanTierModel previous)
        {
            _previous = previous;
        }

        public override int MinClanTier => _previous.MinClanTier;
        public override int MaxClanTier => _previous.MaxClanTier;
        public override int MercenaryEligibleTier => _previous.MercenaryEligibleTier;
        public override int VassalEligibleTier => _previous.VassalEligibleTier;
        public override int BannerEligibleTier => _previous.BannerEligibleTier;
        public override int RebelClanStartingTier => _previous.RebelClanStartingTier;
        public override int CompanionToLordClanStartingTier => _previous.CompanionToLordClanStartingTier;

        public override int CalculateInitialRenown(Clan clan)
        {
            return _previous.CalculateInitialRenown(clan);
        }

        public override int CalculateInitialInfluence(Clan clan)
        {
            return _previous.CalculateInitialInfluence(clan);
        }

        public override int CalculateTier(Clan clan)
        {
            return _previous.CalculateTier(clan);
        }

        public override ValueTuple<ExplainedNumber, bool> HasUpcomingTier(Clan clan, out TextObject extraExplanation, bool includeDescriptions = false)
        {
            return _previous.HasUpcomingTier(clan, out extraExplanation, includeDescriptions);
        }

        public override int GetRequiredRenownForTier(int tier)
        {
            return _previous.GetRequiredRenownForTier(tier);
        }

        public override int GetPartyLimitForTier(Clan clan, int clanTierToCheck)
        {
            int baseLimit = _previous.GetPartyLimitForTier(clan, clanTierToCheck);

            if (clan?.Leader != null && UpgradeBehavior.Current != null)
            {
                int bonus = UpgradeBehavior.Current.GetTotalPartyAmountBonus(clan);
                if (bonus != 0)
                    baseLimit += bonus;
            }

            // Add bonus from active mercenary armies
            //var behavior = MercenaryArmyBehavior.Current;
            //if (behavior != null)
            //{
            //    int mercenaryArmies = behavior.GetActiveArmiesForClan(clan);
            //    baseLimit += mercenaryArmies; // +1 per active army
            //}

            return baseLimit;
        }

        public override int GetCompanionLimit(Clan clan)
        {
            return _previous.GetCompanionLimit(clan);
        }
    }
}
