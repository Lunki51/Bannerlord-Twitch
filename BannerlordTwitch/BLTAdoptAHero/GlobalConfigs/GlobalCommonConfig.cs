using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Annotations;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using BannerlordTwitch.Helpers;
using BLTAdoptAHero.Achievements;
using BLTAdoptAHero.Actions.Util;
using BLTAdoptAHero.Actions.Upgrades;
using BLTAdoptAHero.UI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using TaleWorlds.CampaignSystem;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using YamlDotNet.Serialization;
using static BLTAdoptAHero.Actions.UpgradeAction;

namespace BLTAdoptAHero
{
    [CategoryOrder("General", 1),
     CategoryOrder("Training", 2),
     CategoryOrder("Battle", 3),
     CategoryOrder("Death", 4),
     CategoryOrder("Income", 5),
     CategoryOrder("Upgrades", 6),
     CategoryOrder("XP", 7),
     CategoryOrder("Kill Rewards", 8),
     CategoryOrder("Battle End Rewards", 9),
     CategoryOrder("Kill Streak Rewards", 10),
     CategoryOrder("Achievements", 11),
     CategoryOrder("Shouts", 12),
     LocDisplayName("{=vDjnDtoL}Common Config")]
    internal class GlobalCommonConfig : IUpdateFromDefault, IDocumentable, INotifyPropertyChanged
    {
        #region Static
        private const string ID = "Adopt A Hero - General Config";

        internal static void Register() => ActionManager.RegisterGlobalConfigType(ID, typeof(GlobalCommonConfig));
        internal static GlobalCommonConfig Get() => ActionManager.GetGlobalConfig<GlobalCommonConfig>(ID);
        internal static GlobalCommonConfig Get(BannerlordTwitch.Settings fromSettings) => fromSettings.GetGlobalConfig<GlobalCommonConfig>(ID);
        #endregion

