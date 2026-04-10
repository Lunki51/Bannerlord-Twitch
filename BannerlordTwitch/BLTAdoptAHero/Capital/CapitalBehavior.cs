using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using BLTAdoptAHero.Actions.Upgrades;

namespace BLTAdoptAHero
{
    public class CapitalBehavior : CampaignBehaviorBase
    {
        public static CapitalBehavior Current { get; private set; }

        // clan StringId → active capital settlement StringId
        private Dictionary<string, string> _capitals = new();

        // Pending transfer — flattened for IDataStore serialization
        private Dictionary<string, string> _xferTarget = new(); // clanId → target settlement id
        private Dictionary<string, string> _xferPrevious = new(); // clanId → previous capital id
        private Dictionary<string, float> _xferEndDay = new(); // clanId → CampaignTime day

        // Post-failure cooldown
        private Dictionary<string, float> _cooldownEnd = new(); // clanId → end day
        private Dictionary<string, string> _cooldownRestore = new(); // clanId → settlement to restore

        // Capital-only upgrade ids stored per clan (comma-separated)
        private Dictionary<string, string> _capitalUpgrades = new();

        private GlobalCommonConfig Cfg => GlobalCommonConfig.Get();
        private CapitalConfig CapCfg => Cfg?.CapitalConfig;

        public CapitalBehavior() { Current = this; }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore ds)
        {
            ds.SyncData("BLT_Capitals", ref _capitals);
            ds.SyncData("BLT_XferTarget", ref _xferTarget);
            ds.SyncData("BLT_XferPrevious", ref _xferPrevious);
            ds.SyncData("BLT_XferEndDay", ref _xferEndDay);
            ds.SyncData("BLT_CapCooldownEnd", ref _cooldownEnd);
            ds.SyncData("BLT_CapCooldownRestore", ref _cooldownRestore);
            ds.SyncData("BLT_CapitalUpgrades", ref _capitalUpgrades);
            _capitals ??= new(); _xferTarget ??= new(); _xferPrevious ??= new();
            _xferEndDay ??= new(); _cooldownEnd ??= new(); _cooldownRestore ??= new();
            _capitalUpgrades ??= new();
        }

        // ── Daily tick ────────────────────────────────────────────────────────
        private void OnDailyTick()
        {
            try
            {
                float today = (float)CampaignTime.Now.ToDays;
                TickTransfers(today);
                TickCooldowns(today);
                TickCapitalLoss();
                TickClanBonuses();
            }
            catch (Exception ex) { Log($"Daily tick error: {ex.Message}"); }
        }

        private void TickTransfers(float today)
        {
            foreach (var clanId in _xferTarget.Keys.ToList())
            {
                var clan = Clan.All.FirstOrDefault(c => c.StringId == clanId);
                if (clan == null) { ClearXfer(clanId); continue; }

                var target = Settlement.All.FirstOrDefault(s => s.StringId == _xferTarget[clanId]);

                // Target lost → cancel and enter cooldown
                if (target == null || target.OwnerClan != clan)
                {
                    FailTransfer(clanId, clan, today, "target settlement was lost");
                    continue;
                }

                if (!_xferEndDay.TryGetValue(clanId, out float end)) { ClearXfer(clanId); continue; }
                if (today >= end)
                {
                    _capitals[clanId] = _xferTarget[clanId];
                    Log($"{clan.Name}'s capital has moved to {target.Name}!");
                    ClearXfer(clanId);
                }
            }
        }

        private void TickCooldowns(float today)
        {
            foreach (var clanId in _cooldownEnd.Keys.ToList())
            {
                if (today < _cooldownEnd[clanId]) continue;
                _cooldownEnd.Remove(clanId);
                var clan = Clan.All.FirstOrDefault(c => c.StringId == clanId);

                if (_cooldownRestore.TryGetValue(clanId, out var restoreId))
                {
                    _cooldownRestore.Remove(clanId);
                    var prev = Settlement.All.FirstOrDefault(s => s.StringId == restoreId);
                    if (clan != null && prev != null && prev.OwnerClan == clan)
                    {
                        _capitals[clanId] = restoreId;
                        Log($"{clan.Name}'s capital restored to {prev.Name}.");
                    }
                    else if (clan != null)
                        Log($"{clan.Name}'s cooldown ended but previous capital was also lost. Capital unset.");
                }
            }
        }

