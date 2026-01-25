using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using BLTAdoptAHero.Actions.Upgrades;

namespace BLTAdoptAHero
{
    /// <summary>
    /// BLTUpgradeBehavior - typed, safe, non-mutating provider.
    /// - Persists upgrades as comma-separated strings per fief/clan/kingdom
    /// - Exposes Get/Has/Add/Remove helpers used by UI/actions
    /// - Exposes typed aggregated getters that consult the injected Settings instance
    /// - Handles daily troop spawning from clan upgrades
    /// </summary>
    public class UpgradeBehavior : CampaignBehaviorBase
    {
        public static UpgradeBehavior Current { get; private set; }

        // persisted storage (CSV strings per id) - compatible with older saves
        private Dictionary<string, string> _fiefUpgrades = new();
        private Dictionary<string, string> _clanUpgrades = new();
        private Dictionary<string, string> _kingdomUpgrades = new();

        // Troop spawn accumulation: "clanId:upgradeId" -> accumulated value
        private Dictionary<string, float> _troopSpawnAccumulation = new();

        private GlobalCommonConfig ConfigSafe => GlobalCommonConfig.Get();

        public UpgradeBehavior()
        {
            Current = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, OnDailyTickClan);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("BLT_FiefUpgrades", ref _fiefUpgrades);
            dataStore.SyncData("BLT_ClanUpgrades", ref _clanUpgrades);
            dataStore.SyncData("BLT_KingdomUpgrades", ref _kingdomUpgrades);
            dataStore.SyncData("BLT_TroopSpawnAccumulation", ref _troopSpawnAccumulation);

            _fiefUpgrades ??= new Dictionary<string, string>();
            _clanUpgrades ??= new Dictionary<string, string>();
            _kingdomUpgrades ??= new Dictionary<string, string>();
            _troopSpawnAccumulation ??= new Dictionary<string, float>();
        }

        #region Serialization helpers
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

