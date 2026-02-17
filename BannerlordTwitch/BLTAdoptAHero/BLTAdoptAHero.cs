using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using BLTAdoptAHero.UI;
using BLTAdoptAHero;
using HarmonyLib;
using JetBrains.Annotations;
using SandBox.GauntletUI.Missions;
using SandBox.Tournaments.MissionLogics;
using SandBox.View;
using SandBox.View.Missions.NameMarkers;
using SandBox.ViewModelCollection.Missions.NameMarker;
using SandBox.ViewModelCollection.Missions.NameMarker.Targets;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Mission.NameMarker;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using static TaleWorlds.MountAndBlade.Launcher.Library.NativeMessageBox;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using BLTAdoptAHero.Models;
using BLTAdoptAHero.Actions;
using BLTAdoptAHero.Behaviors;

#pragma warning disable 649

namespace BLTAdoptAHero
{
    public static class TwitchDevUsers
    {
        public static readonly HashSet<string> Developers = new HashSet<string>
        {
            "randomchair22",
            "kanboru201"
        };
    }


    [UsedImplicitly]
    [HarmonyPatch]
    public class BLTAdoptAHeroModule : MBSubModuleBase
    {
        private Harmony harmony;

        internal static GlobalCommonConfig CommonConfig { get; private set; }
        internal static GlobalTournamentConfig TournamentConfig { get; private set; }
        internal static GlobalHeroClassConfig HeroClassConfig { get; private set; }
        internal static GlobalHeroPowerConfig HeroPowerConfig { get; private set; }

        public BLTAdoptAHeroModule()
        {
            ActionManager.RegisterAll(typeof(BLTAdoptAHeroModule).Assembly);

            GlobalCommonConfig.Register();
            GlobalTournamentConfig.Register();
            GlobalHeroClassConfig.Register();
            GlobalHeroPowerConfig.Register();

            TournamentHub.Register();
            MissionInfoHub.Register();
            MapHub.Register();
            
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            try
            {
                mission.AddMissionBehavior(new BLTAdoptAHeroCommonMissionBehavior());
                mission.AddMissionBehavior(new BLTAdoptAHeroCustomMissionBehavior());
                mission.AddMissionBehavior(new BLTSummonBehavior());
                mission.AddMissionBehavior(new BLTRemoveAgentsBehavior());
                mission.AddMissionBehavior(new BLTHeroPowersMissionBehavior());
                //if (mission.CombatType == Mission.MissionCombatType.Combat && mission.PlayerTeam != null && mission.HasMissionBehavior<BLTAdoptAHeroCommonMissionBehavior>())
                //{
                //    mission.AddMissionBehavior(new HeroWidgetMissionView());
                //}
            }
            catch (Exception e)
            {
                Log.Exception(nameof(OnMissionBehaviorInitialize), e);
            }
        }


        [UsedImplicitly, HarmonyPostfix, HarmonyPatch(typeof(MissionScreen), "TaleWorlds.MountAndBlade.IMissionSystemHandler.OnMissionAfterStarting")]
        static void OnMissionAfterStartingPostFix(MissionScreen __instance)
        {
            if (__instance.Mission.GetMissionBehavior<MissionNameMarkerUIHandler>() == null
            && (__instance.Mission.GetMissionBehavior<BattleSpawnLogic>() != null
                || __instance.Mission.GetMissionBehavior<TournamentFightMissionController>() != null))
            {
                __instance.AddMissionView(SandBoxViewCreator.CreateMissionNameMarkerUIHandler(__instance.Mission));
            }
        }

