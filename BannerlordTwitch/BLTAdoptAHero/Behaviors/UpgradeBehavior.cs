using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using BLTAdoptAHero.Actions.Upgrades;
using BLTAdoptAHero.Actions;

namespace BLTAdoptAHero
{
    /// <summary>
    /// UpgradeBehavior - safe, non-mutating provider.
    /// - Persists upgrades as comma-separated strings per fief/clan/kingdom (save-compatible)
    /// - Exposes Get/Has/Add/Remove helpers used by UI/actions
    /// - Exposes aggregated getters (tax, loyalty/prosperity/security/militia/food, party/garrison) computed on-demand from UpgradeAction.CurrentSettings
    /// </summary>
    public class UpgradeBehavior : CampaignBehaviorBase
    {
        public static UpgradeBehavior Current { get; private set; }

        // Persisted across saves - string IDs lists stored as comma-separated strings (for compatibility)
        private Dictionary<string, string> _fiefUpgrades = new();
        private Dictionary<string, string> _clanUpgrades = new();
        private Dictionary<string, string> _kingdomUpgrades = new();

        public UpgradeBehavior()
        {
            Current = this;
            // don't touch engine state here
        }

        public override void RegisterEvents()
        {
            // No unsafe DailyTick mutators - models will query this provider on demand
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Persist upgrade lists as comma-separated strings keyed by stringId
            dataStore.SyncData("BLT_FiefUpgrades", ref _fiefUpgrades);
            dataStore.SyncData("BLT_ClanUpgrades", ref _clanUpgrades);
            dataStore.SyncData("BLT_KingdomUpgrades", ref _kingdomUpgrades);

            // Ensure dictionaries exist after load
            _fiefUpgrades ??= new Dictionary<string, string>();
            _clanUpgrades ??= new Dictionary<string, string>();
            _kingdomUpgrades ??= new Dictionary<string, string>();
        }

