using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Actions.Upgrades;
using BLTAdoptAHero.Actions;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Campaign behavior that tracks and applies upgrade effects
    /// </summary>
    public class UpgradeBehavior : CampaignBehaviorBase
    {
        public static UpgradeBehavior Current { get; private set; }

        // --- Persisted as lists (IDataStore-friendly) ---
        [Serializable]
        public class UpgradeEntry
        {
            public string Key;
            public List<string> Values = new List<string>();
        }

        private List<UpgradeEntry> _fiefUpgradesList = new();
        private List<UpgradeEntry> _clanUpgradesList = new();
        private List<UpgradeEntry> _kingdomUpgradesList = new();

        // --- Runtime dictionaries for fast lookups/operations (not persisted directly) ---
        private Dictionary<string, List<string>> _fiefUpgrades = new();
        private Dictionary<string, List<string>> _clanUpgrades = new();
        private Dictionary<string, List<string>> _kingdomUpgrades = new();

        // Cache for accumulated tax bonuses (recalculated daily) - runtime only
        private Dictionary<string, int> _fiefTaxBonuses = new();
        private Dictionary<string, int> _clanTaxBonuses = new();
        private Dictionary<string, int> _kingdomTaxBonuses = new();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

            Initialize();
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                // Convert runtime dictionaries into lists so the IDataStore can persist them
                ConvertDictsToLists();

                dataStore.SyncData("_fiefUpgrades", ref _fiefUpgradesList);
                dataStore.SyncData("_clanUpgrades", ref _clanUpgradesList);
                dataStore.SyncData("_kingdomUpgrades", ref _kingdomUpgradesList);

                // Rebuild runtime dictionaries from the loaded/persisted lists
                RebuildDictionariesFromLists();

                // Set Current instance after successful sync
                Current = this;

                // Ensure non-persisted runtime collections are initialized
                Initialize();
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT Upgrades] Error in SyncData: {ex}");

                // Initialize to safe defaults on error
                Initialize();
            }
        }

        private void Initialize()
        {
            // Do not wipe data that was just loaded; only ensure collections are non-null
            _fiefUpgrades ??= new Dictionary<string, List<string>>();
            _clanUpgrades ??= new Dictionary<string, List<string>>();
            _kingdomUpgrades ??= new Dictionary<string, List<string>>();

            _fiefUpgradesList ??= new List<UpgradeEntry>();
            _clanUpgradesList ??= new List<UpgradeEntry>();
            _kingdomUpgradesList ??= new List<UpgradeEntry>();

            _fiefTaxBonuses = _fiefTaxBonuses ?? new Dictionary<string, int>();
            _clanTaxBonuses = _clanTaxBonuses ?? new Dictionary<string, int>();
            _kingdomTaxBonuses = _kingdomTaxBonuses ?? new Dictionary<string, int>();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            Current = this;
        }

        private void OnDailyTick()
        {
            // HARD STOP during save
            if (Campaign.Current?.SaveHandler?.IsSaving == true)
                return;

            // Don't process if game isn't properly loaded
            if (Campaign.Current == null)
                return;

            try
            {
                _fiefTaxBonuses.Clear();
                _clanTaxBonuses.Clear();
                _kingdomTaxBonuses.Clear();

                ProcessFiefUpgrades();
                ProcessClanUpgrades();
                ProcessKingdomUpgrades();
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT Upgrades] Error in daily tick: {ex}");
            }
        }

        #region Persistence helpers
        private void ConvertDictsToLists()
        {
            _fiefUpgradesList = _fiefUpgrades
                .Select(kvp => new UpgradeEntry { Key = kvp.Key, Values = new List<string>(kvp.Value) })
                .ToList();

            _clanUpgradesList = _clanUpgrades
                .Select(kvp => new UpgradeEntry { Key = kvp.Key, Values = new List<string>(kvp.Value) })
                .ToList();

            _kingdomUpgradesList = _kingdomUpgrades
                .Select(kvp => new UpgradeEntry { Key = kvp.Key, Values = new List<string>(kvp.Value) })
                .ToList();
        }

        private void RebuildDictionariesFromLists()
        {
            _fiefUpgrades = new Dictionary<string, List<string>>();
            if (_fiefUpgradesList != null)
            {
                foreach (var entry in _fiefUpgradesList)
                {
                    if (string.IsNullOrEmpty(entry.Key)) continue;
                    _fiefUpgrades[entry.Key] = entry.Values != null ? new List<string>(entry.Values) : new List<string>();
                }
            }

            _clanUpgrades = new Dictionary<string, List<string>>();
            if (_clanUpgradesList != null)
            {
                foreach (var entry in _clanUpgradesList)
                {
                    if (string.IsNullOrEmpty(entry.Key)) continue;
                    _clanUpgrades[entry.Key] = entry.Values != null ? new List<string>(entry.Values) : new List<string>();
                }
            }

            _kingdomUpgrades = new Dictionary<string, List<string>>();
            if (_kingdomUpgradesList != null)
            {
                foreach (var entry in _kingdomUpgradesList)
                {
                    if (string.IsNullOrEmpty(entry.Key)) continue;
                    _kingdomUpgrades[entry.Key] = entry.Values != null ? new List<string>(entry.Values) : new List<string>();
                }
            }
        }
        #endregion

        #region Fief Upgrades
        public bool HasFiefUpgrade(Settlement settlement, string upgradeId)
        {
            if (settlement == null || string.IsNullOrEmpty(upgradeId)) return false;
            string key = settlement.StringId;
            return _fiefUpgrades.ContainsKey(key) && _fiefUpgrades[key].Contains(upgradeId);
        }

        public List<string> GetFiefUpgrades(Settlement settlement)
        {
            if (settlement == null) return new List<string>();
            string key = settlement.StringId;
            return _fiefUpgrades.ContainsKey(key) ? new List<string>(_fiefUpgrades[key]) : new List<string>();
        }

        public bool AddFiefUpgrade(Settlement settlement, string upgradeId)
        {
            if (settlement == null || string.IsNullOrEmpty(upgradeId)) return false;
            string key = settlement.StringId;

            if (!_fiefUpgrades.ContainsKey(key))
                _fiefUpgrades[key] = new List<string>();

            if (_fiefUpgrades[key].Contains(upgradeId))
                return false;

            _fiefUpgrades[key].Add(upgradeId);
            // list will be updated when SyncData runs (ConvertDictsToLists)
            return true;
        }

        private void ProcessFiefUpgrades()
        {
            var settings = UpgradeAction.CurrentSettings;
            if (settings?.FiefUpgrades == null) return;

            foreach (var kvp in _fiefUpgrades.ToList())
            {
                var settlement = Settlement.Find(kvp.Key);
                if (settlement == null || settlement.Town == null) continue;

                foreach (var upgradeId in kvp.Value)
                {
                    var upgrade = settings.FiefUpgrades.FirstOrDefault(u => u.ID == upgradeId);
                    if (upgrade == null) continue;

                    try
                    {
                        ApplyFiefUpgradeEffects(settlement, upgrade);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[BLT Upgrades] Error applying fief upgrade {upgradeId} to {settlement.Name}: {ex}");
                    }
                }
            }
        }

        private void ApplyFiefUpgradeEffects(Settlement settlement, FiefUpgrade upgrade)
        {
            var town = settlement?.Town;
            if (town == null) return;

            // Apply daily growth modifiers
            if (upgrade.LoyaltyDailyFlat != 0)
                town.Loyalty += upgrade.LoyaltyDailyFlat;

            if (upgrade.LoyaltyDailyPercent != 0 && town.LoyaltyChange != 0)
                town.Loyalty += town.LoyaltyChange * (upgrade.LoyaltyDailyPercent / 100f);

            if (upgrade.ProsperityDailyFlat != 0)
                town.Prosperity += upgrade.ProsperityDailyFlat;

            if (upgrade.ProsperityDailyPercent != 0 && town.ProsperityChange != 0)
                town.Prosperity += town.ProsperityChange * (upgrade.ProsperityDailyPercent / 100f);

            if (upgrade.SecurityDailyFlat != 0)
                town.Security += upgrade.SecurityDailyFlat;

            if (upgrade.SecurityDailyPercent != 0 && town.SecurityChange != 0)
                town.Security += town.SecurityChange * (upgrade.SecurityDailyPercent / 100f);

            if (upgrade.MilitiaDailyFlat != 0 || upgrade.MilitiaDailyPercent != 0)
            {
                float militiaChange = 0f;
                if (upgrade.MilitiaDailyFlat != 0)
                    militiaChange += upgrade.MilitiaDailyFlat;

                if (upgrade.MilitiaDailyPercent != 0 && town.MilitiaChange != 0)
                    militiaChange += town.MilitiaChange * (upgrade.MilitiaDailyPercent / 100f);
            }

            if (upgrade.FoodDailyFlat != 0)
                town.FoodStocks += upgrade.FoodDailyFlat;

            if (upgrade.FoodDailyPercent != 0 && town.FoodChange != 0)
                town.FoodStocks += town.FoodChange * (upgrade.FoodDailyPercent / 100f);

            // Accumulate tax bonuses (applied when needed)
            if (upgrade.TaxIncomeFlat != 0 || upgrade.TaxIncomePercent != 0)
            {
                string key = settlement.StringId;
                if (!_fiefTaxBonuses.ContainsKey(key))
                    _fiefTaxBonuses[key] = 0;

                _fiefTaxBonuses[key] += upgrade.TaxIncomeFlat;
            }
        }

        public int GetFiefTaxBonus(Settlement settlement)
        {
            if (settlement == null) return 0;
            string key = settlement.StringId;
            return _fiefTaxBonuses.ContainsKey(key) ? _fiefTaxBonuses[key] : 0;
        }

        public int GetFiefGarrisonCapacityBonus(Settlement settlement)
        {
            if (settlement == null) return 0;

            var settings = UpgradeAction.CurrentSettings;
            if (settings?.FiefUpgrades == null) return 0;

            var upgrades = GetFiefUpgrades(settlement);
            int bonus = 0;

            foreach (var upgradeId in upgrades)
            {
                var upgrade = settings.FiefUpgrades.FirstOrDefault(u => u.ID == upgradeId);
                if (upgrade != null)
                    bonus += upgrade.GarrisonCapacityBonus;
            }

            return bonus;
        }
        #endregion

        #region Clan Upgrades
        public bool HasClanUpgrade(Clan clan, string upgradeId)
        {
            if (clan == null || string.IsNullOrEmpty(upgradeId)) return false;
            string key = clan.StringId;
            return _clanUpgrades.ContainsKey(key) && _clanUpgrades[key].Contains(upgradeId);
        }

        public List<string> GetClanUpgrades(Clan clan)
        {
            if (clan == null) return new List<string>();
            string key = clan.StringId;
            return _clanUpgrades.ContainsKey(key) ? new List<string>(_clanUpgrades[key]) : new List<string>();
        }

        public bool AddClanUpgrade(Clan clan, string upgradeId)
        {
            if (clan == null || string.IsNullOrEmpty(upgradeId)) return false;
            string key = clan.StringId;

            if (!_clanUpgrades.ContainsKey(key))
                _clanUpgrades[key] = new List<string>();

            if (_clanUpgrades[key].Contains(upgradeId))
                return false;

            _clanUpgrades[key].Add(upgradeId);
            return true;
        }

        private void ProcessClanUpgrades()
        {
            var settings = UpgradeAction.CurrentSettings;
            if (settings?.ClanUpgrades == null) return;

            foreach (var kvp in _clanUpgrades.ToList())
            {
                var clan = Clan.All.FirstOrDefault(c => c.StringId == kvp.Key);
                if (clan == null || clan.IsEliminated) continue;

                foreach (var upgradeId in kvp.Value)
                {
                    var upgrade = settings.ClanUpgrades.FirstOrDefault(u => u.ID == upgradeId);
                    if (upgrade == null) continue;

                    try
                    {
                        ApplyClanUpgradeEffects(clan, upgrade);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[BLT Upgrades] Error applying clan upgrade {upgradeId} to {clan.Name}: {ex}");
                    }
                }
            }
        }

        private void ApplyClanUpgradeEffects(Clan clan, ClanUpgrade upgrade)
        {
            if (clan == null) return;

            // Apply clan-wide effects
            if (upgrade.RenownDaily != 0)
                clan.Renown += upgrade.RenownDaily;

            // Apply effects to all clan settlements
            foreach (var settlement in clan.Settlements)
            {
                if (settlement?.Town == null) continue;
                var town = settlement.Town;

                if (upgrade.LoyaltyDailyFlat != 0)
                    town.Loyalty += upgrade.LoyaltyDailyFlat;

                if (upgrade.LoyaltyDailyPercent != 0 && town.LoyaltyChange != 0)
                    town.Loyalty += town.LoyaltyChange * (upgrade.LoyaltyDailyPercent / 100f);

                if (upgrade.ProsperityDailyFlat != 0)
                    town.Prosperity += upgrade.ProsperityDailyFlat;

                if (upgrade.ProsperityDailyPercent != 0 && town.ProsperityChange != 0)
                    town.Prosperity += town.ProsperityChange * (upgrade.ProsperityDailyPercent / 100f);

                if (upgrade.SecurityDailyFlat != 0)
                    town.Security += upgrade.SecurityDailyFlat;

                if (upgrade.SecurityDailyPercent != 0 && town.SecurityChange != 0)
                    town.Security += town.SecurityChange * (upgrade.SecurityDailyPercent / 100f);

                if (upgrade.MilitiaDailyFlat != 0 || upgrade.MilitiaDailyPercent != 0)
                {
                    float militiaChange = 0f;
                    if (upgrade.MilitiaDailyFlat != 0)
                        militiaChange += upgrade.MilitiaDailyFlat;

                    if (upgrade.MilitiaDailyPercent != 0 && town.MilitiaChange != 0)
                        militiaChange += town.MilitiaChange * (upgrade.MilitiaDailyPercent / 100f);
                }

                // Accumulate tax bonuses
                if (upgrade.TaxIncomeFlat != 0 || upgrade.TaxIncomePercent != 0)
                {
                    string key = settlement.StringId;
                    if (!_clanTaxBonuses.ContainsKey(key))
                        _clanTaxBonuses[key] = 0;

                    _clanTaxBonuses[key] += upgrade.TaxIncomeFlat;
                }
            }
        }

        public int GetClanPartySizeBonus(Clan clan)
        {
            if (clan == null) return 0;

            var settings = UpgradeAction.CurrentSettings;
            if (settings?.ClanUpgrades == null) return 0;

            var upgrades = GetClanUpgrades(clan);
            int bonus = 0;

            foreach (var upgradeId in upgrades)
            {
                var upgrade = settings.ClanUpgrades.FirstOrDefault(u => u.ID == upgradeId);
                if (upgrade != null)
                    bonus += upgrade.PartySizeBonus;
            }

            return bonus;
        }

        public int GetClanTaxBonus(Settlement settlement)
        {
            if (settlement == null) return 0;
            string key = settlement.StringId;
            return _clanTaxBonuses.ContainsKey(key) ? _clanTaxBonuses[key] : 0;
        }
        #endregion

        #region Kingdom Upgrades
        public bool HasKingdomUpgrade(Kingdom kingdom, string upgradeId)
        {
            if (kingdom == null || string.IsNullOrEmpty(upgradeId)) return false;
            string key = kingdom.StringId;
            return _kingdomUpgrades.ContainsKey(key) && _kingdomUpgrades[key].Contains(upgradeId);
        }

        public List<string> GetKingdomUpgrades(Kingdom kingdom)
        {
            if (kingdom == null) return new List<string>();
            string key = kingdom.StringId;
            return _kingdomUpgrades.ContainsKey(key) ? new List<string>(_kingdomUpgrades[key]) : new List<string>();
        }

        public bool AddKingdomUpgrade(Kingdom kingdom, string upgradeId)
        {
            if (kingdom == null || string.IsNullOrEmpty(upgradeId)) return false;
            string key = kingdom.StringId;

            if (!_kingdomUpgrades.ContainsKey(key))
                _kingdomUpgrades[key] = new List<string>();

            if (_kingdomUpgrades[key].Contains(upgradeId))
                return false;

            _kingdomUpgrades[key].Add(upgradeId);
            return true;
        }

        private void ProcessKingdomUpgrades()
        {
            var settings = UpgradeAction.CurrentSettings;
            if (settings?.KingdomUpgrades == null) return;

            foreach (var kvp in _kingdomUpgrades.ToList())
            {
                var kingdom = Kingdom.All.FirstOrDefault(k => k.StringId == kvp.Key);
                if (kingdom == null || kingdom.IsEliminated) continue;

                foreach (var upgradeId in kvp.Value)
                {
                    var upgrade = settings.KingdomUpgrades.FirstOrDefault(u => u.ID == upgradeId);
                    if (upgrade == null) continue;

                    try
                    {
                        ApplyKingdomUpgradeEffects(kingdom, upgrade);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[BLT Upgrades] Error applying kingdom upgrade {upgradeId} to {kingdom.Name}: {ex}");
                    }
                }
            }
        }

        private void ApplyKingdomUpgradeEffects(Kingdom kingdom, KingdomUpgrade upgrade)
        {
            if (kingdom == null) return;

            // Apply kingdom-wide effects
            if (upgrade.InfluenceDaily != 0 && kingdom.Leader?.Clan != null)
            {
                kingdom.Leader.Clan.Influence += upgrade.InfluenceDaily;
            }

            // Apply effects to all kingdom clans
            foreach (var clan in kingdom.Clans)
            {
                if (clan == null || clan.IsEliminated) continue;

                if (upgrade.RenownDaily != 0)
                    clan.Renown += upgrade.RenownDaily;

                // Apply effects to all settlements in each clan
                foreach (var settlement in clan.Settlements)
                {
                    if (settlement?.Town == null) continue;
                    var town = settlement.Town;

                    if (upgrade.LoyaltyDailyFlat != 0)
                        town.Loyalty += upgrade.LoyaltyDailyFlat;

                    if (upgrade.LoyaltyDailyPercent != 0 && town.LoyaltyChange != 0)
                        town.Loyalty += town.LoyaltyChange * (upgrade.LoyaltyDailyPercent / 100f);

                    if (upgrade.ProsperityDailyFlat != 0)
                        town.Prosperity += upgrade.ProsperityDailyFlat;

                    if (upgrade.ProsperityDailyPercent != 0 && town.ProsperityChange != 0)
                        town.Prosperity += town.ProsperityChange * (upgrade.ProsperityDailyPercent / 100f);

                    if (upgrade.SecurityDailyFlat != 0)
                        town.Security += upgrade.SecurityDailyFlat;

                    if (upgrade.SecurityDailyPercent != 0 && town.SecurityChange != 0)
                        town.Security += town.SecurityChange * (upgrade.SecurityDailyPercent / 100f);

                    if (upgrade.MilitiaDailyFlat != 0 || upgrade.MilitiaDailyPercent != 0)
                    {
                        float militiaChange = 0f;
                        if (upgrade.MilitiaDailyFlat != 0)
                            militiaChange += upgrade.MilitiaDailyFlat;

                        if (upgrade.MilitiaDailyPercent != 0 && town.MilitiaChange != 0)
                            militiaChange += town.MilitiaChange * (upgrade.MilitiaDailyPercent / 100f);
                    }

                    // Accumulate tax bonuses
                    if (upgrade.TaxIncomeFlat != 0 || upgrade.TaxIncomePercent != 0)
                    {
                        string key = settlement.StringId;
                        if (!_kingdomTaxBonuses.ContainsKey(key))
                            _kingdomTaxBonuses[key] = 0;

                        _kingdomTaxBonuses[key] += upgrade.TaxIncomeFlat;
                    }
                }
            }
        }

        public int GetKingdomPartySizeBonus(Kingdom kingdom)
        {
            if (kingdom == null) return 0;

            var settings = UpgradeAction.CurrentSettings;
            if (settings?.KingdomUpgrades == null) return 0;

            var upgrades = GetKingdomUpgrades(kingdom);
            int bonus = 0;

            foreach (var upgradeId in upgrades)
            {
                var upgrade = settings.KingdomUpgrades.FirstOrDefault(u => u.ID == upgradeId);
                if (upgrade != null)
                    bonus += upgrade.PartySizeBonus;
            }

            return bonus;
        }

        public int GetKingdomTaxBonus(Settlement settlement)
        {
            if (settlement == null) return 0;
            string key = settlement.StringId;
            return _kingdomTaxBonuses.ContainsKey(key) ? _kingdomTaxBonuses[key] : 0;
        }
        #endregion

        #region Helper Methods
        public int GetTotalTaxBonus(Settlement settlement)
        {
            if (settlement == null) return 0;
            return GetFiefTaxBonus(settlement) + GetClanTaxBonus(settlement) + GetKingdomTaxBonus(settlement);
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
        #endregion
    }
}
