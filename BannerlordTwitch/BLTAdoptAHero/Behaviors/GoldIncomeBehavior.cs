using BannerlordTwitch.Util;
using BLTAdoptAHero.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace BLTAdoptAHero.Behaviors
{
    public class GoldIncomeBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, OnDailyTickClan);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistence required
        }

        private void OnDailyTickClan(Clan clan)
        {
            if (clan == null)
                return;
            if (!BLTAdoptAHeroModule.CommonConfig.GoldIncomeEnabled)
                return;

            Hero leader = clan.Leader;
            if (leader == null || !leader.IsAdopted())
                return;

            // Check if this is a ruling clan - if so, collect all kingdom taxes
            bool isRulingClan = clan.Kingdom != null && clan.Kingdom.RulingClan == clan;

            if (isRulingClan && BLTAdoptAHeroModule.CommonConfig.FiefIncomeEnabled && KingdomTaxBehavior.Current != null)
            {
                CollectKingdomTaxes(clan);
            }

            // Calculate this clan's income
            int total = CalculateClanIncome(clan, !isRulingClan);

            // Apply gold change if there's any income
            if (total != 0)
            {
                BLTAdoptAHeroCampaignBehavior.Current?.ChangeHeroGold(leader, total, false);
            }
        }

        private void CollectKingdomTaxes(Clan rulingClan)
        {
            if (rulingClan?.Kingdom == null || KingdomTaxBehavior.Current == null)
                return;

            float taxRate = KingdomTaxBehavior.Current.GetKingdomTaxRate(rulingClan.Kingdom);
            if (taxRate <= 0f)
                return;

            int totalTaxCollected = 0;
            var taxBreakdown = new StringBuilder();

            // Collect taxes from all clans in the kingdom (except ruling clan)
            foreach (var clan in rulingClan.Kingdom.Clans)
            {
                if (clan == rulingClan || clan == null)
                    continue;

                // Calculate this clan's fief income
                int fiefIncome = 0;
                if (clan.Settlements != null)
                {
                    foreach (var settlement in clan.Settlements)
                    {
                        fiefIncome += GoldIncomeAction.CalculateSettlementIncome(settlement);
                    }
                }

                // Add vassal fief income if applicable
                if (VassalBehavior.Current != null)
                {
                    fiefIncome += VassalBehavior.Current.CalculateVassalFiefIncome(clan);
                }

                if (fiefIncome <= 0)
                    continue;

                // Calculate tax
                int taxAmount = (int)(fiefIncome * taxRate);
                if (taxAmount <= 0)
                    continue;

                totalTaxCollected += taxAmount;

                // If this is a BLT or Vassal clan, deduct the tax from their income
                // (This will happen when their tick processes and they see reduced income)
                // For AI clans, we just collect the tax from thin air

                // Build breakdown for display
                if (taxBreakdown.Length > 0)
                    taxBreakdown.Append(", ");
                taxBreakdown.Append($"{clan.Name}: +{taxAmount}");
            }

            // Give total tax to ruling clan
            if (totalTaxCollected > 0)
            {
                BLTAdoptAHeroCampaignBehavior.Current?.ChangeHeroGold(rulingClan.Leader, totalTaxCollected, false);

                // Log tax collection
                //string taxMessage = $"Tax collected: +{totalTaxCollected}/day ({(taxRate * 100f):F1}% rate) [{taxBreakdown}]";
                //Log.LogFeedResponse(taxMessage); //Commented this out since it floods the overlay everyday
            }
        }

        private int CalculateClanIncome(Clan clan, bool applyTax)
        {
            int total = 0;
            int fiefIncome = 0;

            // Calculate fief income
            if (BLTAdoptAHeroModule.CommonConfig.FiefIncomeEnabled && clan.Settlements != null)
            {
                foreach (var settlement in clan.Settlements)
                {
                    fiefIncome += GoldIncomeAction.CalculateSettlementIncome(settlement);
                }
            }

            // Calculate bonus from vassal fief income
            if (BLTAdoptAHeroModule.CommonConfig.FiefIncomeEnabled && VassalBehavior.Current != null)
            {
                fiefIncome += VassalBehavior.Current.CalculateVassalFiefIncome(clan);
            }

            // Apply kingdom taxes to fief income (for non-ruling clans only)
            if (applyTax && KingdomTaxBehavior.Current != null && clan.Kingdom != null)
            {
                var taxResult = KingdomTaxBehavior.Current.CalculateTax(clan, fiefIncome);
                fiefIncome = taxResult.incomeAfterTax;
                // Note: The tax amount was already collected by the ruling clan's tick
            }

            total += fiefIncome;

            // Calculate mercenary income for this BLT clan (not taxed)
            if (BLTAdoptAHeroModule.CommonConfig.MercenaryIncomeEnabled && clan.IsUnderMercenaryService)
            {
                int MercUpBonus = UpgradeBehavior.Current.GetFlatMercBonus(clan.Leader);
                float MercUpMult = UpgradeBehavior.Current.GetPercentClanMercBonus(clan);

                total += (int)(GoldIncomeAction.CalculateMercenaryIncome(clan) * MercUpMult);
                total += (int)(MercUpBonus * MercUpMult);
            }

            // Calculate bonus from vassal mercenary contracts (not taxed)
            if (BLTAdoptAHeroModule.CommonConfig.MercenaryIncomeEnabled && VassalBehavior.Current != null)
            {
                int vassalBonus = VassalBehavior.Current.CalculateVassalMercenaryBonus(clan);
                total += vassalBonus;
            }

            return total;
        }
    }
}