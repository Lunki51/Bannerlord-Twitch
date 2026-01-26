using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem.Settlements;
using Helpers;

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
            if (party == null)
                return;

            try
            {
                string partyId = party.StringId;
                if (string.IsNullOrEmpty(partyId))
                    return;

                bool removedFromSet = _mercenaryPartyIds.Remove(partyId);
                bool removedFromDict = _mercenaryArmyData.Remove(partyId);

                if (removedFromSet || removedFromDict)
                {
                    Log.Info($"[BLT] Unregistered mercenary party: {partyId}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] UnregisterMercenaryParty error: {ex}");

                // Emergency fallback - try to remove by StringId even if exception occurred
                try
                {
                    if (!string.IsNullOrEmpty(party?.StringId))
                    {
                        _mercenaryPartyIds.Remove(party.StringId);
                        _mercenaryArmyData.Remove(party.StringId);
                    }
                }
                catch { }
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

        // ===== AI BEHAVIOR ENFORCEMENT =====

        /// <summary>
        /// Prevent mercenary armies from changing their default behavior away from BesiegeSettlement
        /// This allows normal AI to handle siege mechanics while preventing objective changes
        /// </summary>
        [HarmonyPatch(typeof(MobileParty))]
        [HarmonyPatch("ShouldConsiderAvoiding")]
        public static class MercenaryArmy_ShouldConsiderAvoiding_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                MobileParty __instance,
                MobileParty party,
                MobileParty targetParty,
                ref bool __result)
            {
                try
                {
                    // Add null checks for both parameters
                    if (party == null || targetParty == null)
                        return true; // Run vanilla logic

                    // Only intervene for mercenary armies
                    if (!IsMercenaryArmy(party))
                        return true; // Run vanilla logic

                    // Re-implementation of vanilla logic with mercenary-safe rules
                    __result =
                        (targetParty.SiegeEvent == null
                            || !targetParty.SiegeEvent.BesiegedSettlement.HasPort
                            || targetParty.SiegeEvent.IsBlockadeActive
                            || !party.IsTargetingPort)
                        && (targetParty.IsMainParty
                            || MobilePartyHelper.CanPartyAttackWithCurrentMorale(targetParty))
                        && ((targetParty.Aggressiveness > 0.01f
                            && !targetParty.IsInRaftState)
                            || targetParty.IsGarrison);

                    return false; // Skip original method
                }
                catch (Exception ex)
                {
                    Log.Error($"[BLT] ShouldConsiderAvoiding patch error: {ex}");
                    return true; // Fail open to avoid breaking AI
                }
            }
        }

        /// <summary>
        /// Prevent mercenary armies from engaging hostile parties - they only siege
        /// </summary>
        [HarmonyPatch(typeof(MobileParty), "IsEngagingParty")]
        [HarmonyPrefix]
        private static bool Prefix_IsEngagingParty(MobileParty __instance, ref bool __result)
        {
            try
            {
                if (__instance != null && IsMercenaryArmy(__instance))
                {
                    __result = false; // Never engage in field battles
                    return false; // Skip original method
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Prefix_IsEngagingParty error: {ex}");
            }
            return true;
        }

        /// <summary>
        /// Prevent other parties from considering mercenary armies as valid targets
        /// This prevents the AI from trying to intercept them
        /// </summary>
        [HarmonyPatch(typeof(MobileParty), "IsValidTarget")]
        [HarmonyPostfix]
        private static void Postfix_IsValidTarget(MobileParty __instance, MobileParty party, ref bool __result)
        {
            try
            {
                // If the party being checked is a mercenary army, it's not a valid target for interception
                if (party != null && IsMercenaryArmy(party))
                {
                    __result = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Postfix_IsValidTarget error: {ex}");
            }
        }

        /// <summary>
        /// Ensure mercenary armies can properly initiate sieges
        /// </summary>
        [HarmonyPatch(typeof(MobileParty), "CanStartSiegeEvent")]
        [HarmonyPostfix]
        private static void Postfix_CanStartSiegeEvent(MobileParty __instance, ref bool __result)
        {
            try
            {
                // If this is a mercenary army and it's at its target, ensure it CAN siege
                if (__instance != null && IsMercenaryArmy(__instance))
                {
                    // Get the army data to check target
                    if (_mercenaryArmyData.TryGetValue(__instance.StringId, out var armyData))
                    {
                        var targetSettlement = Settlement.Find(armyData.TargetSettlementId);

                        if (targetSettlement != null &&
                            __instance.CurrentSettlement == targetSettlement &&
                            targetSettlement.SiegeEvent == null)
                        {
                            // Make sure we can start a siege if we're at our target
                            __result = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] Postfix_CanStartSiegeEvent error: {ex}");
            }
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
                                $"Party={party?.Name?.ToString() ?? "MISSING"}, " +
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