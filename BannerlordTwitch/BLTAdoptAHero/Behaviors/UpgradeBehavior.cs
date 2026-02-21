using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using BLTAdoptAHero.Actions.Upgrades;

namespace BLTAdoptAHero
{
    public class UpgradeBehavior : CampaignBehaviorBase
    {
        public static UpgradeBehavior Current { get; private set; }

        private Dictionary<string, string> _fiefUpgrades = new();
        private Dictionary<string, string> _clanUpgrades = new();
        private Dictionary<string, string> _kingdomUpgrades = new();
        private Dictionary<string, float> _troopSpawnAccumulation = new();

        private GlobalCommonConfig ConfigSafe => GlobalCommonConfig.Get();

        public UpgradeBehavior() { Current = this; }

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
            if (string.IsNullOrEmpty(upgradeString)) return new List<string>();
            return upgradeString
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        private static string SerializeUpgradeList(List<string> upgrades)
        {
            if (upgrades == null || upgrades.Count == 0) return string.Empty;
            return string.Join(",", upgrades);
        }
        #endregion

        #region Fief Get/Has/Add/Remove
        public List<string> GetFiefUpgrades(Settlement settlement)
        {
            if (settlement == null) return new List<string>();
            if (!_fiefUpgrades.TryGetValue(settlement.StringId, out string s)) return new List<string>();
            return ParseUpgradeString(s);
        }

        public bool HasFiefUpgrade(Settlement settlement, string upgradeId)
        {
            if (settlement == null || string.IsNullOrEmpty(upgradeId)) return false;
            return GetFiefUpgrades(settlement).Contains(upgradeId);
        }

        public bool AddFiefUpgrade(Settlement settlement, string upgradeId)
        {
            if (settlement == null || string.IsNullOrEmpty(upgradeId)) return false;
            var upgrades = _fiefUpgrades.TryGetValue(settlement.StringId, out string s)
                ? ParseUpgradeString(s) : new List<string>();
            if (upgrades.Contains(upgradeId)) return false;
            upgrades.Add(upgradeId);
            _fiefUpgrades[settlement.StringId] = SerializeUpgradeList(upgrades);
            return true;
        }

        public bool RemoveFiefUpgrade(Settlement settlement, string upgradeId)
        {
            if (settlement == null || string.IsNullOrEmpty(upgradeId)) return false;
            if (!_fiefUpgrades.TryGetValue(settlement.StringId, out string s)) return false;
            var upgrades = ParseUpgradeString(s);
            if (!upgrades.Remove(upgradeId)) return false;
            if (upgrades.Count == 0) _fiefUpgrades.Remove(settlement.StringId);
            else _fiefUpgrades[settlement.StringId] = SerializeUpgradeList(upgrades);
            return true;
        }
        #endregion

        #region Clan Get/Has/Add/Remove
        public List<string> GetClanUpgrades(Clan clan)
        {
            if (clan == null) return new List<string>();
            if (!_clanUpgrades.TryGetValue(clan.StringId, out string s)) return new List<string>();
            return ParseUpgradeString(s);
        }

        public bool HasClanUpgrade(Clan clan, string upgradeId)
        {
            if (clan == null || string.IsNullOrEmpty(upgradeId)) return false;
            return GetClanUpgrades(clan).Contains(upgradeId);
        }

        public bool AddClanUpgrade(Clan clan, string upgradeId)
        {
            if (clan == null || string.IsNullOrEmpty(upgradeId)) return false;
            var upgrades = _clanUpgrades.TryGetValue(clan.StringId, out string s)
                ? ParseUpgradeString(s) : new List<string>();
            if (upgrades.Contains(upgradeId)) return false;
            upgrades.Add(upgradeId);
            _clanUpgrades[clan.StringId] = SerializeUpgradeList(upgrades);
            return true;
        }