        [HarmonyPatch(typeof(MissionAgentMarkerTargetVM))]
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new[] { typeof(Agent) })]
        public static class MissionAgentMarkerTargetVM_Ctor_Patch
        {
            static void Postfix(MissionAgentMarkerTargetVM __instance, Agent target)
            {
                if (!(MissionHelpers.InSiegeMission() ||
                      MissionHelpers.InFieldBattleMission() /*||
                      MissionHelpers.InHideOutMission()*/))
                    return;

                bool isEnemy =
                    (Agent.Main != null && target.IsEnemyOf(Agent.Main)) ||
                    (Mission.Current.PlayerTeam?.IsValid == true && target.Team.IsEnemyOf(Mission.Current.PlayerTeam));

                bool isFriendly =
                    (Agent.Main != null && target.IsFriendOf(Agent.Main)) ||
                    (Mission.Current.PlayerTeam?.IsValid == true && target.Team.IsFriendOf(Mission.Current.PlayerTeam));

                if (isEnemy)
                {
                    __instance.NameType = "Enemy";
                    if (TwitchDevUsers.Developers.Contains(__instance.Name))
                    {
                        __instance.Name = __instance.Name.Replace(" [Dev]", "");
                    }
                    else
                    {
                        __instance.Name = __instance.Name.Replace(" [BLT]", "");
                    }
                    __instance.IsFriendly = false;
                    __instance.IsEnemy = true;
                    __instance.IsTracked = true;
                }
                else if (isFriendly)
                {
                    __instance.NameType = "Friendly";
                    if (TwitchDevUsers.Developers.Contains(__instance.Name))
                    {
                        __instance.Name = __instance.Name.Replace(" [Dev]", "");
                    }
                    else
                    {
                        __instance.Name = __instance.Name.Replace(" [BLT]", "");
                    }
                    __instance.IsFriendly = true;
                    __instance.IsEnemy = false;
                    __instance.IsTracked = true;
                }
            }
        }


        [UsedImplicitly, HarmonyPrefix, HarmonyPatch(typeof(MissionGauntletNameMarkerView), "OnConversationEnd")]
        public static bool OnConversationEndPrefix(MissionGauntletNameMarkerView __instance)
        {
            return __instance.Mission != null;
        }

        [UsedImplicitly, HarmonyPostfix, HarmonyPatch(typeof(NameMarkerScreenWidget), "OnLateUpdate")]
        public static void NameMarkerScreenWidget_OnLateUpdatePostfix(List<NameMarkerListPanel> ____markers)
        {
            foreach (var marker in ____markers)
            {
                marker.IsFocused = marker.IsInScreenBoundaries;
            }
        }




        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            if (harmony == null)
            {
                harmony = new Harmony("mod.bannerlord.bltadoptahero");
                harmony.PatchAll();
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            try
            {
                if (game.GameType is Campaign)
                {
                    // Reload settings here so they are fresh
                    CommonConfig = GlobalCommonConfig.Get();
                    TournamentConfig = GlobalTournamentConfig.Get();
                    HeroClassConfig = GlobalHeroClassConfig.Get();
                    HeroPowerConfig = GlobalHeroPowerConfig.Get();

                    var campaignStarter = (CampaignGameStarter)gameStarterObject;
                    campaignStarter.AddBehavior(new BLTAdoptAHeroCampaignBehavior());
                    campaignStarter.AddBehavior(new BLTTournamentQueueBehavior());
                    campaignStarter.AddBehavior(new BLTCustomItemsCampaignBehavior());
                    //campaignStarter.AddBehavior(new BLTClanBannerSaveBehavior());
                    campaignStarter.AddBehavior(new BLTClanBehavior());
                    campaignStarter.AddBehavior(new GoldIncomeBehavior()); 
                    campaignStarter.AddBehavior(new BLTSettlementUpgradeBehavior());
                    campaignStarter.AddBehavior(new ReinforcementBehavior());
                    campaignStarter.AddBehavior(new UpgradeBehavior());
                    campaignStarter.AddBehavior(new VassalBehavior());
                    campaignStarter.AddBehavior(new KingdomTaxBehavior());
                    campaignStarter.AddBehavior(new MercenaryArmyBehavior());
                    campaignStarter.AddBehavior(new PartyOrderBehavior());
                    // Diplomacy
                    campaignStarter.AddBehavior(new BLTTreatyManager());       // 1. Core data
                    campaignStarter.AddBehavior(new BLTDiplomacyHelper());     // 2. Rebellion tracking
                    campaignStarter.AddBehavior(new BLTAllianceBehavior());    // 3. Alliance auto-join
                    campaignStarter.AddBehavior(new BLTDiplomacyBehavior());   // 4. Cleanup

                    gameStarterObject.AddModel(new BLTAgentApplyDamageModel(gameStarterObject.Models.OfType<AgentApplyDamageModel>().FirstOrDefault()));
                    gameStarterObject.AddModel(new BLTPartySizeLimitModel(gameStarterObject.Models.OfType<PartySizeLimitModel>().FirstOrDefault()));
                    gameStarterObject.AddModel(new BLTPartySpeedModel(gameStarterObject.Models.OfType<PartySpeedModel>().FirstOrDefault()));
                    gameStarterObject.AddModel(new BLTClanTierModel(gameStarterObject.Models.OfType<ClanTierModel>().FirstOrDefault()));
                }
            }
            catch (Exception e)
            {
                Log.Exception(nameof(OnGameStart), e);
                MessageBox.Show($"Error in {nameof(OnGameStart)}, please report this on the discord: {e}", "Bannerlord Twitch Mod STARTUP ERROR");
            }
        }

        public override void BeginGameStart(Game game)
        {
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            if (game.GameType is Campaign campaign)
            {
                JoinTournament.OnGameEnd(campaign);
            }
        }

        internal const string Tag = "[BLT]";
        internal const string DevTag = "[DEV]";
    }

    public class BLTAgentApplyDamageModel : AgentApplyDamageModel
    {
        private readonly AgentApplyDamageModel previousModel;

        public BLTAgentApplyDamageModel(AgentApplyDamageModel previousModel)
        {
            this.previousModel = previousModel;
        }

        public override float ApplyDamageAmplifications(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
        {
            return previousModel.ApplyDamageAmplifications(in attackInformation, in collisionData, baseDamage);
        }

        public override float ApplyDamageReductions(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
        {
            return previousModel.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage);
        }

        public override float ApplyDamageScaling(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
        {
            return previousModel.ApplyDamageScaling(in attackInformation, in collisionData, baseDamage);
        }

        public override float ApplyGeneralDamageModifiers(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
        {
            return previousModel.ApplyGeneralDamageModifiers(in attackInformation, in collisionData, baseDamage);
        }

        public override float CalculateAlternativeAttackDamage(in AttackInformation attackInformation, in AttackCollisionData collisionData, WeaponComponentData weapon)
        {
            return previousModel.CalculateAlternativeAttackDamage(in attackInformation, in collisionData, weapon);
        }

        public new float CalculateDamage(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
        {
            return previousModel.CalculateDamage(in attackInformation, in collisionData, baseDamage);
        }

        public override void CalculateDefendedBlowStunMultipliers(
        Agent attackerAgent,
        Agent defenderAgent,
        CombatCollisionResult collisionResult,
        WeaponComponentData attackerWeapon,
        WeaponComponentData defenderWeapon,
        ref float attackerStunMultiplier,
        ref float defenderStunMultiplier)
        {
            previousModel.CalculateDefendedBlowStunMultipliers(
                attackerAgent,
                defenderAgent,
                collisionResult,
                attackerWeapon,
                defenderWeapon,
                ref attackerStunMultiplier,
                ref defenderStunMultiplier
            );
        }

        public override float CalculateHullFireDamage(float baseFireDamage, IShipOrigin shipOrigin)
        {
            if (CampaignHelpers.NavalDLC())
                return previousModel.CalculateHullFireDamage(baseFireDamage, shipOrigin);
            return baseFireDamage;
        }

        public override float CalculatePassiveAttackDamage(BasicCharacterObject attackerCharacter, in AttackCollisionData collisionData, float baseDamage)
        {
            return previousModel.CalculatePassiveAttackDamage(attackerCharacter, in collisionData, baseDamage);
        }

        public override float CalculateRemainingMomentum(float originalMomentum, in Blow b, in AttackCollisionData collisionData, Agent attacker, Agent victim, in MissionWeapon attackerWeapon, bool isCrushThrough)
        {
            return previousModel.CalculateRemainingMomentum(originalMomentum, in b, in collisionData, attacker, victim, in attackerWeapon, isCrushThrough);
        }

        public override float CalculateSailFireDamage(Agent attackerAgent, IShipOrigin shipOrigin, float baseDamage, bool damageFromShipMachine)
        {
            return previousModel.CalculateSailFireDamage(attackerAgent, shipOrigin, baseDamage, damageFromShipMachine);
        }

        public override float CalculateShieldDamage(in AttackInformation attackInformation, float baseDamage)
        {
            return previousModel.CalculateShieldDamage(in attackInformation, baseDamage);
        }

        public override float CalculateStaggerThresholdDamage(Agent defenderAgent, in Blow blow)
        {
            return previousModel.CalculateStaggerThresholdDamage(defenderAgent, in blow);
        }

        public override bool CanWeaponDealSneakAttack(in AttackInformation attackInformation, WeaponComponentData weapon)
            => previousModel.CanWeaponDealSneakAttack(in attackInformation, weapon);

        public override bool CanWeaponDismount(Agent attackerAgent, WeaponComponentData attackerWeapon, in Blow blow, in AttackCollisionData collisionData)
        {
            return previousModel.CanWeaponDismount(attackerAgent, attackerWeapon, in blow, in collisionData);
        }

        public override bool CanWeaponIgnoreFriendlyFireChecks(WeaponComponentData weapon)
        {
            return previousModel.CanWeaponIgnoreFriendlyFireChecks(weapon);
        }

        public override bool CanWeaponKnockback(Agent attackerAgent, WeaponComponentData attackerWeapon, in Blow blow, in AttackCollisionData collisionData)
        {
            return previousModel.CanWeaponKnockback(attackerAgent, attackerWeapon, in blow, in collisionData);
        }

        public override bool CanWeaponKnockDown(Agent attackerAgent, Agent victimAgent, WeaponComponentData attackerWeapon, in Blow blow, in AttackCollisionData collisionData)
        {
            return previousModel.CanWeaponKnockDown(attackerAgent, victimAgent, attackerWeapon, in blow, in collisionData);
        }

        public override bool DecideAgentDismountedByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
        {
            return previousModel.DecideAgentDismountedByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);
        }

        public override bool DecideAgentKnockedBackByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
        {
            return previousModel.DecideAgentKnockedBackByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);
        }

        public override bool DecideAgentKnockedDownByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
        {
            return previousModel.DecideAgentKnockedDownByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);
        }

        public override bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
        {
            return previousModel.DecideAgentShrugOffBlow(victimAgent, collisionData, in blow);
        }

        public class DecideCrushedThroughParams
        {
            public float totalAttackEnergy;
            public Agent.UsageDirection attackDirection;
            public StrikeType strikeType;
            public WeaponComponentData defendItem;
            public bool isPassiveUsageHit;
            public bool crushThrough; // set this to override the behaviour
        }
        public override bool DecideCrushedThrough(Agent attackerAgent, Agent defenderAgent, float totalAttackEnergy,
            Agent.UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsageHit)
        {
            bool originalResult = previousModel.DecideCrushedThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit);
            var args = new DecideCrushedThroughParams
            {
                totalAttackEnergy = totalAttackEnergy,
                attackDirection = attackDirection,
                strikeType = strikeType,
                defendItem = defendItem,
                isPassiveUsageHit = isPassiveUsageHit,
                crushThrough = originalResult,
            };

            BLTHeroPowersMissionBehavior.PowerHandler?.CallHandlersForAgentPair(attackerAgent, defenderAgent,
                handlers => handlers.DecideCrushedThrough(attackerAgent, defenderAgent, args));

            return args.crushThrough;
        }

        public class DecideMissileWeaponFlagsParams
        {
            public MissionWeapon missileWeapon;
            public WeaponFlags missileWeaponFlags;
        }
        public override void DecideMissileWeaponFlags(Agent attackerAgent, in MissionWeapon missileWeapon, ref WeaponFlags missileWeaponFlags)
        {
            previousModel.DecideMissileWeaponFlags(attackerAgent, in missileWeapon, ref missileWeaponFlags);
            var args = new DecideMissileWeaponFlagsParams
            {
                missileWeapon = missileWeapon,
                missileWeaponFlags = missileWeaponFlags,
            };

            if (BLTHeroPowersMissionBehavior.PowerHandler?.CallHandlersForAgent(attackerAgent,
                handlers => handlers.DecideMissileWeaponFlags(attackerAgent, args)
                ) == true)
            {
                missileWeaponFlags = args.missileWeaponFlags;
            }
        }

        public override bool DecideMountRearedByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
        {
            return previousModel.DecideMountRearedByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);
        }

        public override MeleeCollisionReaction DecidePassiveAttackCollisionReaction(Agent attacker, Agent defender, bool isFatalHit)
        {
            return previousModel.DecidePassiveAttackCollisionReaction(attacker, defender, isFatalHit);
        }

        public override void DecideWeaponCollisionReaction(in Blow registeredBlow, in AttackCollisionData collisionData, Agent attacker, Agent defender, in MissionWeapon attackerWeapon, bool isFatalHit, bool isShruggedOff, float momentumRemaining, out MeleeCollisionReaction colReaction)
        {
            previousModel.DecideWeaponCollisionReaction(in registeredBlow, in collisionData, attacker, defender, in attackerWeapon, isFatalHit, isShruggedOff, momentumRemaining, out colReaction);
        }

        public override float GetDamageMultiplierForBodyPart(BoneBodyPartType bodyPart, DamageTypes type, bool isHuman, bool isMissile)
        {
            return previousModel.GetDamageMultiplierForBodyPart(bodyPart, type, isHuman, isMissile);
        }

        public override float GetDismountPenetration(Agent attackerAgent, WeaponComponentData attackerWeapon, in Blow blow, in AttackCollisionData collisionData)
        {
            return previousModel.GetDismountPenetration(attackerAgent, attackerWeapon, in blow, in collisionData);
        }

        public override float GetHorseChargePenetration()
        {
            return previousModel.GetHorseChargePenetration();
        }

        public override float GetKnockBackPenetration(Agent attackerAgent, WeaponComponentData attackerWeapon, in Blow blow, in AttackCollisionData collisionData)
        {
            return previousModel.GetKnockBackPenetration(attackerAgent, attackerWeapon, in blow, in collisionData);
        }

        public override float GetKnockDownPenetration(Agent attackerAgent, WeaponComponentData attackerWeapon, in Blow blow, in AttackCollisionData collisionData)
        {
            return previousModel.GetKnockDownPenetration(attackerAgent, attackerWeapon, in blow, in collisionData);
        }

        public override bool IsDamageIgnored(in AttackInformation attackInformation, in AttackCollisionData collisionData)
        {
            return previousModel.IsDamageIgnored(in attackInformation, in collisionData);
        }

        public override bool ShouldMissilePassThroughAfterShieldBreak(Agent attackerAgent, WeaponComponentData attackerWeapon)
        {
            return previousModel.ShouldMissilePassThroughAfterShieldBreak(attackerAgent, attackerWeapon);
        }

        //public override float CalculateDefaultRemainingMomentum(float originalMomentum, in Blow b, in AttackCollisionData collisionData, Agent attacker, Agent victim, in MissionWeapon attackerWeapon, bool isCrushThrough)
        //{
        //    return previousModel.CalculateDefaultRemainingMomentum(originalMomentum, in b, in collisionData, attacker, victim, in attackerWeapon, isCrushThrough);
        //}


        }
}