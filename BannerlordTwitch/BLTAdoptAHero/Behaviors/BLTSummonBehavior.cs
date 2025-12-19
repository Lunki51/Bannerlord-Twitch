using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using NavalDLC.Missions.MissionLogics;

namespace BLTAdoptAHero
{
    internal class BLTSummonBehavior : AutoMissionBehavior<BLTSummonBehavior>
    {
        public class RetinueState
        {
            public CharacterObject Troop;
            public Agent Agent;
            // We must record this separately, as the Agent.State is undefined once the Agent is deleted (the internal handle gets reused by the engine)
            public AgentState State;
            public bool Died;
        }

        public class HeroSummonState
        {
            public Hero Hero;
            public bool WasPlayerSide;
            public bool SpawnWithRetinue;
            public PartyBase Party;
            public AgentState State;
            public Agent CurrentAgent;
            public float SummonTime;
            public int TimesSummoned = 0;
            public List<RetinueState> Retinue { get; set; } = new();
            public List<RetinueState> Retinue2 { get; set; } = new();

            public int ActiveRetinue => Retinue.Count(r => r.State == AgentState.Active);
            public int DeadRetinue => Retinue.Count(r => r.Died);

            public int ActiveRetinue2 => Retinue2.Count(r => r.State == AgentState.Active);
            public int DeadRetinue2 => Retinue2.Count(r => r.Died);

            private float CooldownTime => BLTAdoptAHeroModule.CommonConfig.CooldownEnabled
                ? BLTAdoptAHeroModule.CommonConfig.GetCooldownTime(TimesSummoned) : 0;

            public bool InCooldown => BLTAdoptAHeroModule.CommonConfig.CooldownEnabled && SummonTime + CooldownTime > CampaignHelpers.GetTotalMissionTime();
            public float CooldownRemaining => !BLTAdoptAHeroModule.CommonConfig.CooldownEnabled ? 0 : Math.Max(0, SummonTime + CooldownTime - CampaignHelpers.GetTotalMissionTime());
            public float CoolDownFraction => !BLTAdoptAHeroModule.CommonConfig.CooldownEnabled ? 1 : 1f - CooldownRemaining / CooldownTime;
        }

        private readonly List<HeroSummonState> heroSummonStates = new();
        private readonly List<Action> onTickActions = new();

        public HeroSummonState GetHeroSummonState(Hero hero)
            => heroSummonStates.FirstOrDefault(h => h.Hero == hero);