        public bool RemoveClanUpgrade(Clan clan, string upgradeId)
        {
            if (clan == null || string.IsNullOrEmpty(upgradeId)) return false;
            if (!_clanUpgrades.TryGetValue(clan.StringId, out string s)) return false;
            var upgrades = ParseUpgradeString(s);
            if (!upgrades.Remove(upgradeId)) return false;
            if (upgrades.Count == 0) _clanUpgrades.Remove(clan.StringId);
            else _clanUpgrades[clan.StringId] = SerializeUpgradeList(upgrades);
            return true;
        }
        #endregion

        #region Kingdom Get/Has/Add/Remove
        public List<string> GetKingdomUpgrades(Kingdom kingdom)
        {
            if (kingdom == null) return new List<string>();
            if (!_kingdomUpgrades.TryGetValue(kingdom.StringId, out string s)) return new List<string>();
            return ParseUpgradeString(s);
        }

        public bool HasKingdomUpgrade(Kingdom kingdom, string upgradeId)
        {
            if (kingdom == null || string.IsNullOrEmpty(upgradeId)) return false;
            return GetKingdomUpgrades(kingdom).Contains(upgradeId);
        }

        public bool AddKingdomUpgrade(Kingdom kingdom, string upgradeId)
        {
            if (kingdom == null || string.IsNullOrEmpty(upgradeId)) return false;
            var upgrades = _kingdomUpgrades.TryGetValue(kingdom.StringId, out string s)
                ? ParseUpgradeString(s) : new List<string>();
            if (upgrades.Contains(upgradeId)) return false;
            upgrades.Add(upgradeId);
            _kingdomUpgrades[kingdom.StringId] = SerializeUpgradeList(upgrades);
            return true;
        }

        public bool RemoveKingdomUpgrade(Kingdom kingdom, string upgradeId)
        {
            if (kingdom == null || string.IsNullOrEmpty(upgradeId)) return false;
            if (!_kingdomUpgrades.TryGetValue(kingdom.StringId, out string s)) return false;
            var upgrades = ParseUpgradeString(s);
            if (!upgrades.Remove(upgradeId)) return false;
            if (upgrades.Count == 0) _kingdomUpgrades.Remove(kingdom.StringId);
            else _kingdomUpgrades[kingdom.StringId] = SerializeUpgradeList(upgrades);
            return true;
        }
        #endregion

        #region Troop Spawning

        /// <summary>
        /// Effective tier for a clan upgrade's troop spawning, buffed by other clan upgrades.
        /// </summary>
        private int GetEffectiveTroopTier(Clan clan, ClanUpgrade spawningUpgrade)
        {
            if (clan == null || spawningUpgrade == null) return 1;
            int tierBonus = 0;
            foreach (var upgradeId in GetClanUpgrades(clan))
            {
                var upgrade = ConfigSafe?.ClanUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
                if (upgrade == null) continue;
                if ((upgrade.LordOnly && clan.IsUnderMercenaryService) || (upgrade.MercOnly && !clan.IsUnderMercenaryService)) continue;
                if (upgrade.BuffsTroopTierOfIDs.Contains(spawningUpgrade.ID, StringComparer.OrdinalIgnoreCase))
                    tierBonus += upgrade.TroopTierBonus;
            }
            return Math.Max(1, spawningUpgrade.TroopTier + tierBonus);
        }

        /// <summary>
        /// Effective tier for a kingdom upgrade's troop spawning, buffed by other kingdom upgrades.
        /// </summary>
        private int GetEffectiveTroopTierFromKingdom(Clan clan, KingdomUpgrade spawningUpgrade)
        {
            if (clan == null || spawningUpgrade == null || clan.Kingdom == null) return 1;
            int tierBonus = 0;
            foreach (var upgradeId in GetKingdomUpgrades(clan.Kingdom))
            {
                var upgrade = ConfigSafe?.KingdomUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
                if (upgrade == null) continue;
                if (upgrade.BuffsTroopTierOfIDs.Contains(spawningUpgrade.ID, StringComparer.OrdinalIgnoreCase))
                    tierBonus += upgrade.TroopTierBonus;
            }
            return Math.Max(1, spawningUpgrade.TroopTier + tierBonus);
        }

