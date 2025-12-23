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
    //[HarmonyPatch(typeof(ChangeShipOwnerAction), "ApplyInternal")]
    //public static class ChangeShipOwnerActionPatch
    //{
    //    [HarmonyPrefix]
    //    private static bool Prefix(PartyBase newOwner, Ship ship, ChangeShipOwnerAction.ShipOwnerChangeDetail changeDetail)
    //    {
    //        try
    //        {
    //            // 1. Basic Null Checks
    //            if (ship == null) { Log.Trace("[FIX] ship is null"); return false; }
    //            if (newOwner == null) { Log.Trace("[FIX] newOwner is null"); return false; }

    //            PartyBase oldOwner = ship.Owner;
    //            //if (oldOwner == null) { Log.Trace("[FIX] ship.Owner (oldOwner) is null"); return false; }

    //            // 2. Scenario: Buying from a Town (Settlement -> MobileParty)
    //            if (changeDetail == ChangeShipOwnerAction.ShipOwnerChangeDetail.ApplyByTrade)
    //            {
    //                if (oldOwner.IsSettlement)
    //                {
    //                    // Check if the buyer (newOwner) is missing critical components
    //                    if (newOwner.MobileParty == null)
    //                    {
    //                        Log.Trace($"[FIX] Buyer '{newOwner.Name}' has no MobileParty attached.");
    //                        return false;
    //                    }

    //                    // The decompiled code checks ActualClan.Leader and LeaderHero
    //                    bool hasClanLeader = newOwner.MobileParty.ActualClan?.Leader != null;
    //                    bool hasLeaderHero = newOwner.MobileParty.LeaderHero != null;

    //                    if (!newOwner.MobileParty.IsCaravan && !newOwner.MobileParty.IsVillager && !hasClanLeader && !hasLeaderHero)
    //                    {
    //                        Log.Trace($"[FIX] Buyer '{newOwner.Name}' is not a caravan/villager AND has no Clan Leader or LeaderHero. This will crash ApplyInternal.");
    //                        return false;
    //                    }
    //                }
    //                // 3. Scenario: Selling to a Town (MobileParty -> Settlement)
    //                else
    //                {
    //                    if (oldOwner.MobileParty == null)
    //                    {
    //                        Log.Trace($"[FIX] Seller '{oldOwner.Name}' has no MobileParty attached.");
    //                        return false;
    //                    }

    //                    bool hasClanLeader = oldOwner.MobileParty.ActualClan?.Leader != null;
    //                    bool hasLeaderHero = oldOwner.LeaderHero != null;

    //                    if (!oldOwner.MobileParty.IsCaravan && !oldOwner.MobileParty.IsVillager && !hasClanLeader && !hasLeaderHero)
    //                    {
    //                        Log.Trace($"[FIX] Seller '{oldOwner.Name}' lacks leaders required for gold transfer. Blocking crash.");
    //                        return false;
    //                    }
    //                }
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Log.Trace($"[FIX] Error in Patch Logic: {ex.Message}");
    //        }

    //        return true; // Proceed if safe
    //    }
    //}

    //[HarmonyPatch(typeof(ShipTradeCampaignBehavior), "ConsiderPurchasingShip")]
    //static class BLT_BlockShipPurchase
    //{
    //    static bool Prefix(Clan clan)
    //    {
    //        if (clan == null || clan.Leader == null)
    //            return false;
    //
    //        if (clan.Name.ToString().StartsWith("[BLT Clan]") || clan.Leader.IsAdopted())
    //            Log.Trace($"[ShipTradeBlock] '{clan.Name.ToString()}' has been blocked from considering a ship purchase.");
    //        return false;
    //
    //        return true;
    //    }
    //}

    [HarmonyPatch(typeof(ShipTradeCampaignBehavior), "OnShipOwnerChanged")]
    static class BLT_Suppress_OnShipOwnerChanged_Exception
    {
        static Exception Finalizer(Exception __exception)
        {
            // If the method threw, swallow it completely
            if (__exception != null)
            {
                // Optional logging (disabled for now)
                // Log.Trace("[BLT] Suppressed exception in OnShipOwnerChanged:\n" + __exception);
                return null;
            }

            return null;
        }
    }

    #endregion

}