        #region Fief Get/Has/Add/Remove
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
            if (settlement == null || string.IsNullOrEmpty(upgradeId)) return false;
            var list = GetFiefUpgrades(settlement);
            return list.Contains(upgradeId);
        }

        public bool AddFiefUpgrade(Settlement settlement, string upgradeId)
        {
            if (settlement == null || string.IsNullOrEmpty(upgradeId)) return false;
            string key = settlement.StringId;

            var upgrades = _fiefUpgrades.TryGetValue(key, out string upgradeString)
                ? ParseUpgradeString(upgradeString)
                : new List<string>();

            if (upgrades.Contains(upgradeId)) return false;

            upgrades.Add(upgradeId);
            _fiefUpgrades[key] = SerializeUpgradeList(upgrades);
            return true;
        }

        public bool RemoveFiefUpgrade(Settlement settlement, string upgradeId)
        {
            if (settlement == null || string.IsNullOrEmpty(upgradeId)) return false;
            string key = settlement.StringId;

            if (!_fiefUpgrades.TryGetValue(key, out string upgradeString)) return false;

            var upgrades = ParseUpgradeString(upgradeString);
            bool removed = upgrades.Remove(upgradeId);
            if (!removed) return false;

            if (upgrades.Count == 0) _fiefUpgrades.Remove(key);
            else _fiefUpgrades[key] = SerializeUpgradeList(upgrades);

            return true;
        }
        #endregion

        #region Clan Get/Has/Add/Remove
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
            if (clan == null || string.IsNullOrEmpty(upgradeId)) return false;
            var list = GetClanUpgrades(clan);
            return list.Contains(upgradeId);
        }

        public bool AddClanUpgrade(Clan clan, string upgradeId)
        {
            if (clan == null || string.IsNullOrEmpty(upgradeId)) return false;
            string key = clan.StringId;

            var upgrades = _clanUpgrades.TryGetValue(key, out string upgradeString)
                ? ParseUpgradeString(upgradeString)
                : new List<string>();

            if (upgrades.Contains(upgradeId)) return false;

            upgrades.Add(upgradeId);
            _clanUpgrades[key] = SerializeUpgradeList(upgrades);
            return true;
        }

        public bool RemoveClanUpgrade(Clan clan, string upgradeId)
        {
            if (clan == null || string.IsNullOrEmpty(upgradeId)) return false;
            string key = clan.StringId;

            if (!_clanUpgrades.TryGetValue(key, out string upgradeString)) return false;

            var upgrades = ParseUpgradeString(upgradeString);
            bool removed = upgrades.Remove(upgradeId);
            if (!removed) return false;

            if (upgrades.Count == 0) _clanUpgrades.Remove(key);
            else _clanUpgrades[key] = SerializeUpgradeList(upgrades);

            return true;
        }
        #endregion

        #region Kingdom Get/Has/Add/Remove
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
            if (kingdom == null || string.IsNullOrEmpty(upgradeId)) return false;
            var list = GetKingdomUpgrades(kingdom);
            return list.Contains(upgradeId);
        }

        public bool AddKingdomUpgrade(Kingdom kingdom, string upgradeId)
        {
            if (kingdom == null || string.IsNullOrEmpty(upgradeId)) return false;
            string key = kingdom.StringId;

            var upgrades = _kingdomUpgrades.TryGetValue(key, out string upgradeString)
                ? ParseUpgradeString(upgradeString)
                : new List<string>();

            if (upgrades.Contains(upgradeId)) return false;

            upgrades.Add(upgradeId);
            _kingdomUpgrades[key] = SerializeUpgradeList(upgrades);
            return true;
        }

        public bool RemoveKingdomUpgrade(Kingdom kingdom, string upgradeId)
        {
            if (kingdom == null || string.IsNullOrEmpty(upgradeId)) return false;
            string key = kingdom.StringId;

            if (!_kingdomUpgrades.TryGetValue(key, out string upgradeString)) return false;

            var upgrades = ParseUpgradeString(upgradeString);
            bool removed = upgrades.Remove(upgradeId);
            if (!removed) return false;

            if (upgrades.Count == 0) _kingdomUpgrades.Remove(key);
            else _kingdomUpgrades[key] = SerializeUpgradeList(upgrades);

            return true;
        }
        #endregion

        #region Troop Spawning Logic

        /// <summary>
        /// Calculate the effective tier for a troop spawning upgrade, including buffs from other upgrades
        /// </summary>
        private int GetEffectiveTroopTier(Clan clan, ClanUpgrade spawningUpgrade)
        {
            if (clan == null || spawningUpgrade == null) return 1;

            int baseTier = spawningUpgrade.TroopTier;
            int tierBonus = 0;

            // Check all clan upgrades for tier buffs
            foreach (var upgradeId in GetClanUpgrades(clan))
            {
                var upgrade = ConfigSafe?.ClanUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
                if (upgrade == null) continue;

                // Check if this upgrade buffs the spawning upgrade
                if (upgrade.BuffsTroopTierOfIDs.Contains(spawningUpgrade.ID, StringComparer.OrdinalIgnoreCase))
                {
                    tierBonus += upgrade.TroopTierBonus;
                }
            }

            return Math.Max(1, baseTier + tierBonus);
        }

        /// <summary>
        /// Get the appropriate troop character based on culture, tree type, and tier
        /// </summary>
        private CharacterObject GetTroopForCulture(CultureObject culture, TroopTreeType treeType, int tier)
        {
            if (culture == null) return null;
            if (culture.BasicTroop == null && culture.EliteBasicTroop == null) return null;
            else if (culture.BasicTroop == null && culture.EliteBasicTroop != null) treeType = TroopTreeType.Noble;

            try
            {
                if (treeType == TroopTreeType.Noble)
                {
                    // Noble tree: tiers 2-6
                    tier = Math.Min(Math.Max(tier, 2), 6);
                    switch (tier)
                    {
                        case 2: return culture.EliteBasicTroop;
                        case 3: return culture.EliteBasicTroop?.UpgradeTargets?.GetRandomElement();
                        case 4: return culture.EliteBasicTroop?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement();
                        case 5: return culture.EliteBasicTroop?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement();
                        case 6: return culture.EliteBasicTroop?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement();
                    }
                }
                else // Basic tree
                {
                    // Basic tree: tiers 1-5
                    tier = Math.Min(Math.Max(tier, 1), 5);
                    switch (tier)
                    {
                        case 1: return culture.BasicTroop;
                        case 2: return culture.BasicTroop?.UpgradeTargets?.GetRandomElement();
                        case 3: return culture.BasicTroop?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement();
                        case 4: return culture.BasicTroop?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement();
                        case 5: return culture.BasicTroop?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement();
                    }
                }
            }
            catch
            {
                // Fallback to basic troop
                if (culture.BasicTroop == null) return culture.BasicTroop;
                else return null;
            }
            if (culture.BasicTroop == null) return culture.BasicTroop;
            else return null;
        }

        private void OnDailyTickClan(Clan clan)
        {
            try
            {
                if (clan == null || ConfigSafe == null) return;

                // Apply renown
                ApplyRenownDaily(clan);

                // Process troop spawning upgrades
                foreach (var upgradeId in GetClanUpgrades(clan))
                {
                    var upgrade = ConfigSafe.ClanUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
                    if (upgrade == null || upgrade.DailyTroopSpawnAmount <= 0) continue;

                    // Check mercenary restriction
                    if (upgrade.MercOnly && !clan.IsUnderMercenaryService) continue;

                    string accumulationKey = $"{clan.StringId}:{upgradeId}";

                    // Get current accumulation
                    if (!_troopSpawnAccumulation.TryGetValue(accumulationKey, out float accumulated))
                    {
                        accumulated = 0f;
                    }

                    // Add daily amount
                    accumulated += upgrade.DailyTroopSpawnAmount;

                    // Spawn troops if we have at least 1.0 accumulated
                    while (accumulated >= 1.0f && clan.WarPartyComponents.Any(p => p.MobileParty.MemberRoster.Count < p.Party.PartySizeLimit))
                    {
                        SpawnTroopForClan(clan, upgrade);
                        accumulated -= 1.0f;
                    }

                    // Store accumulation
                    if (accumulated > 0f)
                    {
                        _troopSpawnAccumulation[accumulationKey] = accumulated;
                    }
                    else
                    {
                        _troopSpawnAccumulation.Remove(accumulationKey);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the game
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage($"[BLT Upgrade] Daily tick error: {ex.Message}")
                );
            }
        }

        private void SpawnTroopForClan(Clan clan, ClanUpgrade upgrade)
        {
            try
            {
                var party = clan.Leader.PartyBelongedTo != null && clan.Leader.PartyBelongedTo.Party.MemberRoster.Count < clan.Leader.PartyBelongedTo.Party.PartySizeLimit ? clan.Leader.PartyBelongedTo : clan.WarPartyComponents.Where(p => p.MobileParty.MemberRoster.Count < p.Party.PartySizeLimit).SelectRandom().MobileParty ?? null;
                if (party == null) return;

                var culture = clan.Culture;
                if (culture == null) return;

                // Get effective tier (including buffs)
                int effectiveTier = GetEffectiveTroopTier(clan, upgrade);

                // Get the troop
                var troop = GetTroopForCulture(culture, upgrade.TroopTree, effectiveTier);

                if (troop != null)
                {
                    party.MemberRoster.AddToCounts(troop, 1);
                }
            }
            catch (Exception ex)
            {
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage($"[BLT Upgrade] Spawn troop error: {ex.Message}")
                );
            }
        }

        #endregion

        #region Typed aggregation helpers (no duplicates)
        // Sum fief float values
        private float SumFiefFloat(Settlement s, Func<FiefUpgrade, float> selector)
        {
            if (s == null || ConfigSafe == null) return 0f;
            float sum = 0f;
            foreach (var id in GetFiefUpgrades(s))
            {
                var up = ConfigSafe.FiefUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null && (!up.CoastalOnly || (up.CoastalOnly && s.HasPort))) sum += selector(up);
            }
            return sum;
        }

        // Sum clan float values (applies to clan's settlements + renown)
        private float SumClanFloat(Clan clan, Func<ClanUpgrade, float> selector, bool applyToVassalsOnly = false)
        {
            if (clan == null || ConfigSafe == null) return 0f;
            float sum = 0f;
            foreach (var id in GetClanUpgrades(clan))
            {
                var up = ConfigSafe.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null)
                {
                    bool mercAllow = !up.MercOnly || (up.MercOnly && clan.IsUnderMercenaryService);
                    bool vassalAllow = !up.ApplyToVassals || (up.ApplyToVassals && applyToVassalsOnly);

                    if (mercAllow && vassalAllow)
                    {
                        sum += selector(up);
                    }
                }
            }
            return sum;
        }

        // Sum kingdom float values (applies to kingdom's settlements + clans' renown)
        private float SumKingdomFloat(Kingdom kingdom, Func<KingdomUpgrade, float> selector)
        {
            if (kingdom == null || ConfigSafe == null) return 0f;
            float sum = 0f;
            foreach (var id in GetKingdomUpgrades(kingdom))
            {
                var up = ConfigSafe.KingdomUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null) sum += selector(up);
            }
            return sum;
        }

        // Sum fief/clan/kingdom aggregated float for a settlement
        private float SumSettlementFloatTyped(
            Settlement s,
            Func<FiefUpgrade, float> fiefSel,
            Func<ClanUpgrade, float> clanSel,
            Func<KingdomUpgrade, float> kingSel)
        {
            if (s == null) return 0f;
            float sum = 0f;

            // fief
            sum += SumFiefFloat(s, fiefSel);

            // clan
            var clan = s.OwnerClan;
            if (clan != null)
            {
                sum += SumClanFloat(clan, clanSel);

                // kingdom
                var kingdom = clan.Kingdom;
                if (kingdom != null)
                    sum += SumKingdomFloat(kingdom, kingSel);
            }

            return sum;
        }

        // Int versions
        private int SumFiefInt(Settlement s, Func<FiefUpgrade, int> selector)
        {
            if (s == null || ConfigSafe == null) return 0;
            int sum = 0;
            foreach (var id in GetFiefUpgrades(s))
            {
                var up = ConfigSafe.FiefUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null) sum += selector(up);
            }
            return sum;
        }

        private int SumClanInt(Clan clan, Func<ClanUpgrade, int> selector, bool applyToVassalsOnly = false)
        {
            if (clan == null || ConfigSafe == null) return 0;
            int sum = 0;
            foreach (var id in GetClanUpgrades(clan))
            {
                var up = ConfigSafe.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null)
                {
                    bool mercAllow = !up.MercOnly || (up.MercOnly && clan.IsUnderMercenaryService);
                    bool vassalAllow = !up.ApplyToVassals || (up.ApplyToVassals && applyToVassalsOnly);

                    if (mercAllow && vassalAllow)
                    {
                        sum += selector(up);
                    }
                }
            }
            return sum;
        }

        private int SumKingdomInt(Kingdom kingdom, Func<KingdomUpgrade, int> selector)
        {
            if (kingdom == null || ConfigSafe == null) return 0;
            int sum = 0;
            foreach (var id in GetKingdomUpgrades(kingdom))
            {
                var up = ConfigSafe.KingdomUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null) sum += selector(up);
            }
            return sum;
        }

        private int SumSettlementIntTyped(Settlement s,
            Func<FiefUpgrade, int> fiefSel,
            Func<ClanUpgrade, int> clanSel,
            Func<KingdomUpgrade, int> kingSel)
        {
            if (s == null) return 0;
            int sum = 0;

            sum += SumFiefInt(s, fiefSel);

            var clan = s.OwnerClan;
            if (clan != null)
            {
                sum += SumClanInt(clan, clanSel);
                var kingdom = clan.Kingdom;
                if (kingdom != null)
                    sum += SumKingdomInt(kingdom, kingSel);
            }

            return sum;
        }
        #endregion

        #region Aggregated getters used by models and actions
        // Tax
        public int GetTotalTaxBonus(Settlement settlement)
            => SumSettlementIntTyped(settlement,
                f => (int)f.TaxIncomeFlat,
                c => (int)c.TaxIncomeFlat,
                k => (int)k.TaxIncomeFlat);

        // Hearth
        public float GetTotalHearthDaily(Settlement settlement)
            => SumSettlementFloatTyped(settlement,
                f => (float)f.HearthDaily,
                c => (float)c.HearthDaily,
                k => (float)k.HearthDaily);

        // Garrison capacity (sum fief+clan+kingdom)
        public int GetTotalGarrisonCapacityBonus(Settlement settlement)
            => SumSettlementIntTyped(settlement,
                f => f.GarrisonCapacityBonus,
                c => c.GarrisonCapacityBonus,
                k => k.GarrisonCapacityBonus);

        // Party size
        public int GetClanPartySizeBonus(Clan clan)
            => SumClanInt(clan, c => c.PartySizeBonus);

        public int GetKingdomPartySizeBonus(Kingdom kingdom)
            => SumKingdomInt(kingdom, k => k.PartySizeBonus);

        public int GetTotalPartySizeBonus(Hero hero)
        {
            if (hero?.Clan == null) return 0;
            int bonus = 0;
            bonus += GetClanPartySizeBonus(hero.Clan);
            if (hero.Clan.Kingdom != null) bonus += GetKingdomPartySizeBonus(hero.Clan.Kingdom);
            return bonus;
        }

        // Party speed
        public float GetClanPartySpeedBonus(Clan clan)
            => SumClanFloat(clan, c => c.PartySpeedBonus);

        public float GetKingdomPartySpeedBonus(Kingdom kingdom)
            => SumKingdomFloat(kingdom, k => k.PartySpeedBonus);

        public float GetTotalPartySpeedBonus(Hero hero)
        {
            if (hero?.Clan == null) return 0;
            float bonus = 0f;
            bonus += GetClanPartySpeedBonus(hero.Clan);
            if (hero.Clan.Kingdom != null) bonus += GetKingdomPartySpeedBonus(hero.Clan.Kingdom);
            return bonus;
        }

        // Party amount
        public int GetClanPartyAmountBonus(Clan clan)
            => SumClanInt(clan, c => c.PartyAmountBonus);

        public int GetTotalPartyAmountBonus(Clan clan)
        {
            if (clan == null) return 0;
            int bonus = 0;
            bonus += GetClanPartyAmountBonus(clan);
            return bonus;
        }

        // Max vassals
        public int GetClanMaxVassalsBonus(Clan clan)
            => SumClanInt(clan, c => c.MaxVassalsBonus);

        public int GetTotalMaxVassalsBonus(Clan clan)
        {
            if (clan == null) return 0;
            int bonus = 0;
            bonus += GetClanMaxVassalsBonus(clan);
            return bonus;
        }

        // Renown
        public float GetClanRenownDaily(Clan clan)
            => SumClanFloat(clan, c => c.RenownDaily);

        public float GetKingdomRenownDaily(Kingdom kingdom)
            => SumKingdomFloat(kingdom, k => k.RenownDaily);

        public float GetTotalRenownDaily(Hero hero)
        {
            if (hero?.Clan == null) return 0f;
            float bonus = 0;
            bonus += GetClanRenownDaily(hero.Clan);
            if (hero.Clan.Kingdom != null) bonus += GetKingdomRenownDaily(hero.Clan.Kingdom);
            return bonus;
        }

        public void ApplyRenownDaily(Clan clan)
        {
            float bonus = GetTotalRenownDaily(clan.Leader);
            clan.AddRenown(bonus, false);
        }

        // Max Clans
        public int GetKingdomMaxClansBonus(Kingdom kingdom)
            => SumKingdomInt(kingdom, k => k.MaxClansBonus);

        public int GetTotalGetKingdomMaxClansBonus(Kingdom kingdom)
        {
            if (kingdom == null) return 0;
            int bonus = 0;
            bonus += GetKingdomMaxClansBonus(kingdom);
            return bonus;
        }

        // Mercenary Income
        public int GetFlatClanMercBonus(Clan clan)
            => SumClanInt(clan, c => c.MercIncomeFlat);

        public float GetPercentClanMercBonus(Clan clan)
            => 1f + SumClanFloat(clan, c => c.MercIncomePercent);

        public int GetFlatMercBonus(Hero hero)
        {
            if (hero?.Clan == null) return 0;
            int bonus = GetFlatClanMercBonus(hero.Clan);
            return bonus;
        }

        // DAILY FLAT / PERCENT getters (typed)
        public float GetTotalLoyaltyDailyFlat(Settlement s)
            => SumSettlementFloatTyped(s, f => f.LoyaltyDailyFlat, c => c.LoyaltyDailyFlat, k => k.LoyaltyDailyFlat);

        public float GetTotalLoyaltyDailyPercent(Settlement s)
            => SumSettlementFloatTyped(s, f => f.LoyaltyDailyPercent, c => c.LoyaltyDailyPercent, k => k.LoyaltyDailyPercent);

        public float GetTotalProsperityDailyFlat(Settlement s)
            => SumSettlementFloatTyped(s, f => f.ProsperityDailyFlat, c => c.ProsperityDailyFlat, k => k.ProsperityDailyFlat);

        public float GetTotalProsperityDailyPercent(Settlement s)
            => SumSettlementFloatTyped(s, f => f.ProsperityDailyPercent, c => c.ProsperityDailyPercent, k => k.ProsperityDailyPercent);

        public float GetTotalSecurityDailyFlat(Settlement s)
            => SumSettlementFloatTyped(s, f => f.SecurityDailyFlat, c => c.SecurityDailyFlat, k => k.SecurityDailyFlat);

        public float GetTotalSecurityDailyPercent(Settlement s)
            => SumSettlementFloatTyped(s, f => f.SecurityDailyPercent, c => c.SecurityDailyPercent, k => k.SecurityDailyPercent);

        public float GetTotalMilitiaDailyFlat(Settlement s)
            => SumSettlementFloatTyped(s, f => f.MilitiaDailyFlat, c => c.MilitiaDailyFlat, k => k.MilitiaDailyFlat);

        public float GetTotalMilitiaDailyPercent(Settlement s)
            => SumSettlementFloatTyped(s, f => f.MilitiaDailyPercent, c => c.MilitiaDailyPercent, k => k.MilitiaDailyPercent);

        public float GetTotalFoodDailyFlat(Settlement s)
            => SumSettlementFloatTyped(s, f => f.FoodDailyFlat, c => c.FoodDailyFlat, k => k.FoodDailyFlat);

        public float GetTotalFoodDailyPercent(Settlement s)
            => SumSettlementFloatTyped(s, f => f.FoodDailyPercent, c => c.FoodDailyPercent, k => k.FoodDailyPercent);

        // Backward-compatible short names used by some models
        public float GetLoyaltyFlat(Settlement s) => GetTotalLoyaltyDailyFlat(s);
        public float GetLoyaltyPercent(Settlement s) => GetTotalLoyaltyDailyPercent(s);

        public float GetProsperityFlat(Settlement s) => GetTotalProsperityDailyFlat(s);
        public float GetProsperityPercent(Settlement s) => GetTotalProsperityDailyPercent(s);

        public float GetSecurityFlat(Settlement s) => GetTotalSecurityDailyFlat(s);
        public float GetSecurityPercent(Settlement s) => GetTotalSecurityDailyPercent(s);

        public float GetMilitiaFlat(Settlement s) => GetTotalMilitiaDailyFlat(s);
        public float GetMilitiaPercent(Settlement s) => GetTotalMilitiaDailyPercent(s);

        public float GetFoodFlat(Settlement s) => GetTotalFoodDailyFlat(s);
        public float GetFoodPercent(Settlement s) => GetTotalFoodDailyPercent(s);

        #endregion
    }
}