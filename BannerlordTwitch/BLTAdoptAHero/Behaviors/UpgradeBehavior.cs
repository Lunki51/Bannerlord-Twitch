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

        /// <summary>
        /// When false, any accumulation above the current integer floor is discarded the moment
        /// a spawn attempt fails due to a full party/garrison. When true (default), the remainder
        /// is preserved and delivered once space becomes available.
        /// Controlled by UpgradeAction.Settings and written on each command execution.
        /// </summary>
        public bool AccumulateWhenFull { get; set; } = true;

        public UpgradeBehavior() { Current = this; }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, OnDailyTickClan);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
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
        private static List<string> ParseUpgradeString(string s)
        {
            if (string.IsNullOrEmpty(s)) return new List<string>();
            return s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();
        }

        private static string SerializeUpgradeList(List<string> upgrades)
            => (upgrades == null || upgrades.Count == 0) ? string.Empty : string.Join(",", upgrades);
        #endregion

        #region Fief Get/Has/Add/Remove
        public List<string> GetFiefUpgrades(Settlement settlement)
        {
            if (settlement == null) return new List<string>();
            return _fiefUpgrades.TryGetValue(settlement.StringId, out var s) ? ParseUpgradeString(s) : new List<string>();
        }

        public bool HasFiefUpgrade(Settlement settlement, string upgradeId)
            => settlement != null && !string.IsNullOrEmpty(upgradeId) && GetFiefUpgrades(settlement).Contains(upgradeId);

        public bool AddFiefUpgrade(Settlement settlement, string upgradeId)
        {
            if (settlement == null || string.IsNullOrEmpty(upgradeId)) return false;
            var upgrades = _fiefUpgrades.TryGetValue(settlement.StringId, out var s) ? ParseUpgradeString(s) : new List<string>();
            if (upgrades.Contains(upgradeId)) return false;
            upgrades.Add(upgradeId);
            _fiefUpgrades[settlement.StringId] = SerializeUpgradeList(upgrades);
            return true;
        }

        public bool RemoveFiefUpgrade(Settlement settlement, string upgradeId)
        {
            if (settlement == null || string.IsNullOrEmpty(upgradeId)) return false;
            if (!_fiefUpgrades.TryGetValue(settlement.StringId, out var s)) return false;
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
            return _clanUpgrades.TryGetValue(clan.StringId, out var s) ? ParseUpgradeString(s) : new List<string>();
        }

        public bool HasClanUpgrade(Clan clan, string upgradeId)
            => clan != null && !string.IsNullOrEmpty(upgradeId) && GetClanUpgrades(clan).Contains(upgradeId);

        public bool AddClanUpgrade(Clan clan, string upgradeId)
        {
            if (clan == null || string.IsNullOrEmpty(upgradeId)) return false;
            var upgrades = _clanUpgrades.TryGetValue(clan.StringId, out var s) ? ParseUpgradeString(s) : new List<string>();
            if (upgrades.Contains(upgradeId)) return false;
            upgrades.Add(upgradeId);
            _clanUpgrades[clan.StringId] = SerializeUpgradeList(upgrades);
            return true;
        }

        public bool RemoveClanUpgrade(Clan clan, string upgradeId)
        {
            if (clan == null || string.IsNullOrEmpty(upgradeId)) return false;
            if (!_clanUpgrades.TryGetValue(clan.StringId, out var s)) return false;
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
            return _kingdomUpgrades.TryGetValue(kingdom.StringId, out var s) ? ParseUpgradeString(s) : new List<string>();
        }

        public bool HasKingdomUpgrade(Kingdom kingdom, string upgradeId)
            => kingdom != null && !string.IsNullOrEmpty(upgradeId) && GetKingdomUpgrades(kingdom).Contains(upgradeId);

        public bool AddKingdomUpgrade(Kingdom kingdom, string upgradeId)
        {
            if (kingdom == null || string.IsNullOrEmpty(upgradeId)) return false;
            var upgrades = _kingdomUpgrades.TryGetValue(kingdom.StringId, out var s) ? ParseUpgradeString(s) : new List<string>();
            if (upgrades.Contains(upgradeId)) return false;
            upgrades.Add(upgradeId);
            _kingdomUpgrades[kingdom.StringId] = SerializeUpgradeList(upgrades);
            return true;
        }

        public bool RemoveKingdomUpgrade(Kingdom kingdom, string upgradeId)
        {
            if (kingdom == null || string.IsNullOrEmpty(upgradeId)) return false;
            if (!_kingdomUpgrades.TryGetValue(kingdom.StringId, out var s)) return false;
            var upgrades = ParseUpgradeString(s);
            if (!upgrades.Remove(upgradeId)) return false;
            if (upgrades.Count == 0) _kingdomUpgrades.Remove(kingdom.StringId);
            else _kingdomUpgrades[kingdom.StringId] = SerializeUpgradeList(upgrades);
            return true;
        }
        #endregion

        #region Troop tree traversal
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
        #endregion

        #region Effective tier resolution
        private int GetEffectiveTroopTier(Clan clan, ClanUpgrade spawningUpgrade)
        {
            if (clan == null || spawningUpgrade == null) return 1;
            int bonus = 0;
            foreach (var id in GetClanUpgrades(clan))
            {
                var up = ConfigSafe?.ClanUpgrades?.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                if ((up.LordOnly && clan.IsUnderMercenaryService) || (up.MercOnly && !clan.IsUnderMercenaryService)) continue;
                if (up.BuffsTroopTierOfIDs.Contains(spawningUpgrade.ID, StringComparer.OrdinalIgnoreCase))
                    bonus += up.TroopTierBonus;
            }
            return Math.Max(1, spawningUpgrade.TroopTier + bonus);
        }

        private int GetEffectiveTroopTierFromKingdom(Clan clan, KingdomUpgrade spawningUpgrade)
        {
            if (clan == null || spawningUpgrade == null || clan.Kingdom == null) return 1;
            int bonus = 0;
            foreach (var id in GetKingdomUpgrades(clan.Kingdom))
            {
                var up = ConfigSafe?.KingdomUpgrades?.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                if (up.BuffsTroopTierOfIDs.Contains(spawningUpgrade.ID, StringComparer.OrdinalIgnoreCase))
                    bonus += up.TroopTierBonus;
            }
            return Math.Max(1, spawningUpgrade.TroopTier + bonus);
        }

        // Garrison tier: fief upgrade buffed by other fief upgrades on the same settlement
        private int GetEffectiveGarrisonTierFief(Settlement settlement, FiefUpgrade spawningUpgrade)
        {
            if (settlement == null || spawningUpgrade == null) return 1;
            int bonus = 0;
            foreach (var id in GetFiefUpgrades(settlement))
            {
                var up = ConfigSafe?.FiefUpgrades?.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                if (up.GarrisonBuffsTroopTierOfIDs.Contains(spawningUpgrade.ID, StringComparer.OrdinalIgnoreCase))
                    bonus += up.GarrisonTroopTierBonus;
            }
            return Math.Max(1, spawningUpgrade.GarrisonTroopTier + bonus);
        }

        // Garrison tier: clan upgrade buffed by other clan upgrades
        private int GetEffectiveGarrisonTierClan(Clan clan, ClanUpgrade spawningUpgrade)
        {
            if (clan == null || spawningUpgrade == null) return 1;
            int bonus = 0;
            foreach (var id in GetClanUpgrades(clan))
            {
                var up = ConfigSafe?.ClanUpgrades?.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                if ((up.LordOnly && clan.IsUnderMercenaryService) || (up.MercOnly && !clan.IsUnderMercenaryService)) continue;
                if (up.GarrisonBuffsTroopTierOfIDs.Contains(spawningUpgrade.ID, StringComparer.OrdinalIgnoreCase))
                    bonus += up.GarrisonTroopTierBonus;
            }
            return Math.Max(1, spawningUpgrade.GarrisonTroopTier + bonus);
        }

        // Garrison tier: kingdom upgrade buffed by other kingdom upgrades
        private int GetEffectiveGarrisonTierKingdom(Clan clan, KingdomUpgrade spawningUpgrade)
        {
            if (clan == null || spawningUpgrade == null || clan.Kingdom == null) return 1;
            int bonus = 0;
            foreach (var id in GetKingdomUpgrades(clan.Kingdom))
            {
                var up = ConfigSafe?.KingdomUpgrades?.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                if (up.GarrisonBuffsTroopTierOfIDs.Contains(spawningUpgrade.ID, StringComparer.OrdinalIgnoreCase))
                    bonus += up.GarrisonTroopTierBonus;
            }
            return Math.Max(1, spawningUpgrade.GarrisonTroopTier + bonus);
        }
        #endregion

        #region Spawn helpers
        // Returns true only if a troop was actually added. Caller must only decrement
        // accumulation on true to avoid silent troop loss.
        private bool TrySpawnTroopToParty(Clan clan, TroopTreeType tree, int tier)
        {
            var party = clan.Leader?.PartyBelongedTo != null &&
                        clan.Leader.PartyBelongedTo.Party.MemberRoster.TotalManCount < clan.Leader.PartyBelongedTo.Party.PartySizeLimit
                ? clan.Leader.PartyBelongedTo
                : clan.WarPartyComponents
                      .Where(p => p.MobileParty.MemberRoster.TotalManCount < p.Party.PartySizeLimit)
                      .SelectRandom()?.MobileParty;
            if (party == null) return false;
            if (party.MemberRoster.TotalManCount >= party.Party.PartySizeLimit) return false;

            var troop = GetTroopForCulture(clan.Culture, tree, tier);
            if (troop == null) return false;

            party.MemberRoster.AddToCounts(troop, 1);
            return true;
        }

        // Returns true only if a troop was actually added to the garrison.
        private bool TrySpawnTroopToGarrison(Settlement settlement, TroopTreeType tree, int tier)
        {
            var garrison = settlement?.Town?.GarrisonParty;
            if (garrison == null) return false;
            if (garrison.MemberRoster.TotalManCount >= garrison.Party.PartySizeLimit) return false;

            var troop = GetTroopForCulture(settlement.Culture, tree, tier);
            if (troop == null) return false;

            garrison.MemberRoster.AddToCounts(troop, 1);
            return true;
        }

        // Picks a random clan settlement whose garrison has room. Returns null if none found.
        private Settlement GetRandomGarrisonSettlementForClan(Clan clan)
            => clan?.Settlements
                   .Where(s => s.Town?.GarrisonParty != null &&
                               s.Town.GarrisonParty.MemberRoster.TotalManCount < s.Town.GarrisonParty.Party.PartySizeLimit)
                   .SelectRandom();

        private void RunAccumulation(string key, float amount, Func<bool> trySpawn)
        {
            _troopSpawnAccumulation.TryGetValue(key, out float acc);
            acc += amount;
            while (acc >= 1.0f)
            {
                if (!trySpawn())
                {
                    // Spawn failed (party/garrison full, or no valid troop).
                    // If AccumulateWhenFull is disabled, discard everything >= 1.0
                    // so troops don't bank up while there's no room.
                    // The sub-1.0 fractional remainder is always kept — it represents
                    // a partially-earned troop, not a banked one.
                    if (!AccumulateWhenFull) acc %= 1.0f;
                    break;
                }
                acc -= 1.0f;
            }
            if (acc > 0f) _troopSpawnAccumulation[key] = acc;
            else _troopSpawnAccumulation.Remove(key);
        }
        #endregion

        #region Daily tick handlers
        private void OnDailyTickClan(Clan clan)
        {
            try
            {
                if (clan == null || ConfigSafe == null) return;

                ApplyRenownDaily(clan);

                // ── Clan upgrade: war-party spawning ──────────────────────────
                foreach (var upgradeId in GetClanUpgrades(clan))
                {
                    var up = ConfigSafe.ClanUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
                    if (up == null) continue;
                    bool lordBlock = up.LordOnly && clan.IsUnderMercenaryService;
                    bool mercBlock = up.MercOnly && !clan.IsUnderMercenaryService;
                    if (lordBlock || mercBlock) continue;

                    if (up.DailyTroopSpawnAmount > 0)
                    {
                        int tier = GetEffectiveTroopTier(clan, up);
                        RunAccumulation($"{clan.StringId}:{upgradeId}",
                            up.DailyTroopSpawnAmount,
                            () => TrySpawnTroopToParty(clan, up.TroopTree, tier));
                    }

                    if (up.GarrisonDailyTroopSpawnAmount > 0)
                    {
                        int gTier = GetEffectiveGarrisonTierClan(clan, up);
                        RunAccumulation($"clan_garrison:{clan.StringId}:{upgradeId}",
                            up.GarrisonDailyTroopSpawnAmount,
                            () =>
                            {
                                var target = GetRandomGarrisonSettlementForClan(clan);
                                return target != null && TrySpawnTroopToGarrison(target, up.GarrisonTroopTree, gTier);
                            });
                    }
                }

                // ── Kingdom upgrade: war-party and garrison spawning per clan ──
                if (clan.Kingdom == null || ConfigSafe.KingdomUpgrades == null) return;

                foreach (var upgradeId in GetKingdomUpgrades(clan.Kingdom))
                {
                    var up = ConfigSafe.KingdomUpgrades.FirstOrDefault(u => u.ID == upgradeId);
                    if (up == null) continue;

                    if (up.DailyTroopSpawnAmount > 0)
                    {
                        int tier = GetEffectiveTroopTierFromKingdom(clan, up);
                        RunAccumulation($"kdom:{clan.Kingdom.StringId}:{clan.StringId}:{upgradeId}",
                            up.DailyTroopSpawnAmount,
                            () => TrySpawnTroopToParty(clan, up.TroopTree, tier));
                    }

                    if (up.GarrisonDailyTroopSpawnAmount > 0)
                    {
                        int gTier = GetEffectiveGarrisonTierKingdom(clan, up);
                        RunAccumulation($"kdom_garrison:{clan.Kingdom.StringId}:{clan.StringId}:{upgradeId}",
                            up.GarrisonDailyTroopSpawnAmount,
                            () =>
                            {
                                var target = GetRandomGarrisonSettlementForClan(clan);
                                return target != null && TrySpawnTroopToGarrison(target, up.GarrisonTroopTree, gTier);
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Daily clan tick error: {ex.Message}");
            }
        }

        private void OnDailyTickSettlement(Settlement settlement)
        {
            try
            {
                // Fief garrison spawning only applies to towns/castles (which have garrison parties)
                if (settlement?.Town == null || ConfigSafe?.FiefUpgrades == null) return;

                foreach (var upgradeId in GetFiefUpgrades(settlement))
                {
                    var up = ConfigSafe.FiefUpgrades.FirstOrDefault(u => u.ID == upgradeId);
                    if (up == null || up.GarrisonDailyTroopSpawnAmount <= 0) continue;
                    if (up.CoastalOnly && !settlement.HasPort) continue;

                    int tier = GetEffectiveGarrisonTierFief(settlement, up);
                    RunAccumulation($"fief_garrison:{settlement.StringId}:{upgradeId}",
                        up.GarrisonDailyTroopSpawnAmount,
                        () => TrySpawnTroopToGarrison(settlement, up.GarrisonTroopTree, tier));
                }
            }
            catch (Exception ex)
            {
                Log($"Daily settlement tick error: {ex.Message}");
            }
        }

        private static void Log(string msg)
            => TaleWorlds.Library.InformationManager.DisplayMessage(
                new TaleWorlds.Library.InformationMessage($"[BLT Upgrade] {msg}"));
        #endregion

        #region Typed aggregation helpers
        private float SumFiefFloat(Settlement s, Func<FiefUpgrade, float> sel)
        {
            if (s == null || ConfigSafe == null) return 0f;
            float sum = 0f;
            foreach (var id in GetFiefUpgrades(s))
            {
                var up = ConfigSafe.FiefUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null && (!up.CoastalOnly || s.HasPort)) sum += sel(up);
            }
            return sum;
        }

        private float SumClanFloat(Clan clan, Func<ClanUpgrade, float> sel, bool vassalOnly = false)
        {
            if (clan == null || ConfigSafe == null) return 0f;
            float sum = 0f;
            foreach (var id in GetClanUpgrades(clan))
            {
                var up = ConfigSafe.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                if (up.LordOnly && clan.IsUnderMercenaryService) continue;
                if (up.MercOnly && !clan.IsUnderMercenaryService) continue;
                if (up.ApplyToVassals && !vassalOnly) continue;
                sum += sel(up);
            }
            return sum;
        }

        private float SumKingdomFloat(Kingdom kingdom, Func<KingdomUpgrade, float> sel)
        {
            if (kingdom == null || ConfigSafe == null) return 0f;
            float sum = 0f;
            foreach (var id in GetKingdomUpgrades(kingdom))
            {
                var up = ConfigSafe.KingdomUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null) sum += sel(up);
            }
            return sum;
        }

        private float SumSettlementFloat(Settlement s,
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

        // BUG FIX: SumFiefInt now correctly applies the CoastalOnly filter (previously missing)
        private int SumFiefInt(Settlement s, Func<FiefUpgrade, int> sel)
        {
            if (s == null || ConfigSafe == null) return 0;
            int sum = 0;
            foreach (var id in GetFiefUpgrades(s))
            {
                var up = ConfigSafe.FiefUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null && (!up.CoastalOnly || s.HasPort)) sum += sel(up);
            }
            return sum;
        }

        private int SumClanInt(Clan clan, Func<ClanUpgrade, int> sel, bool vassalOnly = false)
        {
            if (clan == null || ConfigSafe == null) return 0;
            int sum = 0;
            foreach (var id in GetClanUpgrades(clan))
            {
                var up = ConfigSafe.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                if (up.LordOnly && clan.IsUnderMercenaryService) continue;
                if (up.MercOnly && !clan.IsUnderMercenaryService) continue;
                if (up.ApplyToVassals && !vassalOnly) continue;
                sum += sel(up);
            }
            return sum;
        }

        private int SumKingdomInt(Kingdom kingdom, Func<KingdomUpgrade, int> sel)
        {
            if (kingdom == null || ConfigSafe == null) return 0;
            int sum = 0;
            foreach (var id in GetKingdomUpgrades(kingdom))
            {
                var up = ConfigSafe.KingdomUpgrades.FirstOrDefault(u => u.ID == id);
                if (up != null) sum += sel(up);
            }
            return sum;
        }

        private int SumSettlementInt(Settlement s,
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
            => SumSettlementInt(s, f => f.TaxIncomeFlat, c => c.TaxIncomeFlat, k => k.TaxIncomeFlat);

        public float GetTotalHearthDaily(Settlement s)
            => SumSettlementFloat(s, f => f.HearthDaily, c => c.HearthDaily, k => k.HearthDaily);

        public int GetTotalGarrisonCapacityBonus(Settlement s)
            => SumSettlementInt(s, f => f.GarrisonCapacityBonus, c => c.GarrisonCapacityBonus, k => k.GarrisonCapacityBonus);

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
            float influence = GetClanInfluenceDaily(clan);
            if (clan.Kingdom != null) influence += GetKingdomInfluenceDaily(clan.Kingdom);
            if (influence != 0f) clan.Influence += influence;
        }

        public int GetKingdomMaxClansBonus(Kingdom k) => SumKingdomInt(k, u => u.MaxClansBonus);
        public int GetTotalKingdomMaxClansBonus(Kingdom k) => k == null ? 0 : GetKingdomMaxClansBonus(k);
        public int GetKingdomMaxMercClansBonus(Kingdom k) => SumKingdomInt(k, u => u.MaxMercClansBonus);
        public int GetTotalKingdomMaxMercClansBonus(Kingdom k) => k == null ? 0 : GetKingdomMaxMercClansBonus(k);

        public int GetFlatClanMercBonus(Clan clan) => SumClanInt(clan, c => c.MercIncomeFlat);
        public float GetPercentClanMercBonus(Clan clan) => 1f + SumClanFloat(clan, c => c.MercIncomePercent);
        public int GetFlatMercBonus(Hero hero) => hero?.Clan == null ? 0 : GetFlatClanMercBonus(hero.Clan);

        public float GetTotalLoyaltyDailyFlat(Settlement s) => SumSettlementFloat(s, f => f.LoyaltyDailyFlat, c => c.LoyaltyDailyFlat, k => k.LoyaltyDailyFlat);
        public float GetTotalLoyaltyDailyPercent(Settlement s) => SumSettlementFloat(s, f => f.LoyaltyDailyPercent, c => c.LoyaltyDailyPercent, k => k.LoyaltyDailyPercent);
        public float GetTotalProsperityDailyFlat(Settlement s) => SumSettlementFloat(s, f => f.ProsperityDailyFlat, c => c.ProsperityDailyFlat, k => k.ProsperityDailyFlat);
        public float GetTotalProsperityDailyPercent(Settlement s) => SumSettlementFloat(s, f => f.ProsperityDailyPercent, c => c.ProsperityDailyPercent, k => k.ProsperityDailyPercent);
        public float GetTotalSecurityDailyFlat(Settlement s) => SumSettlementFloat(s, f => f.SecurityDailyFlat, c => c.SecurityDailyFlat, k => k.SecurityDailyFlat);
        public float GetTotalSecurityDailyPercent(Settlement s) => SumSettlementFloat(s, f => f.SecurityDailyPercent, c => c.SecurityDailyPercent, k => k.SecurityDailyPercent);
        public float GetTotalMilitiaDailyFlat(Settlement s) => SumSettlementFloat(s, f => f.MilitiaDailyFlat, c => c.MilitiaDailyFlat, k => k.MilitiaDailyFlat);
        public float GetTotalMilitiaDailyPercent(Settlement s) => SumSettlementFloat(s, f => f.MilitiaDailyPercent, c => c.MilitiaDailyPercent, k => k.MilitiaDailyPercent);
        public float GetTotalFoodDailyFlat(Settlement s) => SumSettlementFloat(s, f => f.FoodDailyFlat, c => c.FoodDailyFlat, k => k.FoodDailyFlat);
        public float GetTotalFoodDailyPercent(Settlement s) => SumSettlementFloat(s, f => f.FoodDailyPercent, c => c.FoodDailyPercent, k => k.FoodDailyPercent);

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