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
            if (fiefs != null && fiefs.Count > 0)
            {
                ShowFiefIncome(clan, fiefs, onSuccess);
                return;
            }

            // If no fiefs, check for mercenary contract
            if (clan.IsUnderMercenaryService)
            {
                ShowMercIncome(clan, onSuccess);
                return;
            }

            // No income sources
            onSuccess("You have no income sources (no settlements or mercenary contract).");
        }

        private void ShowFiefIncome(Clan masterclan, List<Settlement> settlements, Action<string> onSuccess)
        {
            var sb = new StringBuilder();
            int totalIncome = 0;
            int vassalincome;
            if (VassalBehavior.Current != null)
            {
                vassalincome = VassalBehavior.Current.CalculateVassalFiefIncome(masterclan);
            }
            else
            {
                vassalincome = 0;
            }

            foreach (var s in settlements)
            {
                int income = CalculateSettlementIncome(s);
                totalIncome += income;
                sb.Append($"{s.Name}: {(income >= 0 ? "+" : "")}{income} | ");
            }

            int totalBeforeTax = totalIncome + vassalincome;

            var result = sb.ToString().TrimEnd(' ', '|');
            result += $" | Total income from Vassals' fiefs: {(vassalincome >= 0 ? "+" : "")}{vassalincome}/day";

            // Check if this is the ruling clan
            bool isRulingClan = masterclan.Kingdom != null && masterclan.Kingdom.RulingClan == masterclan;

            if (isRulingClan && KingdomTaxBehavior.Current != null && masterclan.Kingdom != null)
            {
                // Calculate total tax revenue from all kingdom clans
                float taxRate = KingdomTaxBehavior.Current.GetKingdomTaxRate(masterclan.Kingdom);
                if (taxRate > 0f)
                {
                    int totalTaxRevenue = 0;

                    foreach (var clan in masterclan.Kingdom.Clans)
                    {
                        if (clan == masterclan || clan == null)
                            continue;

                        // Calculate this clan's fief income
                        int fiefIncome = 0;
                        if (clan.Settlements != null)
                        {
                            foreach (var settlement in clan.Settlements)
                            {
                                fiefIncome += CalculateSettlementIncome(settlement);
                            }
                        }

                        // Add vassal fief income if applicable
                        if (VassalBehavior.Current != null)
                        {
                            fiefIncome += VassalBehavior.Current.CalculateVassalFiefIncome(clan);
                        }

                        if (fiefIncome > 0)
                        {
                            totalTaxRevenue += (int)(fiefIncome * taxRate);
                        }
                    }

                    result += $" | Tax revenue ({(taxRate * 100f):F1}%): +{totalTaxRevenue}/day";
                }
                else
                {
                    result += " | Tax revenue: +0/day (0% tax)";
                }

                result += $" | Total: {(totalBeforeTax >= 0 ? "+" : "")}{totalBeforeTax}/day";
            }
            else
            {
                // Apply tax if in a kingdom and not ruling clan
                if (KingdomTaxBehavior.Current != null && masterclan.Kingdom != null)
                {
                    float taxRate = KingdomTaxBehavior.Current.GetKingdomTaxRate(masterclan.Kingdom);
                    if (taxRate > 0f)
                    {
                        var taxResult = KingdomTaxBehavior.Current.CalculateTax(masterclan, totalBeforeTax);
                        int taxAmount = taxResult.taxAmount;
                        totalBeforeTax = taxResult.incomeAfterTax;
                        result += $" | Tax ({(taxRate * 100f):F1}%): -{taxAmount}";
                    }
                }

                result += $" | Total after tax: {(totalBeforeTax >= 0 ? "+" : "")}{totalBeforeTax}/day";
            }

            onSuccess(result);
        }

        private void ShowMercIncome(Clan clan, Action<string> onSuccess)
        {
            int income = CalculateMercenaryIncome(clan) - (int)(UpgradeBehavior.Current.GetFlatMercBonus(clan.Leader) * UpgradeBehavior.Current.GetPercentClanMercBonus(clan));
            int bonusmerc = (int)(UpgradeBehavior.Current.GetFlatMercBonus(clan.Leader) * UpgradeBehavior.Current.GetPercentClanMercBonus(clan));
            int vassalincome;
            if (VassalBehavior.Current != null)
            {
                vassalincome = VassalBehavior.Current.CalculateVassalMercenaryBonus(clan);
            }
            else
            {
                vassalincome = 0;
            }
            onSuccess(
                $"Mercenary contract income: {(income >= 0 ? "+" : "")}{income}(+{bonusmerc})/day | " + 
                $"Total income from Vassals' contracts: {(vassalincome >= 0 ? "+" : "")}{vassalincome}/day"
                );
        }

        // Helper methods for income calculation (can be used by behavior)
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
            int MercUpBonus = UpgradeBehavior.Current.GetFlatMercBonus(clan.Leader);
            float MercUpMult = UpgradeBehavior.Current.GetPercentClanMercBonus(clan);
            if (value > BLTAdoptAHeroModule.CommonConfig.MercenaryMaxIncome)
            {
                if (BLTAdoptAHeroModule.CommonConfig.MercenaryMaxIncome < int.MaxValue) 
                {
                    value = BLTAdoptAHeroModule.CommonConfig.MercenaryMaxIncome += MercUpBonus;
                    value = (int)(value * MercUpMult);
                }
                else
                {
                    return int.MaxValue;
                }
            }

            return (int)value;
        }
    }
}