using System.Collections.Generic;
using System;
using System.ComponentModel;
using BannerlordTwitch;
using System.Linq;
using BannerlordTwitch.Localization;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Actions.Upgrades;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions.Upgrades
{
    [CategoryOrder("General", 0),
     CategoryOrder("Transfer", 1),
     CategoryOrder("Settlement Effects", 2),
     CategoryOrder("Clan Effects", 3)]
    public class CapitalConfig : INotifyPropertyChanged, IDocumentable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void OnProp(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // ── General ───────────────────────────────────────────────────────────
        [LocDisplayName("Enabled"), LocCategory("General", "General"),
         LocDescription("Enable the capital system"), PropertyOrder(1), UsedImplicitly]
        public bool Enabled { get; set; } = true;

        [LocDisplayName("Allow List Command"), LocCategory("General", "General"),
         PropertyOrder(2), UsedImplicitly]
        public bool AllowListCommand { get; set; } = true;

        [LocDisplayName("Allow Independent Clans"), LocCategory("General", "General"),
         LocDescription("Allow clans not in any kingdom to designate a capital"),
         PropertyOrder(3), UsedImplicitly]
        public bool AllowIndependentClans { get; set; } = true;

        [LocDisplayName("Require Ruling Clan"), LocCategory("General", "General"),
         LocDescription("Only the kingdom ruler can manage the capital. Independent clans are exempt."),
         PropertyOrder(4), UsedImplicitly]
        public bool RequireRulingClan { get; set; } = true;

        [LocDisplayName("Set Capital Cost"), LocCategory("General", "General"),
         LocDescription("Gold cost to designate the first capital"),
         PropertyOrder(5), UsedImplicitly]
        public int SetCost { get; set; } = 500000;

        // ── Transfer ──────────────────────────────────────────────────────────
        [LocDisplayName("Transfer Cost"), LocCategory("Transfer", "Transfer"),
         LocDescription("Gold cost to begin moving the capital to a new settlement"),
         PropertyOrder(1), UsedImplicitly]
        public int TransferCost { get; set; } = 1000000;

        [LocDisplayName("Transfer Days"), LocCategory("Transfer", "Transfer"),
         LocDescription("In-game days a capital transfer takes to complete"),
         PropertyOrder(2), UsedImplicitly]
        public int TransferDays { get; set; } = 30;

        [LocDisplayName("Cooldown Days"), LocCategory("Transfer", "Transfer"),
         LocDescription("Days capital bonuses are suspended if a transfer is cancelled because the target was lost"),
         PropertyOrder(3), UsedImplicitly]
        public int CooldownDays { get; set; } = 30;

        // ── Settlement Effects ────────────────────────────────────────────────
        [LocDisplayName("Loyalty Daily (Flat)"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(1), UsedImplicitly] public float LoyaltyDailyFlat { get; set; }
        [LocDisplayName("Loyalty Daily (%)"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(2), UsedImplicitly] public float LoyaltyDailyPercent { get; set; }
        [LocDisplayName("Prosperity Daily (Flat)"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(3), UsedImplicitly] public float ProsperityDailyFlat { get; set; }
        [LocDisplayName("Prosperity Daily (%)"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(4), UsedImplicitly] public float ProsperityDailyPercent { get; set; }
        [LocDisplayName("Security Daily (Flat)"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(5), UsedImplicitly] public float SecurityDailyFlat { get; set; }
        [LocDisplayName("Security Daily (%)"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(6), UsedImplicitly] public float SecurityDailyPercent { get; set; }
        [LocDisplayName("Militia Daily (Flat)"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(7), UsedImplicitly] public float MilitiaDailyFlat { get; set; }
        [LocDisplayName("Militia Daily (%)"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(8), UsedImplicitly] public float MilitiaDailyPercent { get; set; }
        [LocDisplayName("Food Daily (Flat)"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(9), UsedImplicitly] public float FoodDailyFlat { get; set; }
        [LocDisplayName("Food Daily (%)"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(10), UsedImplicitly] public float FoodDailyPercent { get; set; }
        [LocDisplayName("Tax Income (Flat)"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(11), UsedImplicitly] public int TaxIncomeFlat { get; set; }
        [LocDisplayName("Tax Income (%)"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(12), UsedImplicitly] public float TaxIncomePercent { get; set; }
        [LocDisplayName("Garrison Capacity Bonus"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(13), UsedImplicitly] public int GarrisonCapacityBonus { get; set; }
        [LocDisplayName("Hearth Daily"), LocCategory("Settlement Effects", "Settlement Effects"), PropertyOrder(14), UsedImplicitly] public float HearthDaily { get; set; }

        // ── Clan Effects ──────────────────────────────────────────────────────
        [LocDisplayName("Party Size Bonus"), LocCategory("Clan Effects", "Clan Effects (Owning Clan)"),
         LocDescription("Bonus applied to all parties of the clan that owns the capital"),
         PropertyOrder(1), UsedImplicitly]
        public int PartySizeBonus { get; set; }

        [LocDisplayName("Party Speed Bonus"), LocCategory("Clan Effects", "Clan Effects (Owning Clan)"),
         PropertyOrder(2), UsedImplicitly]
        public float PartySpeedBonus { get; set; }

        [LocDisplayName("Renown Daily"), LocCategory("Clan Effects", "Clan Effects (Owning Clan)"),
         PropertyOrder(3), UsedImplicitly]
        public float RenownDaily { get; set; }

        [LocDisplayName("Influence Daily"), LocCategory("Clan Effects", "Clan Effects (Owning Clan)"),
         PropertyOrder(4), UsedImplicitly]
        public float InfluenceDaily { get; set; }

        public void GenerateDocumentation(IDocumentationGenerator gen)
        {
            gen.P($"<strong>Enabled:</strong> {Enabled}");
            gen.P($"<strong>Allow Independent Clans:</strong> {AllowIndependentClans}");
            gen.P($"<strong>Require Ruling Clan:</strong> {RequireRulingClan}");
            gen.P($"<strong>Set Cost:</strong> {SetCost}{Naming.Gold}");
            gen.P($"<strong>Transfer Cost:</strong> {TransferCost}{Naming.Gold}");
            gen.P($"<strong>Transfer Days:</strong> {TransferDays}");
            gen.P($"<strong>Cooldown Days:</strong> {CooldownDays}");
            static string S(float v) => v > 0 ? $"+{v}" : v.ToString();
            if (LoyaltyDailyFlat != 0) gen.P($"Loyalty: {S(LoyaltyDailyFlat)}/day");
            if (LoyaltyDailyPercent != 0) gen.P($"Loyalty: {S(LoyaltyDailyPercent)}%/day");
            if (ProsperityDailyFlat != 0) gen.P($"Prosperity: {S(ProsperityDailyFlat)}/day");
            if (ProsperityDailyPercent != 0) gen.P($"Prosperity: {S(ProsperityDailyPercent)}%/day");
            if (SecurityDailyFlat != 0) gen.P($"Security: {S(SecurityDailyFlat)}/day");
            if (SecurityDailyPercent != 0) gen.P($"Security: {S(SecurityDailyPercent)}%/day");
            if (MilitiaDailyFlat != 0) gen.P($"Militia: {S(MilitiaDailyFlat)}/day");
            if (MilitiaDailyPercent != 0) gen.P($"Militia: {S(MilitiaDailyPercent)}%/day");
            if (FoodDailyFlat != 0) gen.P($"Food: {S(FoodDailyFlat)}/day");
            if (FoodDailyPercent != 0) gen.P($"Food: {S(FoodDailyPercent)}%/day");
            if (TaxIncomeFlat != 0) gen.P($"Tax Income: {S(TaxIncomeFlat)}{Naming.Gold}/day");
            if (TaxIncomePercent != 0) gen.P($"Tax Income: {S(TaxIncomePercent)}%");
            if (GarrisonCapacityBonus != 0) gen.P($"Garrison Cap: {S(GarrisonCapacityBonus)}");
            if (HearthDaily != 0) gen.P($"Hearth: {S(HearthDaily)}/day");
            if (PartySizeBonus != 0) gen.P($"Party Size: {S(PartySizeBonus)}");
            if (PartySpeedBonus != 0) gen.P($"Party Speed: {S(PartySpeedBonus)}");
            if (RenownDaily != 0) gen.P($"Renown: {S(RenownDaily)}/day");
            if (InfluenceDaily != 0) gen.P($"Influence: {S(InfluenceDaily)}/day");
        }
    }
}