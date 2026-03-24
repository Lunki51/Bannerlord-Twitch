using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using BannerlordTwitch.Helpers;

namespace BLTAdoptAHero
{
    internal class BLTHeroDetachmentBehavior : AutoMissionBehavior<BLTHeroDetachmentBehavior>
    {
        private readonly Dictionary<Agent, HeroDetachment> _detachments = new();

        public bool IsDetached(Agent agent) => _detachments.ContainsKey(agent);

        public bool TryGetDetachment(Agent agent, out HeroDetachment detachment)
            => _detachments.TryGetValue(agent, out detachment);

        public string Detach(Agent agent)
        {
            if (_detachments.ContainsKey(agent))
                return "Already detached";

            var formation = agent.Formation;
            if (formation == null)
                return "No formation";

            var detachment = new HeroDetachment(formation);
            formation.JoinDetachment(detachment);
            detachment.AddAgentAtSlotIndex(agent, 0);
            _detachments[agent] = detachment;
            return null;
        }

        public string Attach(Agent agent)
        {
            if (!_detachments.TryGetValue(agent, out var detachment))
                return "Not detached";

            CleanupDetachment(agent, detachment);
            return null;
        }

        public string Charge(Agent agent)
        {
            if (!_detachments.TryGetValue(agent, out var detachment))
                return "Not detached";

            agent.DisableScriptedMovement();
            agent.DisableScriptedCombatMovement();
            agent.SetScriptedCombatFlags(Agent.AISpecialCombatModeFlags.None);

            var closestEnemy = detachment.ParentFormation?.CachedClosestEnemyFormation;
            if (closestEnemy != null)
                agent.SetTargetFormationIndex(closestEnemy.Formation.Index);

            return null;
        }

        public string Hold(Agent agent)
        {
            if (!_detachments.ContainsKey(agent))
                return "Not detached";

            agent.DisableScriptedMovement();
            agent.DisableScriptedCombatMovement();
            var pos = agent.GetWorldPosition();
            agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
            return null;
        }

        public string Mimic(Agent agent)
        {
            if (!_detachments.TryGetValue(agent, out var detachment))
                return "Not detached";

            var parent = detachment.ParentFormation;
            if (parent == null)
                return "No parent formation";

            agent.DisableScriptedMovement();
            agent.DisableScriptedCombatMovement();

            var order = parent.GetReadonlyMovementOrderReference();
            var pos = order.CreateNewOrderWorldPositionMT(parent, WorldPosition.WorldPositionEnforcedCache.NavMeshVec3);
            if (pos.IsValid)
                agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);

            return null;
        }

        public string TargetDoor(Agent agent)
        {
            if (!_detachments.ContainsKey(agent))
                return "Not detached";

            CastleGate nearestGate = null;
            float nearestDist = float.MaxValue;

            foreach (var obj in Mission.Current.ActiveMissionObjects)
            {
                if (obj is not CastleGate gate) continue;
                if (gate.IsGateOpen && agent.Team.IsAttacker) continue;

                float dist = gate.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestGate = gate;
                }
            }

            if (nearestGate == null)
                return "No gate found";

            agent.DisableScriptedMovement();
            if (agent.Team.IsAttacker)
            {                        
                agent.SetScriptedTargetEntity(
                nearestGate.GameEntity,
                Agent.AISpecialCombatModeFlags.AttackEntity,
                true);
            }
            else
            {
                var pos = nearestGate.DefenseWaitFrame.Origin;
                agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
            }

