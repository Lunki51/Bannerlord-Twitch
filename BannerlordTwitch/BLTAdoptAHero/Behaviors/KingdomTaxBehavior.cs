using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace BLTAdoptAHero
{
    public class KingdomTaxBehavior : CampaignBehaviorBase
    {
        // Store tax rates per kingdom (kingdom string ID -> tax rate percentage)
        [SaveableField(1)]
        private Dictionary<string, float> kingdomTaxRates = new Dictionary<string, float>();

        public static KingdomTaxBehavior Current { get; private set; }

        public KingdomTaxBehavior()
        {
            Current = this;
        }

        public override void RegisterEvents()
        {
            // No events needed, just data storage
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("kingdomTaxRates", ref kingdomTaxRates);
        }

        /// <summary>
        /// Set tax rate for a kingdom (0.0 to 1.0, e.g. 0.15 = 15%)
        /// </summary>
        public void SetKingdomTaxRate(Kingdom kingdom, float taxRate)
        {
            if (kingdom == null) return;

            taxRate = Math.Max(0f, Math.Min(1f, taxRate)); // Clamp between 0 and 1

            if (kingdomTaxRates.ContainsKey(kingdom.StringId))
                kingdomTaxRates[kingdom.StringId] = taxRate;
            else
                kingdomTaxRates.Add(kingdom.StringId, taxRate);
        }

        /// <summary>
        /// Get tax rate for a kingdom (returns 0.0 if not set)
        /// </summary>
        public float GetKingdomTaxRate(Kingdom kingdom)
        {
            if (kingdom == null) return 0f;

            if (kingdomTaxRates.TryGetValue(kingdom.StringId, out float rate))
                return rate;

            return 0f;
        }

        /// <summary>
        /// Calculate tax owed by a clan based on their fief income
        /// Returns the tax amount and the income after tax
        /// </summary>
        public (int taxAmount, int incomeAfterTax) CalculateTax(Clan clan, int fiefIncome)
        {
            if (clan?.Kingdom == null || fiefIncome <= 0)
                return (0, fiefIncome);

            float taxRate = GetKingdomTaxRate(clan.Kingdom);
            if (taxRate <= 0f)
                return (0, fiefIncome);

            // Don't tax the ruling clan
            if (clan == clan.Kingdom.RulingClan)
                return (0, fiefIncome);

            int taxAmount = (int)(fiefIncome * taxRate);
            int incomeAfterTax = fiefIncome - taxAmount;

            return (taxAmount, incomeAfterTax);
        }

        /// <summary>
        /// Collect all taxes for a kingdom and give to the king
        /// </summary>
        public void CollectKingdomTaxes(Kingdom kingdom, Dictionary<Clan, int> clanTaxes)
        {
            if (kingdom?.RulingClan?.Leader == null || !kingdom.RulingClan.Leader.IsAdopted())
                return;

            int totalTax = clanTaxes.Values.Sum();

            if (totalTax > 0)
            {
                BLTAdoptAHeroCampaignBehavior.Current?.ChangeHeroGold(kingdom.RulingClan.Leader, totalTax, false);
            }
        }
    }
}