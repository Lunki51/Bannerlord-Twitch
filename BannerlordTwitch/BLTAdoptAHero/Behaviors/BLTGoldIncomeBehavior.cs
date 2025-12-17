using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using JetBrains.Annotations;
using BannerlordTwitch.Localization;
using BannerlordTwitch;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using static BLTAdoptAHero.BLTClanBehavior;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Daily BLT gold income behavior.
    /// Settlement ownership + mercenary contract income.
    /// </summary>
    public class BLTGoldIncomeBehavior : CampaignBehaviorBase
    {
        public static Settings settings { get; private set; } = new Settings();
        public KingdomManager kingdomManager { get; } = new KingdomManager();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(
                this, OnDailyTickClan);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Config is handled by BLT config system, not save data
        }

        private void OnDailyTickClan(Clan clan)
        {
            if (clan == null)
                return;

            Hero leader = clan.Leader;
            if (leader == null || !leader.IsAdopted())
                return;

            // Settlement-based income
            if (settings.EnableSettlementIncome)
            {
                int settlementGold = CalculateSettlementGold(clan);
                if (settlementGold > 0)
                {
                    BLTAdoptAHeroCampaignBehavior.Current
                        .ChangeHeroGold(leader, settlementGold, false);
                }
            }

            // Mercenary-based income
            if (settings.EnableMercenaryIncome)
            {
                int mercGold = CalculateMercenaryGold(clan);
                if (mercGold > 0)
                {
                    BLTAdoptAHeroCampaignBehavior.Current
                        .ChangeHeroGold(leader, mercGold, false);
                }
            }
        }

        // -------------------------
        // Settlement Income Logic
        // -------------------------

        private int CalculateSettlementGold(Clan clan)
        {
            if (clan.Settlements == null || clan.Settlements.Count == 0)
                return 0;

            int total = 0;

            foreach (Settlement settlement in clan.Settlements)
            {
                if (settlement == null)
                    continue;

                if (settlement.IsTown)
                {
                    total += settings.TownBaseGold;
                }
                else if (settlement.IsCastle)
                {
                    total += settings.CastleBaseGold;
                }
                else
                {
                    continue;
                }

                if (settings.IncludeProsperity)
                {
                    int prosperityBonus =
                        (int)(settlement.Town.Prosperity * settings.ProsperityMultiplier);

                    if (prosperityBonus > 0)
                        total += prosperityBonus;
                }
            }

            return total;
        }

        // -------------------------
        // Mercenary Income Logic
        // -------------------------

        private int CalculateMercenaryGold(Clan clan)
        {
            if (!clan.IsUnderMercenaryService)
                return 0;

            int contractValue = kingdomManager.GetMercenaryWageAmount(clan.Leader);
            if (contractValue <= 0)
                return 0;

            int multiplier = (int)MathF.Clamp(settings.MercenaryMultiplier, 1, 100);
            return contractValue * multiplier;
        }

        // -------------------------
        // Config
        // -------------------------

        [UsedImplicitly]
        public class Settings : IDocumentable
        {
            // -------- General --------

            [LocDisplayName("{=BLTGoldEnableSettlement}Enable Settlement Income"),
             LocCategory("General", "{=GeneralCat}General"),
             LocDescription("{=BLTGoldEnableSettlementDesc}Enable daily BLT gold income from owned settlements"),
             PropertyOrder(1), UsedImplicitly]
            public bool EnableSettlementIncome { get; set; } = true;

            [LocDisplayName("{=BLTGoldEnableMerc}Enable Mercenary Income"),
             LocCategory("General", "{=GeneralCat}General"),
             LocDescription("{=BLTGoldEnableMercDesc}Enable daily BLT gold income from mercenary contracts"),
             PropertyOrder(2), UsedImplicitly]
            public bool EnableMercenaryIncome { get; set; } = true;

            // -------- Settlement --------

            [LocDisplayName("{=BLTGoldTownBase}Town Base Gold"),
             LocCategory("Settlement", "{=SettlementCat}Settlement"),
             LocDescription("{=BLTGoldTownBaseDesc}Base BLT gold earned per town per day"),
             PropertyOrder(1), UsedImplicitly]
            public int TownBaseGold { get; set; } = 50;

            [LocDisplayName("{=BLTGoldCastleBase}Castle Base Gold"),
             LocCategory("Settlement", "{=SettlementCat}Settlement"),
             LocDescription("{=BLTGoldCastleBaseDesc}Base BLT gold earned per castle per day"),
             PropertyOrder(2), UsedImplicitly]
            public int CastleBaseGold { get; set; } = 25;

            [LocDisplayName("{=BLTGoldUseProsperity}Include Prosperity"),
             LocCategory("Settlement", "{=SettlementCat}Settlement"),
             LocDescription("{=BLTGoldUseProsperityDesc}Add settlement prosperity to daily BLT gold"),
             PropertyOrder(3), UsedImplicitly]
            public bool IncludeProsperity { get; set; } = true;

            [LocDisplayName("{=BLTGoldProsMult}Prosperity Multiplier"),
             LocCategory("Settlement", "{=SettlementCat}Settlement"),
             LocDescription("{=BLTGoldProsMultDesc}Multiplier applied to settlement prosperity"),
             PropertyOrder(4), UsedImplicitly]
            public float ProsperityMultiplier { get; set; } = 0.01f;

            // -------- Mercenary --------

            [LocDisplayName("{=BLTGoldMercMult}Mercenary Contract Multiplier"),
             LocCategory("Mercenary", "{=MercCat}Mercenary"),
             LocDescription("{=BLTGoldMercMultDesc}Multiplier applied to mercenary contract value (1-100)"),
             PropertyOrder(1), UsedImplicitly]
            public float MercenaryMultiplier { get; set; } = 10;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value($"<strong>Settlement income enabled:</strong> {EnableSettlementIncome}");
                generator.Value($"<strong>Mercenary income enabled:</strong> {EnableMercenaryIncome}");

                if (EnableSettlementIncome)
                {
                    generator.Value($"<strong>Town base gold:</strong> {TownBaseGold}");
                    generator.Value($"<strong>Castle base gold:</strong> {CastleBaseGold}");
                    generator.Value($"<strong>Include prosperity:</strong> {IncludeProsperity}");
                    if (IncludeProsperity)
                        generator.Value($"<strong>Prosperity multiplier:</strong> {ProsperityMultiplier}");
                }

                if (EnableMercenaryIncome)
                {
                    generator.Value($"<strong>Mercenary multiplier:</strong> {MercenaryMultiplier}");
                }
            }
        }
    }
}
