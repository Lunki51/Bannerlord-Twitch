using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem.Settlements;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Harmony patches to control mercenary army behavior.
    /// Simple patches for flee prevention, wage elimination, and party limit bonuses.
    /// </summary>
    [HarmonyPatch]
    internal static class MercenaryArmyPatches
    {
        // Fast lookup for mercenary parties
        private static readonly HashSet<string> _mercenaryPartyIds = new HashSet<string>();
        private static readonly Dictionary<string, MercenaryArmyBehavior.MercenaryArmyData> _mercenaryArmyData = new Dictionary<string, MercenaryArmyBehavior.MercenaryArmyData>();

        /// <summary>
        /// Register a mercenary party (called from MercenaryArmyBehavior)
        /// </summary>
        public static void RegisterMercenaryParty(MobileParty party, MercenaryArmyBehavior.MercenaryArmyData armyData)
        {
            if (party == null || string.IsNullOrEmpty(party.StringId) || armyData == null)
                return;

            try
            {
                _mercenaryPartyIds.Add(party.StringId);
                _mercenaryArmyData[party.StringId] = armyData;

                Log.Info($"[BLT] Registered mercenary party: {party.Name} ({party.StringId})");
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] RegisterMercenaryParty error: {ex}");
            }
        }

        /// <summary>
        /// Unregister a mercenary party
        /// </summary>
        public static void UnregisterMercenaryParty(MobileParty party)
        {
            if (party == null || string.IsNullOrEmpty(party.StringId))
                return;

            try
            {
                _mercenaryPartyIds.Remove(party.StringId);
                _mercenaryArmyData.Remove(party.StringId);

                Log.Info($"[BLT] Unregistered mercenary party: {party.StringId}");
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] UnregisterMercenaryParty error: {ex}");
            }
        }

        /// <summary>
        /// Clear all registrations (called during save/load)
        /// </summary>
        public static void ClearAllRegistrations()
        {
            _mercenaryPartyIds.Clear();
            _mercenaryArmyData.Clear();
            Log.Info("[BLT] Cleared all mercenary party registrations");
        }

        /// <summary>
        /// Check if a party is a mercenary army
        /// </summary>
        public static bool IsMercenaryArmy(MobileParty party)
        {
            if (party == null || string.IsNullOrEmpty(party.StringId))
                return false;

            return _mercenaryPartyIds.Contains(party.StringId);
        }

        // ===== FLEE PREVENTION (WORLD MAP ONLY) =====

        /// <summary>
        /// Prevent mercenary armies from fleeing on the world map
        /// (Does NOT affect in-battle AI)
        /// </summary>
        [HarmonyPatch(typeof(MobileParty), "ShouldPartyTryToFleeFromEnemies")]
        [HarmonyPrefix]
        private static bool Prefix_ShouldFleeFromEnemies(MobileParty __instance, ref bool __result)
        {
            try
            {
                if (__instance != null && IsMercenaryArmy(__instance))
                {
                    __result = false; // Never flee on world map
                    return false; // Skip original method
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Prefix_ShouldFleeFromEnemies error: {ex}");
            }
            return true;
        }

        // ===== WAGE ELIMINATION =====

        /// <summary>
        /// Eliminate wage costs for mercenary armies (all paid upfront)
        /// </summary>
        [HarmonyPatch(typeof(MobileParty), "TotalWage", MethodType.Getter)]
        [HarmonyPostfix]
        private static void Postfix_TotalWage(MobileParty __instance, ref int __result)
        {
            try
            {
                if (__instance != null && IsMercenaryArmy(__instance))
                {
                    __result = 0; // No ongoing wages
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Postfix_TotalWage error: {ex}");
            }
        }

        /// <summary>
        /// Prevent wage payment for mercenary armies
        /// </summary>
        [HarmonyPatch(typeof(MobileParty), "PayWages")]
        [HarmonyPrefix]
        private static bool Prefix_PayWages(MobileParty __instance)
        {
            try
            {
                if (__instance != null && IsMercenaryArmy(__instance))
                {
                    return false; // Skip wage payment entirely
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Prefix_PayWages error: {ex}");
            }
            return true;
        }

        // ===== PARTY LIMIT BONUS =====

        /// <summary>
        /// Add party limit bonus for active mercenary armies
        /// Prevents mercenary armies from reducing available party slots
        /// </summary>
        [HarmonyPatch(typeof(Clan), "CommanderLimit", MethodType.Getter)]
        [HarmonyPostfix]
        private static void Postfix_CommanderLimit(Clan __instance, ref int __result)
        {
            try
            {
                if (__instance == null)
                    return;

                // Add bonus from UpgradeBehavior (from other mod features)
                if (UpgradeBehavior.Current != null)
                {
                    __result += UpgradeBehavior.Current.GetTotalPartyAmountBonus(__instance);
                }

                // Add bonus from active mercenary armies
                var behavior = MercenaryArmyBehavior.Current;
                if (behavior != null)
                {
                    int mercenaryArmies = behavior.GetActiveArmiesForClan(__instance);
                    __result += mercenaryArmies; // +1 per active army
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Postfix_CommanderLimit error: {ex}");
            }
        }

        // ===== DEBUG AND DIAGNOSTICS =====

        /// <summary>
        /// Diagnostic method to check mercenary army status
        /// </summary>
        public static void LogMercenaryArmyStatus()
        {
            try
            {
                Log.Info($"[BLT] === Mercenary Army Status ===");
                Log.Info($"[BLT] Registered parties: {_mercenaryPartyIds.Count}");

                var behavior = MercenaryArmyBehavior.Current;
                if (behavior != null)
                {
                    // Get all armies (pass null to get all)
                    var allHeroes = Hero.AllAliveHeroes.Where(h => h != null);
                    var allArmies = new List<MercenaryArmyBehavior.MercenaryArmyData>();
                    foreach (var hero in allHeroes)
                    {
                        allArmies.AddRange(behavior.GetArmiesForHero(hero));
                    }

                    Log.Info($"[BLT] Tracked armies in behavior: {allArmies.Count}");

                    foreach (var army in allArmies)
                    {
                        var party = MobileParty.All.FirstOrDefault(p => p.StringId == army.PartyId);
                        var target = Settlement.Find(army.TargetSettlementId);

                        Log.Info($"[BLT]   Army {army.PartyId}: " +
                                $"Party={party?.Name.ToString() ?? "MISSING"}, " +
                                $"Target={target?.Name?.ToString() ?? "MISSING"}, " +
                                $"Active={army.IsActive}");
                    }
                }

                Log.Info($"[BLT] === End Status ===");
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] LogMercenaryArmyStatus error: {ex}");
            }
        }
    }
}