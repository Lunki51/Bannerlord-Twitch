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
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using NavalDLC.CampaignBehaviors;
using NavalDLC.CharacterDevelopment;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using static TaleWorlds.MountAndBlade.Launcher.Library.NativeMessageBox;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Siege;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using System.Runtime.CompilerServices;
using BLTAdoptAHero.Behaviors;

namespace BLTAdoptAHero
{
    public static class AdoptedHeroFlags
    {
        public static bool _allowKingdomMove = false;
        public static bool _allowDiplomacyAction = false;
        public static bool _allowBLTArmyCreation = false;
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
    internal class BLTAfterMarriage
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

    [HarmonyPatch(typeof(DefaultMarriageModel), nameof(DefaultMarriageModel.IsSuitableForMarriage))]
    internal class BLTMarriageBlock
    {
        static void Postfix(ref bool __result, Hero maidenOrSuitor)
        {
            if (maidenOrSuitor == null) return;
            if (maidenOrSuitor.IsAdopted())
            {
                __result = false;
#if DEBUG
                Log.Trace($"[BLT] Overwrote marriage for adopted hero");
#endif
                return;
            }

            var heirs = Campaign.Current.GetCampaignBehavior<BLTHeirBehavior>()?._heirs;
            if (heirs != null && heirs.Contains(maidenOrSuitor))
            {
#if DEBUG
                Log.Trace($"[BLT] Overwrote marriage for heir");
#endif
                __result = false;
            }
        }
    }

    #endregion

    #region DEATH