        // ── FIX 6: TickCapitalLoss ────────────────────────────────────────────────────
        // Previously, direct capital loss (e.g. siege) silently unset the capital with
        // no cooldown, while FailTransfer (losing a transfer target) imposed CooldownDays.
        // Now both paths impose the same cooldown for symmetry.
        private void TickCapitalLoss()
        {
            float today = (float)CampaignTime.Now.ToDays;
            foreach (var clanId in _capitals.Keys.ToList())
            {
                var clan = Clan.All.FirstOrDefault(c => c.StringId == clanId);
                var settlement = Settlement.All.FirstOrDefault(s => s.StringId == _capitals[clanId]);
                if (clan == null || settlement == null || settlement.OwnerClan != clan)
                {
                    _capitals.Remove(clanId);
                    if (clan != null && settlement != null)
                    {
                        int coolDays = CapCfg?.CooldownDays ?? 30;
                        _cooldownEnd[clanId] = today + coolDays;
                        Log($"{clan.Name} lost their capital {settlement.Name}. Cooldown: {coolDays} days.");
                    }
                    else if (clan != null)
                        Log($"{clan.Name}'s capital settlement no longer exists. Capital unset.");
                }
            }
        }

        private void TickClanBonuses()
        {
            var cfg = CapCfg;
            if (cfg == null || !cfg.Enabled) return;
            if (cfg.RenownDaily == 0f && cfg.InfluenceDaily == 0f) return;

            foreach (var clanId in _capitals.Keys)
            {
                var clan = Clan.All.FirstOrDefault(c => c.StringId == clanId);
                if (clan == null) continue;
                if (cfg.RenownDaily != 0f) clan.AddRenown(cfg.RenownDaily, false);
                if (cfg.InfluenceDaily != 0f) clan.Influence += cfg.InfluenceDaily;
            }
        }

        private void FailTransfer(string clanId, Clan clan, float today, string reason)
        {
            var cfg = CapCfg;
            int coolDays = cfg?.CooldownDays ?? 30;
            string prev = _xferPrevious.TryGetValue(clanId, out var p) ? p : null;
            ClearXfer(clanId);
            _capitals.Remove(clanId);
            _cooldownEnd[clanId] = today + coolDays;
            if (!string.IsNullOrEmpty(prev)) _cooldownRestore[clanId] = prev;
            Log($"{clan.Name}'s capital transfer cancelled ({reason}). Cooldown: {coolDays} days.");
        }

        private void ClearXfer(string clanId)
        {
            _xferTarget.Remove(clanId); _xferPrevious.Remove(clanId); _xferEndDay.Remove(clanId);
        }

        // ── Public state API ─────────────────────────────────────────────────
        public Settlement GetCapital(Clan clan)
        {
            if (clan == null || !_capitals.TryGetValue(clan.StringId, out var id)) return null;
            return Settlement.All.FirstOrDefault(s => s.StringId == id);
        }

        public bool IsCapital(Settlement s, Clan clan = null)
        {
            if (s == null) return false;
            var c = clan ?? s.OwnerClan;
            return c != null && _capitals.TryGetValue(c.StringId, out var id) && id == s.StringId;
        }

        public bool HasActiveCapital(Clan clan) => clan != null && _capitals.ContainsKey(clan.StringId);
        public bool IsTransferPending(Clan clan) => clan != null && _xferTarget.ContainsKey(clan.StringId);
        public bool IsInCooldown(Clan clan) => clan != null && _cooldownEnd.ContainsKey(clan.StringId);

        public float GetCooldownDaysLeft(Clan clan) =>
            clan == null ? 0f : Math.Max(0f, (_cooldownEnd.TryGetValue(clan.StringId, out var d) ? d : 0f) - (float)CampaignTime.Now.ToDays);

        public float GetTransferDaysLeft(Clan clan) =>
            clan == null ? 0f : Math.Max(0f, (_xferEndDay.TryGetValue(clan.StringId, out var d) ? d : 0f) - (float)CampaignTime.Now.ToDays);

        public Settlement GetPendingTarget(Clan clan)
        {
            if (clan == null || !_xferTarget.TryGetValue(clan.StringId, out var id)) return null;
            return Settlement.All.FirstOrDefault(s => s.StringId == id);
        }

