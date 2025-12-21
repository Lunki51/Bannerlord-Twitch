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
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

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
                ShowFiefIncome(fiefs, onSuccess);
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

        private void ShowFiefIncome(List<Settlement> settlements, Action<string> onSuccess)
        {
            var sb = new StringBuilder();
            int totalIncome = 0;

            foreach (var s in settlements)
            {
                int income = CalculateSettlementIncome(s);
                totalIncome += income;
                sb.Append($"{s.Name}: {(income >= 0 ? "+" : "")}{income} | ");
            }

            var result = sb.ToString().TrimEnd(' ', '|');
            result += $" | Total: {(totalIncome >= 0 ? "+" : "")}{totalIncome}/day";

            onSuccess(result);
        }

        private void ShowMercIncome(Clan clan, Action<string> onSuccess)
        {
            int income = CalculateMercenaryIncome(clan);
            onSuccess($"Mercenary contract income: {(income >= 0 ? "+" : "")}{income}/day");
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
            int contract = creator.GetMercenaryWageAmount(clan.Leader);

            if (contract <= 0)
                return 0;

            // Multiply may be large, keep in int (Bannerlord uses int gold)
            long value = (long)contract * (long)mult;
            if (value > BLTAdoptAHeroModule.CommonConfig.MercenaryMaxIncome)
                if (BLTAdoptAHeroModule.CommonConfig.MercenaryMaxIncome < int.MaxValue)
                    return BLTAdoptAHeroModule.CommonConfig.MercenaryMaxIncome;
                else
                {
                    return int.MaxValue;
                }

            return (int)value;
        }
    }
}