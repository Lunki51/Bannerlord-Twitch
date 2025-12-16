using System;
using System.Collections.Generic;
using System.Reflection;
using Helpers;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using NavalDLC.CampaignBehaviors;
using NavalDLC.CharacterDevelopment;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero
{
    public static class AdoptedHeroFlags
    {
        public static bool _allowKingdomMove = false;
    }
    #region FactionDiscontinuationCampaignBehavior
    [HarmonyPatch(typeof(FactionDiscontinuationCampaignBehavior))]
    internal static class FactionDiscontinuationPatches
    {
        // 1. Define the Delegate for the private method: 
        //    It must include the instance (__instance) as the first parameter.
        private delegate void FinalizeMapEventsDelegate(FactionDiscontinuationCampaignBehavior instance, Clan clan);

        // 2. Static field to hold the callable delegate
        private static FinalizeMapEventsDelegate FinalizeMapEvents;

        // 3. Static Constructor: Runs once to initialize the delegate via Reflection.
        static FactionDiscontinuationPatches()
        {
            Type instanceType = typeof(FactionDiscontinuationCampaignBehavior);
            // Get the private instance method "FinalizeMapEvents"
            MethodInfo methodInfo = instanceType.GetMethod("FinalizeMapEvents", BindingFlags.NonPublic | BindingFlags.Instance);

            if (methodInfo != null)
            {
                // Create the delegate from the MethodInfo
                FinalizeMapEvents = (FinalizeMapEventsDelegate)Delegate.CreateDelegate(
                    typeof(FinalizeMapEventsDelegate),
                    null,
                    methodInfo
                );
            }
            // Optional: If methodInfo is null, FinalizeMapEvents remains null, 
            // which the Prefix should handle.
        }

        [HarmonyPrefix]
        [HarmonyPatch("DiscontinueClan")]
        private static bool Prefix_DiscontinueClan(Clan clan)
        {
            if (clan?.Leader != null && clan.Leader.IsAdopted())
            {
                try
                {
#if DEBUG
                    Log.Trace("[BLT] Prevented DiscontinueClan for adopted leader clan");
#endif
                    return false; // skip original -> clan not destroyed
                }
                catch (Exception ex)
                {
                    Log.Error($"[BLT] Prefix_DiscontinueClan error: {ex}");
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("CanClanBeDiscontinued")]
        private static bool Prefix_CanClanBeDiscontinued(Clan clan, ref bool __result)
        {
            if (clan?.Leader != null && clan.Leader.IsAdopted())
            {
                try
                {
                    __result = false;
#if DEBUG
                    Log.Trace("[BLT] CanClanBeDiscontinued -> false for adopted leader clan");
#endif
                    return false; // skip original
                }
                catch (Exception ex)
                {
                    Log.Error($"[BLT] Prefix_CanClanBeDiscontinued error: {ex}");
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("DiscontinueKingdom")]
        private static bool Prefix(Kingdom kingdom, FactionDiscontinuationCampaignBehavior __instance)
        {
            try
            {
                // Safety check: if reflection failed, log and let the original method run
                if (FinalizeMapEvents == null)
                {
                    Log.Error("[BLT] FinalizeMapEvents delegate is null. Running original method.");
                    return true;
                }

                // Re-implement the original method's logic here
                foreach (Clan clan in new List<Clan>(kingdom.Clans))
                {
                    FinalizeMapEvents(__instance, clan);
                    // YOUR CUSTOM LOGIC: Check if the clan leader is adopted
                    if (clan.Leader != null && clan.Leader.IsAdopted())
                    {
                        AdoptedHeroFlags._allowKingdomMove = true;
                        ChangeKingdomAction.ApplyByLeaveKingdom(clan);
                        AdoptedHeroFlags._allowKingdomMove = false;
#if DEBUG
                        Log.Trace("[BLT] DiscontinueKingdom success ");
#endif
                    }
                    else
                    {

                        ChangeKingdomAction.ApplyByLeaveByKingdomDestruction(clan, true);
                    }
                }

                // Re-implement the rest of the original method
                kingdom.RulingClan = null;
                DestroyKingdomAction.Apply(kingdom);

                // CRITICAL: Return false to prevent the original method from running
                return false;
            }
            catch (Exception ex)
            {
                // If anything goes wrong, log the error and run the original method to be safe
                Log.Error($"[BLT] DiscontinueKingdom Prefix error: {ex}");
                return true;
            }
            finally { AdoptedHeroFlags._allowKingdomMove = false; }
        }
    }
    #endregion

    #region ChangeKingdomAction
    [HarmonyPatch(typeof(ChangeKingdomAction))]
    internal static class ChangeKingdomActionPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("ApplyByJoinToKingdom")]
        private static bool Prefix_ApplyByJoinToKingdom(Clan clan)
        {
            if (clan?.Leader != null && clan.Leader.IsAdopted() && !AdoptedHeroFlags._allowKingdomMove)
            {
                try
                {
#if DEBUG
                    Log.Trace("[BLT] Blocked ApplyByJoinToKingdom for adopted clan (join blocked)");
#endif
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Error($"[BLT] Prefix_ApplyByJoinToKingdom error: {ex}");
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ApplyByLeaveKingdom")]
        private static bool Prefix_ApplyByLeaveKingdom(Clan clan)
        {
            if (clan?.Leader != null && clan.Leader.IsAdopted() && !AdoptedHeroFlags._allowKingdomMove)
            {
                try
                {
#if DEBUG
                    Log.Trace("[BLT] Blocked ApplyByLeaveKingdom for adopted clan (leave blocked)");
#endif
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Error($"[BLT] Prefix_ApplyByLeaveKingdom error: {ex}");
                }
            }
            return true;
        }
    }
    #endregion

    #region ClanPatches
    [HarmonyPatch(typeof(Clan))]
    internal static class ClanPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("UpdateBannerColorsAccordingToKingdom")]
        private static bool Prefix_UpdateBannerColorsAccordingToKingdom(Clan __instance)
        {
            if (__instance?.Leader != null && __instance.Leader.IsAdopted())
            {
                try
                {
#if DEBUG
                    Log.Trace("[BLT] Blocked UpdateBannerColorsAccordingToKingdom for adopted clan");
#endif
                    return false; // skip original
                }
                catch (Exception ex)
                {
                    Log.Error($"[BLT] Prefix_UpdateBannerColorsAccordingToKingdom error: {ex}");
                }
            }
            return true; // run original if not blocked
        }
    }
    #endregion

    #region OnShipOwnerChanged
    [HarmonyPatch(typeof(ShipTradeCampaignBehavior), "ConsiderSellingShips")]
    public static class Patch_ConsiderSellingShips
    {
        static bool Prefix(Clan clan)
        {
            // SAFETY CHECK: If components are null or empty, SKIP the original method
            if (clan == null || clan.WarPartyComponents == null || clan.WarPartyComponents.Count == 0)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ShipTradeCampaignBehavior), "ConsiderSwappingClanLeaderShips")]
    public static class Patch_ConsiderSwappingClanLeaderShips
    {
        static bool Prefix(Clan clan)
        {
            if (clan == null || clan.WarPartyComponents == null || clan.WarPartyComponents.Count == 0)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ShipTradeCampaignBehavior), "ConsiderSwappingShipsBetweenClanParties")]
    public static class Patch_ConsiderSwappingShipsBetweenClanParties
    {
        static bool Prefix(Clan clan)
        {
            if (clan == null || clan.WarPartyComponents == null || clan.WarPartyComponents.Count == 0)
            {
                return false;
            }
            return true;
        }
    }

    // =============================================================
    // FIX 2: The "Stack Trace" Fix (Null Reference in Event Handler)
    // =============================================================

    [HarmonyPatch(typeof(ShipTradeCampaignBehavior), "OnShipOwnerChanged")]
    public static class Patch_OnShipOwnerChanged
    {
        // We use a Prefix that returns FALSE to completely overwrite the original broken method
        static bool Prefix(Ship ship, PartyBase oldOwner, ChangeShipOwnerAction.ShipOwnerChangeDetail details)
        {
            // 1. Initial Null Safety Check
            if (ship == null || oldOwner == null)
            {
                return false; // Skip original, do nothing
            }

            if (details == ChangeShipOwnerAction.ShipOwnerChangeDetail.ApplyByTrade)
            {
                Hero hero = null;

                // 2. Safe checking for Old Owner (The Seller)
                if (oldOwner.IsSettlement &&
                    oldOwner.Settlement != null &&
                    oldOwner.Settlement.IsTown &&
                    oldOwner.Settlement.Town != null)
                {
                    hero = oldOwner.Settlement.Town.Governor;
                }
                // 3. Safe checking for New Owner (The Buyer)
                else if (ship.Owner != null &&
                         ship.Owner.IsSettlement &&
                         ship.Owner.Settlement != null &&
                         ship.Owner.Settlement.IsTown &&
                         ship.Owner.Settlement.Town != null)
                {
                    hero = ship.Owner.Settlement.Town.Governor;
                }

                // 4. Execute Logic only if valid Hero and valid Owner exist
                if (hero != null && (hero != Hero.MainHero || (ship.Owner != null && ship.Owner.LeaderHero != Hero.MainHero)))
                {
                    ExplainedNumber explainedNumber = new ExplainedNumber(0f, false, null);

                    // Note: Ensure you have reference to Helper classes or use reflection if they are internal
                    PerkHelper.AddPerkBonusForTown(NavalPerks.Boatswain.MerchantPrince, hero.CurrentSettlement.Town, ref explainedNumber);

                    GiveGoldAction.ApplyBetweenCharacters(null, hero, explainedNumber.RoundedResultNumber, false);
                }
            }

            return false; // Skips the original method so it doesn't crash
        }
    }

    #endregion

}