        /// <summary>Set the initial capital (no transfer delay).</summary>
        public bool SetCapital(Clan clan, Settlement s, out string err)
        {
            err = null;
            if (IsInCooldown(clan)) { err = "Cannot set capital during cooldown"; return false; }
            if (IsTransferPending(clan)) { err = "A transfer is already pending; cancel it first"; return false; }
            if (s.OwnerClan != clan) { err = "You do not own that settlement"; return false; }
            if (s.Town == null) { err = "Only towns and castles can be capitals"; return false; }
            _capitals[clan.StringId] = s.StringId;
            return true;
        }

        /// <summary>Begin transferring the capital. Removes bonuses from old capital immediately.</summary>
        public bool BeginTransfer(Clan clan, Settlement newCap, int days, out string err)
        {
            err = null;
            if (IsInCooldown(clan)) { err = "Cannot transfer capital during cooldown"; return false; }
            if (IsTransferPending(clan)) { err = "A transfer is already in progress; cancel it first"; return false; }
            if (!HasActiveCapital(clan)) { err = "Set a capital first before transferring"; return false; }
            if (newCap.OwnerClan != clan) { err = "You do not own that settlement"; return false; }
            if (newCap.Town == null) { err = "Only towns and castles can be capitals"; return false; }
            if (IsCapital(newCap, clan)) { err = "That settlement is already your capital"; return false; }

            string id = clan.StringId;
            _xferPrevious[id] = _capitals[id];
            _xferTarget[id] = newCap.StringId;
            _xferEndDay[id] = (float)CampaignTime.Now.ToDays + days;
            _capitals.Remove(id);   // bonuses stripped from old capital immediately
            return true;
        }

        /// <summary>Cancel a pending transfer; restores the previous capital if still owned.</summary>
        public bool CancelTransfer(Clan clan, out string err)
        {
            err = null;
            if (!IsTransferPending(clan)) { err = "No pending transfer to cancel"; return false; }
            string id = clan.StringId;
            if (_xferPrevious.TryGetValue(id, out var prevId))
            {
                var prev = Settlement.All.FirstOrDefault(s => s.StringId == prevId);
                if (prev != null && prev.OwnerClan == clan)
                    _capitals[id] = prevId;
            }
            ClearXfer(id);
            return true;
        }

        // ── Capital-only upgrade storage ─────────────────────────────────────
        private static List<string> Parse(string s)
            => string.IsNullOrEmpty(s) ? new() : s.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        private static string Ser(List<string> l) => l == null || l.Count == 0 ? "" : string.Join(",", l);

        public List<string> GetCapitalUpgrades(Clan clan)
        {
            if (clan == null) return new();
            return _capitalUpgrades.TryGetValue(clan.StringId, out var s) ? Parse(s) : new();
        }

        public bool HasCapitalUpgrade(Clan clan, string id)
            => clan != null && GetCapitalUpgrades(clan).Contains(id, StringComparer.OrdinalIgnoreCase);

        public bool AddCapitalUpgrade(Clan clan, string id)
        {
            if (clan == null || string.IsNullOrEmpty(id)) return false;
            var l = _capitalUpgrades.TryGetValue(clan.StringId, out var s) ? Parse(s) : new List<string>();
            if (l.Contains(id, StringComparer.OrdinalIgnoreCase)) return false;
            l.Add(id);
            _capitalUpgrades[clan.StringId] = Ser(l);
            return true;
        }

