﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BannerlordTwitch.Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Customizable mission behaviour
    /// </summary>
    internal class BLTAdoptAHeroCustomMissionBehavior : AutoMissionBehavior<BLTAdoptAHeroCustomMissionBehavior>
    {
        public delegate void AgentCreatedDelegate(Agent agent);
        public delegate void MissionOverDelegate();
        public delegate void MissionModeChangeDelegate(MissionMode oldMode, MissionMode newMode, bool atStart);
        public delegate void MissionResetDelegate();
        public delegate void GotAKillDelegate(Agent killer, Agent killed, AgentState agentState);
        public delegate void GotKilledDelegate(Agent killed, Agent killer, AgentState agentState);
        public delegate void MissionTickDelegate(float dt);

        private class Listeners
        {
            public AgentCreatedDelegate onAgentCreated;
            public MissionOverDelegate onMissionOver;
            public GotAKillDelegate onGotAKill;
            public GotKilledDelegate onGotKilled;
            public MissionTickDelegate onMissionTick;
            public MissionTickDelegate onSlowTick;
        }

        private readonly Dictionary<Hero, Listeners> heroListeners = new();
        private readonly Dictionary<Agent, Listeners> agentListeners = new();
        private IEnumerable<Listeners> AllListeners => heroListeners.Values.Concat(agentListeners.Values);

        public bool AddListeners(Hero hero,
            AgentCreatedDelegate onAgentCreated = null,
            MissionOverDelegate onMissionOver = null,
            GotAKillDelegate onGotAKill = null,
            GotKilledDelegate onGotKilled = null,
            MissionTickDelegate onMissionTick = null,
            MissionTickDelegate onSlowTick = null,
            bool replaceExisting = false
        )
        {
            if (!replaceExisting && HasListeners(hero))
                return false;
            RemoveListeners(hero);
            heroListeners.Add(hero, new Listeners
            {
                onAgentCreated = onAgentCreated,
                onMissionOver = onMissionOver,
                onGotAKill = onGotAKill,
                onGotKilled = onGotKilled,
                onMissionTick = onMissionTick,
                onSlowTick = onSlowTick,
            });
            return true;
        }

        public bool AddListeners(Agent agent,
            AgentCreatedDelegate onAgentCreated = null,
            MissionOverDelegate onMissionOver = null,
            MissionModeChangeDelegate onModeChange = null,
            MissionResetDelegate onMissionReset = null,
            GotAKillDelegate onGotAKill = null,
            GotKilledDelegate onGotKilled = null,
            MissionTickDelegate onMissionTick = null,
            MissionTickDelegate onSlowTick = null,
            bool replaceExisting = false
        )
        {
            if (!replaceExisting && HasListeners(agent))
                return false;
            RemoveListeners(agent);
            agentListeners.Add(agent, new()
            {
                onAgentCreated = onAgentCreated,
                onMissionOver = onMissionOver,
                onGotAKill = onGotAKill,
                onGotKilled = onGotKilled,
                onMissionTick = onMissionTick,
                onSlowTick = onSlowTick,
            });
            return true;
        }

        public void RemoveListeners(Hero hero) => heroListeners.Remove(hero);
        public void RemoveListeners(Agent agent) => agentListeners.Remove(agent);

        public bool HasListeners(Hero hero) => heroListeners.ContainsKey(hero);
        public bool HasListeners(Agent agent) => agentListeners.ContainsKey(agent);

        public override void OnAgentCreated(Agent agent)
        {
            ForAgent(agent, l => l.onAgentCreated?.Invoke(agent));
        }

        public override void OnAgentRemoved(Agent killedAgent, Agent killerAgent, AgentState agentState, KillingBlow blow)
        {
            ForAgent(killedAgent, l => l.onGotKilled?.Invoke(killedAgent, killerAgent, agentState));
            if (killerAgent != null)
            {
                ForAgent(killerAgent, l => l.onGotAKill?.Invoke(killerAgent, killedAgent, agentState));
            }
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            // Have to do this, as agent state become undefined after they are deleted 
            agentListeners.Remove(affectedAgent);
        }

        protected override void OnEndMission()
        {
            ForAll(listeners => listeners.onMissionOver?.Invoke());
        }

        private const float SlowTickDuration = 2;
        private float slowTick = 0;

        public override void OnMissionTick(float dt)
        {
            slowTick += dt;
            if (slowTick > SlowTickDuration)
            {
                slowTick -= SlowTickDuration;
                ForAll(listeners => listeners.onSlowTick?.Invoke(SlowTickDuration));
            }
            ForAll(listeners => listeners.onMissionTick?.Invoke(dt));
        }


        // public override void OnMissionActivate()
        // {
        //     base.OnMissionActivate();
        // }
        //
        // public override void OnMissionDeactivate()
        // {
        //     base.OnMissionDeactivate();
        // }
        //
        // public override void OnMissionRestart()
        // {
        //     base.OnMissionRestart();
        // }

        private Hero FindHero(Agent agent)
        {
            var hero = agent.GetAdoptedHero();
            if (hero == null) return null;
            return heroListeners.ContainsKey(hero) ? hero : null;
        }

        private void ForAll(Action<Listeners> action, [CallerMemberName] string callerName = "")
        {
            foreach (var listener in AllListeners)
            {
                SafeCall(() => action(listener));
            }
        }

        private void ForAgent(Agent agent, Action<Listeners> action, [CallerMemberName] string callerName = "")
        {
            var hero = FindHero(agent);
            if (hero != null && heroListeners.TryGetValue(hero, out var hl))
            {
                SafeCall(() => action(hl));
            }

            if (agentListeners.TryGetValue(agent, out var al))
            {
                SafeCall(() => action(al));
            }
        }
    }
}