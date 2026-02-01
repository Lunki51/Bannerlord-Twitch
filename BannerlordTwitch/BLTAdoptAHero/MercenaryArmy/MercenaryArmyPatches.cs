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
        private static readonly HashSet<string> _mercenaryPartyIds = new HashSet<string>();

        public static void RegisterMercenaryArmy(MobileParty party, Army army)
        {
            if (party == null || army == null) return;

            _mercenaryPartyIds.Add(party.StringId);

            Log.Info($"[BLT] Registered mercenary: Party={party.StringId}");
        }

        public static void UnregisterMercenaryArmy(string partyId)
        {
            if (!string.IsNullOrEmpty(partyId))
                _mercenaryPartyIds.Remove(partyId);

            Log.Info($"[BLT] Unregistered mercenary army");
        }

        public static void ClearAllRegistrations()
        {
            _mercenaryPartyIds.Clear();
        }

        public static bool IsMercenaryParty(MobileParty party)
        {
            return party != null && _mercenaryPartyIds.Contains(party.StringId);
        }

        public static bool IsMercenaryArmy(Army army)
        {
            // Check if the army's leader party is a mercenary party
            return army?.LeaderParty != null && IsMercenaryParty(army.LeaderParty);
        }

        /// <summary>
        /// No wages for mercenary armies (paid upfront)
        /// </summary>
        [HarmonyPatch(typeof(MobileParty), "TotalWage", MethodType.Getter)]
        [HarmonyPostfix]
        private static void Postfix_TotalWage(MobileParty __instance, ref int __result)
        {
            try
            {
                if (IsMercenaryParty(__instance))
                {
                    __result = 0;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Postfix_TotalWage error: {ex}");
            }
        }

        /// <summary>
        /// Prevents mercenary army leaders from changing their target
        /// Blocks all SetMove* methods which call this first
        /// </summary>
        [HarmonyPatch(typeof(MobileParty), "ResetAllMovementParameters")]
        [HarmonyPrefix]
        private static bool Prefix_ResetAllMovementParameters(MobileParty __instance)
        {
            try
            {
                // Don't let mercenary army leaders reset their movement parameters
                // This keeps them locked on their target settlement
                if (__instance?.Army != null &&
                    __instance.Army.LeaderParty == __instance &&
                    IsMercenaryArmy(__instance.Army))
                {
                    return false; // Skip - keep current target
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Prefix_ResetAllMovementParameters error: {ex}");
            }
            return true; // Allow for everyone else
        }
    }
}