    [HarmonyPatch(typeof(KillCharacterAction), "ApplyInternal")]
    internal class BLTNoDeathAllowed
    {
        static bool Prefix(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail actionDetail, bool showNotification, bool isForced = false)
        {           
            if (isForced) return true;
            if (!victim.IsAdopted()) return true;
            if (killer == Hero.MainHero && actionDetail == KillCharacterAction.KillCharacterActionDetail.Executed) return true;
            var config = GlobalCommonConfig.Get();
            if (!config.AllowDeath) return false;
            if (victim.Age > config.MinimumAge) return true;

            return false;
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
                __result = BLTAdoptAHeroModule.CommonConfig.UncapFoodStocks ? 10000 : 300;
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

    [HarmonyPatch]
    public class BLTDiplomacyPatches
    {
        /// <summary>
        /// Intercepts MakePeaceAction.ApplyInternal BEFORE any siege/stance teardown occurs.
        /// For BLT-involved kingdoms we either block outright (min duration) or block and
        /// route the attempt into a visible proposal (AI→BLT). No war re-declaration is ever
        /// needed because peace never actually takes effect.
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
            // Always allow peace that BLT itself initiated
            if (AdoptedHeroFlags._allowDiplomacyAction)
                return true;

            var k1 = faction1 as Kingdom;
            var k2 = faction2 as Kingdom;
            if (k1 == null || k2 == null)
                return true;

            if (BLTTreatyManager.Current == null)
                return true;

            bool k1IsBLT = k1.Leader != null && k1.Leader.IsAdopted() && k1 != Hero.MainHero?.Clan?.Kingdom;
            bool k2IsBLT = k2.Leader != null && k2.Leader.IsAdopted() && k2 != Hero.MainHero?.Clan?.Kingdom;

            if (!k1IsBLT && !k2IsBLT)
                return true; // pure AI-vs-AI: let it through

            // ── Case 1: minimum war duration not yet met ──────────────────────────
            if (!BLTTreatyManager.Current.CanMakePeace(k1, k2, out string reason))
            {
#if DEBUG
            Log.Trace($"[BLT-Harmony] Blocked peace (min duration): {k1.Name} <-> {k2.Name} - {reason}");
#endif
                if (k1IsBLT && k1.Leader != null)
                {
                    string n = k1.Leader.FirstName.ToString()
                        .Replace(BLTAdoptAHeroModule.Tag, "").Replace(BLTAdoptAHeroModule.DevTag, "").Trim();
                    Log.LogFeedResponse($"@{n} Peace with {k2.Name} rejected - {reason}");
                }
                if (k2IsBLT && k2.Leader != null)
                {
                    string n = k2.Leader.FirstName.ToString()
                        .Replace(BLTAdoptAHeroModule.Tag, "").Replace(BLTAdoptAHeroModule.DevTag, "").Trim();
                    Log.LogFeedResponse($"@{n} Peace with {k1.Name} rejected - {reason}");
                }
                Log.ShowInformation($"Peace rejected - {reason}", k1.Leader?.CharacterObject);
                return false; // blocked — no re-declare needed, war never ended
            }

            // ── Case 2: AI trying to make peace with a BLT kingdom ───────────────
            if (k1IsBLT != k2IsBLT)
            {
                Kingdom aiKingdom = k1IsBLT ? k2 : k1;
                Kingdom bltKingdom = k1IsBLT ? k1 : k2;
#if DEBUG
            Log.Trace($"[BLT-Harmony] Blocked AI->BLT peace: {aiKingdom.Name} -> {bltKingdom.Name}. Creating proposal.");
#endif
                // Delegate proposal creation to the behavior (handles dedup + notifications)
                BLTDiplomacyBehavior.Current?.HandleAIPeaceAttempt(aiKingdom, bltKingdom);
                return false; // blocked — siege state untouched
            }

            // ── Case 3: Both BLT without _allowDiplomacyAction (shouldn't happen) ─
#if DEBUG
        Log.Trace($"[BLT-Harmony] Blocked unsanctioned BLT-BLT peace: {k1.Name} <-> {k2.Name}");
#endif
            return false;
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
    /// besieging army to be captured/killed, and lords made fugitive respawning with 1 troop.
    /// This version safely tracks mutated MapEvent instances instead of using ThreadStatic.
    /// </summary>

    [HarmonyPatch(typeof(MapEvent), "CalculateAndCommitMapEventResults")]
    internal static class BLT_SiegeRetreatFix
    {
        private static readonly PropertyInfo RetreatingSideProp =
            typeof(MapEvent).GetProperty("RetreatingSide",
                BindingFlags.Public | BindingFlags.Instance);

        // Tracks MapEvent instances we mutate so we can safely restore them.
        private static readonly HashSet<MapEvent> _mutated = new();

        private static bool IsSiegeRelated(MapEvent e) =>
            e.IsSiegeAssault || e.IsSallyOut || e.IsSiegeOutside;

        static void Prefix(MapEvent __instance)
        {
            try
            {
                if (!IsSiegeRelated(__instance))
                    return;

                if (!__instance.HasWinner)
                    return;

                if (__instance.RetreatingSide != BattleSideEnum.None)
                    return;

                var defeatedSide = __instance.GetMapEventSide(__instance.DefeatedSide);
                if (defeatedSide == null)
                    return;

                int survivors = defeatedSide.GetTotalHealthyTroopCountOfSide();
                if (survivors <= 0)
                    return; // Truly wiped out — allow vanilla full capture

                RetreatingSideProp?.SetValue(__instance, __instance.DefeatedSide);
                _mutated.Add(__instance);

#if DEBUG
            Log.Trace($"[BLT] SiegeRetreatFix: {survivors} survivors on " +
                      $"{__instance.DefeatedSide} side — temporarily suppressing troop capture.");
#endif
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] BLT_SiegeRetreatFix Prefix error: {ex}");
            }
        }

        static void Postfix(MapEvent __instance)
        {
            try
            {
                if (!_mutated.Remove(__instance))
                    return;

                // Restore original state so later systems see correct battle result
                RetreatingSideProp?.SetValue(__instance, BattleSideEnum.None);

#if DEBUG
            Log.Trace("[BLT] SiegeRetreatFix: RetreatingSide restored to None.");
#endif
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] BLT_SiegeRetreatFix Postfix error: {ex}");
            }
        }
    }


    // -------------------------------------------------------------------------
    // Part 2: Safety net — prevent lords from being made fugitive if they belong
    //         to a party actively part of a siege besieger camp.
    // -------------------------------------------------------------------------

    [HarmonyPatch(typeof(MakeHeroFugitiveAction), nameof(MakeHeroFugitiveAction.Apply))]
    internal static class BLT_SiegeLordFugitiveFix
    {
        static bool Prefix(Hero fugitive, bool showNotification)
        {
            try
            {
                MobileParty party = fugitive.PartyBelongedTo;
                if (party == null)
                    return true;

                var mapEvent = party.MapEvent ?? party.Army?.LeaderParty?.MapEvent;

                // Ignore naval blockades
                if (mapEvent != null && (mapEvent.IsBlockade || mapEvent.IsBlockadeSallyOut))
                    return true;

                bool isInSiegingParty =
                    party.BesiegedSettlement != null ||
                    (party.Army != null &&
                     party.Army.LeaderParty?.BesiegedSettlement != null);

                if (!isInSiegingParty)
                    return true;

                // Only suppress if the party still has healthy troops
                if (party.Party?.NumberOfHealthyMembers <= 0)
                    return true;

#if DEBUG
            Log.Trace($"[BLT] SiegeLordFugitiveFix: Blocked MakeHeroFugitive for " +
                      $"{fugitive.Name} (besieging " +
                      $"{party.BesiegedSettlement?.Name ?? party.Army?.LeaderParty?.BesiegedSettlement?.Name})");
#endif

                return false; // Prevent fugitive conversion
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] BLT_SiegeLordFugitiveFix error: {ex}");
                return true;
            }
        }
    }

