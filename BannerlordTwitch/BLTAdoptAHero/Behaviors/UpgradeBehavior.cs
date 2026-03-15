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

        /// <summary>
        /// When true (default), clans that own fiefs but belong to no kingdom are treated as
        /// lords for the purpose of LordOnly upgrade eligibility.
        /// Set by UpgradeAction.Settings on each command execution.
        /// </summary>
        public bool IndependentClansCountAsLords { get; set; } = true;

        /// <summary>
        /// When true, clans that own fiefs but belong to no kingdom are treated as mercenaries
        /// for the purpose of MercOnly upgrade eligibility. Default false.
        /// Set by UpgradeAction.Settings on each command execution.
        /// </summary>
        public bool IndependentClansCountAsMercs { get; set; } = false;

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

        // ── Clan upgrade eligibility ──────────────────────────────────────────
        /// <summary>
        /// Central filter for LordOnly / MercOnly / independent-clan logic.
        /// A clan is "independent" if it has no kingdom and is not under mercenary service.
        /// The two IndependentClans* toggles control whether such clans qualify.
        /// </summary>
        private bool IsClanUpgradeActive(ClanUpgrade up, Clan clan)
        {
            bool isIndependent = clan.Kingdom == null && !clan.IsUnderMercenaryService;

            if (up.LordOnly)
            {
                if (clan.IsUnderMercenaryService) return false;                          // mercs are never lords
                if (isIndependent && !IndependentClansCountAsLords) return false;        // block independents unless opted-in
            }

            if (up.MercOnly)
            {
                bool qualifies = clan.IsUnderMercenaryService
                              || (isIndependent && IndependentClansCountAsMercs);
                if (!qualifies) return false;
            }

            return true;
        }

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
                if (!IsClanUpgradeActive(up, clan)) continue;           // ← unified filter
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

        private int GetEffectiveGarrisonTierClan(Clan clan, ClanUpgrade spawningUpgrade)
        {
            if (clan == null || spawningUpgrade == null) return 1;
            int bonus = 0;
            foreach (var id in GetClanUpgrades(clan))
            {
                var up = ConfigSafe?.ClanUpgrades?.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                if (!IsClanUpgradeActive(up, clan)) continue;           // ← unified filter
                if (up.GarrisonBuffsTroopTierOfIDs.Contains(spawningUpgrade.ID, StringComparer.OrdinalIgnoreCase))
                    bonus += up.GarrisonTroopTierBonus;
            }
            return Math.Max(1, spawningUpgrade.GarrisonTroopTier + bonus);
        }

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

                foreach (var upgradeId in GetClanUpgrades(clan))
                {
                    var up = ConfigSafe.ClanUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
                    if (up == null) continue;
                    if (!IsClanUpgradeActive(up, clan)) continue;       // ← unified filter

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
        private bool FloorGuardEnabled => ConfigSafe?.BlockNegativesAtFloor ?? true;

        private float SumFiefFloat(Settlement s, Func<FiefUpgrade, float> sel, float currentValue = float.MaxValue)
        {
            if (s == null || ConfigSafe == null) return 0f;
            bool guard = FloorGuardEnabled;
            float sum = 0f;
            foreach (var id in GetFiefUpgrades(s))
            {
                var up = ConfigSafe.FiefUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                if (up.CoastalOnly && !s.HasPort) continue;
                if (up.CapitalOnly) continue;
                float v = sel(up);
                if (guard && v < 0f && currentValue + sum + v < 0f) continue;
                sum += v;
            }
            return sum;
        }

        private float SumClanFloat(Clan clan, Func<ClanUpgrade, float> sel, bool vassalOnly = false, float currentValue = float.MaxValue)
        {
            if (clan == null || ConfigSafe == null) return 0f;
            bool guard = FloorGuardEnabled;
            float sum = 0f;
            foreach (var id in GetClanUpgrades(clan))
            {
                var up = ConfigSafe.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                if (!IsClanUpgradeActive(up, clan)) continue;           // ← unified filter
                if (up.ApplyToVassals && !vassalOnly) continue;
                float v = sel(up);
                if (guard && v < 0f && currentValue + sum + v < 0f) continue;
                sum += v;
            }
            return sum;
        }

        private float SumKingdomFloat(Kingdom kingdom, Func<KingdomUpgrade, float> sel, float currentValue = float.MaxValue)
        {
            if (kingdom == null || ConfigSafe == null) return 0f;
            bool guard = FloorGuardEnabled;
            float sum = 0f;
            foreach (var id in GetKingdomUpgrades(kingdom))
            {
                var up = ConfigSafe.KingdomUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                float v = sel(up);
                if (guard && v < 0f && currentValue + sum + v < 0f) continue;
                sum += v;
            }
            return sum;
        }

        private float SumSettlementFloat(Settlement s,
            Func<FiefUpgrade, float> fiefSel,
            Func<ClanUpgrade, float> clanSel,
            Func<KingdomUpgrade, float> kingSel,
            float currentValue = float.MaxValue)
        {
            if (s == null) return 0f;
            float fiefSum = SumFiefFloat(s, fiefSel, currentValue);
            float runningTotal = currentValue + fiefSum;
            var clan = s.OwnerClan;
            float clanSum = 0f, kingSum = 0f;
            if (clan != null)
            {
                clanSum = SumClanFloat(clan, clanSel, currentValue: runningTotal);
                runningTotal += clanSum;
                if (clan.Kingdom != null)
                    kingSum = SumKingdomFloat(clan.Kingdom, kingSel, runningTotal);
            }
            return fiefSum + clanSum + kingSum;
        }

        private int SumFiefInt(Settlement s, Func<FiefUpgrade, int> sel, float currentValue = float.MaxValue)
        {
            if (s == null || ConfigSafe == null) return 0;
            bool guard = FloorGuardEnabled;
            float sum = 0f;
            foreach (var id in GetFiefUpgrades(s))
            {
                var up = ConfigSafe.FiefUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                if (up.CoastalOnly && !s.HasPort) continue;
                if (up.CapitalOnly) continue;
                int v = sel(up);
                if (guard && v < 0 && currentValue + sum + v < 0f) continue;
                sum += v;
            }
            return (int)sum;
        }

        private int SumClanInt(Clan clan, Func<ClanUpgrade, int> sel, bool vassalOnly = false, float currentValue = float.MaxValue)
        {
            if (clan == null || ConfigSafe == null) return 0;
            bool guard = FloorGuardEnabled;
            float sum = 0f;
            foreach (var id in GetClanUpgrades(clan))
            {
                var up = ConfigSafe.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                if (!IsClanUpgradeActive(up, clan)) continue;           // ← unified filter
                if (up.ApplyToVassals && !vassalOnly) continue;
                int v = sel(up);
                if (guard && v < 0 && currentValue + sum + v < 0f) continue;
                sum += v;
            }
            return (int)sum;
        }

        private int SumKingdomInt(Kingdom kingdom, Func<KingdomUpgrade, int> sel, float currentValue = float.MaxValue)
        {
            if (kingdom == null || ConfigSafe == null) return 0;
            bool guard = FloorGuardEnabled;
            float sum = 0f;
            foreach (var id in GetKingdomUpgrades(kingdom))
            {
                var up = ConfigSafe.KingdomUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) continue;
                int v = sel(up);
                if (guard && v < 0 && currentValue + sum + v < 0f) continue;
                sum += v;
            }
            return (int)sum;
        }

        private int SumSettlementInt(Settlement s,
            Func<FiefUpgrade, int> fiefSel,
            Func<ClanUpgrade, int> clanSel,
            Func<KingdomUpgrade, int> kingSel,
            float currentValue = float.MaxValue)
        {
            if (s == null) return 0;
            int fiefSum = SumFiefInt(s, fiefSel, currentValue);
            float runningTotal = currentValue + fiefSum;
            var clan = s.OwnerClan;
            int clanSum = 0, kingSum = 0;
            if (clan != null)
            {
                clanSum = SumClanInt(clan, clanSel, currentValue: runningTotal);
                runningTotal += clanSum;
                if (clan.Kingdom != null)
                    kingSum = SumKingdomInt(clan.Kingdom, kingSel, runningTotal);
            }
            return fiefSum + clanSum + kingSum;
        }
        #endregion

        #region Aggregated getters
        public int GetTotalTaxBonus(Settlement s, float currentValue = float.MaxValue)
        {
            int sum = SumSettlementInt(s, f => f.TaxIncomeFlat, c => c.TaxIncomeFlat, k => k.TaxIncomeFlat, currentValue);
            if (CapitalBehavior.Current != null)
                sum += CapitalBehavior.Current.GetCapTaxFlat(s, currentValue + sum);
            return sum;
        }
        public float GetTotalHearthDaily(Settlement s, float currentValue = float.MaxValue)
        {
            float sum = SumSettlementFloat(s, f => f.HearthDaily, c => c.HearthDaily, k => k.HearthDaily, currentValue);
            if (CapitalBehavior.Current != null)
                sum += CapitalBehavior.Current.GetCapHearth(s, currentValue + sum);
            return sum;
        }
        public int GetTotalGarrisonCapacityBonus(Settlement s, float currentValue = float.MaxValue)
        {
            int sum = SumSettlementInt(s, f => f.GarrisonCapacityBonus, c => c.GarrisonCapacityBonus, k => k.GarrisonCapacityBonus, currentValue);
            if (CapitalBehavior.Current != null)
                sum += CapitalBehavior.Current.GetCapGarrisonCap(s, currentValue + sum);
            return sum;
        }

        public int GetClanRetinueSizeBonus(Clan clan, float currentValue = float.MaxValue) => SumClanInt(clan, c => c.RetinueSizeBonus, currentValue: currentValue);
        public int GetKingdomRetinueSizeBonus(Kingdom kingdom, float currentValue = float.MaxValue) => SumKingdomInt(kingdom, k => k.RetinueSizeBonus, currentValue);
        public int GetTotalRetinueSizeBonus(Hero hero, float currentValue = float.MaxValue)
        {
            if (hero?.Clan == null) return 0;
            int bonus = GetClanRetinueSizeBonus(hero.Clan, currentValue);
            if (hero.Clan.Kingdom != null)
                bonus += GetKingdomRetinueSizeBonus(hero.Clan.Kingdom, currentValue + bonus);
            return bonus;
        }

        public int GetClanPartySizeBonus(Clan clan, float currentValue = float.MaxValue) => SumClanInt(clan, c => c.PartySizeBonus, currentValue: currentValue);
        public int GetKingdomPartySizeBonus(Kingdom k, float currentValue = float.MaxValue) => SumKingdomInt(k, u => u.PartySizeBonus, currentValue);
        public int GetTotalPartySizeBonus(Hero hero, float currentValue = float.MaxValue)
        {
            if (hero?.Clan == null) return 0;
            int b = GetClanPartySizeBonus(hero.Clan, currentValue);
            float running = currentValue + b;
            if (hero.Clan.Kingdom != null) b += GetKingdomPartySizeBonus(hero.Clan.Kingdom, running);
            if (CapitalBehavior.Current != null) b += CapitalBehavior.Current.GetCapPartySizeBonus(hero.Clan);
            return b;
        }

        public float GetClanPartySpeedBonus(Clan clan, float currentValue = float.MaxValue) => SumClanFloat(clan, c => c.PartySpeedBonus, currentValue: currentValue);
        public float GetKingdomPartySpeedBonus(Kingdom k, float currentValue = float.MaxValue) => SumKingdomFloat(k, u => u.PartySpeedBonus, currentValue);
        public float GetTotalPartySpeedBonus(Hero hero, float currentValue = float.MaxValue)
        {
            if (hero?.Clan == null) return 0f;
            float b = GetClanPartySpeedBonus(hero.Clan, currentValue);
            if (hero.Clan.Kingdom != null) b += GetKingdomPartySpeedBonus(hero.Clan.Kingdom, currentValue + b);
            if (CapitalBehavior.Current != null) b += CapitalBehavior.Current.GetCapPartySpeedBonus(hero.Clan);
            return b;
        }

        public int GetClanPartyAmountBonus(Clan clan, float currentValue = float.MaxValue) => SumClanInt(clan, c => c.PartyAmountBonus, currentValue: currentValue);
        public int GetTotalPartyAmountBonus(Clan clan, float currentValue = float.MaxValue) => clan == null ? 0 : GetClanPartyAmountBonus(clan, currentValue);

        public int GetClanMaxVassalsBonus(Clan clan, float currentValue = float.MaxValue) => SumClanInt(clan, c => c.MaxVassalsBonus, currentValue: currentValue);
        public int GetTotalMaxVassalsBonus(Clan clan, float currentValue = float.MaxValue) => clan == null ? 0 : GetClanMaxVassalsBonus(clan, currentValue);

        public float GetClanRenownDaily(Clan clan, float currentValue = float.MaxValue) => SumClanFloat(clan, c => c.RenownDaily, currentValue: currentValue);
        public float GetKingdomRenownDaily(Kingdom k, float currentValue = float.MaxValue) => SumKingdomFloat(k, u => u.RenownDaily, currentValue);
        public float GetTotalRenownDaily(Hero hero, float currentValue = float.MaxValue)
        {
            if (hero?.Clan == null) return 0f;
            float b = GetClanRenownDaily(hero.Clan, currentValue);
            if (hero.Clan.Kingdom != null) b += GetKingdomRenownDaily(hero.Clan.Kingdom, currentValue + b);
            return b;
        }

        public float GetClanInfluenceDaily(Clan clan, float currentValue = float.MaxValue) => SumClanFloat(clan, c => c.InfluenceDaily, currentValue: currentValue);
        public float GetKingdomInfluenceDaily(Kingdom kingdom, float currentValue = float.MaxValue) => SumKingdomFloat(kingdom, k => k.InfluenceDaily, currentValue);

        public void ApplyRenownDaily(Clan clan)
        {
            clan.AddRenown(GetTotalRenownDaily(clan.Leader, clan.Renown), false);
            float influence = GetClanInfluenceDaily(clan, clan.Influence);
            if (clan.Kingdom != null) influence += GetKingdomInfluenceDaily(clan.Kingdom, clan.Influence + influence);
            if (influence != 0f) clan.Influence += influence;
        }

        public int GetKingdomMaxClansBonus(Kingdom k, float currentValue = float.MaxValue) => SumKingdomInt(k, u => u.MaxClansBonus, currentValue);
        public int GetTotalKingdomMaxClansBonus(Kingdom k, float currentValue = float.MaxValue) => k == null ? 0 : GetKingdomMaxClansBonus(k, currentValue);
        public int GetKingdomMaxMercClansBonus(Kingdom k, float currentValue = float.MaxValue) => SumKingdomInt(k, u => u.MaxMercClansBonus, currentValue);
        public int GetTotalKingdomMaxMercClansBonus(Kingdom k, float currentValue = float.MaxValue) => k == null ? 0 : GetKingdomMaxMercClansBonus(k, currentValue);

        public int GetFlatClanMercBonus(Clan clan, float currentValue = float.MaxValue) => SumClanInt(clan, c => c.MercIncomeFlat, currentValue: currentValue);
        public float GetPercentClanMercBonus(Clan clan) => 1f + SumClanFloat(clan, c => c.MercIncomePercent);
        public int GetFlatMercBonus(Hero hero, float currentValue = float.MaxValue) => hero?.Clan == null ? 0 : GetFlatClanMercBonus(hero.Clan, currentValue);

        public float GetTotalLoyaltyDailyFlat(Settlement s, float currentValue = float.MaxValue)
        {
            float sum = SumSettlementFloat(s, f => f.LoyaltyDailyFlat, c => c.LoyaltyDailyFlat, k => k.LoyaltyDailyFlat, currentValue);
            if (CapitalBehavior.Current != null) sum += CapitalBehavior.Current.GetCapLoyaltyFlat(s, currentValue + sum);
            return sum;
        }
        public float GetTotalLoyaltyDailyPercent(Settlement s, float currentValue = float.MaxValue)
        {
            float sum = SumSettlementFloat(s, f => f.LoyaltyDailyPercent, c => c.LoyaltyDailyPercent, k => k.LoyaltyDailyPercent, currentValue);
            if (CapitalBehavior.Current != null) sum += CapitalBehavior.Current.GetCapLoyaltyPercent(s, currentValue + sum);
            return sum;
        }
        public float GetTotalProsperityDailyFlat(Settlement s, float currentValue = float.MaxValue)
        {
            float sum = SumSettlementFloat(s, f => f.ProsperityDailyFlat, c => c.ProsperityDailyFlat, k => k.ProsperityDailyFlat, currentValue);
            if (CapitalBehavior.Current != null) sum += CapitalBehavior.Current.GetCapLoyaltyFlat(s, currentValue + sum);
            return sum;
        }
        public float GetTotalProsperityDailyPercent(Settlement s, float currentValue = float.MaxValue)
        {
            float sum = SumSettlementFloat(s, f => f.ProsperityDailyPercent, c => c.ProsperityDailyPercent, k => k.ProsperityDailyPercent, currentValue);
            if (CapitalBehavior.Current != null) sum += CapitalBehavior.Current.GetCapLoyaltyPercent(s, currentValue + sum);
            return sum;
        }
        public float GetTotalSecurityDailyFlat(Settlement s, float currentValue = float.MaxValue)
        {
            float sum = SumSettlementFloat(s, f => f.SecurityDailyFlat, c => c.SecurityDailyFlat, k => k.SecurityDailyFlat, currentValue);
            if (CapitalBehavior.Current != null) sum += CapitalBehavior.Current.GetCapLoyaltyFlat(s, currentValue + sum);
            return sum;
        }
        public float GetTotalSecurityDailyPercent(Settlement s, float currentValue = float.MaxValue)
        {
            float sum = SumSettlementFloat(s, f => f.SecurityDailyPercent, c => c.SecurityDailyPercent, k => k.SecurityDailyPercent, currentValue);
            if (CapitalBehavior.Current != null) sum += CapitalBehavior.Current.GetCapLoyaltyPercent(s, currentValue + sum);
            return sum;
        }
        public float GetTotalMilitiaDailyFlat(Settlement s, float currentValue = float.MaxValue)
        {
            float sum = SumSettlementFloat(s, f => f.MilitiaDailyFlat, c => c.MilitiaDailyFlat, k => k.MilitiaDailyFlat, currentValue);
            if (CapitalBehavior.Current != null) sum += CapitalBehavior.Current.GetCapLoyaltyFlat(s, currentValue + sum);
            return sum;
        }
        public float GetTotalMilitiaDailyPercent(Settlement s, float currentValue = float.MaxValue)
        {
            float sum = SumSettlementFloat(s, f => f.MilitiaDailyPercent, c => c.MilitiaDailyPercent, k => k.MilitiaDailyPercent, currentValue);
            if (CapitalBehavior.Current != null) sum += CapitalBehavior.Current.GetCapLoyaltyPercent(s, currentValue + sum);
            return sum;
        }
        public float GetTotalFoodDailyFlat(Settlement s, float currentValue = float.MaxValue)
        {
            float sum = SumSettlementFloat(s, f => f.FoodDailyFlat, c => c.FoodDailyFlat, k => k.FoodDailyFlat, currentValue);
            if (CapitalBehavior.Current != null) sum += CapitalBehavior.Current.GetCapLoyaltyFlat(s, currentValue + sum);
            return sum;
        }
        public float GetTotalFoodDailyPercent(Settlement s, float currentValue = float.MaxValue)
        {
            float sum = SumSettlementFloat(s, f => f.FoodDailyPercent, c => c.FoodDailyPercent, k => k.FoodDailyPercent, currentValue);
            if (CapitalBehavior.Current != null) sum += CapitalBehavior.Current.GetCapLoyaltyPercent(s, currentValue + sum);
            return sum;
        }

        // Backward-compatible short names
        public float GetLoyaltyFlat(Settlement s, float cv = float.MaxValue) => GetTotalLoyaltyDailyFlat(s, cv);
        public float GetLoyaltyPercent(Settlement s, float cv = float.MaxValue) => GetTotalLoyaltyDailyPercent(s, cv);
        public float GetProsperityFlat(Settlement s, float cv = float.MaxValue) => GetTotalProsperityDailyFlat(s, cv);
        public float GetProsperityPercent(Settlement s, float cv = float.MaxValue) => GetTotalProsperityDailyPercent(s, cv);
        public float GetSecurityFlat(Settlement s, float cv = float.MaxValue) => GetTotalSecurityDailyFlat(s, cv);
        public float GetSecurityPercent(Settlement s, float cv = float.MaxValue) => GetTotalSecurityDailyPercent(s, cv);
        public float GetMilitiaFlat(Settlement s, float cv = float.MaxValue) => GetTotalMilitiaDailyFlat(s, cv);
        public float GetMilitiaPercent(Settlement s, float cv = float.MaxValue) => GetTotalMilitiaDailyPercent(s, cv);
        public float GetFoodFlat(Settlement s, float cv = float.MaxValue) => GetTotalFoodDailyFlat(s, cv);
        public float GetFoodPercent(Settlement s, float cv = float.MaxValue) => GetTotalFoodDailyPercent(s, cv);
        #endregion
    }
}