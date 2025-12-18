using System;
using BannerlordTwitch.Localization;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions.Upgrades
{
    /// <summary>
    /// Base class for all upgrade types
    /// </summary>
    public abstract class UpgradeBase
    {
        [LocDisplayName("{=BLT_UpgradeID}Upgrade ID"),
         LocDescription("{=BLT_UpgradeIDDesc}Unique identifier for this upgrade (use for tiered upgrades)"),
         PropertyOrder(1), UsedImplicitly]
        public string ID { get; set; } = "";

        [LocDisplayName("{=BLT_UpgradeName}Upgrade Name"),
         LocDescription("{=BLT_UpgradeNameDesc}Display name shown to players"),
         PropertyOrder(2), UsedImplicitly, InstanceName]
        public string Name { get; set; } = "New Upgrade";

        [LocDisplayName("{=BLT_UpgradeDesc}Description"),
         LocDescription("{=BLT_UpgradeDescDesc}Description of what this upgrade does"),
         PropertyOrder(3), UsedImplicitly]
        public string Description { get; set; } = "Upgrade description";

        [LocDisplayName("{=BLT_UpgradeTier}Tier Level"),
         LocDescription("{=BLT_UpgradeTierDesc}Tier level (0 or 1 for non-tiered, 2+ for higher tiers)"),
         PropertyOrder(4), UsedImplicitly]
        public int TierLevel { get; set; } = 0;

        [LocDisplayName("{=BLT_UpgradeRequired}Required Upgrade ID"),
         LocDescription("{=BLT_UpgradeRequiredDesc}ID of upgrade required before this can be purchased (for tiered upgrades, leave empty for tier 1)"),
         PropertyOrder(5), UsedImplicitly]
        public string RequiredUpgradeID { get; set; } = "";

        [LocDisplayName("{=BLT_UpgradeGoldCost}Gold Cost"),
         LocDescription("{=BLT_UpgradeGoldCostDesc}Cost in gold to purchase this upgrade"),
         PropertyOrder(6), UsedImplicitly]
        public int GoldCost { get; set; } = 10000;

        public virtual string GetCostString()
        {
            return $"{GoldCost}{Naming.Gold}";
        }

        public virtual string GetFullDescription()
        {
            string desc = $"{Name}";
            if (TierLevel > 1)
                desc += $" (Tier {TierLevel})";
            desc += $"\n{Description}\nCost: {GetCostString()}";
            if (!string.IsNullOrEmpty(RequiredUpgradeID))
                desc += $"\nRequires: {RequiredUpgradeID}";
            return desc;
        }
    }

    /// <summary>
    /// Fief (Settlement) upgrade - affects a single town or castle
    /// </summary>
    [CategoryOrder("General", 0),
     CategoryOrder("Daily Growth Effects", 1),
     CategoryOrder("Static Bonuses", 2)]
    public class FiefUpgrade : UpgradeBase
    {
        // Daily growth modifiers (applied each day)
        [LocDisplayName("{=BLT_LoyaltyFlat}Loyalty Daily (Flat)"),
         LocCategory("Daily Growth Effects", "{=BLT_DailyGrowth}Daily Growth Effects"),
         LocDescription("{=BLT_LoyaltyFlatDesc}Flat loyalty gain per day (e.g., +0.5 loyalty per day)"),
         PropertyOrder(1), UsedImplicitly]
        public float LoyaltyDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_LoyaltyPercent}Loyalty Daily (%)"),
         LocCategory("Daily Growth Effects", "{=BLT_DailyGrowth}Daily Growth Effects"),
         LocDescription("{=BLT_LoyaltyPercentDesc}Percentage bonus to loyalty change per day (e.g., 10 = +10% of natural change)"),
         PropertyOrder(2), UsedImplicitly]
        public float LoyaltyDailyPercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_ProsperityFlat}Prosperity Daily (Flat)"),
         LocCategory("Daily Growth Effects", "{=BLT_DailyGrowth}Daily Growth Effects"),
         LocDescription("{=BLT_ProsperityFlatDesc}Flat prosperity gain per day"),
         PropertyOrder(3), UsedImplicitly]
        public float ProsperityDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_ProsperityPercent}Prosperity Daily (%)"),
         LocCategory("Daily Growth Effects", "{=BLT_DailyGrowth}Daily Growth Effects"),
         LocDescription("{=BLT_ProsperityPercentDesc}Percentage bonus to prosperity change per day"),
         PropertyOrder(4), UsedImplicitly]
        public float ProsperityDailyPercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_SecurityFlat}Security Daily (Flat)"),
         LocCategory("Daily Growth Effects", "{=BLT_DailyGrowth}Daily Growth Effects"),
         LocDescription("{=BLT_SecurityFlatDesc}Flat security gain per day"),
         PropertyOrder(5), UsedImplicitly]
        public float SecurityDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_SecurityPercent}Security Daily (%)"),
         LocCategory("Daily Growth Effects", "{=BLT_DailyGrowth}Daily Growth Effects"),
         LocDescription("{=BLT_SecurityPercentDesc}Percentage bonus to security change per day"),
         PropertyOrder(6), UsedImplicitly]
        public float SecurityDailyPercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_MilitiaFlat}Militia Daily (Flat)"),
         LocCategory("Daily Growth Effects", "{=BLT_DailyGrowth}Daily Growth Effects"),
         LocDescription("{=BLT_MilitiaFlatDesc}Flat militia gain per day"),
         PropertyOrder(7), UsedImplicitly]
        public float MilitiaDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_MilitiaPercent}Militia Daily (%)"),
         LocCategory("Daily Growth Effects", "{=BLT_DailyGrowth}Daily Growth Effects"),
         LocDescription("{=BLT_MilitiaPercentDesc}Percentage bonus to militia change per day"),
         PropertyOrder(8), UsedImplicitly]
        public float MilitiaDailyPercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_FoodFlat}Food Daily (Flat)"),
         LocCategory("Daily Growth Effects", "{=BLT_DailyGrowth}Daily Growth Effects"),
         LocDescription("{=BLT_FoodFlatDesc}Flat food stock gain per day"),
         PropertyOrder(9), UsedImplicitly]
        public float FoodDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_FoodPercent}Food Daily (%)"),
         LocCategory("Daily Growth Effects", "{=BLT_DailyGrowth}Daily Growth Effects"),
         LocDescription("{=BLT_FoodPercentDesc}Percentage bonus to food change per day"),
         PropertyOrder(10), UsedImplicitly]
        public float FoodDailyPercent { get; set; } = 0f;

        // Static bonuses
        [LocDisplayName("{=BLT_TaxFlat}Tax Income (Flat)"),
         LocCategory("Static Bonuses", "{=BLT_StaticBonuses}Static Bonuses"),
         LocDescription("{=BLT_TaxFlatDesc}Flat daily gold bonus from taxes"),
         PropertyOrder(1), UsedImplicitly]
        public int TaxIncomeFlat { get; set; } = 0;

        [LocDisplayName("{=BLT_TaxPercent}Tax Income (%)"),
         LocCategory("Static Bonuses", "{=BLT_StaticBonuses}Static Bonuses"),
         LocDescription("{=BLT_TaxPercentDesc}Percentage bonus to tax income"),
         PropertyOrder(2), UsedImplicitly]
        public float TaxIncomePercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_GarrisonCap}Garrison Capacity Bonus"),
         LocCategory("Static Bonuses", "{=BLT_StaticBonuses}Static Bonuses"),
         LocDescription("{=BLT_GarrisonCapDesc}Additional garrison troop capacity (Warning: High values may cause issues)"),
         PropertyOrder(3), UsedImplicitly]
        public int GarrisonCapacityBonus { get; set; } = 0;

        public override string GetFullDescription()
        {
            string desc = base.GetFullDescription();
            desc += "\n\nEffects:";

            if (LoyaltyDailyFlat != 0) desc += $"\n  Loyalty: {(LoyaltyDailyFlat > 0 ? "+" : "")}{LoyaltyDailyFlat}/day";
            if (LoyaltyDailyPercent != 0) desc += $"\n  Loyalty: {(LoyaltyDailyPercent > 0 ? "+" : "")}{LoyaltyDailyPercent}%/day";
            if (ProsperityDailyFlat != 0) desc += $"\n  Prosperity: {(ProsperityDailyFlat > 0 ? "+" : "")}{ProsperityDailyFlat}/day";
            if (ProsperityDailyPercent != 0) desc += $"\n  Prosperity: {(ProsperityDailyPercent > 0 ? "+" : "")}{ProsperityDailyPercent}%/day";
            if (SecurityDailyFlat != 0) desc += $"\n  Security: {(SecurityDailyFlat > 0 ? "+" : "")}{SecurityDailyFlat}/day";
            if (SecurityDailyPercent != 0) desc += $"\n  Security: {(SecurityDailyPercent > 0 ? "+" : "")}{SecurityDailyPercent}%/day";
            if (MilitiaDailyFlat != 0) desc += $"\n  Militia: {(MilitiaDailyFlat > 0 ? "+" : "")}{MilitiaDailyFlat}/day";
            if (MilitiaDailyPercent != 0) desc += $"\n  Militia: {(MilitiaDailyPercent > 0 ? "+" : "")}{MilitiaDailyPercent}%/day";
            if (FoodDailyFlat != 0) desc += $"\n  Food: {(FoodDailyFlat > 0 ? "+" : "")}{FoodDailyFlat}/day";
            if (FoodDailyPercent != 0) desc += $"\n  Food: {(FoodDailyPercent > 0 ? "+" : "")}{FoodDailyPercent}%/day";
            if (TaxIncomeFlat != 0) desc += $"\n  Tax Income: {(TaxIncomeFlat > 0 ? "+" : "")}{TaxIncomeFlat}{Naming.Gold}/day";
            if (TaxIncomePercent != 0) desc += $"\n  Tax Income: {(TaxIncomePercent > 0 ? "+" : "")}{TaxIncomePercent}%";
            if (GarrisonCapacityBonus != 0) desc += $"\n  Garrison Capacity: {(GarrisonCapacityBonus > 0 ? "+" : "")}{GarrisonCapacityBonus}";

            return desc;
        }
    }

    /// <summary>
    /// Clan upgrade - affects the clan and all its settlements
    /// </summary>
    [CategoryOrder("General", 0),
     CategoryOrder("Clan Effects", 1),
     CategoryOrder("Settlement Effects", 2)]
    public class ClanUpgrade : UpgradeBase
    {
        // Clan-specific effects
        [LocDisplayName("{=BLT_RenownDaily}Renown Daily"),
         LocCategory("Clan Effects", "{=BLT_ClanEffects}Clan Effects"),
         LocDescription("{=BLT_RenownDailyDesc}Renown gained per day for the clan"),
         PropertyOrder(1), UsedImplicitly]
        public float RenownDaily { get; set; } = 0f;

        [LocDisplayName("{=BLT_PartySize}Party Size Bonus"),
         LocCategory("Clan Effects", "{=BLT_ClanEffects}Clan Effects"),
         LocDescription("{=BLT_PartySizeDesc}Additional party size limit for all clan parties"),
         PropertyOrder(2), UsedImplicitly]
        public int PartySizeBonus { get; set; } = 0;

        // Settlement effects (applied to all clan settlements)
        [LocDisplayName("{=BLT_LoyaltyFlat}Loyalty Daily (Flat)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Clan Settlements)"),
         LocDescription("{=BLT_LoyaltyFlatDesc}Flat loyalty gain per day for all clan settlements"),
         PropertyOrder(1), UsedImplicitly]
        public float LoyaltyDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_LoyaltyPercent}Loyalty Daily (%)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Clan Settlements)"),
         LocDescription("{=BLT_LoyaltyPercentDesc}Percentage bonus to loyalty change per day for all clan settlements"),
         PropertyOrder(2), UsedImplicitly]
        public float LoyaltyDailyPercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_ProsperityFlat}Prosperity Daily (Flat)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Clan Settlements)"),
         LocDescription("{=BLT_ProsperityFlatDesc}Flat prosperity gain per day for all clan settlements"),
         PropertyOrder(3), UsedImplicitly]
        public float ProsperityDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_ProsperityPercent}Prosperity Daily (%)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Clan Settlements)"),
         LocDescription("{=BLT_ProsperityPercentDesc}Percentage bonus to prosperity change per day for all clan settlements"),
         PropertyOrder(4), UsedImplicitly]
        public float ProsperityDailyPercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_SecurityFlat}Security Daily (Flat)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Clan Settlements)"),
         LocDescription("{=BLT_SecurityFlatDesc}Flat security gain per day for all clan settlements"),
         PropertyOrder(5), UsedImplicitly]
        public float SecurityDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_SecurityPercent}Security Daily (%)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Clan Settlements)"),
         LocDescription("{=BLT_SecurityPercentDesc}Percentage bonus to security change per day for all clan settlements"),
         PropertyOrder(6), UsedImplicitly]
        public float SecurityDailyPercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_MilitiaFlat}Militia Daily (Flat)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Clan Settlements)"),
         LocDescription("{=BLT_MilitiaFlatDesc}Flat militia gain per day for all clan settlements"),
         PropertyOrder(7), UsedImplicitly]
        public float MilitiaDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_MilitiaPercent}Militia Daily (%)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Clan Settlements)"),
         LocDescription("{=BLT_MilitiaPercentDesc}Percentage bonus to militia change per day for all clan settlements"),
         PropertyOrder(8), UsedImplicitly]
        public float MilitiaDailyPercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_TaxFlat}Tax Income (Flat)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Clan Settlements)"),
         LocDescription("{=BLT_TaxFlatDesc}Flat daily gold bonus from taxes for all clan settlements"),
         PropertyOrder(9), UsedImplicitly]
        public int TaxIncomeFlat { get; set; } = 0;

        [LocDisplayName("{=BLT_TaxPercent}Tax Income (%)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Clan Settlements)"),
         LocDescription("{=BLT_TaxPercentDesc}Percentage bonus to tax income for all clan settlements"),
         PropertyOrder(10), UsedImplicitly]
        public float TaxIncomePercent { get; set; } = 0f;

        public override string GetFullDescription()
        {
            string desc = base.GetFullDescription();
            desc += "\n\nClan Effects:";

            if (RenownDaily != 0) desc += $"\n  Renown: {(RenownDaily > 0 ? "+" : "")}{RenownDaily}/day";
            if (PartySizeBonus != 0) desc += $"\n  Party Size: {(PartySizeBonus > 0 ? "+" : "")}{PartySizeBonus}";

            desc += "\n\nSettlement Effects (All Clan Settlements):";
            if (LoyaltyDailyFlat != 0) desc += $"\n  Loyalty: {(LoyaltyDailyFlat > 0 ? "+" : "")}{LoyaltyDailyFlat}/day";
            if (LoyaltyDailyPercent != 0) desc += $"\n  Loyalty: {(LoyaltyDailyPercent > 0 ? "+" : "")}{LoyaltyDailyPercent}%/day";
            if (ProsperityDailyFlat != 0) desc += $"\n  Prosperity: {(ProsperityDailyFlat > 0 ? "+" : "")}{ProsperityDailyFlat}/day";
            if (ProsperityDailyPercent != 0) desc += $"\n  Prosperity: {(ProsperityDailyPercent > 0 ? "+" : "")}{ProsperityDailyPercent}%/day";
            if (SecurityDailyFlat != 0) desc += $"\n  Security: {(SecurityDailyFlat > 0 ? "+" : "")}{SecurityDailyFlat}/day";
            if (SecurityDailyPercent != 0) desc += $"\n  Security: {(SecurityDailyPercent > 0 ? "+" : "")}{SecurityDailyPercent}%/day";
            if (MilitiaDailyFlat != 0) desc += $"\n  Militia: {(MilitiaDailyFlat > 0 ? "+" : "")}{MilitiaDailyFlat}/day";
            if (MilitiaDailyPercent != 0) desc += $"\n  Militia: {(MilitiaDailyPercent > 0 ? "+" : "")}{MilitiaDailyPercent}%/day";
            if (TaxIncomeFlat != 0) desc += $"\n  Tax Income: {(TaxIncomeFlat > 0 ? "+" : "")}{TaxIncomeFlat}{Naming.Gold}/day per settlement";
            if (TaxIncomePercent != 0) desc += $"\n  Tax Income: {(TaxIncomePercent > 0 ? "+" : "")}{TaxIncomePercent}%";

            return desc;
        }
    }

    /// <summary>
    /// Kingdom upgrade - affects the kingdom, all its clans, and all settlements
    /// </summary>
    [CategoryOrder("General", 0),
     CategoryOrder("Kingdom Effects", 1),
     CategoryOrder("Clan Effects", 2),
     CategoryOrder("Settlement Effects", 3)]
    public class KingdomUpgrade : UpgradeBase
    {
        [LocDisplayName("{=BLT_InfluenceCost}Influence Cost"),
         LocCategory("General", "{=BLT_General}General"),
         LocDescription("{=BLT_InfluenceCostDesc}Cost in influence to purchase this upgrade (in addition to gold)"),
         PropertyOrder(7), UsedImplicitly]
        public int InfluenceCost { get; set; } = 0;

        // Kingdom-specific effects
        [LocDisplayName("{=BLT_InfluenceDaily}Influence Daily"),
         LocCategory("Kingdom Effects", "{=BLT_KingdomEffects}Kingdom Effects"),
         LocDescription("{=BLT_InfluenceDailyDesc}Influence gained per day for the kingdom ruler"),
         PropertyOrder(1), UsedImplicitly]
        public float InfluenceDaily { get; set; } = 0f;

        // Clan effects (applied to all kingdom clans)
        [LocDisplayName("{=BLT_RenownDaily}Renown Daily"),
         LocCategory("Clan Effects", "{=BLT_ClanEffects}Clan Effects (All Kingdom Clans)"),
         LocDescription("{=BLT_RenownDailyDesc}Renown gained per day for all clans in the kingdom"),
         PropertyOrder(1), UsedImplicitly]
        public float RenownDaily { get; set; } = 0f;

        [LocDisplayName("{=BLT_PartySize}Party Size Bonus"),
         LocCategory("Clan Effects", "{=BLT_ClanEffects}Clan Effects (All Kingdom Clans)"),
         LocDescription("{=BLT_PartySizeDesc}Additional party size limit for all kingdom parties"),
         PropertyOrder(2), UsedImplicitly]
        public int PartySizeBonus { get; set; } = 0;

        // Settlement effects (applied to all kingdom settlements)
        [LocDisplayName("{=BLT_LoyaltyFlat}Loyalty Daily (Flat)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Kingdom Settlements)"),
         LocDescription("{=BLT_LoyaltyFlatDesc}Flat loyalty gain per day for all kingdom settlements"),
         PropertyOrder(1), UsedImplicitly]
        public float LoyaltyDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_LoyaltyPercent}Loyalty Daily (%)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Kingdom Settlements)"),
         LocDescription("{=BLT_LoyaltyPercentDesc}Percentage bonus to loyalty change per day for all kingdom settlements"),
         PropertyOrder(2), UsedImplicitly]
        public float LoyaltyDailyPercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_ProsperityFlat}Prosperity Daily (Flat)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Kingdom Settlements)"),
         LocDescription("{=BLT_ProsperityFlatDesc}Flat prosperity gain per day for all kingdom settlements"),
         PropertyOrder(3), UsedImplicitly]
        public float ProsperityDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_ProsperityPercent}Prosperity Daily (%)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Kingdom Settlements)"),
         LocDescription("{=BLT_ProsperityPercentDesc}Percentage bonus to prosperity change per day for all kingdom settlements"),
         PropertyOrder(4), UsedImplicitly]
        public float ProsperityDailyPercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_SecurityFlat}Security Daily (Flat)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Kingdom Settlements)"),
         LocDescription("{=BLT_SecurityFlatDesc}Flat security gain per day for all kingdom settlements"),
         PropertyOrder(5), UsedImplicitly]
        public float SecurityDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_SecurityPercent}Security Daily (%)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Kingdom Settlements)"),
         LocDescription("{=BLT_SecurityPercentDesc}Percentage bonus to security change per day for all kingdom settlements"),
         PropertyOrder(6), UsedImplicitly]
        public float SecurityDailyPercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_MilitiaFlat}Militia Daily (Flat)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Kingdom Settlements)"),
         LocDescription("{=BLT_MilitiaFlatDesc}Flat militia gain per day for all kingdom settlements"),
         PropertyOrder(7), UsedImplicitly]
        public float MilitiaDailyFlat { get; set; } = 0f;

        [LocDisplayName("{=BLT_MilitiaPercent}Militia Daily (%)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Kingdom Settlements)"),
         LocDescription("{=BLT_MilitiaPercentDesc}Percentage bonus to militia change per day for all kingdom settlements"),
         PropertyOrder(8), UsedImplicitly]
        public float MilitiaDailyPercent { get; set; } = 0f;

        [LocDisplayName("{=BLT_TaxFlat}Tax Income (Flat)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Kingdom Settlements)"),
         LocDescription("{=BLT_TaxFlatDesc}Flat daily gold bonus from taxes for all kingdom settlements"),
         PropertyOrder(9), UsedImplicitly]
        public int TaxIncomeFlat { get; set; } = 0;

        [LocDisplayName("{=BLT_TaxPercent}Tax Income (%)"),
         LocCategory("Settlement Effects", "{=BLT_SettlementEffects}Settlement Effects (All Kingdom Settlements)"),
         LocDescription("{=BLT_TaxPercentDesc}Percentage bonus to tax income for all kingdom settlements"),
         PropertyOrder(10), UsedImplicitly]
        public float TaxIncomePercent { get; set; } = 0f;

        public override string GetCostString()
        {
            string cost = $"{GoldCost}{Naming.Gold}";
            if (InfluenceCost > 0)
                cost += $" + {InfluenceCost} Influence";
            return cost;
        }

        public override string GetFullDescription()
        {
            string desc = base.GetFullDescription();

            desc += "\n\nKingdom Effects:";
            if (InfluenceDaily != 0) desc += $"\n  Influence: {(InfluenceDaily > 0 ? "+" : "")}{InfluenceDaily}/day (ruler only)";

            desc += "\n\nClan Effects (All Kingdom Clans):";
            if (RenownDaily != 0) desc += $"\n  Renown: {(RenownDaily > 0 ? "+" : "")}{RenownDaily}/day per clan";
            if (PartySizeBonus != 0) desc += $"\n  Party Size: {(PartySizeBonus > 0 ? "+" : "")}{PartySizeBonus}";

            desc += "\n\nSettlement Effects (All Kingdom Settlements):";
            if (LoyaltyDailyFlat != 0) desc += $"\n  Loyalty: {(LoyaltyDailyFlat > 0 ? "+" : "")}{LoyaltyDailyFlat}/day";
            if (LoyaltyDailyPercent != 0) desc += $"\n  Loyalty: {(LoyaltyDailyPercent > 0 ? "+" : "")}{LoyaltyDailyPercent}%/day";
            if (ProsperityDailyFlat != 0) desc += $"\n  Prosperity: {(ProsperityDailyFlat > 0 ? "+" : "")}{ProsperityDailyFlat}/day";
            if (ProsperityDailyPercent != 0) desc += $"\n  Prosperity: {(ProsperityDailyPercent > 0 ? "+" : "")}{ProsperityDailyPercent}%/day";
            if (SecurityDailyFlat != 0) desc += $"\n  Security: {(SecurityDailyFlat > 0 ? "+" : "")}{SecurityDailyFlat}/day";
            if (SecurityDailyPercent != 0) desc += $"\n  Security: {(SecurityDailyPercent > 0 ? "+" : "")}{SecurityDailyPercent}%/day";
            if (MilitiaDailyFlat != 0) desc += $"\n  Militia: {(MilitiaDailyFlat > 0 ? "+" : "")}{MilitiaDailyFlat}/day";
            if (MilitiaDailyPercent != 0) desc += $"\n  Militia: {(MilitiaDailyPercent > 0 ? "+" : "")}{MilitiaDailyPercent}%/day";
            if (TaxIncomeFlat != 0) desc += $"\n  Tax Income: {(TaxIncomeFlat > 0 ? "+" : "")}{TaxIncomeFlat}{Naming.Gold}/day per settlement";
            if (TaxIncomePercent != 0) desc += $"\n  Tax Income: {(TaxIncomePercent > 0 ? "+" : "")}{TaxIncomePercent}%";

            return desc;
        }
    }
}