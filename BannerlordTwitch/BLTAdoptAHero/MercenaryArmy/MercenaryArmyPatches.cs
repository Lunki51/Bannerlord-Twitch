using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero
{
    [HarmonyPatch]
    internal static class MercenaryArmyPatches
    {
        // ─────────────────────────────────────────────
        //  REGISTRATION
        // ─────────────────────────────────────────────

        private static readonly HashSet<string> _mercPartyIds = new();

        public static void RegisterMercenaryArmy(MobileParty party, Army army)
        {
            if (party == null || army == null) return;
            _mercPartyIds.Add(party.StringId);
            Log.Info($"[BLT] Registered mercenary party: {party.StringId}");
        }

        public static void UnregisterMercenaryArmy(string partyId)
        {
            if (!string.IsNullOrEmpty(partyId))
                _mercPartyIds.Remove(partyId);
        }

        public static void ClearAllRegistrations() => _mercPartyIds.Clear();

        public static bool IsMercenaryParty(MobileParty party) =>
            party != null && _mercPartyIds.Contains(party.StringId);

        public static bool IsMercenaryArmy(Army army) =>
            army?.LeaderParty != null && IsMercenaryParty(army.LeaderParty);

        // ─────────────────────────────────────────────
        //  PATCH 1 — Zero wages (paid upfront)
        // ─────────────────────────────────────────────

        [HarmonyPatch(typeof(MobileParty), "TotalWage", MethodType.Getter)]
        [HarmonyPostfix]
        private static void Postfix_TotalWage(MobileParty __instance, ref int __result)
        {
            try
            {
                if (IsMercenaryParty(__instance))
                    __result = 0;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Postfix_TotalWage error: {ex}");
            }
        }

        // ─────────────────────────────────────────────
        //  PATCH 2 — Block army dispersion for mercenary armies
        //
        //  Cohesion drain, food shortage, inactivity, and the "not enough troops"
        //  automatic checks all run through Army.CheckArmyDispersion. We own the
        //  lifecycle of mercenary armies entirely (MercenaryArmyBehavior handles
        //  troop thresholds and food), so we block this wholesale.
        //
        //  NOTE: This intentionally does NOT block dispersion caused by
        //  LeaderPartyRemoved, ArmyLeaderIsDead, or KingdomChanged — those
        //  are handled by our event listeners and should take priority.
        // ─────────────────────────────────────────────

        [HarmonyPatch(typeof(Army), "CheckArmyDispersion")]
        [HarmonyPrefix]
        private static bool Prefix_CheckArmyDispersion(Army __instance)
        {
            try
            {
                if (IsMercenaryArmy(__instance))
                    return false; // Skip — we manage disbanding ourselves
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Prefix_CheckArmyDispersion error: {ex}");
            }
            return true;
        }

        // ─────────────────────────────────────────────
        //  PATCH 3 — Lock cohesion at maximum for mercenary armies
        //
        //  Army.Cohesion has a public setter. We postfix it to clamp the
        //  value back to 100 whenever the engine tries to reduce it.
        //  This covers the hourly drain tick, influence spending, and any
        //  other path that writes to Cohesion.
        // ─────────────────────────────────────────────

        // ─────────────────────────────────────────────
        //  PATCH 3 — Lock cohesion at maximum for mercenary armies
        //
        //  Uses a PREFIX (not postfix) with a ref value parameter so we
        //  intercept and clamp the incoming value BEFORE the setter runs.
        //  A postfix that writes __instance.Cohesion = 100f would re-enter
        //  the setter and loop infinitely — this avoids that entirely.
        // ─────────────────────────────────────────────

        [HarmonyPatch(typeof(Army), nameof(Army.Cohesion), MethodType.Setter)]
        [HarmonyPrefix]
        private static void Prefix_CohesionSetter(Army __instance, ref float value)
        {
            try
            {
                if (IsMercenaryArmy(__instance))
                    value = Math.Max(value, 100f);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Prefix_CohesionSetter error: {ex}");
            }
        }

        // ─────────────────────────────────────────────
        //  NOTE: Prefix_ResetAllMovementParameters has been REMOVED.
        //
        //  The previous patch blocked every SetMove* call on mercenary
        //  army leaders (all of them call ResetAllMovementParameters first).
        //  This prevented our own re-issue logic from working and was the
        //  primary cause of the broken siege order.
        //
        //  Correct approach: use party.Ai.SetDoNotMakeNewDecisions(true)
        //  to block Tier 3 AI re-evaluation, and re-issue the siege order
        //  via SetPartyAiAction when the behavior drifts (see behavior).
        // ─────────────────────────────────────────────
    }
}