        /// <summary>
        /// Traverse the troop tree to find a troop of the given tier and tree type.
        /// </summary>
        private CharacterObject GetTroopForCulture(CultureObject culture, TroopTreeType treeType, int tier)
        {
            if (culture == null) return null;
            if (culture.BasicTroop == null && culture.EliteBasicTroop == null) return null;
            if (culture.BasicTroop == null) treeType = TroopTreeType.Noble;

            try
            {
                if (treeType == TroopTreeType.Noble)
                {
                    tier = Math.Min(Math.Max(tier, 2), 6);
                    return tier switch
                    {
                        2 => culture.EliteBasicTroop,
                        3 => culture.EliteBasicTroop?.UpgradeTargets?.GetRandomElement(),
                        4 => culture.EliteBasicTroop?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement(),
                        5 => culture.EliteBasicTroop?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement(),
                        6 => culture.EliteBasicTroop?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement(),
                        _ => null
                    };
                }
                else
                {
                    tier = Math.Min(Math.Max(tier, 1), 5);
                    return tier switch
                    {
                        1 => culture.BasicTroop,
                        2 => culture.BasicTroop?.UpgradeTargets?.GetRandomElement(),
                        3 => culture.BasicTroop?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement(),
                        4 => culture.BasicTroop?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement(),
                        5 => culture.BasicTroop?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement()?.UpgradeTargets?.GetRandomElement(),
                        _ => null
                    };
                }
            }
            catch
            {
                return culture.BasicTroop;
            }
        }

        private void OnDailyTickClan(Clan clan)
        {
            try
            {
                if (clan == null || ConfigSafe == null) return;

                ApplyRenownDaily(clan);

                // ── Clan upgrade troop spawning ──────────────────────────────────
                foreach (var upgradeId in GetClanUpgrades(clan))
                {
                    var upgrade = ConfigSafe.ClanUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
                    if (upgrade == null || upgrade.DailyTroopSpawnAmount <= 0) continue;
                    if ((upgrade.LordOnly && clan.IsUnderMercenaryService) || (upgrade.MercOnly && !clan.IsUnderMercenaryService)) continue;

                    string key = $"{clan.StringId}:{upgradeId}";
                    if (!_troopSpawnAccumulation.TryGetValue(key, out float accumulated)) accumulated = 0f;
                    accumulated += upgrade.DailyTroopSpawnAmount;

                    while (accumulated >= 1.0f &&
                           clan.WarPartyComponents.Any(p => p.MobileParty.MemberRoster.TotalManCount < p.Party.PartySizeLimit))
                    {
                        SpawnTroopForClan(clan, upgrade);
                        accumulated -= 1.0f;
                    }

                    if (accumulated > 0f) _troopSpawnAccumulation[key] = accumulated;
                    else _troopSpawnAccumulation.Remove(key);
                }

                // ── Kingdom upgrade troop spawning (fires once per clan in kingdom) ──
                if (clan.Kingdom != null && ConfigSafe.KingdomUpgrades != null)
                {
                    foreach (var upgradeId in GetKingdomUpgrades(clan.Kingdom))
                    {
                        var upgrade = ConfigSafe.KingdomUpgrades.FirstOrDefault(u => u.ID == upgradeId);
                        if (upgrade == null || upgrade.DailyTroopSpawnAmount <= 0) continue;

                        // Key is scoped per kingdom+clan+upgrade so each clan accumulates independently
                        string key = $"kdom:{clan.Kingdom.StringId}:{clan.StringId}:{upgradeId}";
                        if (!_troopSpawnAccumulation.TryGetValue(key, out float accumulated)) accumulated = 0f;
                        accumulated += upgrade.DailyTroopSpawnAmount;

                        while (accumulated >= 1.0f &&
                               clan.WarPartyComponents.Any(p => p.MobileParty.MemberRoster.TotalManCount < p.Party.PartySizeLimit))
                        {
                            SpawnTroopForClanFromKingdomUpgrade(clan, upgrade);
                            accumulated -= 1.0f;
                        }

                        if (accumulated > 0f) _troopSpawnAccumulation[key] = accumulated;
                        else _troopSpawnAccumulation.Remove(key);
                    }
                }
            }
            catch (Exception ex)
            {
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage($"[BLT Upgrade] Daily tick error: {ex.Message}"));
            }
        }