            return null;
        }

        public string Walls(Agent agent)
        {
            if (!_detachments.ContainsKey(agent))
                return "Not detached";

            float nearestDist;
            WorldPosition targetPos;

            // ─────────────────────────────
            // 1. WALLS
            // ─────────────────────────────
            nearestDist = float.MaxValue;
            targetPos = default;

            foreach (var obj in Mission.ActiveMissionObjects)
            {
                if (obj is not WallSegment wall)
                    continue;

                if (!wall.IsBreachedWall && agent.Team.IsAttacker)
                    continue;

                var pos = agent.Team.IsAttacker ? wall.AttackerWaitFrame : wall.DefenseWaitFrame;
                float dist = wall.GameEntity.GlobalPosition.DistanceSquared(agent.Position);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    targetPos = pos.Origin;
                }
            }

            if (nearestDist < float.MaxValue)
            {
                agent.SetScriptedPosition(ref targetPos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
                return null;
            }

            // ─────────────────────────────
            // 2. SIEGE TOWERS
            // ─────────────────────────────

            foreach (var obj in Mission.ActiveMissionObjects)
            {
                if (agent.Team.IsDefender)
                    break;

                if (obj is not SiegeTower tower)
                    continue;

                if (tower.IsDeactivated || !tower.HasArrivedAtTarget)
                    continue;

                var pos = tower.GameEntity.GlobalPosition;
                float dist = tower.GameEntity.GlobalPosition.DistanceSquared(agent.Position);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    targetPos = new WorldPosition(Mission.Scene, pos);
                }
            }

            if (nearestDist < float.MaxValue)
            {
                agent.SetScriptedPosition(ref targetPos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
                return null;
            }

            // ─────────────────────────────
            // 3. LADDERS
            // ─────────────────────────────
            StandingPoint nearestPoint = null;

            foreach (var obj in Mission.ActiveMissionObjects)
            {
                if (agent.Team.IsDefender)
                    break;

                if (obj is not SiegeLadder ladder)
                    continue;

                foreach (var sp in ladder.StandingPoints)
                {
                    if (sp.IsDeactivated || sp.HasUser)
                        continue;

                    float dist = sp.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestPoint = sp;
                    }
                }
            }

            if (nearestPoint == null)
                return "No valid target (wall/tower/ladder)";

            var worldPos = new WorldPosition(
                nearestPoint.GameEntity.Scene,
                nearestPoint.GameEntity.GlobalPosition);

            agent.SetScriptedPosition(ref worldPos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);

            return null;
        }

        public override void OnAgentRemoved(Agent killedAgent, Agent killerAgent,
            AgentState agentState, KillingBlow blow)
        {
            if (_detachments.TryGetValue(killedAgent, out var detachment))
                CleanupDetachment(killedAgent, detachment);
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            _detachments.Remove(affectedAgent);
        }

        protected override void OnEndMission()
        {
            foreach (var kvp in new Dictionary<Agent, HeroDetachment>(_detachments))
                CleanupDetachment(kvp.Key, kvp.Value);
        }

        private void CleanupDetachment(Agent agent, HeroDetachment detachment)
        {
            detachment.RemoveAgent(agent);
            agent.Formation?.AttachUnit(agent);
            agent.Formation?.LeaveDetachment(detachment);
            _detachments.Remove(agent);
        }
    }

    internal class HeroDetachment : IDetachment
    {
        public Formation ParentFormation { get; private set; }

        private readonly MBList<Formation> _userFormations = new();
        private readonly List<Agent> _agents = new();

        public MBReadOnlyList<Formation> UserFormations => _userFormations;
        public bool IsLoose => true;

        public HeroDetachment(Formation parent)
        {
            ParentFormation = parent;
        }

        public void AddAgent(Agent agent, int slotIndex = -1,
            Agent.AIScriptedFrameFlags customFlags = Agent.AIScriptedFrameFlags.None)
            => _agents.Add(agent);

        public void AddAgentAtSlotIndex(Agent agent, int slotIndex)
        {
            _agents.Add(agent);
            agent.Formation?.DetachUnit(agent, IsLoose);
            agent.Detachment = this;
            agent.SetDetachmentWeight(1f);
        }

        public void RemoveAgent(Agent agent)
        {
            _agents.Remove(agent);
            agent.DisableScriptedMovement();
            agent.DisableScriptedCombatMovement();
        }

        public void FormationStartUsing(Formation formation) => _userFormations.Add(formation);
        public void FormationStopUsing(Formation formation) => _userFormations.Remove(formation);
        public bool IsUsedByFormation(Formation formation) => _userFormations.Contains(formation);

        public void OnFormationLeave(Formation formation)
        {
            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                var agent = _agents[i];
                if (agent.Formation == formation)
                {
                    RemoveAgent(agent);
                    formation.AttachUnit(agent);
                }
            }
        }

        // Returns null = no fixed frame, agent moves freely (same as ClimbingMachineDetachment)
        public WorldFrame? GetAgentFrame(Agent agent) => null;

        public bool IsAgentUsingOrInterested(Agent agent) => _agents.Contains(agent);
        public bool IsAgentEligible(Agent agent) => _agents.Contains(agent);
        public bool IsStandingPointAvailableForAgent(Agent agent) => false;
        public int GetNumberOfUsableSlots() => int.MaxValue;
        public Agent GetMovingAgentAtSlotIndex(int slotIndex)
            => slotIndex < _agents.Count ? _agents[slotIndex] : null;

        // All scoring worst values — AI never auto-assigns
        public float GetDetachmentWeight(BattleSideEnum side) => float.MinValue;
        public float ComputeAndCacheDetachmentWeight(BattleSideEnum side) => float.MinValue;
        public float GetDetachmentWeightFromCache() => float.MinValue;
        public float? GetWeightOfNextSlot(BattleSideEnum side) => null;
        public float GetWeightOfOccupiedSlot(Agent agent) => float.MinValue;
        public float? GetWeightOfAgentAtOccupiedSlot(Agent detachedAgent, List<Agent> candidates, out Agent match)
        { match = null; return float.MaxValue; }
        public float? GetWeightOfAgentAtNextSlot(List<Agent> candidates, out Agent match)
        { match = null; return null; }
        public float? GetWeightOfAgentAtNextSlot(List<ValueTuple<Agent, float>> agentTemplateScores, out Agent match)
        { match = null; return null; }
        public float GetTemplateWeightOfAgent(Agent candidate) => float.MaxValue;
        public List<float> GetTemplateCostsOfAgent(Agent candidate, List<float> oldValue) => oldValue;
        public float GetExactCostOfAgentAtSlot(Agent candidate, int slotIndex) => float.MaxValue;

        public void GetSlotIndexWeightTuples(List<ValueTuple<int, float>> slotIndexWeightTuples) { }
        public bool IsSlotAtIndexAvailableForAgent(int slotIndex, Agent agent) => false;
        public void MarkSlotAtIndex(int slotIndex) { }
        public void UnmarkDetachment() { }
        public bool IsDetachmentRecentlyEvaluated() => true;
        public void ResetEvaluation() { }
        public bool IsEvaluated() => true;
        public void SetAsEvaluated() { }
    }
}