using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Stores and manages "Reinforcement" (BLT extra militia) per settlement.
    /// Provides runtime bookkeeping for siege-parties created for the reinforcements.
    /// Persisted: _reinforcements (stringId -> int)
    /// Runtime-only: _openSiegeParties (settlementId -> list of party stringIds created for an active siege)
    /// </summary>
    public class ReinforcementBehavior : CampaignBehaviorBase
    {
        public static ReinforcementBehavior Current { get; private set; }

        // persisted across saves
        private Dictionary<string, int> _reinforcements = new();

        // runtime-only bookkeeping to map settlement -> list of party string ids we created for its current siege
        // Not persisted; rebuilt at runtime when we create parties.
        private readonly Dictionary<string, List<string>> _openSiegeParties = new();

        public ReinforcementBehavior()
        {
            Current = this;
        }

        public override void RegisterEvents()
        {
            // Use AfterSiegeCompletedEvent (IMbEvent<Settlement, MobileParty, bool, MapEvent.BattleTypes>)
            CampaignEvents.AfterSiegeCompletedEvent.AddNonSerializedListener(this, OnAfterSiegeCompleted);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Persist reinforcement counts keyed by settlement.StringId
            dataStore.SyncData("BLT_Reinforcements", ref _reinforcements);
        }

        private string KeyFor(Settlement settlement)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            return settlement.StringId;
        }

        // ----------------------
        // Persistence API
        // ----------------------

        public int GetReinforcements(Settlement settlement)
        {
            if (settlement == null) return 0;
            _reinforcements.TryGetValue(KeyFor(settlement), out int val);
            return Math.Max(0, val);
        }

        /// <summary>
        /// Overwrites the stored reinforcement count for a settlement.
        /// Use AddReinforcements / ReduceReinforcements instead for nicer semantics.
        /// </summary>
        public void SetReinforcements(Settlement settlement, int count)
        {
            if (settlement == null) return;
            var key = KeyFor(settlement);
            if (count <= 0) _reinforcements.Remove(key);
            else _reinforcements[key] = Math.Max(0, count);
        }

        /// <summary>
        /// Add up to amount reinforcements for settlement, respecting cap <=0 meaning no cap.
        /// Returns number actually added.
        /// </summary>
        public int AddReinforcements(Settlement settlement, int amount, int cap)
        {
            if (settlement == null) return 0;
            if (amount <= 0) return 0;
            var key = KeyFor(settlement);
            _reinforcements.TryGetValue(key, out int current);

            if (cap > 0)
            {
                var space = Math.Max(0, cap - current);
                var toAdd = Math.Min(space, amount);
                if (toAdd <= 0) return 0;
                _reinforcements[key] = current + toAdd;
                return toAdd;
            }
            else
            {
                _reinforcements[key] = current + amount;
                return amount;
            }
        }

        /// <summary>
        /// Reduce stored reinforcements by amount (clamped). Returns how many were reduced.
        /// </summary>
        public int ReduceReinforcements(Settlement settlement, int amount)
        {
            if (settlement == null) return 0;
            if (amount <= 0) return 0;
            var key = KeyFor(settlement);
            _reinforcements.TryGetValue(key, out int current);
            var reduced = Math.Min(current, amount);
            var newVal = current - reduced;
            if (newVal <= 0) _reinforcements.Remove(key);
            else _reinforcements[key] = newVal;
            return reduced;
        }

        /// <summary>
        /// Wipe all reinforcements for settlement (called when a settlement falls).
        /// </summary>
        public void RemoveAllReinforcements(Settlement settlement)
        {
            if (settlement == null) return;
            _reinforcements.Remove(KeyFor(settlement));
        }

        public int GetRemainingCapacity(Settlement settlement, int cap)
        {
            if (settlement == null) return 0;
            if (cap <= 0) return int.MaxValue;
            var cur = GetReinforcements(settlement);
            return Math.Max(0, cap - cur);
        }

        // ----------------------
        // Runtime siege-party bookkeeping
        // ----------------------

        /// <summary>
        /// Register a party stringId as created by our system for the given settlement's active siege.
        /// Called by the Harmony patch that spawns the party at siege start.
        /// </summary>
        public void RegisterSiegeParty(Settlement settlement, string partyStringId)
        {
            if (settlement == null || string.IsNullOrEmpty(partyStringId)) return;
            var key = KeyFor(settlement);
            if (!_openSiegeParties.TryGetValue(key, out var list))
            {
                list = new List<string>();
                _openSiegeParties[key] = list;
            }
            if (!list.Contains(partyStringId)) list.Add(partyStringId);
        }

        /// <summary>
        /// Get the list of party ids we generated for an active siege on the settlement.
        /// </summary>
        public IReadOnlyList<string> GetRegisteredSiegePartiesForSettlement(Settlement settlement)
        {
            if (settlement == null) return Array.Empty<string>();
            if (_openSiegeParties.TryGetValue(KeyFor(settlement), out var list) && list != null)
                return list;
            return Array.Empty<string>();
        }

        /// <summary>
        /// Called when a siege completes - reconcile survivors for all parties we created for this settlement's siege.
        /// CampaignEvents.SiegeCompleted provides the settlement and whether attackers won; we use that to either wipe or update the stored reinforcements.
        /// </summary>
        // Note: method signature matches IMbEvent<Settlement, MobileParty, bool, MapEvent.BattleTypes>
        private void OnAfterSiegeCompleted(Settlement siegeSettlement, MobileParty attackerParty, bool attackersWon, TaleWorlds.CampaignSystem.MapEvents.MapEvent.BattleTypes battleType)
        {
            try
            {
                if (siegeSettlement == null) return;

                var key = siegeSettlement.StringId;

                // If attackers won (settlement captured), wipe the reinforcements
                if (attackersWon)
                {
                    RemoveAllReinforcements(siegeSettlement);
                    _openSiegeParties.Remove(key);
                    return;
                }

                // Otherwise reconcile survivors from parties we registered for this siege
                if (!_openSiegeParties.TryGetValue(key, out var partyIds) || partyIds == null || partyIds.Count == 0)
                    return;

                int totalSurvivors = 0;
                foreach (var id in partyIds.ToList())
                {
                    try
                    {
                        var party = MobileParty.All.FirstOrDefault(p => string.Equals(p.StringId, id, StringComparison.OrdinalIgnoreCase));
                        if (party == null) continue;

                        int survivors = 0;
                        // Prefer TotalManCount (healthy + wounded) where available
                        try
                        {
                            survivors = party.MemberRoster?.TotalManCount ?? 0;
                        }
                        catch
                        {
                            survivors = (party.MemberRoster?.TotalHealthyCount ?? 0) + (party.MemberRoster?.TotalWounded ?? 0);
                        }

                        totalSurvivors += survivors;

                        // Clear the roster to avoid leaving troops in the party after the siege
                        try { party.MemberRoster?.Clear(); } catch { }
                    }
                    catch (Exception exPart)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[BLT Reinforcement] error reconciling siege party {id}: {exPart.Message}"));
                    }
                }

                // Persist survivors as the new stored reinforcement count
                SetReinforcements(siegeSettlement, totalSurvivors);

                // cleanup runtime records
                _openSiegeParties.Remove(key);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[BLT Reinforcement] OnAfterSiegeCompleted failed: {ex.Message}"));
            }
        }
    }
}