        #region String serialization helpers
        private static List<string> ParseUpgradeString(string upgradeString)
        {
            if (string.IsNullOrEmpty(upgradeString))
                return new List<string>();

            return upgradeString
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        private static string SerializeUpgradeList(List<string> upgrades)
        {
            if (upgrades == null || upgrades.Count == 0)
                return string.Empty;

            return string.Join(",", upgrades);
        }
        #endregion

        #region Fief methods (Get/Has/Add/Remove)
        public List<string> GetFiefUpgrades(Settlement settlement)
        {
            if (settlement == null) return new List<string>();
            string key = settlement.StringId;

            if (!_fiefUpgrades.TryGetValue(key, out string upgradeString))
                return new List<string>();

            return ParseUpgradeString(upgradeString);
        }

        public bool HasFiefUpgrade(Settlement settlement, string upgradeId)
        {
            if (settlement == null || string.IsNullOrEmpty(upgradeId))
                return false;

            var list = GetFiefUpgrades(settlement);
            return list.Contains(upgradeId);
        }

        public bool AddFiefUpgrade(Settlement settlement, string upgradeId)
        {
            if (settlement == null || string.IsNullOrEmpty(upgradeId))
                return false;

            string key = settlement.StringId;
            var upgrades = _fiefUpgrades.TryGetValue(key, out string upgradeString)
                ? ParseUpgradeString(upgradeString)
                : new List<string>();

            if (upgrades.Contains(upgradeId))
                return false;

            upgrades.Add(upgradeId);
            _fiefUpgrades[key] = SerializeUpgradeList(upgrades);
            return true;
        }

        public bool RemoveFiefUpgrade(Settlement settlement, string upgradeId)
        {
            if (settlement == null || string.IsNullOrEmpty(upgradeId))
                return false;

            string key = settlement.StringId;
            if (!_fiefUpgrades.TryGetValue(key, out string upgradeString))
                return false;

            var upgrades = ParseUpgradeString(upgradeString);
            bool removed = upgrades.Remove(upgradeId);
            if (!removed)
                return false;

            if (upgrades.Count == 0)
                _fiefUpgrades.Remove(key);
            else
                _fiefUpgrades[key] = SerializeUpgradeList(upgrades);

            return true;
        }
        #endregion

        #region Clan methods (Get/Has/Add/Remove)
        public List<string> GetClanUpgrades(Clan clan)
        {
            if (clan == null) return new List<string>();
            string key = clan.StringId;

            if (!_clanUpgrades.TryGetValue(key, out string upgradeString))
                return new List<string>();

            return ParseUpgradeString(upgradeString);
        }

        public bool HasClanUpgrade(Clan clan, string upgradeId)
        {
            if (clan == null || string.IsNullOrEmpty(upgradeId))
                return false;

            var list = GetClanUpgrades(clan);
            return list.Contains(upgradeId);
        }

        public bool AddClanUpgrade(Clan clan, string upgradeId)
        {
            if (clan == null || string.IsNullOrEmpty(upgradeId))
                return false;

            string key = clan.StringId;
            var upgrades = _clanUpgrades.TryGetValue(key, out string upgradeString)
                ? ParseUpgradeString(upgradeString)
                : new List<string>();

            if (upgrades.Contains(upgradeId))
                return false;

            upgrades.Add(upgradeId);
            _clanUpgrades[key] = SerializeUpgradeList(upgrades);
            return true;
        }

        public bool RemoveClanUpgrade(Clan clan, string upgradeId)
        {
            if (clan == null || string.IsNullOrEmpty(upgradeId))
                return false;

            string key = clan.StringId;
            if (!_clanUpgrades.TryGetValue(key, out string upgradeString))
                return false;

            var upgrades = ParseUpgradeString(upgradeString);
            bool removed = upgrades.Remove(upgradeId);
            if (!removed)
                return false;

            if (upgrades.Count == 0)
                _clanUpgrades.Remove(key);
            else
                _clanUpgrades[key] = SerializeUpgradeList(upgrades);

            return true;
        }
        #endregion

        #region Kingdom methods (Get/Has/Add/Remove)
        public List<string> GetKingdomUpgrades(Kingdom kingdom)
        {
            if (kingdom == null) return new List<string>();
            string key = kingdom.StringId;

            if (!_kingdomUpgrades.TryGetValue(key, out string upgradeString))
                return new List<string>();

            return ParseUpgradeString(upgradeString);
        }

        public bool HasKingdomUpgrade(Kingdom kingdom, string upgradeId)
        {
            if (kingdom == null || string.IsNullOrEmpty(upgradeId))
                return false;

            var list = GetKingdomUpgrades(kingdom);
            return list.Contains(upgradeId);
        }

        public bool AddKingdomUpgrade(Kingdom kingdom, string upgradeId)
        {
            if (kingdom == null || string.IsNullOrEmpty(upgradeId))
                return false;

            string key = kingdom.StringId;
            var upgrades = _kingdomUpgrades.TryGetValue(key, out string upgradeString)
                ? ParseUpgradeString(upgradeString)
                : new List<string>();

            if (upgrades.Contains(upgradeId))
                return false;

            upgrades.Add(upgradeId);
            _kingdomUpgrades[key] = SerializeUpgradeList(upgrades);
            return true;
        }

        public bool RemoveKingdomUpgrade(Kingdom kingdom, string upgradeId)
        {
            if (kingdom == null || string.IsNullOrEmpty(upgradeId))
                return false;

            string key = kingdom.StringId;
            if (!_kingdomUpgrades.TryGetValue(key, out string upgradeString))
                return false;

            var upgrades = ParseUpgradeString(upgradeString);
            bool removed = upgrades.Remove(upgradeId);
            if (!removed)
                return false;

            if (upgrades.Count == 0)
                _kingdomUpgrades.Remove(key);
            else
                _kingdomUpgrades[key] = SerializeUpgradeList(upgrades);

            return true;
        }
        #endregion

        #region Aggregation helpers used by models (computed on-demand, safe)
        // Fetch current upgrade definitions from your settings container
        private UpgradeAction.Settings GetSettingsSafe()
        {
            return UpgradeAction.CurrentSettings;
        }

        // Sum helpers for settlement-level values (fief + clan + kingdom)
        public int GetTotalTaxBonus(Settlement settlement)
        {
            if (settlement == null) return 0;
            int sum = 0;

            var settings = GetSettingsSafe();
            if (settings == null) return 0;

            // fief upgrades
            foreach (var id in GetFiefUpgrades(settlement))
            {
                var up = settings.FiefUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null) sum += up.TaxIncomeFlat;
            }

            // clan upgrades (applied to clan's settlements)
            var clan = settlement.OwnerClan;
            if (clan != null)
            {
                foreach (var id in GetClanUpgrades(clan))
                {
                    var up = settings.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                    if (up != null) sum += up.TaxIncomeFlat;
                }
            }

            // kingdom upgrades (applied to kingdom's settlements)
            var kingdom = clan?.Kingdom ?? settlement.MapFaction as Kingdom;
            if (kingdom != null)
            {
                foreach (var id in GetKingdomUpgrades(kingdom))
                {
                    var up = settings.KingdomUpgrades.FirstOrDefault(u => u.ID == id);
                    if (up != null) sum += up.TaxIncomeFlat;
                }
            }

            return sum;
        }

        public int GetFiefGarrisonCapacityBonus(Settlement settlement)
        {
            if (settlement == null) return 0;
            int sum = 0;
            var settings = GetSettingsSafe();
            if (settings == null) return 0;

            foreach (var id in GetFiefUpgrades(settlement))
            {
                var up = settings.FiefUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null) sum += up.GarrisonCapacityBonus;
            }

