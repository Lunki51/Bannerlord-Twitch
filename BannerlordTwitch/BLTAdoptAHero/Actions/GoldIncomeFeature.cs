using System;
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
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    // --------------------------
    // ACTION: holds config + CLI
    // --------------------------
    [LocDisplayName("{=GoldIncomeCmd}GoldIncome"),
     LocDescription("{=GoldIncomeDesc}Daily BLT gold income from fiefs and mercenary contracts"),
     UsedImplicitly]
    public class GoldIncomeAction : HeroCommandHandlerBase
    {
        // NOTE: Settings is public so the behavior can reference the type and instance.
        [CategoryOrder("General", 0),
         CategoryOrder("Fiefs", 1),
         CategoryOrder("Mercenary", 2)]
        public class Settings : IDocumentable
        {
            [LocDisplayName("{=GoldIncomeEnabled}Enabled"),
             LocCategory("General", "{=GeneralCat}General"),
             LocDescription("{=GoldIncomeEnabledDesc}Enable daily BLT gold income"),
             PropertyOrder(1), UsedImplicitly]
            public bool Enabled { get; set; } = true;

            // ---- Fiefs ----
            [LocDisplayName("{=GoldIncomeFiefsEnabled}Enable Fief Income"),
             LocCategory("Fiefs", "{=FiefsCat}Fiefs"),
             LocDescription("{=GoldIncomeFiefsEnabledDesc}Enable BLT gold from owned settlements"),
             PropertyOrder(1), UsedImplicitly]
            public bool FiefIncomeEnabled { get; set; } = true;

            [LocDisplayName("{=GoldIncomeTownBase}Town Base Gold"),
             LocCategory("Fiefs", "{=FiefsCat}Fiefs"),
             LocDescription("{=GoldIncomeTownBaseDesc}Base BLT gold per town per day"),
             PropertyOrder(2), UsedImplicitly]
            public int TownBaseGold { get; set; } = 50;

            [LocDisplayName("{=GoldIncomeCastleBase}Castle Base Gold"),
             LocCategory("Fiefs", "{=FiefsCat}Fiefs"),
             LocDescription("{=GoldIncomeCastleBaseDesc}Base BLT gold per castle per day"),
             PropertyOrder(3), UsedImplicitly]
            public int CastleBaseGold { get; set; } = 25;

            [LocDisplayName("{=GoldIncomeUseProsperity}Include Prosperity"),
             LocCategory("Fiefs", "{=FiefsCat}Fiefs"),
             LocDescription("{=GoldIncomeUseProsperityDesc}Add prosperity-based income"),
             PropertyOrder(4), UsedImplicitly]
            public bool IncludeProsperity { get; set; } = true;

            [LocDisplayName("{=GoldIncomeProsMult}Prosperity Multiplier"),
             LocCategory("Fiefs", "{=FiefsCat}Fiefs"),
             LocDescription("{=GoldIncomeProsMultDesc}Prosperity multiplier"),
             PropertyOrder(5), UsedImplicitly]
            public float ProsperityMultiplier { get; set; } = 0.01f;

            // ---- Mercenary ----
            [LocDisplayName("{=GoldIncomeMercEnabled}Enable Mercenary Income"),
             LocCategory("Mercenary", "{=MercCat}Mercenary"),
             LocDescription("{=GoldIncomeMercEnabledDesc}Enable BLT gold from mercenary contracts"),
             PropertyOrder(1), UsedImplicitly]
            public bool MercenaryIncomeEnabled { get; set; } = true;

            [LocDisplayName("{=GoldIncomeMercMult}Mercenary Multiplier"),
             LocCategory("Mercenary", "{=MercCat}Mercenary"),
             LocDescription("{=GoldIncomeMercMultDesc}Multiplier applied to mercenary contract value (1-100)"),
             PropertyOrder(2), UsedImplicitly]
            public int MercenaryMultiplier { get; set; } = 10;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value($"<strong>Enabled:</strong> {Enabled}");
                if (!Enabled) return;
                generator.Value($"<strong>Fief income enabled:</strong> {FiefIncomeEnabled}");
                generator.Value($"<strong>Town base gold:</strong> {TownBaseGold}");
                generator.Value($"<strong>Castle base gold:</strong> {CastleBaseGold}");
                generator.Value($"<strong>Include prosperity:</strong> {IncludeProsperity}");
                if (IncludeProsperity) generator.Value($"<strong>Prosperity multiplier:</strong> {ProsperityMultiplier}");
                generator.Value($"<strong>Mercenary income enabled:</strong> {MercenaryIncomeEnabled}");
                generator.Value($"<strong>Mercenary multiplier:</strong> {MercenaryMultiplier}");
            }
        }

        // BLT wiring: this tells BLT what config type to show in the UI for this handler
        public override Type HandlerConfigType => typeof(Settings);

        // Static holder the behavior will read from.
        // Initialized to defaults to ensure behavior has something even before a command is run.
        public static Settings CurrentSettings { get; private set; } = new Settings();

        // Event to notify listeners (behavior) if settings are updated via command injection.
        public static event Action<Settings> SettingsChanged;

        // ------------------------
        // Command CLI (and update)
        // ------------------------
        protected override void ExecuteInternal(
            Hero adoptedHero,
            ReplyContext context,
            object config,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            // If BLT injected a config object into the command execution, capture it as canonical.
            if (config is Settings injected)
            {
                // copy values to avoid external mutation if desired (shallow copy is fine here)
                CurrentSettings = new Settings()
                {
                    Enabled = injected.Enabled,
                    FiefIncomeEnabled = injected.FiefIncomeEnabled,
                    TownBaseGold = injected.TownBaseGold,
                    CastleBaseGold = injected.CastleBaseGold,
                    IncludeProsperity = injected.IncludeProsperity,
                    ProsperityMultiplier = injected.ProsperityMultiplier,
                    MercenaryIncomeEnabled = injected.MercenaryIncomeEnabled,
                    MercenaryMultiplier = injected.MercenaryMultiplier
                };

                SettingsChanged?.Invoke(CurrentSettings);
            }

            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (!CurrentSettings.Enabled)
            {
                onFailure("Gold income is disabled.");
                return;
            }

            if (context.Args.IsEmpty())
            {
                onFailure("Usage: goldincome fiefs | merc");
                return;
            }

            string arg = context.Args.Trim().ToLowerInvariant();

            if (arg == "fiefs")
            {
                ShowFiefIncome(adoptedHero, onSuccess);
                return;
            }

            if (arg == "merc" || arg == "mercenary")
            {
                ShowMercIncome(adoptedHero, onSuccess, onFailure);
                return;
            }

            onFailure("Usage: goldincome fiefs | merc");
        }

        private void ShowFiefIncome(Hero hero, Action<string> onSuccess)
        {
            var clan = hero.Clan;
            if (clan == null || clan.Settlements == null || clan.Settlements.Count == 0)
            {
                onSuccess("You own no settlements.");
                return;
            }

            var sb = new StringBuilder();
            foreach (var s in clan.Settlements)
            {
                int income = CalculateSettlementIncome(s, CurrentSettings);
                sb.Append($"{s.Name}: {(income >= 0 ? "+" : "")}{income} | ");
            }

            var result = sb.ToString().Trim();
            if (result.EndsWith("|")) result = result.Substring(0, result.Length - 1).TrimEnd();
            onSuccess(result);
        }

        private void ShowMercIncome(Hero hero, Action<string> onSuccess, Action<string> onFailure)
        {
            var clan = hero.Clan;
            if (clan == null)
            {
                onFailure("You are not in a clan.");
                return;
            }

            if (!clan.IsUnderMercenaryService)
            {
                onFailure("You are not under a mercenary contract.");
                return;
            }

            int income = CalculateMercenaryIncome(clan, CurrentSettings);
            onSuccess($"Mercenary contract income: {(income >= 0 ? "+" : "")}{income}");
        }

        // small helpers so behavior can share logic if needed (internal usage allowed)
        internal static int CalculateSettlementIncome(Settlement settlement, Settings settings)
        {
            if (settlement == null || settings == null) return 0;

            int income = 0;
            if (settlement.IsTown) income += settings.TownBaseGold;
            else if (settlement.IsCastle) income += settings.CastleBaseGold;
            else return 0;

            if (settings.IncludeProsperity)
                income += (int)(settlement.Town.Prosperity * settings.ProsperityMultiplier);

            return income;
        }

        internal static int CalculateMercenaryIncome(Clan clan, Settings settings)
        {
            if (clan == null || settings == null) return 0;
            if (!clan.IsUnderMercenaryService) return 0;

            int mult = Math.Max(1, Math.Min(settings.MercenaryMultiplier, 100));
            var creator = Campaign.Current.KingdomManager;
            int contract = creator.GetMercenaryWageAmount(clan.Leader);
            if (contract <= 0) return 0;
            // multiply may be large, keep in int (Bannerlord uses int gold)
            long value = (long)contract * (long)mult;
            if (value > int.MaxValue) return int.MaxValue;
            return (int)value;
        }
    }

    // ---------------------------------
    // BEHAVIOR: daily tick BLT payments
    // ---------------------------------
    public class BLTGoldIncomeBehavior : CampaignBehaviorBase
    {
        private GoldIncomeAction.Settings _settings = GoldIncomeAction.CurrentSettings;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, OnDailyTickClan);

            // keep a reference to current settings
            _settings = GoldIncomeAction.CurrentSettings;
        }

        public override void SyncData(IDataStore dataStore)
        {
            // no persistence required
        }

        private void OnDailyTickClan(Clan clan)
        {
            if (clan == null) return;
            if (_settings == null || !_settings.Enabled) return;

            Hero leader = clan.Leader;
            if (leader == null || !leader.IsAdopted()) return;

            int total = 0;

            if (_settings.FiefIncomeEnabled && clan.Settlements != null)
            {
                foreach (var s in clan.Settlements)
                {
                    total += GoldIncomeAction.CalculateSettlementIncome(s, _settings);
                }
            }

            if (_settings.MercenaryIncomeEnabled && clan.IsUnderMercenaryService)
            {
                total += GoldIncomeAction.CalculateMercenaryIncome(clan, _settings);
            }

            if (total != 0)
            {
                BLTAdoptAHeroCampaignBehavior.Current
                    ?.ChangeHeroGold(leader, total, false);
            }
        }
    }
}