        private void SpawnTroopForClan(Clan clan, ClanUpgrade upgrade)
        {
            try
            {
                if ((upgrade.LordOnly && clan.IsUnderMercenaryService) || (upgrade.MercOnly && !clan.IsUnderMercenaryService)) return;

                var party = clan.Leader.PartyBelongedTo != null &&
                            clan.Leader.PartyBelongedTo.Party.MemberRoster.TotalManCount < clan.Leader.PartyBelongedTo.Party.PartySizeLimit
                    ? clan.Leader.PartyBelongedTo
                    : clan.WarPartyComponents
                          .Where(p => p.MobileParty.MemberRoster.TotalManCount < p.Party.PartySizeLimit)
                          .SelectRandom()?.MobileParty;
                if (party == null) return;
                // Final guard in case counts changed mid-tick
                if (party.MemberRoster.TotalManCount >= party.Party.PartySizeLimit) return;

                var troop = GetTroopForCulture(clan.Culture, upgrade.TroopTree, GetEffectiveTroopTier(clan, upgrade));
                if (troop != null) party.MemberRoster.AddToCounts(troop, 1);
            }
            catch (Exception ex)
            {
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage($"[BLT Upgrade] Spawn troop error: {ex.Message}"));
            }
        }

        private void SpawnTroopForClanFromKingdomUpgrade(Clan clan, KingdomUpgrade upgrade)
        {
            try
            {
                var party = clan.Leader.PartyBelongedTo != null &&
                            clan.Leader.PartyBelongedTo.Party.MemberRoster.TotalManCount < clan.Leader.PartyBelongedTo.Party.PartySizeLimit
                    ? clan.Leader.PartyBelongedTo
                    : clan.WarPartyComponents
                          .Where(p => p.MobileParty.MemberRoster.TotalManCount < p.Party.PartySizeLimit)
                          .SelectRandom()?.MobileParty;
                if (party == null) return;
                // Final guard in case counts changed mid-tick
                if (party.MemberRoster.TotalManCount >= party.Party.PartySizeLimit) return;

                var troop = GetTroopForCulture(clan.Culture, upgrade.TroopTree, GetEffectiveTroopTierFromKingdom(clan, upgrade));
                if (troop != null) party.MemberRoster.AddToCounts(troop, 1);
            }
            catch (Exception ex)
            {
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage($"[BLT Upgrade] Kingdom spawn troop error: {ex.Message}"));
            }
        }

        #endregion

        #region Typed aggregation helpers
        private float SumFiefFloat(Settlement s, Func<FiefUpgrade, float> selector)
        {
            if (s == null || ConfigSafe == null) return 0f;
            float sum = 0f;
            foreach (var id in GetFiefUpgrades(s))
            {
                var up = ConfigSafe.FiefUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null && (!up.CoastalOnly || s.HasPort)) sum += selector(up);
            }
            return sum;
        }

        private float SumClanFloat(Clan clan, Func<ClanUpgrade, float> selector, bool applyToVassalsOnly = false)
        {
            if (clan == null || ConfigSafe == null) return 0f;
            float sum = 0f;
            foreach (var id in GetClanUpgrades(clan))
            {
                var up = ConfigSafe.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                bool lordAllow = !up.LordOnly || !clan.IsUnderMercenaryService;
                bool mercAllow = !up.MercOnly || clan.IsUnderMercenaryService;
                bool vassalAllow = !up.ApplyToVassals || applyToVassalsOnly;
                if (lordAllow && mercAllow && vassalAllow) sum += selector(up);
            }
            return sum;
        }

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

        private float SumSettlementFloatTyped(Settlement s,
            Func<FiefUpgrade, float> fiefSel, Func<ClanUpgrade, float> clanSel, Func<KingdomUpgrade, float> kingSel)
        {
            if (s == null) return 0f;
            float sum = SumFiefFloat(s, fiefSel);
            var clan = s.OwnerClan;
            if (clan != null)
            {
                sum += SumClanFloat(clan, clanSel);
                if (clan.Kingdom != null) sum += SumKingdomFloat(clan.Kingdom, kingSel);
            }
            return sum;
        }

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
                if (up == null) continue;
                bool lordAllow = !up.LordOnly || !clan.IsUnderMercenaryService;
                bool mercAllow = !up.MercOnly || clan.IsUnderMercenaryService;
                bool vassalAllow = !up.ApplyToVassals || applyToVassalsOnly;
                if (lordAllow && mercAllow && vassalAllow) sum += selector(up);
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
            Func<FiefUpgrade, int> fiefSel, Func<ClanUpgrade, int> clanSel, Func<KingdomUpgrade, int> kingSel)
        {
            if (s == null) return 0;
            int sum = SumFiefInt(s, fiefSel);
            var clan = s.OwnerClan;
            if (clan != null)
            {
                sum += SumClanInt(clan, clanSel);
                if (clan.Kingdom != null) sum += SumKingdomInt(clan.Kingdom, kingSel);
            }
            return sum;
        }
        #endregion

        #region Aggregated getters
        public int GetTotalTaxBonus(Settlement s)
            => SumSettlementIntTyped(s, f => f.TaxIncomeFlat, c => c.TaxIncomeFlat, k => k.TaxIncomeFlat);

        public float GetTotalHearthDaily(Settlement s)
            => SumSettlementFloatTyped(s, f => f.HearthDaily, c => c.HearthDaily, k => k.HearthDaily);

        public int GetTotalGarrisonCapacityBonus(Settlement s)
            => SumSettlementIntTyped(s, f => f.GarrisonCapacityBonus, c => c.GarrisonCapacityBonus, k => k.GarrisonCapacityBonus);

        public int GetClanPartySizeBonus(Clan clan) => SumClanInt(clan, c => c.PartySizeBonus);
        public int GetKingdomPartySizeBonus(Kingdom k) => SumKingdomInt(k, u => u.PartySizeBonus);
        public int GetTotalPartySizeBonus(Hero hero)
        {
            if (hero?.Clan == null) return 0;
            int b = GetClanPartySizeBonus(hero.Clan);
            if (hero.Clan.Kingdom != null) b += GetKingdomPartySizeBonus(hero.Clan.Kingdom);
            return b;
        }

        public float GetClanPartySpeedBonus(Clan clan) => SumClanFloat(clan, c => c.PartySpeedBonus);
        public float GetKingdomPartySpeedBonus(Kingdom k) => SumKingdomFloat(k, u => u.PartySpeedBonus);
        public float GetTotalPartySpeedBonus(Hero hero)
        {
            if (hero?.Clan == null) return 0f;
            float b = GetClanPartySpeedBonus(hero.Clan);
            if (hero.Clan.Kingdom != null) b += GetKingdomPartySpeedBonus(hero.Clan.Kingdom);
            return b;
        }

        public int GetClanPartyAmountBonus(Clan clan) => SumClanInt(clan, c => c.PartyAmountBonus);
        public int GetTotalPartyAmountBonus(Clan clan) => clan == null ? 0 : GetClanPartyAmountBonus(clan);

        public int GetClanMaxVassalsBonus(Clan clan) => SumClanInt(clan, c => c.MaxVassalsBonus);
        public int GetTotalMaxVassalsBonus(Clan clan) => clan == null ? 0 : GetClanMaxVassalsBonus(clan);

        public float GetClanRenownDaily(Clan clan) => SumClanFloat(clan, c => c.RenownDaily);
        public float GetKingdomRenownDaily(Kingdom k) => SumKingdomFloat(k, u => u.RenownDaily);
        public float GetTotalRenownDaily(Hero hero)
        {
            if (hero?.Clan == null) return 0f;
            float b = GetClanRenownDaily(hero.Clan);
            if (hero.Clan.Kingdom != null) b += GetKingdomRenownDaily(hero.Clan.Kingdom);
            return b;
        }

        public float GetClanInfluenceDaily(Clan clan) => SumClanFloat(clan, c => c.InfluenceDaily);
        public float GetKingdomInfluenceDaily(Kingdom kingdom) => SumKingdomFloat(kingdom, k => k.InfluenceDaily);

        public void ApplyRenownDaily(Clan clan)
        {
            clan.AddRenown(GetTotalRenownDaily(clan.Leader), false);

            float influenceBonus = GetClanInfluenceDaily(clan);
            if (clan.Kingdom != null) influenceBonus += GetKingdomInfluenceDaily(clan.Kingdom);
            if (influenceBonus != 0f) clan.Influence += influenceBonus;
        }

        public int GetKingdomMaxClansBonus(Kingdom k) => SumKingdomInt(k, u => u.MaxClansBonus);
        public int GetTotalGetKingdomMaxClansBonus(Kingdom k) => k == null ? 0 : GetKingdomMaxClansBonus(k);

        public int GetFlatClanMercBonus(Clan clan) => SumClanInt(clan, c => c.MercIncomeFlat);
        public float GetPercentClanMercBonus(Clan clan) => 1f + SumClanFloat(clan, c => c.MercIncomePercent);
        public int GetFlatMercBonus(Hero hero) => hero?.Clan == null ? 0 : GetFlatClanMercBonus(hero.Clan);

        public float GetTotalLoyaltyDailyFlat(Settlement s) => SumSettlementFloatTyped(s, f => f.LoyaltyDailyFlat, c => c.LoyaltyDailyFlat, k => k.LoyaltyDailyFlat);
        public float GetTotalLoyaltyDailyPercent(Settlement s) => SumSettlementFloatTyped(s, f => f.LoyaltyDailyPercent, c => c.LoyaltyDailyPercent, k => k.LoyaltyDailyPercent);
        public float GetTotalProsperityDailyFlat(Settlement s) => SumSettlementFloatTyped(s, f => f.ProsperityDailyFlat, c => c.ProsperityDailyFlat, k => k.ProsperityDailyFlat);
        public float GetTotalProsperityDailyPercent(Settlement s) => SumSettlementFloatTyped(s, f => f.ProsperityDailyPercent, c => c.ProsperityDailyPercent, k => k.ProsperityDailyPercent);
        public float GetTotalSecurityDailyFlat(Settlement s) => SumSettlementFloatTyped(s, f => f.SecurityDailyFlat, c => c.SecurityDailyFlat, k => k.SecurityDailyFlat);
        public float GetTotalSecurityDailyPercent(Settlement s) => SumSettlementFloatTyped(s, f => f.SecurityDailyPercent, c => c.SecurityDailyPercent, k => k.SecurityDailyPercent);
        public float GetTotalMilitiaDailyFlat(Settlement s) => SumSettlementFloatTyped(s, f => f.MilitiaDailyFlat, c => c.MilitiaDailyFlat, k => k.MilitiaDailyFlat);
        public float GetTotalMilitiaDailyPercent(Settlement s) => SumSettlementFloatTyped(s, f => f.MilitiaDailyPercent, c => c.MilitiaDailyPercent, k => k.MilitiaDailyPercent);
        public float GetTotalFoodDailyFlat(Settlement s) => SumSettlementFloatTyped(s, f => f.FoodDailyFlat, c => c.FoodDailyFlat, k => k.FoodDailyFlat);
        public float GetTotalFoodDailyPercent(Settlement s) => SumSettlementFloatTyped(s, f => f.FoodDailyPercent, c => c.FoodDailyPercent, k => k.FoodDailyPercent);

        // Backward-compatible short names
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