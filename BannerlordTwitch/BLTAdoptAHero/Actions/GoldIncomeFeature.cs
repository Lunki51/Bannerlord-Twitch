using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Annotations;
using BLTAdoptAHero.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=GoldIncomeCmd}GoldIncome"),
     LocDescription("{=GoldIncomeDesc}Daily BLT gold income from fiefs and mercenary contracts"),
     UsedImplicitly]
    public class GoldIncomeAction : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(
            Hero adoptedHero,
            ReplyContext context,
            object config,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            // Ensure hero exists
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            // Check if action is enabled
            if (!BLTAdoptAHeroModule.CommonConfig.GoldIncomeEnabled)
            {
                onFailure("Gold income is disabled.");
                return;
            }

            var clan = adoptedHero.Clan;
            if (clan == null)
            {
                onFailure("You are not in a clan.");
                return;
            }

            // Check for fiefs first (towns/castles only)
            var fiefs = clan.Settlements?.Where(s => !s.IsVillage).ToList();
            bool hasFiefs = fiefs != null && fiefs.Count > 0;

            // Check for mercenary contract
            bool isMercenary = clan.IsUnderMercenaryService;

            // Check for tributes
            var tributesReceiving = CalculateTributeIncome(clan);
            var tributesPaying = CalculateTributePayments(clan);
            bool hasTributes = tributesReceiving > 0 || tributesPaying > 0;

            // If no income sources at all
            if (!hasFiefs && !isMercenary && !hasTributes)
            {
                onSuccess("You have no income sources (no settlements, mercenary contract, or tributes).");
                return;
            }

            // Build comprehensive income report
            ShowCompleteIncome(clan, fiefs, tributesReceiving, tributesPaying, onSuccess);
        }

        private void ShowCompleteIncome(Clan clan, List<Settlement> settlements, int tributeIncome, int tributePayments, Action<string> onSuccess)
        {
            var sb = new StringBuilder();
            int totalIncome = 0;

            // === FIEF INCOME ===
            if (BLTAdoptAHeroModule.CommonConfig.FiefIncomeEnabled && settlements != null && settlements.Count > 0)
            {
                int fiefIncome = 0;
                foreach (var s in settlements)
                {
                    int income = CalculateSettlementIncome(s);
                    fiefIncome += income;
                    sb.Append($"{s.Name}: {(income >= 0 ? "+" : "")}{income} | ");
                }

                // Add vassal fief income
                int vassalFiefIncome = 0;
                if (VassalBehavior.Current != null)
                {
                    vassalFiefIncome = VassalBehavior.Current.CalculateVassalFiefIncome(clan);
                }

                int totalFiefIncome = fiefIncome + vassalFiefIncome;

                if (vassalFiefIncome > 0)
                {
                    sb.Append($"Vassal fiefs: +{vassalFiefIncome} | ");
                }

                // Check if ruling clan (for tax collection)
                bool isRulingClan = clan.Kingdom != null && clan.Kingdom.RulingClan == clan;

                if (isRulingClan && KingdomTaxBehavior.Current != null && clan.Kingdom != null)
                {
                    // Calculate tax revenue
                    float taxRate = KingdomTaxBehavior.Current.GetKingdomTaxRate(clan.Kingdom);
                    if (taxRate > 0f)
                    {
                        int totalTaxRevenue = 0;

                        foreach (var otherClan in clan.Kingdom.Clans)
                        {
                            if (otherClan == clan || otherClan == null)
                                continue;

                            // Calculate this clan's fief income
                            int otherFiefIncome = 0;
                            if (otherClan.Settlements != null)
                            {
                                foreach (var settlement in otherClan.Settlements)
                                {
                                    otherFiefIncome += CalculateSettlementIncome(settlement);
                                }
                            }

                            // Add vassal fief income if applicable
                            if (VassalBehavior.Current != null)
                            {
                                otherFiefIncome += VassalBehavior.Current.CalculateVassalFiefIncome(otherClan);
                            }

                            if (otherFiefIncome > 0)
                            {
                                totalTaxRevenue += (int)(otherFiefIncome * taxRate);
                            }
                        }

                        totalIncome += totalFiefIncome + totalTaxRevenue;
                        sb.Append($"Tax revenue ({(taxRate * 100f):F1}%): +{totalTaxRevenue} | ");
                    }
                    else
                    {
                        totalIncome += totalFiefIncome;
                    }
                }
                else
                {
                    // Apply tax if in a kingdom and not ruling clan
                    if (KingdomTaxBehavior.Current != null && clan.Kingdom != null)
                    {
                        float taxRate = KingdomTaxBehavior.Current.GetKingdomTaxRate(clan.Kingdom);
                        if (taxRate > 0f)
                        {
                            var taxResult = KingdomTaxBehavior.Current.CalculateTax(clan, totalFiefIncome);
                            int taxAmount = taxResult.taxAmount;
                            totalFiefIncome = taxResult.incomeAfterTax;
                            sb.Append($"Tax ({(taxRate * 100f):F1}%): -{taxAmount} | ");
                        }
                    }

                    totalIncome += totalFiefIncome;
                }
            }

            // === MERCENARY INCOME ===
            if (BLTAdoptAHeroModule.CommonConfig.MercenaryIncomeEnabled && clan.IsUnderMercenaryService)
            {
                int mercIncome = (int)(CalculateMercenaryIncome(clan) * UpgradeBehavior.Current.GetPercentClanMercBonus(clan));
                int bonusMerc = (int)(UpgradeBehavior.Current.GetFlatMercBonus(clan.Leader) * UpgradeBehavior.Current.GetPercentClanMercBonus(clan));

                int vassalMercIncome = 0;
                if (VassalBehavior.Current != null)
                {
                    vassalMercIncome = VassalBehavior.Current.CalculateVassalMercenaryBonus(clan);
                }

                int totalMercIncome = mercIncome + bonusMerc + vassalMercIncome;
                totalIncome += totalMercIncome;

                sb.Append($"Mercenary: +{mercIncome}{(bonusMerc > 0 ? $"(+{bonusMerc})" : "")}");
                if (vassalMercIncome > 0)
                {
                    sb.Append($" | Vassal contracts: +{vassalMercIncome}");
                }
                sb.Append(" | ");
            }

            // === TRIBUTE INCOME ===
            if (tributeIncome > 0)
            {
                totalIncome += tributeIncome;
                sb.Append($"Tributes received: +{tributeIncome} | ");
            }

            // === TRIBUTE PAYMENTS ===
            if (tributePayments > 0)
            {
                totalIncome -= tributePayments;
                sb.Append($"Tributes paid: -{tributePayments} | ");
            }

            // === TOTAL ===
            var result = sb.ToString().TrimEnd(' ', '|');
            result += $" | Total: {(totalIncome >= 0 ? "+" : "")}{totalIncome}/day";

            onSuccess(result);
        }

        // === HELPER METHODS ===

        internal static int CalculateSettlementIncome(Settlement settlement)
        {
            if (settlement == null)
                return 0;

            int income = 0;

            if (settlement.IsTown)
                income += BLTAdoptAHeroModule.CommonConfig.TownBaseGold;
            else if (settlement.IsCastle)
                income += BLTAdoptAHeroModule.CommonConfig.CastleBaseGold;
            else
                return 0;

            if (BLTAdoptAHeroModule.CommonConfig.IncludeProsperity)
            {
                income += (int)(settlement.Town.Prosperity *
                    BLTAdoptAHeroModule.CommonConfig.ProsperityMultiplier);
            }

            return income;
        }

        internal static int CalculateMercenaryIncome(Clan clan)
        {
            if (clan == null || !clan.IsUnderMercenaryService)
                return 0;

            int mult = Math.Min(BLTAdoptAHeroModule.CommonConfig.MercenaryMultiplier, 100);
            var creator = Campaign.Current.KingdomManager;
            int contract = Math.Max((int)((double)creator.GetMercenaryWageAmount(clan.Leader) * 0.2), clan.MercenaryAwardMultiplier);

            if (contract <= 0)
                return 0;

            // Multiply may be large, keep in int (Bannerlord uses int gold)
            long value = (long)contract * (long)mult;

            return Math.Min(Math.Min((int)value, BLTAdoptAHeroModule.CommonConfig.MercenaryMaxIncome), int.MaxValue);
        }

        /// <summary>
        /// Calculate total daily tribute income received by this clan (includes all tributes)
        /// </summary>
        internal static int CalculateTributeIncome(Clan clan)
        {
            if (clan?.Kingdom == null || BLTTreatyManager.Current == null)
                return 0;

            // Only calculate for BLT leaders
            if (clan.Leader == null || !clan.Leader.IsAdopted())
                return 0;

            int totalIncome = 0;
            var tributesReceiving = BLTTreatyManager.Current.GetTributesReceivedBy(clan.Kingdom);

            foreach (var tribute in tributesReceiving)
            {
                if (!tribute.IsExpired())
                {
                    totalIncome += tribute.DailyAmount;
                    // Note: AI kingdoms pay game gold only, BLT kingdoms pay BLT gold
                    // Both are shown here for transparency
                }
            }

            return totalIncome;
        }

        /// <summary>
        /// Calculate total daily tribute payments made by this clan
        /// </summary>
        internal static int CalculateTributePayments(Clan clan)
        {
            if (clan?.Kingdom == null || BLTTreatyManager.Current == null)
                return 0;

            // Only calculate for BLT leaders
            if (clan.Leader == null || !clan.Leader.IsAdopted())
                return 0;

            int totalPayments = 0;
            var tributesPaying = BLTTreatyManager.Current.GetTributesPayedBy(clan.Kingdom);

            foreach (var tribute in tributesPaying)
            {
                if (!tribute.IsExpired())
                {
                    totalPayments += tribute.DailyAmount;
                }
            }

            return totalPayments;
        }
    }
}