        public HeroSummonState GetHeroSummonStateForRetinue(Agent retinueAgent)
            => heroSummonStates.FirstOrDefault(h => h.Retinue.Any(r => r.Agent == retinueAgent));
        public HeroSummonState GetHeroSummonStateForRetinue2(Agent retinue2Agent)
            => heroSummonStates.FirstOrDefault(h => h.Retinue2.Any(r => r.Agent == retinue2Agent));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hero"></param>
        /// <param name="playerSide"></param>
        /// <param name="party"></param>
        /// <param name="forced">Whether the player chose to summon, or was part of the battle without choosing it. This affects what statistics will be updated, so streaks etc. aren't broken</param>
        /// <returns></returns>
        public HeroSummonState AddHeroSummonState(Hero hero, bool playerSide, PartyBase party, bool forced, bool withRetinue)
        {
            var heroSummonState = new HeroSummonState
            {
                Hero = hero,
                WasPlayerSide = playerSide,
                Party = party,
                SummonTime = CampaignHelpers.GetTotalMissionTime(),
                SpawnWithRetinue = withRetinue,
            };
            heroSummonStates.Add(heroSummonState);

            BLTAdoptAHeroCampaignBehavior.Current.IncreaseParticipationCount(hero, playerSide, forced);

            return heroSummonState;
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            SafeCall(() =>
            {
                // We only use this for heroes in battle
                if (CampaignMission.Current.Location != null)
                    return;

                var adoptedHero = agent.GetAdoptedHero();
                if (adoptedHero == null)
                    return;

                var heroSummonState = GetHeroSummonState(adoptedHero)
                                   ?? AddHeroSummonState(adoptedHero,
                                       Mission != null
                                       && agent.Team != null
                                       && Mission.PlayerTeam?.IsValid == true
                                       && agent.Team.IsFriendOf(Mission.PlayerTeam),
                                       adoptedHero.GetMapEventParty(),
                                       forced: true,
                                       withRetinue: true);

                // First spawn, so spawn retinue also
                if (heroSummonState.TimesSummoned == 0 && heroSummonState.SpawnWithRetinue && RetinueAllowed())
                {
                    var formationClass = agent.Formation.FormationIndex;
                    SpawnRetinue(adoptedHero, ShouldBeMounted(formationClass), formationClass,
                        heroSummonState, heroSummonState.WasPlayerSide);
                }

                heroSummonState.CurrentAgent = agent;
                heroSummonState.State = AgentState.Active;
                heroSummonState.TimesSummoned++;
                heroSummonState.SummonTime = CampaignHelpers.GetTotalMissionTime();
                // If hero isn't registered yet then this must be a hero that is part of one of the involved parties
                // already
            });
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            SafeCall(() =>
            {
                var heroSummonState = heroSummonStates.FirstOrDefault(h => h.CurrentAgent == affectedAgent);
                if (heroSummonState != null)
                {
                    heroSummonState.State = agentState;
                }

                // Set the final retinue states
                var (retinueOwner, retinueState) = heroSummonStates
                    .Select(h
                        => (state: h, retinue: h.Retinue.FirstOrDefault(r => r.Agent == affectedAgent)))
                    .FirstOrDefault(h => h.retinue != null);
                var (retinue2Owner, retinue2State) = heroSummonStates
                    .Select(h
                        => (state: h, retinue2: h.Retinue2.FirstOrDefault(r => r.Agent == affectedAgent)))
                    .FirstOrDefault(h => h.retinue2 != null);

                bool inRetinue1 = retinueOwner != null;
                bool inRetinue2 = retinue2Owner != null;

                bool runRetinue1 = false;
                bool runRetinue2 = false;

                if (inRetinue1 && inRetinue2)
                {
                    // Same agent exists in both lists, choose one at random
                    if (MBRandom.RandomInt(1,2) == 1)
                        runRetinue1 = true;
                    else
                        runRetinue2 = true;
                }
                else
                {
                    // Only exists in one (or none)
                    runRetinue1 = inRetinue1;
                    runRetinue2 = inRetinue2;
                }


                if (runRetinue1)
                {
                    if (agentState == AgentState.Killed &&
                        MBRandom.RandomFloat <= BLTAdoptAHeroModule.CommonConfig.RetinueDeathChance)
                    {
                        retinueState.Died = true;
                        BLTAdoptAHeroCampaignBehavior.Current.KillRetinue(
                            retinueOwner.Hero,
                            affectedAgent.Character);

                        if (retinueOwner.Hero.FirstName != null)
                        {
                            Log.LogFeedResponse(
                                retinueOwner.Hero.FirstName.ToString(),
                                $"Your {affectedAgent.Character} was killed in battle!");
                        }
                    }

                    retinueState.State = agentState;
                }

                if (runRetinue2)
                {
                    if (agentState == AgentState.Killed &&
                        MBRandom.RandomFloat <= BLTAdoptAHeroModule.CommonConfig.Retinue2DeathChance)
                    {
                        retinue2State.Died = true;
                        BLTAdoptAHeroCampaignBehavior.Current.KillRetinue2(
                            retinue2Owner.Hero,
                            affectedAgent.Character);

                        if (retinue2Owner.Hero.FirstName != null)
                        {
                            Log.LogFeedResponse(
                                retinue2Owner.Hero.FirstName.ToString(),
                                $"Your {affectedAgent.Character} was killed in battle!");
                        }
                    }

                    retinue2State.State = agentState;
                }
            });
        }

        public void DoNextTick(Action action)
        {
            onTickActions.Add(action);
        }

