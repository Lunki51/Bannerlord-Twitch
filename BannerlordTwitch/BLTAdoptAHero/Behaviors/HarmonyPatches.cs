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
using BLTAdoptAHero;
using TaleWorlds.CampaignSystem.GameComponents;

namespace BLTAdoptAHero
{
    public static class AdoptedHeroFlags
    {
        public static bool _allowKingdomMove = false;
        public static bool _allowMarriage = false;
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
            if ((clan?.Leader != null && clan.Leader.IsAdopted()) || clan.Name.ToString().ToLower().Contains("vassal"))
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
            if ((clan?.Leader != null && clan.Leader.IsAdopted()) || clan.Name.ToString().ToLower().Contains("vassal"))
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

    #region KingdomActions
    [HarmonyPatch(typeof(ChangeKingdomAction))]
    internal static class ChangeKingdomActionPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("ApplyByJoinToKingdom")]
        private static bool Prefix_ApplyByJoinToKingdom(Clan clan)
        {
            if (((clan?.Leader != null && clan.Leader.IsAdopted()) || clan.Name.ToString().ToLower().Contains("vassal")) && !AdoptedHeroFlags._allowKingdomMove)
            {
                try
                {
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
        [HarmonyPatch("ApplyByJoinToKingdomByDefection")]
        private static bool Prefix_ApplyByJoinToKingdomByDefection(Clan clan)
        {
            if (((clan?.Leader != null && clan.Leader.IsAdopted()) || clan.Name.ToString().ToLower().Contains("vassal")) && !AdoptedHeroFlags._allowKingdomMove)
            {
                try
                {
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Error($"[BLT] Prefix_ApplyByJoinToKingdomByDefection error: {ex}");
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ApplyByLeaveKingdom")]
        private static bool Prefix_ApplyByLeaveKingdom(Clan clan)
        {
            if (((clan?.Leader != null && clan.Leader.IsAdopted()) || clan.Name.ToString().ToLower().Contains("vassal")) && !AdoptedHeroFlags._allowKingdomMove)
            {
                try
                {
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Error($"[BLT] Prefix_ApplyByLeaveKingdom error: {ex}");
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ApplyByLeaveWithRebellionAgainstKingdom")]
        private static bool Prefix_ApplyByLeaveWithRebellionAgainstKingdom(Clan clan)
        {
            if (((clan?.Leader != null && clan.Leader.IsAdopted()) || clan.Name.ToString().ToLower().Contains("vassal")) && !AdoptedHeroFlags._allowKingdomMove)
            {
                try
                {
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Error($"[BLT] Prefix_ApplyByLeaveWithRebellionAgainstKingdom error: {ex}");
                }
            }
            return true;
        }
    }


    //[HarmonyPatch(typeof(KingdomDiplomacyPatches))]
    //private static class KingdomDiplomacyPatches
    //{
    //    [HarmonyPrefix]
    //    [HarmonyPatch("")]
    //    private static 
    //}
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

    [HarmonyPatch(typeof(DefaultMarriageModel), nameof(DefaultMarriageModel.GetClanAfterMarriage))]
    class BLTMarriage
    {
        static void Postfix(ref Clan __result, Hero firstHero, Hero secondHero)
        {
            if (firstHero.Clan?.Leader == firstHero || secondHero.Clan?.Leader == secondHero)
                return;

            if (firstHero.IsAdopted() == true || secondHero.IsAdopted() == true)
                return;

            if (firstHero.Clan?.Leader.IsAdopted() == false && secondHero.Clan?.Leader.IsAdopted() == false)
                return;

            if (firstHero.Clan?.Leader.IsAdopted() == true && secondHero.Clan?.Leader.IsAdopted() == true)
                return;

            if (firstHero.Clan.Leader.IsAdopted())
            {
                __result = firstHero.Clan;
            }
            else { __result = secondHero.Clan; }

        }
    }
}
#endregion

#region OnShipOwnerChanged

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

    #region TownFoodStocks
    
    [HarmonyPatch(nameof(DefaultSettlementFoodModel), "FoodStocksUpperLimit")]
    [HarmonyPatch(MethodType.Getter)]
    internal static class FoodStocksUpperLimitUncap
    {
        [HarmonyPrefix]
        public static bool FoodStocksUpperLimitPrefix(ref int __result)
        {
            __result = BLTAdoptAHeroModule.CommonConfig.UncapFoodStocks ? 100000 : 300;
            return false; // Skip original method
        }
    }

    [HarmonyPatch(typeof(Village), "GetHearthLevel")]
    public class HearthExpansionPatch
    {
        [HarmonyPrefix]
        public static bool GetHearthLevelPrefix(Village __instance, ref int __result)
        {
            if (__instance.Hearth >= BLTAdoptAHeroModule.CommonConfig.HearthPerVillageTier)
            {
                __result = (int)(__instance.Hearth / BLTAdoptAHeroModule.CommonConfig.HearthPerVillageTier);
            }
            else
            {
                __result = 0;
            }

            // Return false to prevent the original method from running
            return false;
        }
    }

    #endregion

}