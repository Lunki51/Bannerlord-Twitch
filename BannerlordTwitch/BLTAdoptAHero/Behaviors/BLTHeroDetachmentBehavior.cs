using System;
using System.Linq;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;

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
            public float LastNavigationReissueTime;
        }

        private readonly Dictionary<Agent, DetachmentState> _detachments = new();
        // Reused buffer to avoid allocation on every tick
        private readonly List<KeyValuePair<Agent, DetachmentState>> _tickBuffer = new();

        public bool IsDetached(Agent agent) => _detachments.ContainsKey(agent);

        public bool TryGetDetachment(Agent agent, out HeroDetachment detachment)
        {
            if (agent != null && _detachments.TryGetValue(agent, out var state))
            {
                detachment = state.Detachment;
                return true;
            }
            detachment = null;
            return false;
        }

        public string Detach(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (_detachments.ContainsKey(agent)) return "Already detached";

            var formation = agent.Formation;
            if (formation == null) return "No formation";

            try
            {
                var detachment = new HeroDetachment(formation);
                formation.JoinDetachment(detachment);

                
                if (agent.IsDetachedFromFormation)
                {
                    if (!agent.TryAttachToFormation())
                    {
                        return "Failed to detach";
                    }
                    
                }
                              
                agent.Formation?.DetachUnit(agent, false);
                detachment.AddAgentAtSlotIndex(agent, 0);
        
                _detachments[agent] = new DetachmentState { Detachment = detachment };
            }
            catch (Exception e)
            {
                Log.Error($"Detach failed for agent {agent?.Name ?? "unknown"}");
#if DEBUG
                Log.Trace(e.StackTrace);
#endif              
            }
            return null;
        }

        public string Attach(Agent agent)
        {
            if (agent == null) return "Invalid agent";
            if (!agent.IsDetachedFromFormation) return "Not detached";
            _detachments.TryGetValue(agent, out var state);
            if (state != null) CleanupDetachment(agent, state);
            else agent.Formation?.AttachUnit(agent);
            return null;
        }

        public string Charge(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";
            try
            {
                agent.DisableScriptedMovement();
                agent.DisableScriptedCombatMovement();
                agent.SetScriptedCombatFlags(Agent.AISpecialCombatModeFlags.None);
                agent.SetScriptedFlags(Agent.AIScriptedFrameFlags.None);
                agent.SetAutomaticTargetSelection(true);
                agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);

                Agent closestEnemy = null;
                float closestDist = float.MaxValue;

                //foreach (var team in Mission.Current.Teams)
                //{
                //    if (team == null || !team.IsEnemyOf(agent.Team)) continue;
                //    foreach (var enemy in team.ActiveAgents)
                //    {
                //        if (enemy == null || !enemy.IsActive() || enemy.IsMount) continue;
                //        float dist = enemy.Position.DistanceSquared(agent.Position);
                //        if (dist < closestDist)
                //        {
                //            closestDist = dist;
                //            closestEnemy = enemy;
                //        }
                //    }
                //}

                if (closestEnemy != null)
                {
                    var targetPos = closestEnemy.GetWorldPosition();
                    agent.SetScriptedPosition(ref targetPos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
                    state.NavigationTarget = targetPos;
                    state.LastNavigationReissueTime = Mission.Current.CurrentTime;
                    state.Order = DetachmentOrder.Navigate;
                }
                else
                {
                    var closestFormation = state.Detachment.ParentFormation?.CachedClosestEnemyFormation;
                    if (closestFormation != null)
                        agent.SetTargetFormationIndex(closestFormation.Formation.Index);
                    state.Order = DetachmentOrder.None;
                }
            }
            catch { }

            return null;
        }

        public string Hold(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";

            state.HoldPosition = agent.GetWorldPosition();
            state.Order = DetachmentOrder.Hold;
            ApplyHold(agent, state);
            return null;
        }

        public string Follow(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";

            try
            {
                var parent = state.Detachment?.ParentFormation;
                if (parent == null) return "No parent formation";

                agent.DisableScriptedMovement();
                agent.DisableScriptedCombatMovement();
                state.Order = DetachmentOrder.Follow;
            }
            catch { }

            return null;
        }

        public string TargetDoor(Agent agent)
        {
            if (!Mission.Current.IsSiegeBattle) return "Not a siege";
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";

            try
            {
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

                if (nearestGate == null) return "No gate found";

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
                {
                    WorldPosition pos = nearestGate.MiddlePosition != null
                        ? nearestGate.MiddlePosition.Position
                        : nearestGate.WaitPosition.Position;

                    if (!pos.IsValid) return "Gate has no valid position";

                    state.NavigationTarget = pos;
                    state.Order = DetachmentOrder.Navigate;
                    state.LastNavigationReissueTime = Mission.Current.CurrentTime;
                    SetAgentNavigatingAggressively(agent);
                    agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
                }
            }
            catch { }

            return null;
        }

        public string Walls(Agent agent)
        {
            if (!Mission.Current.IsSiegeBattle) return "Not a siege";
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";

            try
            {
                state.Order = DetachmentOrder.None;
                agent.DisableScriptedMovement();
                agent.DisableScriptedCombatMovement();

                float nearestDist = float.MaxValue;
                WorldPosition targetPos = default;
                bool found = false;

                // 1. Wall segments
                WallSegment bestWall = null;
                float bestWallDist = float.MaxValue;

                foreach (var obj in Mission.ActiveMissionObjects)
                {
                    if (obj is not WallSegment wall) continue;

                    if (!wall.IsBreachedWall && agent.Team.IsAttacker)
                        continue;

                    float dist = wall.GameEntity.GlobalPosition.DistanceSquared(agent.Position);

                    if (dist < bestWallDist)
                    {
                        bestWallDist = dist;
                        bestWall = wall;
                    }
                }

                if (bestWall != null)
                {

                    if (agent.Team.IsAttacker)
                    {
                        var tac = bestWall.MiddlePosition ?? bestWall.AttackerWaitPosition ?? bestWall.WaitPosition;

                        if (tac == null) return "No attacker pos";

                        targetPos = tac.Position;
                        found = true;
                    }
                    else
                    {
                        //if (bestWall.DefencePoints != null && bestWall.DefencePoints.Any())
                        //{
                        //    var point = bestWall.DefencePoints
                        //        .Select(dp =>
                        //        {
                        //            dp.PurgeInactiveDefenders();

                        //            var pos = dp.GameEntity.GlobalPosition;

                        //            float dist = pos.DistanceSquared(agent.Position);
                        //            int occupied = dp.CountOccupiedDefenderPositions();

                        //            float score = dist + occupied * 5f;

                        //            return new { dp, pos, score };
                        //        })
                        //        .OrderBy(x => x.score)
                        //        .FirstOrDefault();

                        //    if (point != null)
                        //    {
                        //        // small offset so multiple agents don't stack on exact same spot
                        //        Vec3 pos = point.pos;
                        //        Vec3 dir = (pos - agent.Position);
                        //        dir.z = 0;
                        //        dir.Normalize();

                        //        pos += dir * 1.5f;

                        //        var worldPos = new WorldPosition(Mission.Scene, pos);

                        //        if (worldPos.IsValid)
                        //        {
                        //            targetPos = worldPos;
                        //            found = true;
                        //        }
                        //    }
                        //}
                        //else
                        //{

                        targetPos = bestWall.MiddlePosition.Position;
                        found = true;

                        //Log.Trace("[Walls] No DefencePoint, fallback");
                        //}
                    }
                }

                // 2. Siege towers (attackers only)
                if (!found && agent.Team.IsAttacker)
                {
                    foreach (var obj in Mission.ActiveMissionObjects)
                    {
                        if (obj is not SiegeTower tower) continue;
                        if (tower.IsDeactivated /*|| !tower.HasArrivedAtTarget*/) continue;

                        float dist = tower.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            targetPos = new WorldPosition(Mission.Scene, tower.GameEntity.GlobalPosition);
                            found = true;
                        }
                    }
                }

                // 3. Ladders (attackers only)
                if (!found && agent.Team.IsAttacker)
                {
                    foreach (var obj in Mission.ActiveMissionObjects)
                    {
                        if (obj is not SiegeLadder ladder) continue;
                        foreach (var sp in ladder.StandingPoints)
                        {
                            if (sp == null || sp.IsDeactivated || sp.HasUser) continue;
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

                if (!found) return "No valid target (wall/tower/ladder)";

                state.NavigationTarget = targetPos;
                state.Order = DetachmentOrder.Navigate;
                state.LastNavigationReissueTime = Mission.Current.CurrentTime;
                SetAgentNavigatingAggressively(agent);
                agent.SetScriptedPosition(ref targetPos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
            }
            catch { }
            return null;
        }

        // --- Mission callbacks ---

        public override void OnMissionTick(float dt)
        {
            // Copy to buffer to avoid crash if _detachments is modified during iteration
            // (e.g. agent dies mid-tick triggering OnAgentRemoved)
            _tickBuffer.Clear();
            _tickBuffer.AddRange(_detachments);

            foreach (var kvp in _tickBuffer)
            {
                var agent = kvp.Key;
                var state = kvp.Value;

                // Re-check still in dict — could have been removed by OnAgentRemoved during tick
                if (!_detachments.ContainsKey(agent)) continue;
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
            if (killedAgent != null && _detachments.TryGetValue(killedAgent, out var state))
                CleanupDetachmentOnDeath(killedAgent, state);
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            if (affectedAgent != null)
                _detachments.Remove(affectedAgent);
        }

        protected override void OnEndMission()
        {
            _detachments.Clear();
            _tickBuffer.Clear();
        }

        // --- Helpers ---

        private static void ApplyHold(Agent agent, DetachmentState state)
        {
            if (!state.HoldPosition.IsValid) return;
            agent.DisableScriptedCombatMovement();
            var pos = state.HoldPosition;
            agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private static void ApplyFollow(Agent agent, DetachmentState state)
        {
            var parent = state.Detachment?.ParentFormation;
            if (parent == null) return;

            var medianPos = parent.CachedMedianPosition;
            if (!medianPos.IsValid) return;

            Vec2 behindOffset = -parent.Direction * 3f;
            var targetPos = medianPos;
            targetPos.SetVec2(medianPos.AsVec2 + behindOffset);

            agent.DisableScriptedCombatMovement();
            agent.SetScriptedPosition(ref targetPos, false, Agent.AIScriptedFrameFlags.None);
        }

        private static void ApplyNavigate(Agent agent, DetachmentState state)
        {
            const float ReissueInterval = 1.5f;
            const float ArrivedDistanceSq = 9f;

            if (!state.NavigationTarget.IsValid) return;

            float now = Mission.Current.CurrentTime;
            float distSq = agent.Position.AsVec2.DistanceSquared(state.NavigationTarget.AsVec2);

            if (distSq < ArrivedDistanceSq)
            {
                state.HoldPosition = agent.GetWorldPosition();
                state.Order = DetachmentOrder.Hold;
                ClearAgentNavigatingAggressively(agent);
                ApplyHold(agent, state);
                return;
            }

            if (now - state.LastNavigationReissueTime > ReissueInterval)
            {
                state.LastNavigationReissueTime = now;
                var pos = state.NavigationTarget;
                agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
            }
        }

        private static void SetAgentNavigatingAggressively(Agent agent)
        {
            agent.SetAutomaticTargetSelection(false);
            agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.DefaultDetached);
            agent.SetScriptedFlags(agent.GetScriptedFlags() | Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private static void ClearAgentNavigatingAggressively(Agent agent)
        {
            agent.SetAutomaticTargetSelection(true);
            agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);
            agent.SetScriptedFlags(agent.GetScriptedFlags() & ~Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private void CleanupDetachmentOnDeath(Agent agent, DetachmentState state)
        {
            // Safe to call on dying agent — does NOT call AttachUnit
            try { ClearAgentNavigatingAggressively(agent); } catch { }

            var detachment = state.Detachment;
            var formation = agent.Formation;

            try { detachment?.RemoveAgent(agent); } catch { }
            try { if (formation != null) formation.LeaveDetachment(detachment); } catch { }

            _detachments.Remove(agent);
        }

        private void CleanupDetachment(Agent agent, DetachmentState state)
        {
            ClearAgentNavigatingAggressively(agent);

            var detachment = state.Detachment;
            var formation = agent.Formation;

            // 1. remove from custom detachment first
            detachment?.RemoveAgent(agent);

            // 2. force engine state reset BEFORE reattach
            if (formation != null)
            {
                formation.LeaveDetachment(detachment);


                agent.TryRemoveAllDetachmentScores();
            }

            // 3. reattach cleanly
            if (formation != null)
            {
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
        {
            if (agent == null || _agents.Contains(agent)) return;
            _agents.Add(agent);
        }

        public void AddAgentAtSlotIndex(Agent agent, int slotIndex)
        {
            if (agent == null || _agents.Contains(agent)) return;
            _agents.Add(agent);

            var formation = agent.Formation;
            if (formation != null && !agent.IsDetachedFromFormation)
            {
                int fileIndex = ((IFormationUnit)agent).FormationFileIndex;
                int rankIndex = ((IFormationUnit)agent).FormationRankIndex;

                // FormationFileIndex == -1 means the agent is in the unpositioned list,
                // not in the 2D grid. DetachUnit -> LineFormation.RemoveUnit will crash
                // trying to null out _units2D[fileIndex, rankIndex] with bad indices.
                // Only call DetachUnit when indices are valid (agent is in the 2D grid).
                if (fileIndex >= 0 && rankIndex >= 0)
                {
                    try { formation.DetachUnit(agent, IsLoose); }
                    catch (Exception e)
                    {
                        Log.Error($"BLTHeroDetachment: DetachUnit failed for {agent.Name}");
#if DEBUG
                        Log.Trace(e.StackTrace);
#endif
                    }
                }
                // If fileIndex == -1, agent is unpositioned — skip DetachUnit entirely.
                // The agent will still get Detachment set below so our behavior works,
                // and the formation won't crash trying to remove from an invalid grid position.
            }

            agent.Detachment = this;
            agent.SetDetachmentWeight(1f);
        }

        public void RemoveAgent(Agent agent)
        {
            if (agent == null || !_agents.Contains(agent)) return;
            _agents.Remove(agent);
            // Guard: only call scripted movement disables if agent is still valid
            try { agent.DisableScriptedMovement(); } catch { }
            try { agent.DisableScriptedCombatMovement(); } catch { }
        }

        public void FormationStartUsing(Formation formation)
        {
            if (formation != null && !_userFormations.Contains(formation))
                _userFormations.Add(formation);
        }

        public void FormationStopUsing(Formation formation)
        {
            if (formation != null)
                _userFormations.Remove(formation);
        }

        public bool IsUsedByFormation(Formation formation)
            => formation != null && _userFormations.Contains(formation);

        public void OnFormationLeave(Formation formation)
        {
            if (formation == null) return;
            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                var agent = _agents[i];
                if (agent == null || agent.Formation != formation) continue;

                RemoveAgent(agent);

                // Only reattach if agent is still alive and active
                if (agent.IsActive())
                {
                    try { formation.AttachUnit(agent); } catch { }
                }
            }
        }

        public WorldFrame? GetAgentFrame(Agent agent) => null;

        public bool IsAgentUsingOrInterested(Agent agent)
            => agent != null && _agents.Contains(agent);

        public bool IsAgentEligible(Agent agent)
            => agent != null && _agents.Contains(agent);

        public bool IsStandingPointAvailableForAgent(Agent agent) => false;
        public int GetNumberOfUsableSlots() => int.MaxValue;

        public Agent GetMovingAgentAtSlotIndex(int slotIndex)
            => slotIndex >= 0 && slotIndex < _agents.Count ? _agents[slotIndex] : null;

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
        public List<float> GetTemplateCostsOfAgent(Agent candidate, List<float> oldValue) => oldValue ?? new List<float>();
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