    #endregion

    #region BlockArmies
    [HarmonyPatch(typeof(Kingdom), "CreateArmy")]
    internal static class BLT_BlockAIArmyCreation
    {
        [HarmonyPrefix]
        private static bool Prefix(Kingdom __instance, Hero armyLeader)
        {
            try
            {
                // Always allow the player
                if (armyLeader == Hero.MainHero)
                    return true;

                var pb = PartyOrderBehavior.Current;

                if (armyLeader?.IsAdopted() == true)
                {
                    // Block all BLT army creation unless a BLT command explicitly allowed it
                    if (!AdoptedHeroFlags._allowBLTArmyCreation)
                    {
#if DEBUG
                    Log.Trace($"[BLT] Blocked unsanctioned BLT army creation by {armyLeader?.Name} in {__instance?.Name}");
#endif
                        return false;
                    }
                    // Sanctioned creation: still respect the per-kingdom block flag
                    return pb == null || !pb.IsBLTArmiesBlocked(__instance);
                }

                // AI hero
                if (pb == null) return true;
                return !pb.IsAIArmiesBlocked(__instance);
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] BLT_BlockAIArmyCreation Prefix error: {ex}");
                return true; // fail-safe
            }
        }
    }
    #endregion

    #region ClanArmyPatches

    /// <summary>
    /// Patches Army.FindBestGatheringSettlementAndMoveTheLeader for clan armies
    /// (Army.Kingdom == null).  The vanilla method unconditionally iterates
    /// this.Kingdom.Settlements, which would throw a NullReferenceException.
    ///
    /// When Kingdom is non-null the prefix returns true and vanilla runs unchanged.
    /// When Kingdom is null (clan army) the prefix:
    ///   • Calls BLTClanArmyBehavior.FindClanGatherSettlement to pick a friendly settlement.
    ///   • Sets AiBehaviorObject to that settlement.
    ///   • Moves the leader party toward it (replicating SendLeaderPartyToReachablePointAroundPosition).
    ///   • Returns false to skip the vanilla body entirely.
    /// </summary>
    [HarmonyPatch(typeof(Army), "FindBestGatheringSettlementAndMoveTheLeader")]
    internal static class BLT_ClanArmyFindGatheringPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Army __instance, Settlement focusSettlement)
        {
            try
            {
                if (__instance.Kingdom != null) return true;

                var gather = BLTClanArmyBehavior.FindClanGatherSettlement(__instance)
                             ?? focusSettlement;

                if (gather == null)
                {
                    __instance.LeaderParty.SetMoveModeHold();
                    return false;
                }

                __instance.AiBehaviorObject = gather;

                __instance.LeaderParty.SetMoveGoToPoint(
                    NavigationHelper.FindReachablePointAroundPosition(
                        gather.GatePosition,
                        MobileParty.NavigationType.Default,
                        __instance.GatheringPositionMaxDistanceToTheSettlement,
                        __instance.GatheringPositionMinDistanceToTheSettlement,
                        false),
                    __instance.LeaderParty.NavigationCapability);

                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] BLT_ClanArmyFindGatheringPatch error: {ex}");
                return true;
            }
        }
    }

    #endregion

    #region ClanSiegePatches

    [HarmonyPatch(typeof(AiPartyThinkBehavior), "PartyHourlyAiTick")]
    internal static class BLT_PartyHourlyAiTickPatch
    {
        [HarmonyPrefix]
        static bool Prefix(MobileParty mobileParty)
        {
            try
            {
                // If this party has an active siege order, skip the vanilla AI tick entirely
                // — our PartyOrderBehavior.OnHourlyTickParty handles it instead
                var order = PartyOrderBehavior.Current?.GetActiveOrder(mobileParty.StringId);
                if (order?.Type != PartyOrderType.Siege) return true;
                if (mobileParty.MapFaction.IsKingdomFaction) return true;
                if (mobileParty.MapFaction == Clan.PlayerClan.MapFaction) return true;

                //Log.Trace("AiPartyTick");
                return false; // Skip vanilla tick — prevent Hold/disband interference
            }
            catch (Exception ex)
            {
                Log.Error($"[BLT] BLT_PartyHourlyAiTickPatch error: {ex}");
                return true;
            }
        }
    }

    #endregion
}