        #region User Editable
        #region General
        [LocDisplayName("{=xwcKN7sH}Sub Boost"),
         LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=rX68wbfF}Multiplier applied to all rewards for subscribers (less or equal to 1 means no boost). NOTE: This is only partially implemented, it works for bot commands only currently."),
         PropertyOrder(1), Document, UsedImplicitly,
         Range(0.5, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor))]
        public float SubBoost { get; set; } = 1;

        [LocDisplayName("{=O0LU5WBa}Custom Reward Modifiers"),
         LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=tp3YdGmo}The specification for custom item rewards, applies to tournament prize and achievement rewards"),
         PropertyOrder(2), ExpandableObject, UsedImplicitly]
        public RandomItemModifierDef CustomRewardModifiers { get; set; } = new();

        [LocDisplayName("{=}Custom Inventory Item Limit"),
         LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=}Maximum custom inventory items allowed. This only applies when smithing, other rewards will always be added to inventory (but they will contribute to the limit). If you set this high then inventory management and console spam may become a problem."),
         PropertyOrder(3), UsedImplicitly]
        public int CustomItemLimit { get; set; } = 8;

        [LocDisplayName("{=RstrItm}Restricted Items"),
         LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=RstrItmDesc}Comma-separated list of ItemObject StringIds to exclude from equipment selection in equip, smith, and rewards (e.g., 'battanian_noble_sword,empire_spear_1')"),
         PropertyOrder(4), UsedImplicitly]
        public string RestrictedItems { get; set; } = "";

        [LocDisplayName("{=}Custom Companion Limit"),
         LocCategory("General", "{=C5T6nnix}General"),
         LocDescription("{=}Flat number increase to companion limit"),
         PropertyOrder(5), UsedImplicitly]
        public int CustomCompanionLimit { get; set; } = 7;

        [LocDisplayName("{=}BLT children aging multiplier"),
         LocCategory("General", "{=C5T6nnix}General"),
         LocDescription("{=}Multiplier to BLT children age"),
         PropertyOrder(6), UsedImplicitly]
        public int BLTChildAgeMult { get; set; } = 3;

        [LocDisplayName("{=BLTAdoptAHero_ShowCampaignMap}Show Campaign Map Overlay"),
         LocDescription("{=BLTAdoptAHero_ShowCampaignMap_Desc}Enable or disable the campaign map overlay that shows in the top portion of the overlay. The map automatically hides during missions."),
        LocCategory("General", "{=C5T6nnix}General"),
         PropertyOrder(7)]
        public bool ShowCampaignMapOverlay { get; set; } = true;

        [LocDisplayName("{=BLTAdoptAHero_ShowCampaignMap}Overlay Map Settlement town radius"),
         LocDescription("{=BLTAdoptAHero_ShowCampaignMap_Desc}Overlay Map Settlement town radius"),
        LocCategory("General", "{=C5T6nnix}General"),
         PropertyOrder(8)]
        public float MapTownRadius { get; set; } = 2.15f;

        [LocDisplayName("{=BLTAdoptAHero_ShowCampaignMap}Overlay Map Settlement castle length"),
         LocDescription("{=BLTAdoptAHero_ShowCampaignMap_Desc}Overlay Map Settlement castle length"),
        LocCategory("General", "{=C5T6nnix}General"),
         PropertyOrder(9)]
        public float MapCastleLength { get; set; } = 2.5f;

        [LocDisplayName("{=}Uncap Maximum Foodstocks in Settlements"),
         LocCategory("General", "{=C5T6nnix}General"),
         LocDescription("{=}Enable or disable the vanilla maximum of 300 foodstocks in towns and castles for all settlements."),
         PropertyOrder(10)]
        public bool UncapFoodStocks { get; set; } = false;

        [LocDisplayName("{=}Hearth Per Village Tier"),
         LocCategory("General", "{=C5T6nnix}General"),
         LocDescription("{=}How much hearth is required per village prosperity level (affects food and goods production)."),
         PropertyOrder(11)]
        public float HearthPerVillageTier { get; set; } = 200f;

        [LocDisplayName("{=}Minimum BLT-Led Army Lifetime"),
         LocCategory("General", "{=C5T6nnix}General"),
         LocDescription("{=}Minimum days a BLT-Led army will persist before being allowed to disband."),
         PropertyOrder(12)]
        public float BLTArmyMinLifetimeDays { get; set; } = 30f;

        [LocDisplayName("{=}Lock BLT Army Cohesion"),
         LocCategory("General", "{=C5T6nnix}General"),
         LocDescription("{=}When enabled, (standard) armies led by adopted heroes also have their cohesion locked at 100 and are exempt from automatic dispersion checks. 'Mercenary' armies always have this applied regardless."),
         PropertyOrder(13), UsedImplicitly]
        public bool LockBLTArmyCohesion { get; set; } = true;

        [LocDisplayName("{=}Allow ai clans to join BLT kingdoms"),
         LocCategory("General", "{=C5T6nnix}General"),
         LocDescription("{=}Ai clans allowed to join BLT kingdoms"),
         PropertyOrder(14)]
        public bool AllowAIJoinBLT { get; set; } = true;

        [YamlIgnore, Browsable(false)]
        public HashSet<string> RestrictedItemIds
        {
            get
            {
                return new HashSet<string>(
                    RestrictedItems
                        .Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s)),
                    StringComparer.OrdinalIgnoreCase
                );
            }
        }

        #endregion

        #region Training
        [LocDisplayName("Train Max Daily Spend"),
         LocCategory("Training", "Training"),
         LocDescription("Hard cap on gold spent on training per in-game day regardless of fund size. 0 = no cap."),
         PropertyOrder(1), UsedImplicitly]
        public int TrainMaxDailySpend { get; set; } = 100000;

        [LocDisplayName("Train Gold Cost Multiplier"),
         LocCategory("Training", "Training"),
         LocDescription("Multiplies the gold cost of troop upgrades when using !party train. 1.0 = same cost as player. (This multiplies a very low value, setting this lower than 100 may ruin your game's balance.)"),
         UIRange(0f, 1000f, 10f),
         Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(2), UsedImplicitly]
        public float TrainGoldCostMultiplier { get; set; } = 500f;
        #endregion

        #region Battle
        [LocDisplayName("{=X8r0C5fx}Start With Full Health"),
         LocCategory("Battle", "{=9qAD6eZR}Battle"),
         LocDescription("{=HbNVrZuv}Whether the hero will always start with full health"),
         PropertyOrder(1), Document, UsedImplicitly]
        public bool StartWithFullHealth { get; set; } = true;

        [LocDisplayName("{=fxZIKL65}Start Health Multiplier"),
         LocCategory("Battle", "{=9qAD6eZR}Battle"),
         LocDescription("{=8yNIRS9S}Amount to multiply normal starting health by, to give heroes better staying power vs others"),
         PropertyOrder(2),
         Range(0.1, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float StartHealthMultiplier { get; set; } = 2;

        [LocDisplayName("{=HvcTekVk}Start Retinue Health Multiplier"),
         LocCategory("Battle", "{=9qAD6eZR}Battle"),
         LocDescription("{=G6JJT2ot}Amount to multiply normal retinue starting health by, to give retinue better staying power vs others"),
         PropertyOrder(3),
         Range(0.1, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float StartRetinueHealthMultiplier { get; set; } = 2;

        [LocDisplayName("{=ZPmBe7XI}Morale Loss Factor (not implemented)"),
         LocCategory("Battle", "{=9qAD6eZR}Battle"),
         LocDescription("{=tpgJtS5q}Reduces morale loss when summoned heroes die"),
         PropertyOrder(4),
         Range(0, 2), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float MoraleLossFactor { get; set; } = 0.5f;

        [LocDisplayName("{=bXdC2trk}Retinue Use Heroes Formation"),
         LocCategory("Battle", "{=9qAD6eZR}Battle"),
         LocDescription("{=D8uDzXlV}Whether an adopted heroes retinue should spawn in the same formation as the hero (otherwise they will go into default formations)"),
         PropertyOrder(7), Document, UsedImplicitly]
        public bool RetinueUseHeroesFormation { get; set; }

        [LocDisplayName("{=OlJrCEyE}Summon Cooldown In Seconds"),
         LocCategory("Battle", "{=9qAD6eZR}Battle"),
         LocDescription("{=DeGB2BGZ}Minimum time between summons for a specific hero"),
         PropertyOrder(5),
         Range(0, int.MaxValue),
         Document, UsedImplicitly]
        public int SummonCooldownInSeconds { get; set; } = 20;

        [Browsable(false), YamlIgnore]
        public bool CooldownEnabled => SummonCooldownInSeconds > 0;

        [LocDisplayName("{=f9HVD2cC}Summon Cooldown Use Multiplier"),
         LocCategory("Battle", "{=9qAD6eZR}Battle"),
         LocDescription("{=4gZlfHzM}How much to multiply the cooldown by each time summon is used. e.g. if Summon Cooldown is 20 seconds, and UseMultiplier is 1.1 (the default), then the first summon has a cooldown of 20 seconds, and the next 24 seconds, the 10th 52 seconds, and the 20th 135 seconds. See https://www.desmos.com/calculator/muej1o5eg5 for a visualization of this."),
         PropertyOrder(6),
         Range(1, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float SummonCooldownUseMultiplier { get; set; } = 1.1f;

        [LocDisplayName("{=ViLoy0k3}Summon Cooldown Example"),
         LocCategory("Battle", "{=9qAD6eZR}Battle"),
         LocDescription("{=xZoSFrAb}Shows the consecutive cooldowns (in seconds) for 10 summons"),
         PropertyOrder(8), YamlIgnore, ReadOnly(true), UsedImplicitly]
        public string SummonCooldownExample => string.Join(", ",
            Enumerable.Range(1, 10)
                .Select(i => $"{i}: {GetCooldownTime(i):0}s"));

        [LocDisplayName("{=TESTING}Nametags"),
         LocCategory("Battle", "{=9qAD6eZR}Battle"),
         LocDescription("{=TESTING}Nametags"),
         PropertyOrder(9), Document, UsedImplicitly]

        public bool NametagEnabled { get; set; } = true;

        [LocDisplayName("{=TESTING}Nametag width"),
         LocCategory("Battle", "{=9qAD6eZR}Battle"),
         LocDescription("{=TESTING}Nametag width"),
         PropertyOrder(10),
         Range(50, float.MaxValue),
         Document, UsedImplicitly]
        public float NametagWidth { get; set; } = 150f;

        [LocDisplayName("{=TESTING}Nametag height"),
                LocCategory("Battle", "{=9qAD6eZR}Battle"),
                LocDescription("{=TESTING}Nametag height"),
                PropertyOrder(10),
                Range(10, float.MaxValue),
                Document, UsedImplicitly]
        public float NametagHeight { get; set; } = 30f;

        [LocDisplayName("{=TESTING}Nametag fontsize"),
                 LocCategory("Battle", "{=9qAD6eZR}Battle"),
                 LocDescription("{=TESTING}Nametag fontsize"),
                 PropertyOrder(11),
                 Range(15, float.MaxValue),
                 Document, UsedImplicitly]
        public float NametagFontsize { get; set; } = 20f;

        [LocDisplayName("{=TESTING}Nametag toggle key"),
                 LocCategory("Battle", "{=9qAD6eZR}Battle"),
                 LocDescription("{=TESTING}Case sensitive"),
                 PropertyOrder(12),
                 Document, UsedImplicitly]
        public string NametagKey { get; set; } = "H";
        #endregion

        #region Death
        [LocDisplayName("{=4sNJRQyw}Allow Death"),
         LocCategory("Death", "{=dbU7WEKG}Death"),
         LocDescription("{=VbBUYOfc}Whether an adopted hero is allowed to die"),
         PropertyOrder(1), Document, UsedImplicitly]
        public bool AllowDeath { get; set; } = true;

        [Browsable(false), UsedImplicitly]
        public float DeathChance { get; set; } = 0.1f;

        [LocDisplayName("{=ZEfAPyOm}Final Death Chance Percent"),
         LocCategory("Death", "{=dbU7WEKG}Death"),
         LocDescription("{=xlt1pNuT}Final death chance percent (includes vanilla chance)"),
         PropertyOrder(2),
         Range(0, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         YamlIgnore, Document, UsedImplicitly]
        public float FinalDeathChancePercent
        {
            get => DeathChance * 10f;
            set => DeathChance = value * 0.1f;
        }

        [LocDisplayName("{=sbc5Fp4o}Apply Death Chance To All Heroes"),
         LocCategory("Death", "{=dbU7WEKG}Death"),
         LocDescription("{=nbR7NLNz}Whether to apply the Death Chance changes to all heroes, not just adopted ones"),
         PropertyOrder(5), Document, UsedImplicitly]
        public bool ApplyDeathChanceToAllHeroes { get; set; } = false;

        [LocDisplayName("{=Ret2DeathChance}Retinue Death Chance Percent"),
         LocCategory("Death", "{=dbU7WEKG}Death"),
         LocDescription("{=Ret2Description}Retinue death chance percent (this determines the chance that a killing blow will " +
                        "actually kill the retinue, removing them from the adopted hero's retinue list)"),
         PropertyOrder(6),
         Range(0, 100), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         YamlIgnore, Document, UsedImplicitly]
        public float RetinueDeathChancePercent
        {
            get => RetinueDeathChance * 100f;
            set => RetinueDeathChance = value * 0.01f;
        }

        [Browsable(false), UsedImplicitly]
        public float RetinueDeathChance { get; set; } = 0.025f;

        [LocDisplayName("{=TsGie7KT}Secondary Retinue Death Chance Percent"),
         LocCategory("Death", "{=dbU7WEKG}Death"),
         LocDescription("{=hbP7F9oz}Secondary retinue death chance percent (this determines the chance that a killing blow will " +
                        "actually kill the retinue, removing them from the adopted hero's retinue list)"),
         PropertyOrder(7),
         Range(0, 100), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         YamlIgnore, Document, UsedImplicitly]
        public float Retinue2DeathChancePercent
        {
            get => Retinue2DeathChance * 100f;
            set => Retinue2DeathChance = value * 0.01f;
        }

        [Browsable(false), UsedImplicitly]
        public float Retinue2DeathChance { get; set; } = 0.025f;
        #endregion

        #region Income
        [LocDisplayName("{=GoldIncomeEnabled}Enabled"),
             LocCategory("Income", "{=IncomeCat}Income"),
             LocDescription("{=GoldIncomeEnabledDesc}Enable daily BLT gold income"),
             PropertyOrder(1), UsedImplicitly]
        public bool GoldIncomeEnabled { get; set; } = true;

        // ---- Fiefs ----
        [LocDisplayName("{=GoldIncomeFiefsEnabled}Enable Fief Income"),
         LocCategory("Income", "{=IncomeCat}Income"),
         LocDescription("{=GoldIncomeFiefsEnabledDesc}Enable BLT gold from owned settlements"),
         PropertyOrder(1), UsedImplicitly]
        public bool FiefIncomeEnabled { get; set; } = true;

        [LocDisplayName("{=GoldIncomeTownBase}Town Base Gold"),
         LocCategory("Income", "{=IncomeCat}Income"),
         LocDescription("{=GoldIncomeTownBaseDesc}Base BLT gold per town per day"),
         PropertyOrder(2), UsedImplicitly]
        public int TownBaseGold { get; set; } = 3000;

        [LocDisplayName("{=GoldIncomeCastleBase}Castle Base Gold"),
         LocCategory("Income", "{=IncomeCat}Income"),
         LocDescription("{=GoldIncomeCastleBaseDesc}Base BLT gold per castle per day"),
         PropertyOrder(3), UsedImplicitly]
        public int CastleBaseGold { get; set; } = 1500;

        [LocDisplayName("{=GoldIncomeUseProsperity}Include Prosperity"),
         LocCategory("Income", "{=IncomeCat}Income"),
         LocDescription("{=GoldIncomeUseProsperityDesc}Add prosperity-based income"),
         PropertyOrder(4), UsedImplicitly]
        public bool IncludeProsperity { get; set; } = true;

        [LocDisplayName("{=GoldIncomeProsMult}Prosperity Multiplier"),
         LocCategory("Income", "{=IncomeCat}Income"),
         LocDescription("{=GoldIncomeProsMultDesc}Prosperity multiplier"),
         PropertyOrder(5), UsedImplicitly]
        public float ProsperityMultiplier { get; set; } = 1f;

        // ---- Mercenary ----
        [LocDisplayName("{=GoldIncomeMercEnabled}Enable Mercenary Income"),
         LocCategory("Income", "{=IncomeCat}Income"),
         LocDescription("{=GoldIncomeMercEnabledDesc}Enable BLT gold from mercenary contracts"),
         PropertyOrder(1), UsedImplicitly]
        public bool MercenaryIncomeEnabled { get; set; } = true;

        [LocDisplayName("{=GoldIncomeMercMult}Mercenary Multiplier"),
         LocCategory("Income", "{=IncomeCat}Income"),
         LocDescription("{=GoldIncomeMercMultDesc}Multiplier applied to mercenary contract value (Used for BLT Clans and their Vassals)"),
         PropertyOrder(2), UsedImplicitly]
        public int MercenaryMultiplier { get; set; } = 10;

        [LocDisplayName("{=GoldIncomeMercMax}Mercenary Maximum Daily Income"),
         LocCategory("Income", "{=IncomeCat}Income"),
         LocDescription("{=GoldIncomeMercMaxDesc}Maximum BLT daily income from mercenary contract"),
         PropertyOrder(2), UsedImplicitly]
        public int MercenaryMaxIncome { get; set; } = 2000;

        #endregion

        #region Upgrades
        [LocDisplayName("{=BLT_FiefUpgrades}Fief Upgrades"),
         LocCategory("Upgrades", "{=BLT_Upgrades}Upgrades"),
         LocDescription("{=BLT_FiefUpgradesDesc}List of available fief (settlement) upgrades"),
         PropertyOrder(1), UsedImplicitly,
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor))]
        public ObservableCollection<FiefUpgrade> FiefUpgrades { get; set; } = new()
        {
            new FiefUpgrade
            {
                ID = "fief_loyalty_1",
                Name = "Improved Administration",
                Description = "Better administration increases loyalty growth",
                GoldCost = 15000,
                LoyaltyDailyFlat = 0.5f
            },
            new FiefUpgrade
            {
                ID = "fief_prosperity_1",
                Name = "Trade Hub",
                Description = "Attract more merchants to boost prosperity",
                GoldCost = 20000,
                ProsperityDailyFlat = 1.0f
            },
            new FiefUpgrade
            {
                ID = "fief_security_1",
                Name = "Guard Posts",
                Description = "Additional guard posts improve security",
                GoldCost = 12000,
                SecurityDailyFlat = 0.5f
            },
            new FiefUpgrade
            {
                ID = "fief_militia_1",
                Name = "Militia Training",
                Description = "Train civilians as militia",
                GoldCost = 10000,
                MilitiaDailyFlat = 2.0f
            },
            new FiefUpgrade
            {
                ID = "fief_food_1",
                Name = "Granary Expansion",
                Description = "Larger granaries store more food",
                GoldCost = 8000,
                FoodDailyFlat = 5.0f
            }
        };

        [LocDisplayName("{=BLT_ClanUpgrades}Clan Upgrades"),
         LocCategory("Upgrades", "{=BLT_Upgrades}Upgrades"),
         LocDescription("{=BLT_ClanUpgradesDesc}List of available clan-wide upgrades"),
         PropertyOrder(2), UsedImplicitly,
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor))]
        public ObservableCollection<ClanUpgrade> ClanUpgrades { get; set; } = new()
        {
            new ClanUpgrade
            {
                ID = "clan_renown_1",
                Name = "Clan Prestige",
                Description = "Increase your clan's fame across the land",
                GoldCost = 30000,
                RenownDaily = 1.0f
            },
            new ClanUpgrade
            {
                ID = "clan_party_1",
                Name = "Recruitment Drive",
                Description = "Allow larger party sizes for all clan members",
                GoldCost = 40000,
                PartySizeBonus = 20
            },
            new ClanUpgrade
            {
                ID = "clan_settlements_1",
                Name = "Clan Development",
                Description = "Improve loyalty and prosperity in all clan settlements",
                GoldCost = 50000,
                LoyaltyDailyFlat = 0.3f,
                ProsperityDailyFlat = 0.5f
            }
        };

        [LocDisplayName("{=BLT_KingdomUpgrades}Kingdom Upgrades"),
         LocCategory("Upgrades", "{=BLT_Upgrades}Upgrades"),
         LocDescription("{=BLT_KingdomUpgradesDesc}List of available kingdom-wide upgrades"),
         PropertyOrder(3), UsedImplicitly,
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor))]
        public ObservableCollection<KingdomUpgrade> KingdomUpgrades { get; set; } = new()
        {
            new KingdomUpgrade
            {
                ID = "kingdom_influence_1",
                Name = "Royal Authority",
                Description = "Strengthen the ruler's influence",
                GoldCost = 100000,
                InfluenceCost = 500,
                InfluenceDaily = 2.0f
            },
            new KingdomUpgrade
            {
                ID = "kingdom_military_1",
                Name = "Kingdom Military Reform",
                Description = "Increase party sizes and militia across the kingdom",
                GoldCost = 150000,
                InfluenceCost = 1000,
                PartySizeBonus = 15,
                MilitiaDailyFlat = 1.0f
            },
            new KingdomUpgrade
            {
                ID = "kingdom_prosperity_1",
                Name = "Kingdom Prosperity Initiative",
                Description = "Boost prosperity and loyalty in all kingdom settlements",
                GoldCost = 200000,
                InfluenceCost = 1500,
                LoyaltyDailyFlat = 0.2f,
                ProsperityDailyFlat = 0.5f
            }
        };

        [LocDisplayName("{=BLT_BlockNegativesAtFloor}Block Negative Upgrades At Minimum"),
         LocCategory("Upgrades", "{=BLT_Upgrades}Upgrades"),
         LocDescription("{=BLT_BlockNegativesAtFloorDesc}When enabled, upgrade effects with negative values will not be applied if the stat they affect is already at zero (or would be pushed below zero). Disable this to allow negative upgrades to always apply regardless of the current stat value."),
         PropertyOrder(4), UsedImplicitly]
        public bool BlockNegativesAtFloor { get; set; } = true;
        #endregion

        #region XP
        [LocDisplayName("{=lwU4dELT}Use Raw XP"),
         LocCategory("XP", "{=06KnYhyh}XP"),
         LocDescription("{=dICRr4BH}Use raw XP values instead of adjusting by focus and attributes, also ignoring skill cap. This avoids characters getting stuck when focus and attributes are not well distributed. "),
         PropertyOrder(1), Document, UsedImplicitly]
        public bool UseRawXP { get; set; } = true;

        [LocDisplayName("{=S5FAna09}Raw XP Skill Cap"),
         LocCategory("XP", "{=06KnYhyh}XP"),
         LocDescription("{=WUzqXuHN}Skill cap when using Raw XP. Skills will not go above this value. 330 is the vanilla XP skill cap."),
         PropertyOrder(2), Range(0, 1023), Document, UsedImplicitly]
        public int RawXPSkillCap { get; set; } = 330;
        #endregion

        #region Kill Rewards
        [LocDisplayName("{=94Ouh5It}Gold Per Kill"),
         LocCategory("Kill Rewards", "{=E2RBmb1K}Kill Rewards"),
         LocDescription("{=iSAMxZ8a}Gold the hero gets for every kill"),
         PropertyOrder(1), Document, UsedImplicitly]
        public int GoldPerKill { get; set; } = 5000;

        [LocDisplayName("{=DMGKBoJT}XP Per Kill"),
         LocCategory("Kill Rewards", "{=E2RBmb1K}Kill Rewards"),
         LocDescription("{=kwW5pZT9}XP the hero gets for every kill"),
         PropertyOrder(2), Document, UsedImplicitly]
        public int XPPerKill { get; set; } = 5000;

        [LocDisplayName("{=a1zjEuUe}XP Per Killed"),
         LocCategory("Kill Rewards", "{=E2RBmb1K}Kill Rewards"),
         LocDescription("{=bW8t2g5N}XP the hero gets for being killed"),
         PropertyOrder(3), Document, UsedImplicitly]
        public int XPPerKilled { get; set; } = 2000;

        [LocDisplayName("{=cRV9HDdf}Heal Per Kill"),
         LocCategory("Kill Rewards", "{=E2RBmb1K}Kill Rewards"),
         LocDescription("{=7VWAZgfK}HP the hero gets for every kill"),
         PropertyOrder(4), Document, UsedImplicitly]
        public int HealPerKill { get; set; } = 20;

        [LocDisplayName("{=lIhhHjih}Retinue Gold Per Kill"),
         LocCategory("Kill Rewards", "{=E2RBmb1K}Kill Rewards"),
         LocDescription("{=h93j0qw3}Gold the hero gets for every kill their retinue gets"),
         PropertyOrder(5), Document, UsedImplicitly]
        public int RetinueGoldPerKill { get; set; } = 2500;

        [LocDisplayName("{=KwlWrzDS}Retinue Heal Per Kill"),
         LocCategory("Kill Rewards", "{=E2RBmb1K}Kill Rewards"),
         LocDescription("{=Q3UVoHmt}HP the hero's retinue gets for every kill"),
         PropertyOrder(6), Document, UsedImplicitly]
        public int RetinueHealPerKill { get; set; } = 50;

        [LocDisplayName("{=wSzUkbNR}Relative Level Scaling"),
         LocCategory("Kill Rewards", "{=E2RBmb1K}Kill Rewards"),
         LocDescription("{=1LTDJZ7Y}How much to scale the kill rewards by, based on relative level of the two characters. If this is 0 (or not set) then the rewards are always as specified, if this is higher than 0 then the rewards increase if the killed unit is higher level than the hero, and decrease if it is lower. At a value of 0.5 (recommended) at level difference of 10 would give about 2.5 times the normal rewards for gold, xp and health."),
         PropertyOrder(7),
         Range(0, 1), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float RelativeLevelScaling { get; set; } = 0.5f;

        [LocDisplayName("{=BDk1G4nc}Level Scaling Cap"),
         LocCategory("Kill Rewards", "{=E2RBmb1K}Kill Rewards"),
         LocDescription("{=Vod0pJEN}Caps the maximum multiplier for the level difference, defaults to 5 if not specified"),
         PropertyOrder(8),
         Range(0, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float LevelScalingCap { get; set; } = 5;

        [LocDisplayName("{=EYXeYMQo}Minimum Gold Per Kill"),
         LocCategory("Kill Rewards", "{=E2RBmb1K}Kill Rewards"),
         LocDescription("{=J2GKeUI9}Minimum percent gold earned per kill"),
         PropertyOrder(9),
         Range(0, 1), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float MinimumGoldPerKill { get; set; } = 0.5f;
        #endregion

        #region Battle End Rewards
        [LocDisplayName("{=IQTT5vYE}Win Gold"),
         LocCategory("Battle End Rewards", "{=uPwaOKdT}Battle End Rewards"),
         LocDescription("{=pc3G0W39}Gold won if the heroes side wins"),
         PropertyOrder(1), Document, UsedImplicitly]
        public int WinGold { get; set; } = 10000;

        [LocDisplayName("{=h8I3PWkV}Win XP"),
         LocCategory("Battle End Rewards", "{=uPwaOKdT}Battle End Rewards"),
         LocDescription("{=F7Tw4D07}XP the hero gets if the heroes side wins"),
         PropertyOrder(2), Document, UsedImplicitly]
        public int WinXP { get; set; } = 10000;

        [LocDisplayName("{=lfCWK7aA}Lose Gold"),
         LocCategory("Battle End Rewards", "{=uPwaOKdT}Battle End Rewards"),
         LocDescription("{=E209XRml}Gold lost if the heroes side loses(negative to win gold)"),
         PropertyOrder(3), Document, UsedImplicitly]
        public int LoseGold { get; set; } = 5000;

        [LocDisplayName("{=Vobr36Bl}Lose XP"),
         LocCategory("Battle End Rewards", "{=uPwaOKdT}Battle End Rewards"),
         LocDescription("{=itAfYdmO}XP the hero gets if the heroes side loses"),
         PropertyOrder(4), Document, UsedImplicitly]
        public int LoseXP { get; set; } = 5000;

        [LocDisplayName("{=ihB1KMOY}Difficulty Scaling On Players Side"),
         LocCategory("Battle End Rewards", "{=uPwaOKdT}Battle End Rewards"),
         LocDescription("{=Bt1PS0aC}Apply difficulty scaling to players side"),
         PropertyOrder(5), Document, UsedImplicitly]
        public bool DifficultyScalingOnPlayersSide { get; set; } = true;

        [LocDisplayName("{=nym7EtAd}Difficulty Scaling On Enemy Side"),
         LocCategory("Battle End Rewards", "{=uPwaOKdT}Battle End Rewards"),
         LocDescription("{=U0hZef9L}Apply difficulty scaling to enemy side"),
         PropertyOrder(6), Document, UsedImplicitly]
        public bool DifficultyScalingOnEnemySide { get; set; } = true;

        [LocDisplayName("{=CaVuq5tE}Difficulty Scaling"),
         LocCategory("Battle End Rewards", "{=uPwaOKdT}Battle End Rewards"),
         LocDescription("{=IhhfIQ74}End reward difficulty scaling: determines the extent to which higher difficulty battles increase the above rewards (0 to 1)"),
         Range(0, 1), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(7), Document, UsedImplicitly]
        public float DifficultyScaling { get; set; } = 1;

        [LocDisplayName("{=891WqOrJ}Difficulty Scaling Min"),
         LocCategory("Battle End Rewards", "{=uPwaOKdT}Battle End Rewards"),
         LocDescription("{=FPXz7lBi}Min difficulty scaling multiplier"),
         PropertyOrder(8),
         Range(0, 1), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float DifficultyScalingMin { get; set; } = 0.2f;
        [YamlIgnore, Browsable(false)]
        public float DifficultyScalingMinClamped => MathF.Clamp(DifficultyScalingMin, 0, 1);

        [LocDisplayName("{=Wsho5Yns}Difficulty Scaling Max"),
         LocCategory("Battle End Rewards", "{=uPwaOKdT}Battle End Rewards"),
         LocDescription("{=ZW7O1JTv}Max difficulty scaling multiplier"),
         PropertyOrder(9),
         Range(1, 10), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         Document, UsedImplicitly]
        public float DifficultyScalingMax { get; set; } = 3f;
        [YamlIgnore, Browsable(false)]
        public float DifficultyScalingMaxClamped => Math.Max(DifficultyScalingMax, 1f);
        #endregion

        #region Kill Streak Rewards
        [LocDisplayName("{=3DZYc6hN}Kill Streaks"),
         LocCategory("Kill Streak Rewards", "{=lnz7d1BI}Kill Streak Rewards"),
         LocDescription("{=3DZYc6hN}Kill Streaks"),
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor)),
         PropertyOrder(1), UsedImplicitly]
        public ObservableCollection<KillStreakDef> KillStreaks { get; set; } = new();

        [LocDisplayName("{=wQ7lXgLA}Show Kill Streak Popup"),
         LocCategory("Kill Streak Rewards", "{=lnz7d1BI}Kill Streak Rewards"),
         LocDescription("{=wDW3143d}Whether to use the popup banner to announce kill streaks. Will only print in the overlay instead if disabled."),
         PropertyOrder(2), UsedImplicitly]
        public bool ShowKillStreakPopup { get; set; } = true;

        [LocDisplayName("{=rhwujKvf}Kill Streak Popup Alert Sound"),
         LocCategory("Kill Streak Rewards", "{=lnz7d1BI}Kill Streak Rewards"),
         LocDescription("{=1GVV1fjY}Sound to play when killstreak popup is disabled."),
         PropertyOrder(3), UsedImplicitly]
        public Log.Sound KillStreakPopupAlertSound { get; set; } = Log.Sound.Horns2;

        [LocDisplayName("{=dP9AoB9o}Reference Level Reward"),
         LocCategory("Kill Streak Rewards", "{=lnz7d1BI}Kill Streak Rewards"),
         LocDescription("{=y7AZjeSK}The level at which the rewards normalize and start to reduce (if relative level scaling is enabled)."),
         PropertyOrder(4), UsedImplicitly]
        public int ReferenceLevelReward { get; set; } = 15;
        #endregion

        #region Achievements
        [LocDisplayName("{=zTLei6dQ}Achievements"),
         LocCategory("Achievements", "{=EPr2clqT}Achievements"),
         LocDescription("{=zTLei6dQ}Achievements"),
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor)),
         PropertyOrder(1), UsedImplicitly]
        public ObservableCollection<AchievementDef> Achievements { get; set; } = new();
        #endregion

        #region Shouts
        [LocDisplayName("{=HkD6326j}Shouts"),
         LocCategory("Shouts", "{=UhUpH8C8}Shouts"),
         LocDescription("{=ufqtH5QV}Custom shouts"),
         Editor(typeof(DefaultCollectionEditor), typeof(DefaultCollectionEditor)),
         PropertyOrder(1), UsedImplicitly]
        public ObservableCollection<Shout> Shouts { get; set; } = new();

        [LocDisplayName("{=wehigXCC}Include Default Shouts"),
         LocCategory("Shouts", "{=UhUpH8C8}Shouts"),
         LocDescription("{=m6Vv2LBt}Whether to include default shouts"),
         PropertyOrder(2), UsedImplicitly]
        public bool IncludeDefaultShouts { get; set; } = true;
        #endregion
        #endregion

        #region Public Interface
        [YamlIgnore, Browsable(false)]
        public float DifficultyScalingClamped => MathF.Clamp(DifficultyScaling, 0, 5);

        [YamlIgnore, Browsable(false)]
        public IEnumerable<AchievementDef> ValidAchievements => Achievements.Where(a => a.Enabled);

        public AchievementDef GetAchievement(Guid id) => ValidAchievements?.FirstOrDefault(a => a.ID == id);

        public float GetCooldownTime(int summoned)
           => (float)(Math.Pow(SummonCooldownUseMultiplier, Mathf.Max(0, summoned - 1)) * SummonCooldownInSeconds);
        #endregion

        #region IUpdateFromDefault
        public void OnUpdateFromDefault(BannerlordTwitch.Settings defaultSettings)
        {
            SettingsHelpers.MergeCollectionsSorted(
                KillStreaks,
                Get(defaultSettings).KillStreaks,
                (a, b) => a.ID == b.ID,
                (a, b) => a.KillsRequired.CompareTo(b.KillsRequired)
            );
            SettingsHelpers.MergeCollections(
                Achievements,
                Get(defaultSettings).Achievements,
                (a, b) => a.ID == b.ID
            );
        }
        #endregion

        #region IDocumentable
        public void GenerateDocumentation(IDocumentationGenerator generator)
        {
            generator.Div("common-config", () =>
            {
                generator.H1("{=F6vM1OJo}Common Config".Translate());
                DocumentationHelpers.AutoDocument(generator, this);

                var killStreaks = KillStreaks.Where(k => k.Enabled).ToList();
                if (killStreaks.Any())
                {
                    generator.H2("{=3DZYc6hN}Kill Streaks".Translate());
                    generator.Table("kill-streaks", () =>
                    {
                        generator.TR(() => generator
                            .TH("{=uUzmy7Lh}Name".Translate())
                            .TH("{=mG7HzT0z}Kills Required".Translate())
                            .TH("{=sHWjkhId}Reward".Translate())
                        );

                        foreach (var k in killStreaks.OrderBy(k => k.KillsRequired))
                        {
                            generator.TR(() =>
                                generator
                                    .TD(k.Name.ToString())
                                    .TD($"{k.KillsRequired}")
                                    .TD(() =>
                                    {
                                        if (k.GoldReward > 0) generator.P($"{k.GoldReward}{Naming.Gold}");
                                        if (k.XPReward > 0) generator.P($"{k.XPReward}{Naming.XP}");
                                    })
                            );
                        }
                    });
                }

                var achievements = ValidAchievements.Where(a => a.Enabled).ToList();
                if (achievements.Any())
                {
                    generator.H2("{=ZW9XlwY7}Achievements".Translate());
                    generator.Table("achievements", () =>
                    {
                        generator.TR(() => generator
                            .TH("{=uUzmy7Lh}Name".Translate())
                            .TH("{=TFbiD0CZ}Requirements".Translate())
                            .TH("{=sHWjkhId}Reward".Translate())
                        );

                        foreach (var a in achievements.OrderBy(a => a.Name.ToString()))
                        {
                            generator.TR(() =>
                                generator
                                    .TD(a.Name.ToString())
                                    .TD(() =>
                                    {
                                        foreach (var r in a.Requirements)
                                        {
                                            if (r is IDocumentable d)
                                                d.GenerateDocumentation(generator);
                                            else
                                                generator.P(r.ToString());
                                        }
                                    })
                                    .TD(() =>
                                    {
                                        if (a.GoldGain > 0) generator.P($"{a.GoldGain}{Naming.Gold}");
                                        if (a.XPGain > 0) generator.P($"{a.XPGain}{Naming.XP}");
                                        if (a.GiveItemReward)
                                            generator.P($"{Naming.Item}: {a.ItemReward}");

                                        if (a.GivePassivePower)
                                        {
                                            generator.P(
                                                "power-title",
                                                a.PassivePowerReward.Name.ToString() + ":"
                                            );

                                            foreach (var power in a.PassivePowerReward.Powers)
                                            {
                                                if (power is IDocumentable docPower)
                                                    docPower.GenerateDocumentation(generator);
                                                else
                                                    generator.P(power.ToString());
                                            }
                                        }
                                    })
                            );
                        }
                    });
                }
            });
            new UpgradeSystemDocumentation().GenerateDocumentation(generator);
            if (ShowCampaignMapOverlay)
            {
                var kingdoms = MapHub.CurrentMapData?.Kingdoms;
                if (kingdoms == null || kingdoms.Count == 0)
                    return;
                generator.H1("Campaign Map");
                generator.H2("Legend".Translate());

                generator.Table("legend", () =>
                {
                    generator.TR(() =>
                    {
                        generator.TH("Color");
                        generator.TH("Name");
                    });

                    foreach (var kingdom in kingdoms)
                    {
                        string hex1 = kingdom.Color1.StartsWith("#")
                            ? kingdom.Color1
                            : "#" + kingdom.Color1;
                        string hex2 = kingdom.Color2.StartsWith("#")
                            ? kingdom.Color2
                            : "#" + kingdom.Color2;

                        generator.TR(() =>
                        {
                            generator.TD(
                                "",
                                $"<div style=\"background-color:{hex1}; width:20px; height:20px; border:1px solid {hex2}; border-radius:3px;\"></div>"
                            );

                            var rkingdom = Kingdom.All.FirstOrDefault(f => f.StringId == kingdom.Id);
                            string names = $"{kingdom.Name} - Leader: {rkingdom?.Leader?.Name}";
                            generator.TD(names);
                        });
                    }
                });

                // Map
                var settlements = MapHub.CurrentMapData?.Settlements;
                if (settlements == null || settlements.Count == 0)
                    return;

                var segments = MapHub.CurrentMapData.Coastline;
                generator.H2("Map");

                generator.Div(() =>
                {
                    // Outer container div with inline style
                    generator.P("<div style=\"position:relative; width:1500px; height:1000px;" +
                      "background-color:#1f1f1f; border:3px solid #111; overflow:hidden; left:-25%; \">");

                    float margin = 35f;
                    float mapWidth = 1500f - 2 * margin;
                    float mapHeight = 1000f - 2 * margin;

                    float minX = settlements.Min(s => s.X);
                    float maxX = settlements.Max(s => s.X);
                    float minY = settlements.Min(s => s.Y);
                    float maxY = settlements.Max(s => s.Y);

                    var kingdomDict = MapHub.CurrentMapData.Kingdoms.ToDictionary(k => k.Id, k => k.Color1);
                    var kingdomBorderDict = MapHub.CurrentMapData.Kingdoms.ToDictionary(k => k.Id, k => k.Color2);

                    if (segments != null || segments.Count > 0)
                    {
                        float worldWidth = maxX - minX;
                        float worldHeight = maxY - minY;

                        if (worldWidth == 0) worldWidth = 1;
                        if (worldHeight == 0) worldHeight = 1;

                        foreach (var seg in segments)
                        {
                            float scaledX1 = (seg.X1 - minX) / worldWidth * mapWidth + margin;
                            float scaledY1 = (seg.Y1 - minY) / worldHeight * mapHeight + margin;

                            float scaledX2 = (seg.X2 - minX) / worldWidth * mapWidth + margin;
                            float scaledY2 = (seg.Y2 - minY) / worldHeight * mapHeight + margin;

                            generator.MapSegment(
                                scaledX1,
                                scaledY1,
                                scaledX2,
                                scaledY2
                            );
                        }
                    }

                    foreach (var s in settlements)
                    {
                        float scaledX = (s.X - minX) / (maxX - minX) * mapWidth + margin;
                        float scaledY = (s.Y - minY) / (maxY - minY) * mapHeight + margin;

                        generator.MapLabel(
                            scaledX,
                            scaledY,
                            s.Name,
                            s.Type,
                            s.KingdomId,
                            kingdomId => kingdomDict.TryGetValue(kingdomId, out var c) ? c : "#000080",
                            kingdomId => kingdomBorderDict.TryGetValue(kingdomId, out var c) ? c : "#000000"
                        );
                    }

                    generator.P("</div>"); // close container
                });
            }
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
    }
}