            // also include clan/kingdom bonuses that affect settlement garrison capacity
            var clan = settlement.OwnerClan;
            if (clan != null)
            {
                foreach (var id in GetClanUpgrades(clan))
                {
                    var up = settings.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                    if (up != null) sum += up.GarrisonCapacityBonus;
                }

                var kingdom = clan.Kingdom;
                if (kingdom != null)
                {
                    foreach (var id in GetKingdomUpgrades(kingdom))
                    {
                        var up = settings.KingdomUpgrades.FirstOrDefault(u => u.ID == id);
                        if (up != null) sum += up.GarrisonCapacityBonus;
                    }
                }
            }

            return sum;
        }

        public int GetClanPartySizeBonus(Clan clan)
        {
            if (clan == null) return 0;
            var settings = GetSettingsSafe();
            if (settings == null) return 0;

            int sum = 0;
            foreach (var id in GetClanUpgrades(clan))
            {
                var up = settings.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null) sum += up.PartySizeBonus;
            }
            return sum;
        }

        public int GetKingdomPartySizeBonus(Kingdom kingdom)
        {
            if (kingdom == null) return 0;
            var settings = GetSettingsSafe();
            if (settings == null) return 0;

            int sum = 0;
            foreach (var id in GetKingdomUpgrades(kingdom))
            {
                var up = settings.KingdomUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null) sum += up.PartySizeBonus;
            }
            return sum;
        }

        public int GetTotalPartySizeBonus(Hero hero)
        {
            if (hero?.Clan == null) return 0;
            int bonus = 0;
            bonus += GetClanPartySizeBonus(hero.Clan);
            if (hero.Clan.Kingdom != null)
                bonus += GetKingdomPartySizeBonus(hero.Clan.Kingdom);
            return bonus;
        }

        // daily flat/percent getters (loyalty, prosperity, security, militia, food)
        public float GetTotalLoyaltyDailyFlat(Settlement s)
        {
            if (s == null) return 0f;
            return SumSettlementFloat(s, (u) => u.LoyaltyDailyFlat);
        }

        public float GetTotalLoyaltyDailyPercent(Settlement s)
        {
            if (s == null) return 0f;
            return SumSettlementFloat(s, (u) => u.LoyaltyDailyPercent);
        }

        public float GetTotalProsperityDailyFlat(Settlement s)
            => s == null ? 0f : SumSettlementFloat(s, (u) => u.ProsperityDailyFlat);

        public float GetTotalProsperityDailyPercent(Settlement s)
            => s == null ? 0f : SumSettlementFloat(s, (u) => u.ProsperityDailyPercent);

        public float GetTotalSecurityDailyFlat(Settlement s)
            => s == null ? 0f : SumSettlementFloat(s, (u) => u.SecurityDailyFlat);

        public float GetTotalSecurityDailyPercent(Settlement s)
            => s == null ? 0f : SumSettlementFloat(s, (u) => u.SecurityDailyPercent);

        public float GetTotalMilitiaDailyFlat(Settlement s)
            => s == null ? 0f : SumSettlementFloat(s, (u) => u.MilitiaDailyFlat);

        public float GetTotalMilitiaDailyPercent(Settlement s)
            => s == null ? 0f : SumSettlementFloat(s, (u) => u.MilitiaDailyPercent);

        public float GetTotalFoodDailyFlat(Settlement s)
            => s == null ? 0f : SumSettlementFloat(s, (u) => u.FoodDailyFlat);

        public float GetTotalFoodDailyPercent(Settlement s)
            => s == null ? 0f : SumSettlementFloat(s, (u) => u.FoodDailyPercent);

        // helper that sums a float property across fief/clan/kingdom upgrades for the settlement
        private float SumSettlementFloat(Settlement s, Func<dynamic, float> selector)
        {
            float sum = 0f;
            var settings = GetSettingsSafe();
            if (settings == null) return 0f;

            // fief
            foreach (var id in GetFiefUpgrades(s))
            {
                var up = settings.FiefUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null) sum += selector(up);
            }

            // clan
            var clan = s.OwnerClan;
            if (clan != null)
            {
                foreach (var id in GetClanUpgrades(clan))
                {
                    var up = settings.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                    if (up != null) sum += selector(up);
                }

                // kingdom
                var kingdom = clan.Kingdom;
                if (kingdom != null)
                {
                    foreach (var id in GetKingdomUpgrades(kingdom))
                    {
                        var up = settings.KingdomUpgrades.FirstOrDefault(u => u.ID == id);
                        if (up != null) sum += selector(up);
                    }
                }
            }

            return sum;
        }
        #endregion
    }
}
