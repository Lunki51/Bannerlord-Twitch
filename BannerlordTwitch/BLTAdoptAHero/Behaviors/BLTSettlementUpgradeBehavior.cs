using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;

namespace BLTAdoptAHero.Behaviors
{
    public class BLTSettlementUpgradeBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(
                this, OnDailyTickSettlement);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnDailyTickSettlement(Settlement settlement)
        {
            if (settlement == null || BLTUpgradeBehavior.Current == null)
                return;

            ApplyTownBonuses(settlement);
            ApplyVillageBonuses(settlement);
        }

        private void ApplyTownBonuses(Settlement settlement)
        {
            Town town = settlement.Town;
            if (town == null)
                return;

            float prosperityFlat = BLTUpgradeBehavior.Current.GetProsperityFlat(settlement);
            float prosperityPercent = BLTUpgradeBehavior.Current.GetProsperityPercent(settlement);

            if (prosperityFlat != 0f)
                town.Prosperity += prosperityFlat;

            if (prosperityPercent != 0f)
                town.Prosperity += town.Prosperity * (prosperityPercent / 100f);

            float loyaltyFlat = BLTUpgradeBehavior.Current.GetLoyaltyFlat(settlement);
            float loyaltyPercent = BLTUpgradeBehavior.Current.GetLoyaltyPercent(settlement);

            if (loyaltyFlat != 0f)
                town.Loyalty += loyaltyFlat;

            if (loyaltyPercent != 0f)
                town.Loyalty += town.Loyalty * (loyaltyPercent / 100f);

            town.Loyalty = Math.Min(
    town.Loyalty,
    Campaign.Current.Models.SettlementLoyaltyModel.MaximumLoyaltyInSettlement);

            float securityFlat = BLTUpgradeBehavior.Current.GetSecurityFlat(settlement);
            float securityPercent = BLTUpgradeBehavior.Current.GetSecurityPercent(settlement);

            if (securityFlat != 0f)
                town.Security += securityFlat;

            if (securityPercent != 0f)
                town.Security += town.Security * (securityPercent / 100f);

            town.Security = Math.Min(
    town.Security,
    Campaign.Current.Models.SettlementSecurityModel.MaximumSecurityInSettlement);


            float foodFlat = BLTUpgradeBehavior.Current.GetFoodFlat(settlement);
            float foodPercent = BLTUpgradeBehavior.Current.GetFoodPercent(settlement);

            int maxLimit = Campaign.Current.Models.SettlementFoodModel.FoodStocksUpperLimit;

            if (foodFlat != 0f)
            {
                // Prevent overflow by clamping before assignment
                town.FoodStocks = (int)Math.Min((long)town.FoodStocks + (long)foodFlat, maxLimit);
            }

            if (foodPercent != 0f)
            {
                long newValue = (long)(town.FoodStocks * (1f + foodPercent / 100f));
                town.FoodStocks = (int)Math.Min(newValue, maxLimit);
            }


            float militiaFlat = BLTUpgradeBehavior.Current.GetMilitiaFlat(settlement);
            float militiaPercent = BLTUpgradeBehavior.Current.GetMilitiaPercent(settlement);

            if (militiaFlat != 0f)
                settlement.Militia += militiaFlat;

            if (militiaPercent != 0f)
                settlement.Militia += settlement.Militia * (militiaPercent / 100f);

            int taxFlat = BLTUpgradeBehavior.Current.GetTotalTaxBonus(settlement);
            if (taxFlat > 0 && town.OwnerClan != null)
                town.OwnerClan.Leader.Gold += taxFlat;
        }

        private void ApplyVillageBonuses(Settlement settlement)
        {
            Village village = settlement.Village;
            if (village == null)
                return;

            int taxFlat = BLTUpgradeBehavior.Current.GetTotalTaxBonus(settlement);
        
            if (taxFlat > 0 && village.TradeTaxAccumulated > 1)
                village.Settlement.OwnerClan.Leader.Gold += taxFlat;
        }
    }
}
