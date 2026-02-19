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
using static TaleWorlds.MountAndBlade.Launcher.Library.NativeMessageBox;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;

namespace BLTAdoptAHero
{
    public static class AdoptedHeroFlags
    {
        public static bool _allowKingdomMove = false;
        public static bool _allowDiplomacyAction = false;
        //public static bool _allowMarriage = false;
        public static bool _allowAIjoinBLT = GlobalCommonConfig.Get().AllowAIJoinBLT;
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
        private static bool Prefix_ApplyByJoinToKingdom(Clan clan, Kingdom newKingdom)
        {
            if (!AdoptedHeroFlags._allowKingdomMove)
            {
                if ((clan?.Leader != null && clan.Leader.IsAdopted()) || clan.Name.ToString().ToLower().Contains("vassal"))
                {
                    try
                    {
                        return false;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[BLT] Prefix_ApplyByJoinToKingdom(blt)error: {ex}");
                    }
                }
            }
            if (!AdoptedHeroFlags._allowAIjoinBLT)
            {
                if (clan?.Leader != null && !clan.Leader.IsAdopted() && clan.Leader != Hero.MainHero && newKingdom.Leader.IsAdopted() && !clan.Name.ToString().ToLower().Contains("vassal"))
                {
                    try
                    {
                        return false;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[BLT] Prefix_ApplyByJoinToKingdom(ai)error: {ex}");
                    }
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ApplyByJoinToKingdomByDefection")]
        private static bool Prefix_ApplyByJoinToKingdomByDefection(Clan clan, Kingdom newKingdom)
        {
            if (!AdoptedHeroFlags._allowKingdomMove)
            {
                if ((clan?.Leader != null && clan.Leader.IsAdopted()) || clan.Name.ToString().ToLower().Contains("vassal"))
                {
                    try
                    {
                        return false;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[BLT] Prefix_ApplyByJoinToKingdom(blt)error: {ex}");
                    }
                }
            }
            if (!AdoptedHeroFlags._allowAIjoinBLT)
            {
                if (clan?.Leader != null && !clan.Leader.IsAdopted() && clan.Leader != Hero.MainHero && newKingdom.Leader.IsAdopted() && !clan.Name.ToString().ToLower().Contains("vassal"))
                {
                    try
                    {
                        return false;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[BLT] Prefix_ApplyByJoinToKingdom(ai)error: {ex}");
                    }
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ApplyByLeaveKingdom")]
        private static bool Prefix_ApplyByLeaveKingdom(Clan clan)
        {
            if (!AdoptedHeroFlags._allowKingdomMove)
            {
                if ((clan?.Leader != null && clan.Leader.IsAdopted()) || clan.Name.ToString().ToLower().Contains("vassal"))
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
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ApplyByLeaveWithRebellionAgainstKingdom")]
        private static bool Prefix_ApplyByLeaveWithRebellionAgainstKingdom(Clan clan)
        {
            if (!AdoptedHeroFlags._allowKingdomMove)
            {
                if ((clan?.Leader != null && clan.Leader.IsAdopted()) || clan.Name.ToString().ToLower().Contains("vassal"))
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

    #region ClanKingdomDecisions
    // Block DeclareWarDecision for BLT kingdoms
    [HarmonyPatch(typeof(DeclareWarDecision), MethodType.Constructor, new Type[] { typeof(Clan), typeof(IFaction) })]
        internal static class DeclareWarDecisionConstructorPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Clan proposerClan)
            {
                if (proposerClan?.Kingdom?.Leader != null && proposerClan.Kingdom.Leader.IsAdopted() && Hero.MainHero?.Clan != proposerClan)
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
                if (proposerClan?.Kingdom?.Leader != null && proposerClan.Kingdom.Leader.IsAdopted() && Hero.MainHero?.Clan != proposerClan)
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
                        return false;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[BLT] Prefix_UpdateBannerColorsAccordingToKingdom error: {ex}");
                    }
                }
                return true;
            }
        }
    [HarmonyPatch(typeof(DefaultMarriageModel), nameof(DefaultMarriageModel.GetClanAfterMarriage))]
        internal class BLTMarriage
        {
            static void Postfix(DefaultMarriageModel __instance, ref Clan __result, Hero firstHero, Hero secondHero)
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
#if DEBUG
                Log.Trace($"[BLT] Changed marriage clan for {firstHero.FirstName}/{secondHero.FirstName} to {__result.Name}");
#endif
            }
        }
    [HarmonyPatch(typeof(KillCharacterAction), nameof(KillCharacterAction.ApplyInLabor))]
        internal class BLTNoPregnancyDeath_Action
        {
            static bool Prefix(Hero lostMother, bool showNotification)
            {
                if (lostMother.IsAdopted())
                {
#if DEBUG
                    Log.Trace($"[BLT] Prevented childbirth death for {lostMother?.Name}");
#endif
                    return false;
                }
            return true;
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

    #region DiplomacyPatches
    /// <summary>
    /// Harmony patches to prevent peace in certain conditions
    /// </summary>
    [HarmonyPatch]
    public class BLTDiplomacyPatches
    {
        /// <summary>
        /// Patch MakePeaceAction.Apply to prevent peace when minimum war duration not met
        /// or when AI tries to peace with BLT kingdoms
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MakePeaceAction), "ApplyInternal")]
        public static bool Prefix_MakePeaceAction_Apply(
            IFaction faction1,
            IFaction faction2, 
            int dailyTributeFrom1To2, 
            int dailyTributeDuration, 
            MakePeaceAction.MakePeaceDetail detail = MakePeaceAction.MakePeaceDetail.Default)
        {
            // Allow BLT-controlled peace actions
            if (AdoptedHeroFlags._allowDiplomacyAction)
                return true;

            // Only handle kingdoms
            var k1 = faction1 as Kingdom;
            var k2 = faction2 as Kingdom;
            if (k1 == null || k2 == null)
                return true;

            // Check if BLT treaty system is active
            if (BLTTreatyManager.Current == null)
                return true;

            // Check if either kingdom is BLT-controlled
            bool k1IsBLT = k1.Leader != null && k1.Leader.IsAdopted() && k1 != Hero.MainHero?.Clan?.Kingdom;
            bool k2IsBLT = k2.Leader != null && k2.Leader.IsAdopted() && k2 != Hero.MainHero?.Clan?.Kingdom;

            if (!k1IsBLT && !k2IsBLT)
                return true; // No BLT kingdoms, allow peace

            // Check minimum war duration
            if (!BLTTreatyManager.Current.CanMakePeace(k1, k2, out string reason))
            {
#if DEBUG
                    Log.Trace($"[BLT-Harmony] Blocked peace (min duration): {k1.Name} <-> {k2.Name} - {reason}");
#endif
                // Block the peace entirely
                return false;
            }

            // If AI is trying to make peace with BLT kingdom, block it
            // (we'll handle it via OnMakePeace event to create proposal)
            if (k1IsBLT || k2IsBLT)
            {
#if DEBUG
                    Log.Trace($"[BLT-Harmony] Blocked AI->BLT peace: {k1.Name} <-> {k2.Name} (will create proposal)");
#endif
                CampaignEventDispatcher.Instance.OnMakePeace(faction1, faction2, detail);
                // Block the peace - our event handler will create a proposal instead
                return false;
            }

            // Allow all other peace
            return true;
        }
    }
    #endregion

    #region ArmyDispersionAndCohesionPatches

    [HarmonyPatch(typeof(Army), "CheckArmyDispersion")]
    internal static class BLT_ArmyDispersionPatch
    {
        private static readonly Dictionary<Army, CampaignTime> ArmyCreationTimes = new();

        static bool Prefix(Army __instance)
        {
            try
            {
                if (__instance?.LeaderParty?.LeaderHero == null)
                    return true;

                // Mercenary armies: MercenaryArmyPatches owns those — skip here
                // (MercenaryArmyPatches.Prefix_CheckArmyDispersion already blocks them)
                //if (MercenaryArmyPatches.IsMercenaryArmy(__instance))
                //    return true;

                if (__instance.LeaderParty == MobileParty.MainParty)
                    return true;

                // Only process armies led by adopted heroes
                if (!__instance.LeaderParty.LeaderHero.IsAdopted())
                    return true;

                // Quick cleanup of stale tracking entry
                if (__instance.LeaderParty?.Army != __instance)
                {
                    ArmyCreationTimes.Remove(__instance);
                    return true;
                }

                // Track creation time
                if (!ArmyCreationTimes.ContainsKey(__instance))
                    ArmyCreationTimes[__instance] = CampaignTime.Now;

                float daysAlive =
                    (float)(CampaignTime.Now.ToDays - ArmyCreationTimes[__instance].ToDays);

                // If no active wars with real factions, allow normal disbanding
                var kingdom = __instance.LeaderParty.MapFaction as Kingdom;
                if (kingdom == null
                    || !kingdom.FactionsAtWarWith.Any(f =>
                        f.IsKingdomFaction || (f.IsClan && f.Fiefs.Any())))
                {
                    ArmyCreationTimes.Remove(__instance);
                    return true;
                }

                // Still within minimum lifetime — block dispersion
                if (daysAlive < BLTAdoptAHeroModule.CommonConfig.BLTArmyMinLifetimeDays)
                {
#if DEBUG
                Log.Trace($"[BLT] Blocked dispersion (age {daysAlive:F1}d) for {__instance.LeaderParty.LeaderHero.Name}'s army");
#endif
                    return false;
                }

                // Beyond minimum lifetime but LockBLTArmyCohesion enabled:
                // block dispersion that would have been caused by cohesion only
                // (peace/no-war path already returned above; this blocks the
                //  CohesionDepleted path while leaving LeaderDead etc. through)
                if (BLTAdoptAHeroModule.CommonConfig.LockBLTArmyCohesion
                    && __instance.Cohesion >= 100f)
                {
                    return false; // cohesion can't actually be the problem; skip
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] BLT_ArmyDispersionPatch error: {ex}");
                return true;
            }
        }
    }

    /// <summary>
    /// Clamps cohesion to 100 for player BLT armies when LockPlayerArmyCohesion is on.
    /// Mercenary army cohesion is handled separately in MercenaryArmyPatches.
    /// </summary>
    //[HarmonyPatch(typeof(Army), nameof(Army.Cohesion), MethodType.Setter)]
    //internal static class BLT_ArmyCohesionSetterPatch
    //{
    //    static void Postfix(Army __instance)
    //    {
    //        try
    //        {
    //            // Mercenary armies handled in MercenaryArmyPatches — skip
    //            //if (MercenaryArmyPatches.IsMercenaryArmy(__instance)) return;
    //            if (__instance.LeaderParty == MobileParty.MainParty) return;
    //
    //            if (!BLTAdoptAHeroModule.CommonConfig.LockPlayerArmyCohesion) return;
    //
    //            if (__instance.LeaderParty?.LeaderHero?.IsAdopted() == true
    //                && __instance.Cohesion < 100f)
    //            {
    //                __instance.Cohesion = 100f;
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Log.Error($"[BLT] BLT_ArmyCohesionSetterPatch error: {ex}");
    //        }
    //    }
    //}
    #endregion

    #region MilitiaSallyOut
    [HarmonyPatch(typeof(Town), "GetDefenderParties")]
    class Town_GetDefenderParties_Patch
    {
        static bool Prefix(Town __instance, MapEvent.BattleTypes battleType, ref IEnumerable<PartyBase> __result)
        {
            __result = GetDefenderPartiesWithMilitia(__instance, battleType);
            return false; // Skip original method
        }

        static IEnumerable<PartyBase> GetDefenderPartiesWithMilitia(Town town, MapEvent.BattleTypes battleType)
        {
            yield return town.Settlement.Party;

            foreach (MobileParty mobileParty in town.Settlement.Parties)
            {
                if (mobileParty.MapFaction.IsAtWarWith(town.Settlement.SiegeEvent.BesiegerCamp.MapFaction)
                    && mobileParty.IsActive
                    && !mobileParty.IsVillager
                    && !mobileParty.IsCaravan
                    && (!mobileParty.IsMilitia || !town.InRebelliousState)) // FIXED: Militia now included in SallyOut
                {
                    yield return mobileParty.Party;
                }
            }
        }
    }
    #endregion

    #region SiegeRetreatFix

    /// <summary>
    /// Fixes the vanilla bug where retreating from a siege assault causes the ENTIRE
    /// besieging army (including all attached parties still in camp) to be captured/killed.
    ///
    /// Root cause: After a lost siege battle mission, BattleState = DefenderVictory but
    /// RetreatingSide = None. LootDefeatedPartyMembers checks RetreatingSide == None
    /// before capturing troops, so every party on the attacker side gets processed —
    /// including thousands of troops that never entered the breach.
    ///
    /// Fix: Before results are committed, if the defeated side still has healthy troops,
    /// set RetreatingSide to the defeated side. LootDefeatedPartyMembers will then skip
    /// troop capture entirely (items/gold looting still proceeds normally).
    /// </summary>
    [HarmonyPatch(typeof(MapEvent), "CalculateAndCommitMapEventResults")]
    internal static class BLT_SiegeRetreatFix
    {
        // Cache the reflected PropertyInfo once
        private static readonly PropertyInfo RetreatingSideProp =
            typeof(MapEvent).GetProperty("RetreatingSide",
                BindingFlags.Public | BindingFlags.Instance);

        static void Prefix(MapEvent __instance)
        {
            try
            {
                // Only relevant for siege assaults and sally outs
                if (!__instance.IsSiegeAssault && !__instance.IsSallyOut)
                    return;

                // Must have a decided winner
                if (!__instance.HasWinner)
                    return;

                // Already flagged as a retreat — vanilla handles it correctly
                if (__instance.RetreatingSide != BattleSideEnum.None)
                    return;

                // Get the defeated side and count survivors
                var defeatedSide = __instance.GetMapEventSide(__instance.DefeatedSide);
                if (defeatedSide == null)
                    return;

                // Count all healthy troops remaining on the losing side.
                // If any survived, the army retreated — they shouldn't be captured.
                int survivors = defeatedSide.GetTotalHealthyTroopCountOfSide()
                              + defeatedSide.GetTotalHealthyHeroCountOfSide();

                if (survivors <= 0)
                    return; // Truly wiped out — vanilla behavior is correct

                // Set RetreatingSide so LootDefeatedPartyMembers skips troop capture.
                // Items and gold looting still proceed (appropriate for a retreat).
                RetreatingSideProp?.SetValue(__instance, __instance.DefeatedSide);

#if DEBUG
                Log.Trace($"[BLT] SiegeRetreatFix: {survivors} survivors on " +
                          $"{__instance.DefeatedSide} side — marked as retreat, " +
                          $"troop capture suppressed.");
#endif
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] BLT_SiegeRetreatFix error: {ex}");
            }
        }
    }

    #endregion
}