        public bool RemoveCapitalUpgrade(Clan clan, string id)
        {
            if (clan == null || !_capitalUpgrades.TryGetValue(clan.StringId, out var s)) return false;
            var l = Parse(s);
            int idx = l.FindIndex(x => x.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return false;
            l.RemoveAt(idx);
            if (l.Count == 0) _capitalUpgrades.Remove(clan.StringId); else _capitalUpgrades[clan.StringId] = Ser(l);
            return true;
        }

        // ── Capital bonus getters ─────────────────────────────────────────────
        // Returns 0 if the settlement is not the active capital.
        // Combines the base CapitalConfig bonus + all capital-only FiefUpgrade bonuses.

        private float SumUpgradeF(Clan clan, Func<FiefUpgrade, float> sel, float cv)
        {
            if (clan == null || Cfg == null) return 0f;
            float sum = 0f;
            foreach (var uid in GetCapitalUpgrades(clan))
            {
                var up = Cfg.FiefUpgrades?.FirstOrDefault(u => u.ID == uid);
                if (up == null || !up.CapitalOnly) continue;
                float v = sel(up);
                if (v < 0f && cv + sum + v < 0f) continue;
                sum += v;
            }
            return sum;
        }

        private int SumUpgradeI(Clan clan, Func<FiefUpgrade, int> sel, float cv)
        {
            if (clan == null || Cfg == null) return 0;
            float sum = 0f;
            foreach (var uid in GetCapitalUpgrades(clan))
            {
                var up = Cfg.FiefUpgrades?.FirstOrDefault(u => u.ID == uid);
                if (up == null || !up.CapitalOnly) continue;
                int v = sel(up);
                if (v < 0 && cv + sum + v < 0f) continue;
                sum += v;
            }
            return (int)sum;
        }

        // Settlement-level getters — all return 0 if s is not the active capital
        public float GetCapLoyaltyFlat(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.LoyaltyDailyFlat ?? 0f) + SumUpgradeF(s.OwnerClan, u => u.LoyaltyDailyFlat, cv) : 0f;
        public float GetCapLoyaltyPercent(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.LoyaltyDailyPercent ?? 0f) + SumUpgradeF(s.OwnerClan, u => u.LoyaltyDailyPercent, cv) : 0f;
        public float GetCapProsperityFlat(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.ProsperityDailyFlat ?? 0f) + SumUpgradeF(s.OwnerClan, u => u.ProsperityDailyFlat, cv) : 0f;
        public float GetCapProsperityPct(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.ProsperityDailyPercent ?? 0f) + SumUpgradeF(s.OwnerClan, u => u.ProsperityDailyPercent, cv) : 0f;
        public float GetCapSecurityFlat(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.SecurityDailyFlat ?? 0f) + SumUpgradeF(s.OwnerClan, u => u.SecurityDailyFlat, cv) : 0f;
        public float GetCapSecurityPercent(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.SecurityDailyPercent ?? 0f) + SumUpgradeF(s.OwnerClan, u => u.SecurityDailyPercent, cv) : 0f;
        public float GetCapMilitiaFlat(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.MilitiaDailyFlat ?? 0f) + SumUpgradeF(s.OwnerClan, u => u.MilitiaDailyFlat, cv) : 0f;
        public float GetCapMilPercent(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.MilitiaDailyPercent ?? 0f) + SumUpgradeF(s.OwnerClan, u => u.MilitiaDailyPercent, cv) : 0f;
        public float GetCapFoodFlat(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.FoodDailyFlat ?? 0f) + SumUpgradeF(s.OwnerClan, u => u.FoodDailyFlat, cv) : 0f;
        public float GetCapFoodPercent(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.FoodDailyPercent ?? 0f) + SumUpgradeF(s.OwnerClan, u => u.FoodDailyPercent, cv) : 0f;
        public int GetCapTaxFlat(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.TaxIncomeFlat ?? 0) + SumUpgradeI(s.OwnerClan, u => u.TaxIncomeFlat, cv) : 0;
        public float GetCapTaxPercent(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.TaxIncomePercent ?? 0f) + SumUpgradeF(s.OwnerClan, u => u.TaxIncomePercent, cv) : 0f;
        public int GetCapGarrisonCap(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.GarrisonCapacityBonus ?? 0) + SumUpgradeI(s.OwnerClan, u => u.GarrisonCapacityBonus, cv) : 0;
        public float GetCapHearth(Settlement s, float cv = float.MaxValue) => IsCapital(s) ? (CapCfg?.HearthDaily ?? 0f) + SumUpgradeF(s.OwnerClan, u => u.HearthDaily, cv) : 0f;

        // Clan-level getters (base config only — FiefUpgrade has no party/renown fields)
        public int GetCapPartySizeBonus(Clan clan) => HasActiveCapital(clan) ? (CapCfg?.PartySizeBonus ?? 0) : 0;
        public float GetCapPartySpeedBonus(Clan clan) => HasActiveCapital(clan) ? (CapCfg?.PartySpeedBonus ?? 0f) : 0f;

        private static void Log(string msg)
            => TaleWorlds.Library.InformationManager.DisplayMessage(
                new TaleWorlds.Library.InformationMessage($"[BLT Capital] {msg}"));
    }
}


// ═══════════════════════════════════════════════════════════════════════════
// FILE: CapitalAction.cs
// ═══════════════════════════════════════════════════════════════════════════