using System;
using System.Linq;
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
        private enum DetachmentOrder { None, Hold, Follow, Navigate }

        private class DetachmentState
        {
            public HeroDetachment Detachment;
            public DetachmentOrder Order = DetachmentOrder.None;
            public WorldPosition HoldPosition;
            public WorldPosition NavigationTarget;
            //public WorldPosition WaypointTarget;
            public bool HasWaypoint;
            public float LastNavigationReissueTime;
            //public List<int> BlockedNavmeshIds = new();
        }
        

        private readonly Dictionary<Agent, DetachmentState> _detachments = new();

        public bool IsDetached(Agent agent) => _detachments.ContainsKey(agent);

        public bool TryGetDetachment(Agent agent, out HeroDetachment detachment)
        {
            if (_detachments.TryGetValue(agent, out var state))
            {
                detachment = state.Detachment;
                return true;
            }
            detachment = null;
            return false;
        }

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
            _detachments[agent] = new DetachmentState { Detachment = detachment };
            return null;
        }

        public string Attach(Agent agent)
        {
            if (!_detachments.TryGetValue(agent, out var state))
                return "Not detached";

            CleanupDetachment(agent, state);
            return null;
        }

        public string Charge(Agent agent)
        {
            if (!_detachments.TryGetValue(agent, out var state))
                return "Not detached";

            // Clear all scripted movement so AI is completely free
            agent.DisableScriptedMovement();
            agent.DisableScriptedCombatMovement();
            agent.SetScriptedCombatFlags(Agent.AISpecialCombatModeFlags.None);
            agent.SetScriptedFlags(Agent.AIScriptedFrameFlags.None);

            // Point at closest enemy formation
            var closestFormation = state.Detachment.ParentFormation?.CachedClosestEnemyFormation;
            if (closestFormation != null)
                agent.SetTargetFormationIndex(closestFormation.Formation.Index);

            state.Order = DetachmentOrder.None;
            return null;
        }

        public string Hold(Agent agent)
        {
            if (!_detachments.TryGetValue(agent, out var state))
                return "Not detached";

            state.HoldPosition = agent.GetWorldPosition();
            state.Order = DetachmentOrder.Hold;

            ApplyHold(agent, state);
            return null;
        }

        public string Follow(Agent agent)
        {
            if (!_detachments.TryGetValue(agent, out var state))
                return "Not detached";

            var parent = state.Detachment.ParentFormation;
            if (parent == null)
                return "No parent formation";

            // Clear scripted movement — tick will reapply behind median position
            agent.DisableScriptedMovement();
            agent.DisableScriptedCombatMovement();
            state.Order = DetachmentOrder.Follow;
            return null;
        }

        public string TargetDoor(Agent agent)
        {
            if (!_detachments.TryGetValue(agent, out var state))
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
                state.Order = DetachmentOrder.None;
                agent.SetScriptedTargetEntity(
                    nearestGate.GameEntity,
                    Agent.AISpecialCombatModeFlags.AttackEntity,
                    true);
            }
            else
            {// In TargetDoor and Walls, after finding target:
                var pos = nearestGate.MiddlePosition.Position;
                state.NavigationTarget = pos;
                state.Order = DetachmentOrder.Navigate;
                state.LastNavigationReissueTime = Mission.Current.CurrentTime;
                SetAgentNavigatingAggressively(agent);
                agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
            }

            return null;
        }

        public string Walls(Agent agent)
        {
            if (!_detachments.TryGetValue(agent, out var state))
                return "Not detached";

            state.Order = DetachmentOrder.None;
            agent.DisableScriptedMovement();
            agent.DisableScriptedCombatMovement();

            float nearestDist = float.MaxValue;
            WorldPosition targetPos = default;
            bool found = false;

            // Find nearest gate to use as an obstacle reference
            CastleGate nearestGate = null;
            float nearestGateDist = float.MaxValue;
            foreach (var obj in Mission.Current.ActiveMissionObjects)
            {
                if (obj is not CastleGate gate) continue;
                float dist = gate.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                if (dist < nearestGateDist)
                {
                    nearestGateDist = dist;
                    nearestGate = gate;
                }
            }

            // 1. Wall segments
            foreach (var obj in Mission.ActiveMissionObjects)
            {
                if (obj is not WallSegment wall) continue;
                if (!wall.IsBreachedWall && agent.Team.IsAttacker) continue;

                // Use TacticalPosition.Position — designer-placed, ground level, navmesh valid
                WorldPosition worldPos;
                if (agent.Team.IsAttacker)
                {
                    // AttackerWaitPosition is specifically for attackers approaching breach
                    if (wall.AttackerWaitPosition != null)
                        worldPos = wall.AttackerWaitPosition.Position;
                    else if (wall.AttackerWaitFrame.Origin.IsValid)
                        worldPos = wall.AttackerWaitFrame.Origin;
                    else
                        continue; // no valid position, skip
                }
                else
                {
                    // WaitPosition for defenders, fallback to MiddlePosition
                    if (wall.WaitPosition != null)
                        worldPos = wall.MiddlePosition.Position;
                    else if (wall.MiddlePosition != null)
                        worldPos = wall.WaitPosition.Position;
                    else
                        continue;
                }

                if (!worldPos.IsValid) continue;

                float dist = wall.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    targetPos = worldPos;
                    found = true;
                }
            }

            // 2. Siege towers
            if (!found && agent.Team.IsAttacker)
            {
                foreach (var obj in Mission.ActiveMissionObjects)
                {
                    if (obj is not SiegeTower tower) continue;
                    if (tower.IsDeactivated || !tower.HasArrivedAtTarget) continue;

                    float dist = tower.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        targetPos = new WorldPosition(Mission.Scene, tower.GameEntity.GlobalPosition);
                        found = true;
                    }
                }
            }

            // 3. Ladders
            if (!found && agent.Team.IsAttacker)
            {
                foreach (var obj in Mission.ActiveMissionObjects)
                {
                    if (obj is not SiegeLadder ladder) continue;
                    foreach (var sp in ladder.StandingPoints)
                    {
                        if (sp.IsDeactivated || sp.HasUser) continue;
                        float dist = sp.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            targetPos = new WorldPosition(sp.GameEntity.Scene, sp.GameEntity.GlobalPosition);
                            found = true;
                        }
                    }
                }
            }

            if (!found)
                return "No valid target (wall/tower/ladder)";

            // If there's a gate between agent and target, check if we should
            // use a waypoint to route around it
            if (nearestGate != null)
            {
                Vec2 agentPos2D = agent.Position.AsVec2;
                Vec2 targetPos2D = targetPos.AsVec2;
                Vec2 gatePos2D = nearestGate.GameEntity.GlobalPosition.AsVec2;

                // Check if gate is roughly between agent and target
                Vec2 agentToTarget = (targetPos2D - agentPos2D).Normalized();
                Vec2 agentToGate = gatePos2D - agentPos2D;
                float gateAlongPath = Vec2.DotProduct(agentToTarget, agentToGate);
                float gateLateralDist = (agentToGate - agentToTarget * gateAlongPath).Length;

                // Gate is in the way if it's roughly along the path and within ~8m laterally
                bool gateIsInWay = gateAlongPath > 0f
                    && gateAlongPath < agentToGate.Length
                    && gateLateralDist < 8f;

                if (gateIsInWay)
                {
                    // Find a waypoint to the side of the gate
                    // Use the gate's right vector to pick a side
                    MatrixFrame gateFrame = nearestGate.GameEntity.GetGlobalFrame();
                    Vec2 gateSide = gateFrame.rotation.s.AsVec2.Normalized();

                    // Pick the side that's closer to the target laterally
                    Vec2 waypointLeft = gatePos2D + gateSide * 12f;
                    Vec2 waypointRight = gatePos2D - gateSide * 12f;

                    Vec2 chosenWaypoint = waypointLeft.DistanceSquared(targetPos2D) < waypointRight.DistanceSquared(targetPos2D)
                        ? waypointLeft
                        : waypointRight;

                    // Build a WorldPosition for the waypoint
                    WorldPosition waypointWorldPos = agent.GetWorldPosition();
                    waypointWorldPos.SetVec2(chosenWaypoint);

                    // Only use waypoint if it's on the navmesh
                    if (waypointWorldPos.GetNavMesh() != UIntPtr.Zero)
                    {
                        // Store final target, navigate to waypoint first
                        state.NavigationTarget = targetPos;
                        //state.WaypointTarget = waypointWorldPos;
                        state.Order = DetachmentOrder.Navigate;
                        state.LastNavigationReissueTime = Mission.Current.CurrentTime;
                        state.HasWaypoint = true;

                        SetAgentNavigatingAggressively(agent);
                        agent.SetScriptedPosition(ref waypointWorldPos, false,
                            Agent.AIScriptedFrameFlags.NeverSlowDown);
                        return null;
                    }
                }
            }

            state.NavigationTarget = targetPos;
            state.HasWaypoint = false;
            state.Order = DetachmentOrder.Navigate;
            state.LastNavigationReissueTime = Mission.Current.CurrentTime;

            SetAgentNavigatingAggressively(agent);
            agent.SetScriptedPosition(ref targetPos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
            return null;
        }

        // --- Mission callbacks ---

        public override void OnMissionTick(float dt)
        {
            foreach (var kvp in _detachments)
            {
                var agent = kvp.Key;
                var state = kvp.Value;

                if (!agent.IsActive()) continue;

                switch (state.Order)
                {
                    case DetachmentOrder.Hold:
                        ApplyHold(agent, state);
                        break;

                    case DetachmentOrder.Follow:
                        ApplyFollow(agent, state);
                        break;

                    case DetachmentOrder.Navigate:
                        ApplyNavigate(agent, state);
                        break;
                }
            }
        }

        public override void OnAgentRemoved(Agent killedAgent, Agent killerAgent,
            AgentState agentState, KillingBlow blow)
        {
            if (_detachments.TryGetValue(killedAgent, out var state))
                CleanupDetachmentOnDeath(killedAgent, state);
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            _detachments.Remove(affectedAgent);
        }

        protected override void OnEndMission()
        {
            _detachments.Clear();
        }

        // --- Helpers ---

        private static void ApplyHold(Agent agent, DetachmentState state)
        {
            agent.DisableScriptedCombatMovement();
            var pos = state.HoldPosition;
            agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private static void ApplyFollow(Agent agent, DetachmentState state)
        {
            var parent = state.Detachment.ParentFormation;
            if (parent == null) return;

            // Target a point slightly behind the formation's median position
            var medianPos = parent.CachedMedianPosition;
            if (!medianPos.IsValid) return;

            // Offset behind the formation direction
            Vec2 behindOffset = -parent.Direction * 3f;
            var targetPos = medianPos;
            targetPos.SetVec2(medianPos.AsVec2 + behindOffset);

            agent.DisableScriptedCombatMovement();
            agent.SetScriptedPosition(ref targetPos, false, Agent.AIScriptedFrameFlags.None);
        }

        private static void ApplyNavigate(Agent agent, DetachmentState state)
        {
            const float ReissueInterval = 1.5f;
            const float ArrivedDistanceSq = 3f; // 3m radius

            float now = Mission.Current.CurrentTime;

            // Determine current target — waypoint first if we have one
            WorldPosition currentTarget = /*state.HasWaypoint
                ? state.WaypointTarget
                :*/ state.NavigationTarget;

            float distSq = agent.Position.AsVec2.DistanceSquared(currentTarget.AsVec2);

            if (distSq < ArrivedDistanceSq)
            {
                if (state.HasWaypoint)
                {
                    // Waypoint reached, now navigate to final target
                    state.HasWaypoint = false;
                    state.LastNavigationReissueTime = now;
                    var finalPos = state.NavigationTarget;
                    agent.SetScriptedPosition(ref finalPos, false,
                        Agent.AIScriptedFrameFlags.NeverSlowDown);
                    return;
                }

                // Final target reached
                //UnblockNavmeshIds(agent, state);
                state.HoldPosition = agent.GetWorldPosition();
                state.Order = DetachmentOrder.Hold;
                ClearAgentNavigatingAggressively(agent);
                ApplyHold(agent, state);
                return;
            }

            if (now - state.LastNavigationReissueTime > ReissueInterval)
            {
                state.LastNavigationReissueTime = now;
                agent.SetScriptedPosition(ref currentTarget, false,
                    Agent.AIScriptedFrameFlags.NeverSlowDown);
            }
        }

        //private static void UnblockNavmeshIds(Agent agent, DetachmentState state)
        //{
        //    foreach (var id in state.BlockedNavmeshIds)
        //        agent.SetAgentExcludeStateForFaceGroupId(id, false);
        //    state.BlockedNavmeshIds.Clear();
        //}

        private static void SetAgentNavigatingAggressively(Agent agent)
        {
            // Stop agent getting distracted by enemies en route
            agent.SetAutomaticTargetSelection(false);

            // DefaultDetached makes the agent navigate more aggressively
            // through crowds rather than waiting for space
            agent.HumanAIComponent?.SetBehaviorValueSet(
                HumanAIComponent.BehaviorValueSet.DefaultDetached);

            // NeverSlowDown prevents the agent slowing for obstacles
            agent.SetScriptedFlags(
                agent.GetScriptedFlags() | Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private static void ClearAgentNavigatingAggressively(Agent agent)
        {
            agent.SetAutomaticTargetSelection(true);
            agent.HumanAIComponent?.SetBehaviorValueSet(
                HumanAIComponent.BehaviorValueSet.Default);
            agent.SetScriptedFlags(
                agent.GetScriptedFlags() & ~Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private void CleanupDetachmentOnDeath(Agent agent, DetachmentState state)
        {
            //UnblockNavmeshIds(agent, state);
            ClearAgentNavigatingAggressively(agent);

            var detachment = state.Detachment;
            var formation = agent.Formation;

            // Just remove the agent from the detachment and leave it —
            // do NOT call AttachUnit on a dying agent, it causes the crash
            detachment.RemoveAgent(agent);

            if (formation != null)
                formation.LeaveDetachment(detachment);

            _detachments.Remove(agent);
        }

        // Normal cleanup for Attach command — full reattach is safe here
        private void CleanupDetachment(Agent agent, DetachmentState state)
        {
            //UnblockNavmeshIds(agent, state);
            ClearAgentNavigatingAggressively(agent);

            var detachment = state.Detachment;
            var formation = agent.Formation;

            detachment.RemoveAgent(agent);

            if (formation != null)
            {
                formation.LeaveDetachment(detachment);
                formation.AttachUnit(agent);
            }

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