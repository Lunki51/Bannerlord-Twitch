using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
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
            if (settlement == null || UpgradeBehavior.Current == null)
                return;

            ApplyTownBonuses(settlement);
            ApplyVillageBonuses(settlement);
        }

        private void ApplyTownBonuses(Settlement settlement)
        {
            Town town = settlement.Town;
            if (town == null)
                return;

            float prosperityFlat = UpgradeBehavior.Current.GetProsperityFlat(settlement);
            float prosperityPercent = UpgradeBehavior.Current.GetProsperityPercent(settlement);

            if (prosperityFlat != 0f)
                town.Prosperity += prosperityFlat;

            if (prosperityPercent != 0f)
                town.Prosperity += town.Prosperity * (prosperityPercent / 100f);

            float loyaltyFlat = UpgradeBehavior.Current.GetLoyaltyFlat(settlement);
            float loyaltyPercent = UpgradeBehavior.Current.GetLoyaltyPercent(settlement);

            if (loyaltyFlat != 0f)
                town.Loyalty += loyaltyFlat;

            if (loyaltyPercent != 0f)
                town.Loyalty += town.Loyalty * (loyaltyPercent / 100f);

            float securityFlat = UpgradeBehavior.Current.GetSecurityFlat(settlement);
            float securityPercent = UpgradeBehavior.Current.GetSecurityPercent(settlement);

            if (securityFlat != 0f)
                town.Security += securityFlat;

            if (securityPercent != 0f)
                town.Security += town.Security * (securityPercent / 100f);

            town.Security = Math.Min(
    town.Security,
    Campaign.Current.Models.SettlementSecurityModel.MaximumSecurityInSettlement);


            float foodFlat = UpgradeBehavior.Current.GetFoodFlat(settlement);
            float foodPercent = UpgradeBehavior.Current.GetFoodPercent(settlement);

            if (foodFlat != 0f)
                town.FoodStocks += foodFlat;

            if (foodPercent != 0f)
                town.FoodStocks += town.FoodStocks * (foodPercent / 100f);

            town.FoodStocks = Math.Min(
    town.FoodStocks,
    Campaign.Current.Models.SettlementFoodModel.FoodStocksUpperLimit);


            float militiaFlat = UpgradeBehavior.Current.GetMilitiaFlat(settlement);
            float militiaPercent = UpgradeBehavior.Current.GetMilitiaPercent(settlement);

            if (militiaFlat != 0f)
                settlement.Militia += militiaFlat;

            if (militiaPercent != 0f)
                settlement.Militia += settlement.Militia * (militiaPercent / 100f);

            int taxFlat = UpgradeBehavior.Current.GetTotalTaxBonus(settlement);
            if (taxFlat > 0 && town.OwnerClan != null)
                town.OwnerClan.AddGold(taxFlat);
        }

        private void ApplyVillageBonuses(Settlement settlement)
        {
            Village village = settlement.Village;
            if (village == null)
                return;

            int taxFlat = UpgradeBehavior.Current.GetTotalTaxBonus(settlement);

            if (taxFlat > 0 && village.TradeTaxAccumulated > 1)
                village.OwnerClan.AddGold(taxFlat);
        }
    }
}