        public override void OnMissionTick(float dt)
        {
            SafeCall(() =>
            {
                var actionsToDo = onTickActions.ToList();
                onTickActions.Clear();
                foreach (var action in actionsToDo)
                {
                    action();
                }
            });
        }

        protected override void OnEndMission()
        {
            SafeCall(() =>
            {
                // Remove still living retinue troops from their parties
                foreach (var h in heroSummonStates)
                {
                    foreach (var r in h.Retinue.Where(r => r.State != AgentState.Killed))
                    {
                        h.Party?.MemberRoster?.AddToCounts(r.Troop, -1);
                    }
                }
            });
        }

        private static void SpawnRetinue(Hero adoptedHero, bool ownerIsMounted, FormationClass ownerFormationClass,
            HeroSummonState existingHero, bool onPlayerSide)
        {
            var retinueTroops = BLTAdoptAHeroCampaignBehavior.Current.GetRetinue(adoptedHero).ToList();
            var retinue2Troops = BLTAdoptAHeroCampaignBehavior.Current.GetRetinue2(adoptedHero).ToList();

            bool retinueMounted = Mission.Current.Mode != MissionMode.Stealth
                                  && !MissionHelpers.InSiegeMission()
                                  && (ownerIsMounted || !BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation);
            var agent_name = AccessTools.Field(typeof(Agent), "_name");
            foreach (var retinueTroop in retinueTroops)
            {
                // Don't modify formation for non-player side spawn as we don't really care
                bool hasPrevFormation = Campaign.Current.PlayerFormationPreferences
                                            .TryGetValue(retinueTroop, out var prevFormation)
                                        && onPlayerSide
                                        && BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation;

                if (onPlayerSide && BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation)
                {
                    Campaign.Current.SetPlayerFormationPreference(retinueTroop, ownerFormationClass);
                }

                existingHero.Party.MemberRoster.AddToCounts(retinueTroop, 1);

                bool DeploymentFlag = Mission.Current.Mode is MissionMode.Deployment;
                var retinueAgent = SpawnAgent(onPlayerSide, retinueTroop, existingHero.Party,
                    retinueTroop.IsMounted && retinueMounted, false, !DeploymentFlag);

                existingHero.Retinue.Add(new()
                {
                    Troop = retinueTroop,
                    Agent = retinueAgent,
                    State = AgentState.Active,
                });

                agent_name.SetValue(retinueAgent, new TextObject($"{retinueAgent.Name} ({adoptedHero.FirstName})"));

                retinueAgent.BaseHealthLimit *= Math.Max(1, BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);
                retinueAgent.HealthLimit *= Math.Max(1, BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);
                retinueAgent.Health *= Math.Max(1, BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);

                BLTAdoptAHeroCustomMissionBehavior.Current.AddListeners(retinueAgent,
                    onGotAKill: (killer, killed, state) =>
                    {
                        Log.Trace($"[{nameof(SummonHero)}] {retinueAgent.Name} killed {killed?.Name ?? "unknown"}");
                        BLTAdoptAHeroCommonMissionBehavior.Current.ApplyKillEffects(
                            adoptedHero, killer, killed, state,
                            BLTAdoptAHeroModule.CommonConfig.RetinueGoldPerKill,
                            BLTAdoptAHeroModule.CommonConfig.RetinueHealPerKill,
                            0, 1,
                            BLTAdoptAHeroModule.CommonConfig.RelativeLevelScaling,
                            BLTAdoptAHeroModule.CommonConfig.LevelScalingCap,
                            BLTAdoptAHeroModule.CommonConfig.MinimumGoldPerKill
                        );
                    }
                );

                if (hasPrevFormation)
                {
                    Campaign.Current.SetPlayerFormationPreference(retinueTroop, prevFormation);
                }
            }
            foreach (var retinue2Troop in retinue2Troops)
            {
                // Don't modify formation for non-player side spawn as we don't really care
                bool hasPrevFormation = Campaign.Current.PlayerFormationPreferences
                                            .TryGetValue(retinue2Troop, out var prevFormation)
                                        && onPlayerSide
                                        && BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation;

                if (onPlayerSide && BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation)
                {
                    Campaign.Current.SetPlayerFormationPreference(retinue2Troop, ownerFormationClass);
                }

                existingHero.Party.MemberRoster.AddToCounts(retinue2Troop, 1);

                bool DeploymentFlag = Mission.Current.Mode is MissionMode.Deployment;
                var retinue2Agent = SpawnAgent(onPlayerSide, retinue2Troop, existingHero.Party,
                    retinue2Troop.IsMounted && retinueMounted, false, !DeploymentFlag);

                existingHero.Retinue.Add(new()
                {
                    Troop = retinue2Troop,
                    Agent = retinue2Agent,
                    State = AgentState.Active,
                });

                agent_name.SetValue(retinue2Agent, new TextObject($"{retinue2Agent.Name} ({adoptedHero.FirstName})"));

                retinue2Agent.BaseHealthLimit *= Math.Max(1, BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);
                retinue2Agent.HealthLimit *= Math.Max(1, BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);
                retinue2Agent.Health *= Math.Max(1, BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);

                BLTAdoptAHeroCustomMissionBehavior.Current.AddListeners(retinue2Agent,
                    onGotAKill: (killer, killed, state) =>
                    {
                        Log.Trace($"[{nameof(SummonHero)}] {retinue2Agent.Name} killed {killed?.Name ?? "unknown"}");
                        BLTAdoptAHeroCommonMissionBehavior.Current.ApplyKillEffects(
                            adoptedHero, killer, killed, state,
                            BLTAdoptAHeroModule.CommonConfig.RetinueGoldPerKill,
                            BLTAdoptAHeroModule.CommonConfig.RetinueHealPerKill,
                            0, 1,
                            BLTAdoptAHeroModule.CommonConfig.RelativeLevelScaling,
                            BLTAdoptAHeroModule.CommonConfig.LevelScalingCap,
                            BLTAdoptAHeroModule.CommonConfig.MinimumGoldPerKill
                        );
                    }
                );

                if (hasPrevFormation)
                {
                    Campaign.Current.SetPlayerFormationPreference(retinue2Troop, prevFormation);
                }
            }
        }

