using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using HarmonyLib;
using JetBrains.Annotations;
using SandBox;
using SandBox.Source.Missions.Handlers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=6uWk9iMM}Summon Hero"),
     LocDescription("{=ODnzMmBQ}Spawns the adopted hero into the current active mission"), 
     HarmonyPatch, UsedImplicitly]
    internal class SummonHero : HeroActionHandlerBase
    {
        public class FormationItemSource : IItemsSource
        {
            public static string GetFriendlyName(string formation)
            {
                return FormationMapping.FirstOrDefault(p => (string)p.Value == formation)?.DisplayName 
                       ?? formation?.SplitCamelCase() ?? "(invalid)";
            }

            private static readonly ItemCollection FormationMapping = new()
            {
                { "Infantry", "{=7gt8mcJc}Infantry".Translate() },
                { "Ranged", "{=kDo9EtMh}Ranged".Translate() },
                { "Cavalry", "{=HcfkYEsF}Cavalry".Translate() },
                { "HorseArcher", "{=Xx9xP7jK}Horse Archer".Translate() },
                { "Skirmisher", "{=PpZY6dPX}Skirmisher".Translate() },
                { "HeavyInfantry", "{=VT6VYeVp}Heavy Infantry".Translate() },
                { "LightCavalry", "{=t5iTNQ5p}Light Cavalry".Translate() },
                { "HeavyCavalry", "{=vyVSsiBr}Heavy Cavalry".Translate() },
            };

            public ItemCollection GetValues() => FormationMapping;
        }
        
        [CategoryOrder("General", -1)]
        [CategoryOrder("Allowed Missions", 0)]
        [CategoryOrder("General", 1)]
        [CategoryOrder("Effects", 2)]
        [CategoryOrder("End Effects", 3)]
        [CategoryOrder("Kill Effects", 4)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=DkCdNiwF}AllowFieldBattle"),
             LocCategory("Allowed Missions", "{=i8P1EnE1}Allowed Missions"), 
             LocDescription("{=ddjUyXpV}Can summon for normal field battles between parties"), 
             PropertyOrder(1), UsedImplicitly]
            public bool AllowFieldBattle { get; set; }
            
            [LocDisplayName("{=yVz54Nky}AllowVillageBattle"),
             LocCategory("Allowed Missions", "{=i8P1EnE1}Allowed Missions"), 
             LocDescription("{=buLH4Spj}Can summon in village battles"), 
             PropertyOrder(2), UsedImplicitly]
            public bool AllowVillageBattle { get; set; }
            
            [LocDisplayName("{=0X8bcuPE}AllowSiegeBattle"),
             LocCategory("Allowed Missions", "{=i8P1EnE1}Allowed Missions"), 
             LocDescription("{=uk1kL79V}Can summon in sieges"), 
             PropertyOrder(3), UsedImplicitly]
            public bool AllowSiegeBattle { get; set; }
            
            [LocDisplayName("{=Sd1jguGc}AllowFriendlyMission"),
             LocCategory("Allowed Missions", "{=i8P1EnE1}Allowed Missions"), 
             LocDescription("{=1HaljOia}This includes walking about village/town/dungeon/keep"), 
             PropertyOrder(4), UsedImplicitly]
            public bool AllowFriendlyMission { get; set; }
            
            [LocDisplayName("{=1xJGhP2D}Allow Hide Out"),
             LocCategory("Allowed Missions", "{=i8P1EnE1}Allowed Missions"), 
             LocDescription("{=NZTRYcGV}Can summon in the hideout missions"), 
             PropertyOrder(7), UsedImplicitly]
            public bool AllowHideOut { get; set; }
            
            [LocDisplayName("{=i59Fm8zV}On Player Side"),
             LocCategory("General", "{=C5T5nnix}General"), 
             LocDescription("{=S86V5s0C}Whether the hero is on the player or enemy side"), 
             PropertyOrder(1), UsedImplicitly]
            public bool OnPlayerSide { get; set; }
            
            [LocDisplayName("{=HOZnxjGb}Gold Cost"),
             LocCategory("General", "{=C5T5nnix}General"), 
             LocDescription("{=OQISx7Jz}Gold cost to summon"), 
             PropertyOrder(5), UsedImplicitly]
            public int GoldCost { get; set; }

            [LocDisplayName("{=sZRrJfKm}Preferred Formation"),
             LocCategory("General", "{=C5T5nnix}General"), 
             LocDescription("{=vkFowOeg}Which formation to add summoned heroes to (only applies to ones without a specified class)"), 
             PropertyOrder(6), ItemsSource(typeof(FormationItemSource)), UsedImplicitly]
            public string PreferredFormation { get; set; }

            [LocDisplayName("{=74AvYKCg}Alert Sound"),
             LocCategory("General", "{=C5T5nnix}General"),
             LocDescription("{=R00Lx6aE}Sound to play when summoned"), 
             PropertyOrder(9), UsedImplicitly]
            public Log.Sound AlertSound { get; set; }

            [LocDisplayName("{=H7B3Yzly}Sub Boost"),
             LocCategory("Effects", "{=tYGvtYKd}Effects"), 
             LocDescription("{=lFnV3cR9}Multiplier applied to (positive) effects for subscribers"), 
             PropertyOrder(1), UsedImplicitly]
            public float SubBoost { get; set; }

            [LocDisplayName("{=cXeUjVur}Heal Per Second"),
             LocCategory("Effects", "{=tYGvtYKd}Effects"), 
             LocDescription("{=gnh3KY5a}HP the hero gets every second they are alive in the mission"), 
             PropertyOrder(2), UsedImplicitly]
            public float HealPerSecond { get; set; }

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.PropertyValuePair("{=Uu58rjgC}Side".Translate(), 
                OnPlayerSide 
                    ? "{=myKKaEl9}Streamers side".Translate() 
                    : "{=BTZ84Uww}Enemy side".Translate()
                );
                if (HealPerSecond > 0)
                {
                    generator.PropertyValuePair("{=uHTRhFAh}Heals".Translate(), 
                    "{=cR84crHC}{HealPerSecond}HP per second while summoned"
                        .Translate(("HealPerSecond", HealPerSecond.ToString("0.0"))));
                }
                if (GoldCost > 0)
                {
                    generator.PropertyValuePair("{=hCJhgl1m}Cost".Translate(), 
                    $"{GoldCost}{Naming.Gold}");
                }

                var allowed = new List<string>();
                if(AllowFieldBattle) allowed.Add("{=SRdB89Ca}Field battle".Translate());
                if(AllowVillageBattle) allowed.Add("{=M45VqXVa}Village battle".Translate());
                if(AllowSiegeBattle) allowed.Add("{=hbWdpDwr}Siege battle".Translate());
                if(AllowFriendlyMission) allowed.Add("{=YIfbDpI9}Friendly mission".Translate());
                if(AllowHideOut) allowed.Add("{=pZ239vm5}Hide-out".Translate());
                
                generator.PropertyValuePair("{=zOB6Nadp}Allowed in".Translate(), $"{string.Join(", ", allowed)}");
            }
        }

        protected override Type ConfigType => typeof(Settings);
        
        private delegate Agent MissionAgentHandler_SpawnWanderingAgentDelegate(
            MissionAgentHandler instance,
            LocationCharacter locationCharacter,
            MatrixFrame spawnPointFrame,
            bool hasTorch,
            bool noHorses);

        private static readonly MissionAgentHandler_SpawnWanderingAgentDelegate MissionAgentHandler_SpawnWanderingAgent 
            = (MissionAgentHandler_SpawnWanderingAgentDelegate) AccessTools.Method(typeof(MissionAgentHandler),
                    "SpawnWanderingAgent", new[] {typeof(LocationCharacter), typeof(MatrixFrame), typeof(bool), typeof(bool)})
                .CreateDelegate(typeof(MissionAgentHandler_SpawnWanderingAgentDelegate));
        
        // private delegate Agent SpawnWanderingAgentAutoDelegate(
        //     MissionAgentHandler instance,
        //     LocationCharacter locationCharacter,
        //     bool hasTorch);
        //
        // private static readonly SpawnWanderingAgentAutoDelegate SpawnWanderingAgentAuto = (SpawnWanderingAgentAutoDelegate)
        //     AccessTools.Method(typeof(MissionAgentHandler),
        //             "SpawnWanderingAgent",
        //             new[] {typeof(LocationCharacter), typeof(bool)})
        //         .CreateDelegate(typeof(SpawnWanderingAgentDelegate));
        
        private delegate MatrixFrame ArenaPracticeFightMissionController_GetSpawnFrameDelegate(
            ArenaPracticeFightMissionController instance, bool considerPlayerDistance, bool isInitialSpawn);

        private static readonly ArenaPracticeFightMissionController_GetSpawnFrameDelegate ArenaPracticeFightMissionController_GetSpawnFrame = (ArenaPracticeFightMissionController_GetSpawnFrameDelegate)
            AccessTools.Method(typeof(ArenaPracticeFightMissionController), "GetSpawnFrame", new[] {typeof(bool), typeof(bool)})
                .CreateDelegate(typeof(ArenaPracticeFightMissionController_GetSpawnFrameDelegate));

        private static readonly List<Shout> DefaultShouts = new()
        {
            //  Player side
            //      General
            new("{=DczOKFsA}Don't worry, I've got your back!") { EnemySide = false },
            new("{=pj9Z0M7G}I'm here!") { EnemySide = false },
            new("{=y8DOtj2H}Which one should I stab?") { EnemySide = false },
            new("{=1i1OrZDd}Need a hand?") { EnemySide = false },
            new("{=rVaJZ5Lo}The price has been paid. I am at your service.") { EnemySide = false },

            //      Battle / siege
            new("{=QEyO8W82}Once more unto the breach!") { EnemySide = false, General = false },
            new("{=Bo11t6gj}Freeeeeedddooooooommmm!") { EnemySide = false, General = false },
            new("{=8uO8Isjm}Remember the Alamo!") { EnemySide = false, General = false },
            new("{=A3KdQJok}Alala!") { EnemySide = false, General = false },
            new("{=pXVCAwAr}Eleleu!") { EnemySide = false, General = false },
            new("{=TlZjOFii}Deus vult!") { EnemySide = false, General = false },
            new("{=xPFIIEzw}Banzai!") { EnemySide = false, General = false },
            new("{=gmN3YLPw}Liberty or Death!") { EnemySide = false, General = false },
            new("{=FoDIukJK}Har Har Mahadev!") { EnemySide = false, General = false },
            new("{=SSqdBzkY}Desperta ferro!") { EnemySide = false, General = false },
            new("{=UHhpIpDU}Alba gu bràth!") { EnemySide = false, General = false },
            new("{=lDo1AOCS}Santiago!") { EnemySide = false, General = false },
            new("{=gT95E89y}Huzzah!") { EnemySide = false, General = false },
            new("{=fKKpcZGQ}War... war never changes...") { EnemySide = false, General = false },
            new("{=vZgbxkW9}May we live to see the next sunrise!") { EnemySide = false, General = false },
            new("{=wn7zIy9f}For glory, charge!") { EnemySide = false, General = false },
            new("{=KH7Dnbe7}Give them nothing, but take from them everything!") { EnemySide = false, General = false },
            new("{=FyBFecip}Fell deeds awake, fire and slaughter!") { EnemySide = false },
            //          Rare
            new("{=H7asM2KG}Spooooooooooooooooooon!") { EnemySide = false, Weight = 0.05f },
            new("{=ERDmbO2B}Leeeeeeeerooooy Jeeeeenkins") { EnemySide = false, Weight = 0.05f },
            new("{=hL0vmkUG}I live, I die, I live again!") { EnemySide = false, Weight = 0.05f },
            new("{=KMTRT2EN}Witness me!!") { EnemySide = false, Weight = 0.05f },
            new("{=F9WUaqdj}Now for wrath, now for ruin and a red nightfall!") { EnemySide = false, Weight = 0.05f },
            //          Very rare
            new("{=ZQiRFkFU}n") { EnemySide = false, Weight = 0.01f },
            
            //      Siege Attack
            new("{=JlZ9pRbq}Those are brave men knocking at our door, let's go kill them!") { EnemySide = false, General = false, SiegeAttack = false, FieldBattle = false },
            new("{=5gg0BcKk}Lets take this city!") { EnemySide = false, General = false, SiegeDefend = false, FieldBattle = false },

            //  Enemy side
            //      General
            new("{=lhWht8Em}Defend yourself!") { PlayerSide = false },
            new("{=FmlTSg39}Time for you to die!") { PlayerSide = false },
            new("{=GJpUfALs}You killed my father, prepare to die!") { PlayerSide = false },
            new("{=IKN2G1wi}En garde!") { PlayerSide = false },
            new("{=yTHYR4i9}It's stabbing time! For you.") { PlayerSide = false },
            new("{=4wYLe6f0}It's nothing personal!") { PlayerSide = false },
            new("{=37bg5QNX}Curse my sudden but inevitable betrayal!") { PlayerSide = false },
            new("{=AOpbzIUP}I just don't like you!") { PlayerSide = false },
            new("{=JFCcwLga}I'm gonna put some dirt in your eye!") { PlayerSide = false },
            new("{=jCrCRbyA}I'll mount your head on a pike!") { PlayerSide = false },
            new("{=0uiD2HxZ}Don't hate me, it's just business...") { PlayerSide = false },
            new("{=42Fuy9pm}Never should have come here!") { PlayerSide = false },
            new("{=dClbnb2q}Your money or your life!") { PlayerSide = false },
            new("{=uyjy1geA}I'm sorry, but I must stop you.") { PlayerSide = false },
            
            //          Rare
            new("{=dh0c96lo}I have the high ground!") { PlayerSide = false, Weight = 0.05f },
            
            //          Ultra rare
            new("{=EcD5q9th}DAMN IT DAVE!") { PlayerSide = false, Weight = 0.01f },
        };

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            var settings = (Settings) config;
            int availableGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero);
            if (availableGold < settings.GoldCost)
            {
                onFailure(Naming.NotEnoughGold(settings.GoldCost, availableGold));
                return;
            }
            
            // SpawnAgent (as called by this function) crashes if called in MissionMode.Deployment (would be nice to make it work though)
            if (Mission.Current == null 
                || Mission.Current.Mode is MissionMode.Barter or MissionMode.Conversation or MissionMode.Deployment or
                    MissionMode.Duel or MissionMode.Replay or MissionMode.CutScene)
            {
                onFailure("{=TdykIizS}You cannot be summoned now!".Translate());
                return;
            }

            if(MissionHelpers.InArenaPracticeMission() 
               || MissionHelpers.InTournament()
               || MissionHelpers.InConversationMission()
               || MissionHelpers.InFieldBattleMission() && !settings.AllowFieldBattle
               || MissionHelpers.InVillageEncounter() && !settings.AllowVillageBattle
               || MissionHelpers.InSiegeMission() && !settings.AllowSiegeBattle
               || MissionHelpers.InFriendlyMission() && !settings.AllowFriendlyMission
               || MissionHelpers.InHideOutMission() && (!settings.AllowHideOut || !settings.OnPlayerSide)
               || MissionHelpers.InTrainingFieldMission()
               || MissionHelpers.InArenaPracticeVisitingArea()
            )
            {
                onFailure("{=HWnEJg1M}You cannot be summoned now, this mission does not allow it!".Translate());
                return;
            }

            if (!Mission.Current.IsLoadingFinished 
                || Mission.Current.CurrentState != Mission.State.Continuing
                || Mission.Current?.GetMissionBehaviour<TournamentFightMissionController>() != null && Mission.Current.Mode != MissionMode.Battle)
            {
                onFailure("{=v5dO40vi}You cannot be summoned now, the mission has not started yet!".Translate());
                return;
            }
            if (Mission.Current.IsMissionEnding || Mission.Current.MissionResult?.BattleResolved == true)
            {
                onFailure("{=mTKKaYIf}You cannot be summoned now, the mission is ending!".Translate());
                return;
            }
            
            if (MissionHelpers.HeroIsSpawned(adoptedHero))
            {
                onFailure("{=YTEgKnCW}You cannot be summoned, you are already here!".Translate());
                return;
            }

            bool onAttackingSide = Mission.Current.AttackerTeam.IsValid && 
                                   (settings.OnPlayerSide
                                       ? Mission.Current.AttackerTeam.IsFriendOf(Mission.Current.PlayerTeam)
                                       : !Mission.Current.AttackerTeam.IsFriendOf(Mission.Current.PlayerTeam))
                ;
            bool doingSiegeAttack = MissionHelpers.InSiegeMission() && onAttackingSide;
            bool doingSiegeDefend = MissionHelpers.InSiegeMission() && !onAttackingSide;
            bool doingFieldBattle = MissionHelpers.InFieldBattleMission();
            bool doingGeneral = !doingSiegeAttack && !doingSiegeDefend && !doingFieldBattle;
            var messages = (BLTAdoptAHeroModule.CommonConfig.IncludeDefaultShouts
                    ? DefaultShouts
                    : Enumerable.Empty<Shout>())
                .Concat(BLTAdoptAHeroModule.CommonConfig.Shouts ?? Enumerable.Empty<Shout>())
                .Where(s =>
                    (s.EnemySide && !settings.OnPlayerSide || s.PlayerSide && settings.OnPlayerSide)
                    && (s.General || !doingGeneral)
                    && (s.FieldBattle || !doingFieldBattle)
                    && (s.SiegeAttack || !doingSiegeAttack)
                    && (s.SiegeDefend || !doingSiegeDefend)
                );

            if (CampaignMission.Current.Location != null)
            {
                var locationCharacter = LocationCharacter.CreateBodyguardHero(adoptedHero,
                    MobileParty.MainParty,
                    SandBoxManager.Instance.AgentBehaviorManager.AddBodyguardBehaviors);
                
                var missionAgentHandler = Mission.Current.GetMissionBehaviour<MissionAgentHandler>();
                var worldFrame = missionAgentHandler.Mission.MainAgent.GetWorldFrame();
                worldFrame.Origin.SetVec2(worldFrame.Origin.AsVec2 + (worldFrame.Rotation.f * 10f + worldFrame.Rotation.s).AsVec2);
                
                CampaignMission.Current.Location.AddCharacter(locationCharacter);

                Agent agent;
                if (MissionHelpers.InArenaPracticeMission())
                {
                    var controller = Mission.Current.GetMissionBehaviour<ArenaPracticeFightMissionController>();
                    var pos = ArenaPracticeFightMissionController_GetSpawnFrame(controller, false, false);
                    agent = MissionAgentHandler_SpawnWanderingAgent(missionAgentHandler, locationCharacter, pos, false, true);
                    var _participantAgents = (List<Agent>)AccessTools.Field(typeof(ArenaPracticeFightMissionController), "_participantAgents")
                        .GetValue(controller);
                    _participantAgents.Add(agent);
                }
                else
                {
                    agent = missionAgentHandler.SpawnLocationCharacter(locationCharacter);
                }

                agent.SetTeam(settings.OnPlayerSide 
                    ? missionAgentHandler.Mission.PlayerTeam
                    : missionAgentHandler.Mission.PlayerEnemyTeam, false);
                
                // For player hostile situations we setup a 1v1 fight
                if (!settings.OnPlayerSide)
                {
                    Mission.Current.GetMissionBehaviour<MissionFightHandler>().StartCustomFight(
                        new() {Agent.Main},
                        new() {agent}, false, false, false,
                        playerWon =>
                        {
                            if (BLTAdoptAHeroModule.CommonConfig.WinGold != 0)
                            {
                                if (!playerWon)
                                {
                                    Hero.MainHero.ChangeHeroGold(-BLTAdoptAHeroModule.CommonConfig.WinGold);
                                    // User gets their gold back also
                                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, BLTAdoptAHeroModule.CommonConfig.WinGold + settings.GoldCost);
                                    ActionManager.SendReply(context, 
                                        "{=pmj8vjZj}You won {WonGold}{GoldIcon}!".Translate(
                                                ("WonGold", BLTAdoptAHeroModule.CommonConfig.WinGold),
                                                ("GoldIcon", Naming.Gold)
                                                ));
                                }
                                else if(BLTAdoptAHeroModule.CommonConfig.LoseGold > 0)
                                {
                                    Hero.MainHero.ChangeHeroGold(BLTAdoptAHeroModule.CommonConfig.LoseGold);
                                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -BLTAdoptAHeroModule.CommonConfig.LoseGold);
                                    ActionManager.SendReply(context,
                                        "{=ajhKKeEo}You lost {LostGold}{GoldIcon}!".Translate(
                                            ("LostGold", BLTAdoptAHeroModule.CommonConfig.WinGold),
                                            ("GoldIcon", Naming.Gold)
                                        ));

                                    //$@"You lost {BLTAdoptAHeroModule.CommonConfig.LoseGold + settings.GoldCost}{Naming.Gold}!");
                                }
                            }
                        });
                }

                // Bodyguard
                if (settings.OnPlayerSide && agent.GetComponent<CampaignAgentComponent>().AgentNavigator != null)
                {
                    var behaviorGroup = agent.GetComponent<CampaignAgentComponent>().AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>();
                    (behaviorGroup.GetBehavior<FollowAgentBehavior>() ?? behaviorGroup.AddBehavior<FollowAgentBehavior>()).SetTargetAgent(Agent.Main);
                    behaviorGroup.SetScriptedBehavior<FollowAgentBehavior>();
                }
                
                BLTRemoveAgentsBehavior.Current.Add(adoptedHero);
                // missionAgentHandler.SimulateAgent(agent);
                
                //SetAgentHealth(agent);

                Log.ShowInformation(!string.IsNullOrEmpty(context.Args) 
                        ? context.Args 
                        : messages.SelectRandomWeighted(shout => shout.Weight)?.Text?.ToString() ?? "...",
                    adoptedHero.CharacterObject, settings.AlertSound);

                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.GoldCost);

                onSuccess("{=Ft3cxfIx}You have joined the battle!".Translate());
            }
            else
            {
                // We need to synchronize troop spawning to the MissionBehaviour OnMissionTick, or occasional null pointer  
                // crashes seem to happen in the engine...
                BLTSummonBehavior.Current.DoNextTick(() =>
                {
                    if (Mission.Current.CurrentState != Mission.State.Continuing)
                    {
                        onFailure("{=YZ4V5e4V}You cannot be summoned now, the mission has not started yet!".Translate());
                        return;
                    }

                    var existingHero = BLTSummonBehavior.Current.GetSummonedHero(adoptedHero);
                    if (existingHero != null && existingHero.WasPlayerSide != settings.OnPlayerSide)
                    {
                        onFailure("{=2D2T6xP6}You cannot switch sides, you traitor!".Translate());
                        return;
                    }

                    if (existingHero != null
                        && BLTAdoptAHeroModule.CommonConfig.AllowDeath
                        && existingHero.State == AgentState.Killed)
                    {
                        onFailure("{=RBTDviuM}You cannot be summoned, you DIED!".Translate());
                        return;
                    }
                    
                    // Check again, as tick is delayed...
                    if (existingHero is {State: AgentState.Active})
                    {
                        onFailure("{=YMiZAluP}You cannot be summoned, you are already here!".Translate());
                        return;
                    }

                    if (existingHero?.InCooldown == true)
                    {
                        onFailure("{=kyUh29ij}{CoolDown}s cooldown remaining"
                            .Translate(("CoolDown", existingHero.CooldownRemaining.ToString("0"))));
                        return;
                    }

                    float actualBoost = context.IsSubscriber ? Math.Max(settings.SubBoost, 1) : 1;

                    bool firstSummon = existingHero == null; 
                    if (firstSummon)
                    {
                        // If the hero exists in one of the parties already they cannot summon
                        // TODO: let them summon their retinue anyway
                        var existingParty = (PartyBase.MainParty.MapEvent?.InvolvedParties ?? PartyBase.MainParty.SiegeEvent?.Parties)?
                            .FirstOrDefault(p => p.MemberRoster.GetTroopCount(adoptedHero.CharacterObject) != 0);

                        if(existingParty != null)
                        {
                            onFailure("{=93Hrc2EA}You cannot be summoned, your party is already here!".Translate());
                            return;
                        }
                        
                        var party = settings.OnPlayerSide switch
                        {
                            true when Mission.Current?.PlayerTeam != null &&
                                      Mission.Current?.PlayerTeam?.ActiveAgents.Any() == true => PartyBase.MainParty,
                            false when Mission.Current?.PlayerEnemyTeam != null &&
                                       Mission.Current?.PlayerEnemyTeam.ActiveAgents.Any() == true => Mission.Current
                                .PlayerEnemyTeam?.TeamAgents?.Select(a => a.Origin?.BattleCombatant as PartyBase)
                                .Where(p => p != null)
                                .SelectRandom(),
                            _ => null
                        };

                        if (party == null)
                        {
                            onFailure("{=jtqEqonE}Could not find a party for you to join!".Translate());
                            return;
                        }

                        var originalParty = adoptedHero.PartyBelongedTo;
                        bool wasLeader = adoptedHero.PartyBelongedTo?.LeaderHero == adoptedHero;
                        if (originalParty?.Party != party)
                        {
                            originalParty?.Party?.AddMember(adoptedHero.CharacterObject, -1);
                            party.AddMember(adoptedHero.CharacterObject, 1);
                        }

                        var heroClass = BLTAdoptAHeroCampaignBehavior.Current.GetClass(adoptedHero);

                        // We don't support Unset, or General formations, and implement custom behaviour for Bodyguard
                        if (!Enum.TryParse(heroClass?.Formation ?? settings.PreferredFormation, out FormationClass formationClass)
                                 || formationClass >= FormationClass.NumberOfRegularFormations)
                        {
                            formationClass = FormationClass.Infantry;
                        }

                        BLTAdoptAHeroCustomMissionBehavior.Current.AddListeners(adoptedHero,
                            onSlowTick: dt =>
                            {
                                if (settings.HealPerSecond != 0)
                                {
                                    var activeAgent = Mission.Current?.Agents?.FirstOrDefault(a =>
                                        a.IsActive() && a.Character == adoptedHero.CharacterObject);
                                    if (activeAgent?.IsActive() == true)
                                    {
                                        Log.Trace($"[{nameof(SummonHero)}] healing {adoptedHero}");
                                        activeAgent.Health = Math.Min(activeAgent.HealthLimit,
                                            activeAgent.Health + settings.HealPerSecond * dt * actualBoost);
                                    }
                                }
                            },
                            onMissionOver: () =>
                            {
                                if (adoptedHero.PartyBelongedTo != originalParty)
                                {
                                    party.AddMember(adoptedHero.CharacterObject, -1);
                                    // Use insert at front to make sure we put the character back as party leader if they were previously
                                    originalParty?.Party?.MemberRoster.AddToCounts(adoptedHero.CharacterObject, 1,
                                        insertAtFront: wasLeader);
                                    Log.Trace($"[{nameof(SummonHero)}] moving {adoptedHero} from {party} back to {originalParty?.Party?.ToString() ?? "no party"}");
                                }

                                if (Mission.Current?.MissionResult != null)
                                {
                                    var results = new List<string>();
                                    float finalRewardScaling =
                                            actualBoost * 
                                            (settings.OnPlayerSide 
                                            ? BLTAdoptAHeroCommonMissionBehavior.Current.PlayerSideRewardMultiplier
                                            : BLTAdoptAHeroCommonMissionBehavior.Current.EnemySideRewardMultiplier)
                                        ;
                                    
                                    if (settings.OnPlayerSide == Mission.Current.MissionResult.PlayerVictory)
                                    {
                                        int actualGold = (int) (finalRewardScaling * BLTAdoptAHeroModule.CommonConfig.WinGold + settings.GoldCost);
                                        if (actualGold > 0)
                                        {
                                            int newGold = BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, actualGold);
                                            results.Add(finalRewardScaling != 1
                                                ? $"{Naming.Inc}{actualGold}{Naming.Gold} (x{finalRewardScaling:0.00})"
                                                : $"{Naming.Inc}{actualGold}{Naming.Gold}");
                                        }

                                        int xp = (int) (BLTAdoptAHeroModule.CommonConfig.WinXP * actualBoost);
                                        if (xp > 0)
                                        {
                                            (bool success, string description) = SkillXP.ImproveSkill(adoptedHero, xp,
                                                SkillsEnum.All, auto: true);
                                            if (success)
                                            {
                                                results.Add(finalRewardScaling != 1
                                                    ? $"{description} (x{finalRewardScaling:0.00})"
                                                    : description);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (BLTAdoptAHeroModule.CommonConfig.LoseGold > 0)
                                        {
                                            BLTAdoptAHeroCampaignBehavior.Current
                                                .ChangeHeroGold(adoptedHero, -BLTAdoptAHeroModule.CommonConfig.LoseGold);
                                            results.Add($"{Naming.Dec}{BLTAdoptAHeroModule.CommonConfig.LoseGold}{Naming.Gold}");
                                        }

                                        int xp = (int) (finalRewardScaling * BLTAdoptAHeroModule.CommonConfig.LoseXP);
                                        if (xp > 0)
                                        {
                                            (bool success, string description) = SkillXP.ImproveSkill(adoptedHero, xp,
                                                SkillsEnum.All, auto: true);
                                            if (success)
                                            {
                                                results.Add(finalRewardScaling != 1
                                                    ? $"{description} (x{finalRewardScaling:0.00})"
                                                    : description);
                                            }
                                        }
                                    }

                                    if (results.Any())
                                    {
                                        Log.LogFeedResponse(context.UserName, results.ToArray());
                                    }
                                }
                            },
                            replaceExisting: false
                        );
                        existingHero = BLTSummonBehavior.Current.AddSummonedHero(adoptedHero, settings.OnPlayerSide, formationClass, party);
                        BLTAdoptAHeroCampaignBehavior.Current.IncreaseParticipationCount(adoptedHero, settings.OnPlayerSide);
                    }
                    
                    var troopOrigin = new PartyAgentOrigin(existingHero.Party, adoptedHero.CharacterObject);
                    bool isMounted = Mission.Current.Mode != MissionMode.Stealth 
                                     && !MissionHelpers.InSiegeMission() 
                                     && existingHero.Formation is
                                         FormationClass.Cavalry or
                                         FormationClass.LightCavalry or
                                         FormationClass.HeavyCavalry or
                                         FormationClass.HorseArcher
                                     ;

                    // The standard MissionAgentSpawnLogic.SpawnTroops does this for formations also,
                    // however it is difficult to do for us as the class required is a private subclass,
                    // and not accessible except through raw reflection. 
                    // It doesn't seem to be necessary as the required formations appear to be created dynamically
                    // anyway, so it might just be legacy code.
                    // var formation = Mission.GetAgentTeam(troopOrigin, settings.OnPlayerSide).GetFormation(existingHero.Formation);
                    // if (formation != null && !(bool)AccessTools.Field(typeof(Formation), "HasBeenPositioned").GetValue(formation))
                    // {
                    //     formation.BeginSpawn(1, isMounted);
                    //     Mission.Current.SpawnFormation(formation, 1, Mission.Current.Mode != MissionMode.Stealth 
                    //                                                  && !InSiegeMission(), isMounted, true);
                    //     var spawnLogic = Mission.Current.GetMissionBehaviour<MissionAgentSpawnLogic>();
                    //     var _spawnedFormations = (MBList<Formation>) AccessTools
                    //         .Field(typeof(MissionAgentSpawnLogic), "_spawnedFormations").GetValue(spawnLogic);
                    //     _spawnedFormations.Add(formation);
                    // }

                    bool allowRetinue = firstSummon
                                        && !MissionHelpers.InHideOutMission()
                                        && !MissionHelpers.InArenaPracticeMission()
                                        && !MissionHelpers.InTournament()
                                        && !MissionHelpers.InFriendlyMission();

                    var retinueTroops = allowRetinue 
                        ? BLTAdoptAHeroCampaignBehavior.Current.GetRetinue(adoptedHero).ToList() 
                        : Enumerable.Empty<CharacterObject>().ToList();

                    int formationTroopIdx = 0;

                    int totalTroopsCount = 1 + retinueTroops.Count;
                    if (settings.OnPlayerSide)
                    {
                        Campaign.Current.SetPlayerFormationPreference(adoptedHero.CharacterObject,
                            existingHero.Formation);
                    }

                    existingHero.CurrentAgent = Mission.Current.SpawnTroop(
                          troopOrigin
                        , isPlayerSide: settings.OnPlayerSide
                        , hasFormation: true
                        , spawnWithHorse: adoptedHero.CharacterObject.IsMounted && isMounted
                        , isReinforcement: true
                        , enforceSpawningOnInitialPoint: false
                        , formationTroopCount: totalTroopsCount
                        , formationTroopIndex: formationTroopIdx++
                        , isAlarmed: true
                        , wieldInitialWeapons: true
                        #if e162
                        , forceDismounted: false
                        , initialPosition: null
                        , initialDirection: null
                        #endif
                        );

                    existingHero.State = AgentState.Active;
                    existingHero.TimesSummoned++;
                    existingHero.SummonTime = MBCommon.GetTime(MBCommon.TimeType.Mission);

                    // if (settings.OnPlayerSide && existingHero.Formation == FormationClass.Bodyguard)
                    // {
                    //     var spawnPos = Vec2.Forward * (3 + MBRandom.RandomFloat * 5);
                    //     spawnPos.RotateCCW(MathF.PI * 2 * MBRandom.RandomFloat);
                    //     existingHero.CurrentAgent.SetColumnwiseFollowAgent(Agent.Main, ref spawnPos);
                    //     // agent.HumanAIComponent.FollowAgent(Agent.Main);
                    // }

                    if (allowRetinue)
                    {
                        bool retinueMounted = Mission.Current.Mode != MissionMode.Stealth 
                                              && !MissionHelpers.InSiegeMission() 
                                              && (isMounted || !BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation);
                        var agent_name = AccessTools.Field(typeof(Agent), "_name");
                        foreach (var retinueTroop in retinueTroops)
                        {
                            // Don't modify formation for non-player side spawn as we don't really care
                            bool hasPrevFormation = Campaign.Current.PlayerFormationPreferences
                                                        .TryGetValue(retinueTroop, out var prevFormation)
                                                    && settings.OnPlayerSide
                                                    && BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation;

                            if (settings.OnPlayerSide && BLTAdoptAHeroModule.CommonConfig.RetinueUseHeroesFormation)
                            {
                                Campaign.Current.SetPlayerFormationPreference(retinueTroop, existingHero.Formation);
                            }

                            existingHero.Party.MemberRoster.AddToCounts(retinueTroop, 1);
                            var retinueAgent = Mission.Current.SpawnTroop(
                                  new PartyAgentOrigin(existingHero.Party, retinueTroop)
                                , isPlayerSide: settings.OnPlayerSide
                                , hasFormation: true
                                , spawnWithHorse: retinueTroop.IsMounted && retinueMounted
                                , isReinforcement: true
                                , enforceSpawningOnInitialPoint: false
                                , formationTroopCount: totalTroopsCount
                                , formationTroopIndex: formationTroopIdx++
                                , isAlarmed: true
                                , wieldInitialWeapons: true
                                #if e162
                                , forceDismounted: false
                                , initialPosition: null
                                , initialDirection: null
                                #endif
                                );

                            existingHero.Retinue.Add(new()
                            {
                                Troop = retinueTroop,
                                Agent = retinueAgent,
                                State = AgentState.Active,
                            });

                            agent_name.SetValue(retinueAgent,
                                new TextObject($"{retinueAgent.Name} ({context.UserName})"));

                            retinueAgent.BaseHealthLimit *= Math.Max(1,
                                BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);
                            retinueAgent.HealthLimit *= Math.Max(1,
                                BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);
                            retinueAgent.Health *= Math.Max(1,
                                BLTAdoptAHeroModule.CommonConfig.StartRetinueHealthMultiplier);

                            BLTAdoptAHeroCustomMissionBehavior.Current.AddListeners(retinueAgent,
                                onGotAKill: (killer, killed, state) =>
                                {
                                    Log.Trace($"[{nameof(SummonHero)}] {retinueAgent.Name} killed {killed?.ToString() ?? "unknown"}");
                                    BLTAdoptAHeroCommonMissionBehavior.Current.ApplyKillEffects(
                                        adoptedHero, killer, killed, state,
                                        BLTAdoptAHeroModule.CommonConfig.RetinueGoldPerKill,
                                        BLTAdoptAHeroModule.CommonConfig.RetinueHealPerKill,
                                        0,
                                        actualBoost,
                                        BLTAdoptAHeroModule.CommonConfig.RelativeLevelScaling,
                                        BLTAdoptAHeroModule.CommonConfig.LevelScalingCap
                                    );
                                }
                            );
                            // if (settings.OnPlayerSide && existingHero.Formation == FormationClass.Bodyguard)
                            // {
                            //     var spawnPos = Vec2.Forward * (3 + MBRandom.RandomFloat * 5);
                            //     spawnPos.RotateCCW(MathF.PI * 2 * MBRandom.RandomFloat);
                            //     retinueAgent.SetColumnwiseFollowAgent(Agent.Main, ref spawnPos);
                            //     // agent.HumanAIComponent.FollowAgent(Agent.Main);
                            // }

                            if (hasPrevFormation)
                            {
                                Campaign.Current.SetPlayerFormationPreference(retinueTroop, prevFormation);
                            }
                        }

                        // BLTAdoptAHeroCommonMissionBehavior.Current.RegisterRetinue(adoptedHero, existingHero.Retinue.Select(r => r.Agent).ToList());
                    }

                    // All the units try to occupy the same exact spot if standard body guard is used
                    // if (existingHero.Formation == FormationClass.Bodyguard)
                    // {
                    //     if (settings.OnPlayerSide)
                    //         agent.SetGuardState(Agent.Main, isGuarding: true);
                    //     else if (Mission.Current.PlayerEnemyTeam?.GeneralAgent != null)
                    //         agent.SetGuardState(Mission.Current.PlayerEnemyTeam?.GeneralAgent, isGuarding: true);
                    // }

                    var expireFn = AccessTools.Method(typeof(TeamQuerySystem), "Expire");
                    foreach (var team in Mission.Current.Teams)
                    {
                        expireFn.Invoke(team.QuerySystem, new object[] { }); // .Expire();
                    }
                    foreach (var formation in Mission.Current.Teams.SelectMany(t => t.Formations))
                    {
                        AccessTools.Field(typeof(Formation), "GroupSpawnIndex").SetValue(formation, 0); //formation2.GroupSpawnIndex = 0;
                    }
                    
                    Log.ShowInformation(!string.IsNullOrEmpty(context.Args) 
                        ? context.Args 
                        : (messages.SelectRandomWeighted(shout => shout.Weight)?.Text?.ToString() ?? "..."),
                        adoptedHero.CharacterObject, settings.AlertSound);

                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.GoldCost);

                    onSuccess("{=TvkEBZeY}You have joined the battle!".Translate());
                });
            }
        }

        // Modified KillAgentCheat (usually Ctrl+F4 in debug mode) that can actually kill sometimes instead of only knock out.
        // For testing...
        // ReSharper disable once UnusedMember.Local
        private static void KillAgentCheat(Agent agent)
        {
            var blow = new Blow(Mission.Current.MainAgent?.Index ?? agent.Index)
            {
                DamageType = DamageTypes.Pierce,
                BoneIndex = agent.Monster.HeadLookDirectionBoneIndex,
                Position = agent.Position,
                BaseMagnitude = 2000f,
                InflictedDamage = 2000,
                SwingDirection = agent.LookDirection,
                Direction = agent.LookDirection,
                DamageCalculated = true,
                VictimBodyPart = BoneBodyPartType.Head,
                WeaponRecord = new () { AffectorWeaponSlotOrMissileIndex = -1 }
            };
            blow.Position.z += agent.GetEyeGlobalHeight();
            agent.RegisterBlow(blow);
        }
        
        [UsedImplicitly, HarmonyPostfix, HarmonyPatch(typeof(MissionAgentSpawnLogic), nameof(MissionAgentSpawnLogic.IsSideDepleted))]
        // ReSharper disable once RedundantAssignment
        public static void IsSideDepletedPostfix(MissionAgentSpawnLogic __instance, BattleSideEnum side, ref bool __result)
        {
            __result = !__instance.Mission.Teams.Where(t => t.Side == side).Any(t => t.ActiveAgents.Any());
        }

        [UsedImplicitly, HarmonyPostfix, HarmonyPatch(typeof(DefaultBattleMoraleModel), nameof(DefaultBattleMoraleModel.GetImportance))]
        public static void GetImportancePostfix(DefaultBattleMoraleModel __instance, Agent agent, ref float __result)
        {
            if (agent.IsAdopted())
            {
                __result *= BLTAdoptAHeroModule.CommonConfig.MoraleLossFactor;
            }
        }
    }

    [LocDisplayName("{=CXb5ALls}Shout")]
    public class Shout
    {
        [LocDisplayName("{=V3STT5Ar}Text"), 
         LocDescription("{=wsIzPzfG}Text that will be displayed in game"), 
         PropertyOrder(1), InstanceName, UsedImplicitly] 
        public LocString Text { get; set; } = "{=xf2mTWx8}Enter shout text here";
        [LocDisplayName("{=bCbN9OmH}Weight"), 
         LocDescription("{=xqfCYYwN}Higher weight means more chance this shout is used"), 
         PropertyOrder(2), UsedImplicitly]
        public float Weight { get; set; } = 1f;
        [LocDisplayName("{=xK1fdx7s}Player Side"), 
         LocDescription("{=BpDPlsfY}Can be used when summoning on player side"), 
         PropertyOrder(3), UsedImplicitly]
        public bool PlayerSide { get; set; } = true;
        [LocDisplayName("{=srN8yHp8}Enemy Side"), 
         LocDescription("{=0m4s9zXc}Can be used when summoning on enemy side"), 
         PropertyOrder(4), UsedImplicitly]
        public bool EnemySide { get; set; } = true;
        [LocDisplayName("{=MOOBN2Hz}General"), 
         LocDescription("{=6TWSIu60}Can be used in situations other than battle/siege"), 
         PropertyOrder(5), UsedImplicitly]
        public bool General { get; set; } = true;
        [LocDisplayName("{=4KdKuusC}Field Battle"), 
         LocDescription("{=8aLSNsxr}Can be used when in a field battle"), 
         PropertyOrder(6), UsedImplicitly]
        public bool FieldBattle { get; set; } = true;
        [LocDisplayName("{=2yevL6dV}Siege Defend"), 
         LocDescription("{=8HwihUK3}Can be used when on siege defender side"), 
         PropertyOrder(7), UsedImplicitly]
        public bool SiegeDefend { get; set; } = true;
        [LocDisplayName("{=r5tvIQwx}Siege Attack"), 
         LocDescription("{=mbIwJprm}Can be used when on siege attacker side"), 
         PropertyOrder(8), UsedImplicitly]
        public bool SiegeAttack { get; set; } = true;

        public Shout() { }
        public Shout(LocString text)
        {
            Text = text;
        }

        public override string ToString() => Text.ToString();
    }
}