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
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;

namespace BLTAdoptAHero
{
    public static class AdoptedHeroFlags
    {
        public static bool _allowKingdomMove = false;
        public static bool _allowDiplomacyAction = false;
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

    #region KingdomDecisionPatches

    [HarmonyPatch(typeof(KingdomDecision))]
    internal static class KingdomDecisionPatches
    {
        // Block MakePeaceKingdomDecision for BLT kingdoms
        [HarmonyPatch(typeof(MakePeaceKingdomDecision), MethodType.Constructor, new Type[] { typeof(Clan), typeof(IFaction), typeof(bool) })]
        internal static class MakePeaceKingdomDecisionConstructorPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Clan proposerClan)
            {
                if (proposerClan?.Kingdom?.Leader != null && proposerClan.Kingdom.Leader.IsAdopted())
                {
#if DEBUG
                    Log.Trace($"[BLT] Blocked MakePeaceKingdomDecision for BLT kingdom: {proposerClan.Kingdom.Name}");
#endif
                    return false; // Block decision creation
                }
                return true;
            }
        }

        // Block DeclareWarDecision for BLT kingdoms
        [HarmonyPatch(typeof(DeclareWarDecision), MethodType.Constructor, new Type[] { typeof(Clan), typeof(IFaction), typeof(bool) })]
        internal static class DeclareWarDecisionConstructorPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Clan proposerClan)
            {
                if (proposerClan?.Kingdom?.Leader != null && proposerClan.Kingdom.Leader.IsAdopted())
                {
#if DEBUG
                    Log.Trace($"[BLT] Blocked DeclareWarDecision for BLT kingdom: {proposerClan.Kingdom.Name}");
#endif
                    return false; // Block decision creation
                }
                return true;
            }
        }

        //        // Block KingdomPolicyDecision for BLT kingdoms (optional - you might want to keep this)
        //        [HarmonyPatch(typeof(KingdomPolicyDecision), MethodType.Constructor, new Type[] { typeof(Clan), typeof(PolicyObject), typeof(bool) })]
        //        internal static class KingdomPolicyDecisionConstructorPatch
        //        {
        //            [HarmonyPrefix]
        //            private static bool Prefix(Clan proposerClan)
        //            {
        //                if (proposerClan?.Kingdom?.Leader != null && proposerClan.Kingdom.Leader.IsAdopted())
        //                {
        //#if DEBUG
        //                Log.Trace($"[BLT] Blocked KingdomPolicyDecision for BLT kingdom: {proposerClan.Kingdom.Name}");
        //#endif
        //                    return false; // Block decision creation
        //                }
        //                return true;
        //            }
        //        }

        // Block ExpelClanFromKingdomDecision for BLT kingdoms
        [HarmonyPatch(typeof(ExpelClanFromKingdomDecision), MethodType.Constructor, new Type[] { typeof(Clan), typeof(Clan) })]
        internal static class ExpelClanFromKingdomDecisionConstructorPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Clan proposerClan)
            {
                if (proposerClan?.Kingdom?.Leader != null && proposerClan.Kingdom.Leader.IsAdopted())
                {
#if DEBUG
                    Log.Trace($"[BLT] Blocked ExpelClanFromKingdomDecision for BLT kingdom: {proposerClan.Kingdom.Name}");
#endif
                    return false; // Block decision creation
                }
                return true;
            }
        }

        //        // Block SettlementClaimantDecision for BLT kingdoms (fief distribution)
        //        [HarmonyPatch(typeof(SettlementClaimantDecision), MethodType.Constructor, new Type[] { typeof(Clan), typeof(Settlement) })]
        //        internal static class SettlementClaimantDecisionConstructorPatch
        //        {
        //            [HarmonyPrefix]
        //            private static bool Prefix(Clan proposerClan)
        //            {
        //                if (proposerClan?.Kingdom?.Leader != null && proposerClan.Kingdom.Leader.IsAdopted())
        //                {
        //#if DEBUG
        //                Log.Trace($"[BLT] Blocked SettlementClaimantDecision for BLT kingdom: {proposerClan.Kingdom.Name}");
        //#endif
        //                    return false; // Block decision creation
        //                }
        //                return true;
        //            }
        //        }

        //        // Block AnnexationDecision for BLT kingdoms
        //        [HarmonyPatch(typeof(KingdomDecision), "DetermineChooser")]
        //        internal static class DetermineChooserPatch
        //        {
        //            [HarmonyPrefix]
        //            private static bool Prefix(KingdomDecision __instance, ref Clan __result)
        //            {
        //                if (__instance?.Kingdom?.Leader != null && __instance.Kingdom.Leader.IsAdopted())
        //                {
        //                    // For BLT kingdoms, always return null to prevent AI from choosing
        //                    __result = null;
        //#if DEBUG
        //                Log.Trace($"[BLT] Blocked DetermineChooser for BLT kingdom: {__instance.Kingdom.Name}");
        //#endif
        //                    return false;
        //                }
        //                return true;
        //            }
        //        }
        //    }

        #endregion

        #region DiplomacyProposalPatches

        //    // Additional safety - block at the proposal level
        //    [HarmonyPatch(typeof(KingdomDiplomacyVM))]
        //    internal static class KingdomDiplomacyVMPatches
        //    {
        //        // This blocks the UI from even showing diplomacy options for BLT kingdoms
        //        [HarmonyPatch("CanProposeAction")]
        //        [HarmonyPrefix]
        //        private static bool Prefix_CanProposeAction(ref bool __result, Kingdom ____playerKingdom)
        //        {
        //            if (____playerKingdom?.Leader != null && ____playerKingdom.Leader.IsAdopted())
        //            {
        //                __result = false;
        // #if DEBUG
        //            Log.Trace($"[BLT] Blocked CanProposeAction in KingdomDiplomacyVM for BLT kingdom");
        //#endif
        //                return false;
        //            }
        //            return true;
        //        }
        //    }

        #endregion

        #region KingdomDecisionProposalBehaviorPatches

        // Block the behavior that creates kingdom decisions
        [HarmonyPatch(typeof(KingdomDecisionProposalBehavior))]
        internal static class KingdomDecisionProposalBehaviorPatches
        {
            [HarmonyPrefix]
            [HarmonyPatch("ConsiderWar")]
            private static bool Prefix_ConsiderWar(Clan clan)
            {
                if (clan?.Kingdom?.Leader != null && clan.Kingdom.Leader.IsAdopted())
                {
#if DEBUG
                    Log.Trace($"[BLT] Blocked ConsiderWar for BLT kingdom: {clan.Kingdom.Name}");
#endif
                    return false;
                }
                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch("ConsiderPeace")]
            private static bool Prefix_ConsiderPeace(Clan clan)
            {
                if (clan?.Kingdom?.Leader != null && clan.Kingdom.Leader.IsAdopted())
                {
#if DEBUG
                    Log.Trace($"[BLT] Blocked ConsiderPeace for BLT kingdom: {clan.Kingdom.Name}");
#endif
                    return false;
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
        [HarmonyPatch(typeof(DefaultMarriageModel), nameof(DefaultMarriageModel.GetClanAfterMarriage))]
        internal class BLTMarriage
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
}