        public static Agent SpawnAgent(bool onPlayerSide, CharacterObject troop, PartyBase party, bool spawnWithHorse, bool isReinforcement = false, bool isAlarmed = true)
        {
            var agent = Mission.Current.SpawnTroop(
                new PartyAgentOrigin(party, troop)
                , isPlayerSide: onPlayerSide
                , hasFormation: true
                , spawnWithHorse: spawnWithHorse
                , isReinforcement: isReinforcement
                , formationTroopCount: 1
                , formationTroopIndex: 0
                , isAlarmed: isAlarmed
                , wieldInitialWeapons: true
                , forceDismounted: false
                , initialPosition: null
                , initialDirection: null
            );
            agent.MountAgent?.FadeIn();
            agent.FadeIn();
            return agent;
        }

        public static bool ShouldBeMounted(FormationClass formationClass)
        {
            return Mission.Current.Mode != MissionMode.Stealth
                   && !MissionHelpers.InSiegeMission()
                   && Mission.Current?.IsNavalBattle == false
                   && formationClass is
                       FormationClass.Cavalry or
                       FormationClass.LightCavalry or
                       FormationClass.HeavyCavalry or
                       FormationClass.HorseArcher;
        }

        public static bool RetinueAllowed() => MissionHelpers.InSiegeMission() || MissionHelpers.InFieldBattleMission();

        [HarmonyPatch(typeof(ShipAgentSpawnLogic), "IsAnyTeamsUnfilled")]
        public static class Patch_IsAnyTeamsUnfilled
        {
            static bool Prefix(ref bool __result)
            {
                // Always return true, ignoring original logic
                __result = true;
                return false; // skip original method
            }
        }
    }
}