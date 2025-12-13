using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Siege;

namespace BLTAdoptAHero
{
    /// <summary>
    /// Stores and manages "Reinforcement" (BLT extra militia) per settlement.
    /// Provides runtime bookkeeping for siege-parties created for the reinforcements.
    /// Persisted: _reinforcements (stringId -> int), _eliteReinforcements (stringId -> int)
    /// Runtime-only: _openSiegeParties & _openEliteSiegeParties
    /// </summary>
    public class ReinforcementBehavior : CampaignBehaviorBase
    {
        public static ReinforcementBehavior Current { get; private set; }

        // persisted across saves
        private Dictionary<string, int> _reinforcements = new();
        private Dictionary<string, int> _eliteReinforcements = new();

        // runtime-only bookkeeping to map settlement -> list of party string ids we created for its current siege
        private Dictionary<string, List<string>> _openSiegeParties = new();
        private Dictionary<string, List<string>> _openEliteSiegeParties = new();

        public ReinforcementBehavior()
        {
            Current = this;
            Initialize();
        }

        private void Initialize()
        {
            _eliteReinforcements ??= new Dictionary<string, int>();
            _reinforcements ??= new Dictionary<string, int>();
            _openSiegeParties ??= new Dictionary<string, List<string>>();
            _openEliteSiegeParties ??= new Dictionary<string, List<string>>();
        }

        public override void RegisterEvents()
        {
            Initialize();

            CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(
                this, OnSiegeEventStarted);

            CampaignEvents.AfterSiegeCompletedEvent.AddNonSerializedListener(
                this, OnAfterSiegeCompleted);
        }


        public override void SyncData(IDataStore dataStore)
        {
            // Persist reinforcement counts keyed by settlement.StringId
            dataStore.SyncData("BLT_Reinforcements", ref _reinforcements);
            dataStore.SyncData("BLT_EliteReinforcements", ref _eliteReinforcements);
            // After load, dictionaries may still be null
            Initialize();
        }

        public void ClearOpenSiegeParties(Settlement settlement)
        {
            if (settlement == null) return;
            _openSiegeParties.Remove(settlement.StringId);
        }

        private string KeyFor(Settlement settlement)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            return settlement.StringId;
        }

        // ----------------------
        // Persistence API - Militia
        // ----------------------

        private void SpawnMilitiaParty(
    SiegeEvent siegeEvent,
    Settlement settlement,
    int count,
    CharacterObject meleeTroop, CharacterObject rangedTroop,
    string idSuffix,
    bool isElite)
        {
            if (count <= 0) return;

            string partyId =
                Campaign.Current.CampaignObjectManager
                    .FindNextUniqueStringId<MobileParty>(
                        $"blt_{idSuffix}_{settlement.StringId}");

            var party = MilitiaPartyComponent.CreateMilitiaParty(partyId, settlement);
            if (party == null) return;

            int meleeCount = count / 2 + (count % 2);
            int rangedCount = count - meleeCount;

            if (meleeTroop != null && meleeCount > 0)
                party.MemberRoster.AddToCounts(meleeTroop, meleeCount);

            if (rangedTroop != null && rangedCount > 0)
                party.MemberRoster.AddToCounts(rangedTroop, rangedCount);

            RegisterSiegeParty(settlement, party.StringId, isElite);
        }


        private void OnSiegeEventStarted(SiegeEvent siegeEvent)
        {
            try
            {
                if (siegeEvent == null) return;

                var settlement = siegeEvent.BesiegedSettlement;
                if (settlement == null) return;

                var culture = settlement.Culture;
                if (culture == null) return;

                // Prevent duplicate spawning (save/load safety)
                if (_openSiegeParties.ContainsKey(settlement.StringId))
                    return;

                // -------------------------
                // NORMAL BLT MILITIA
                // -------------------------
                int normalCount = GetReinforcements(settlement);
                if (normalCount > 0)
                {
                    SpawnMilitiaParty(
                    siegeEvent,
                    settlement,
                    normalCount,
                    culture.MeleeMilitiaTroop,
                    culture.RangedMilitiaTroop,
                    "reinforce",
                    isElite: false
                    );
                }

                // -------------------------
                // ELITE BLT MILITIA
                // -------------------------
                int eliteCount = GetEliteReinforcements(settlement);
                if (eliteCount > 0)
                {
                    SpawnMilitiaParty(
                    siegeEvent,
                    settlement,
                    normalCount,
                    culture.MeleeEliteMilitiaTroop,
                    culture.RangedEliteMilitiaTroop,
                    "elite_reinforce",
                    isElite: true
                    );
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"[BLT Reinforcement] OnSiegeEventStarted failed: {ex.Message}")
                );
            }
        }

        public int GetReinforcements(Settlement settlement)
        {
            if (settlement == null) return 0;

            _reinforcements ??= new Dictionary<string, int>();

            _reinforcements.TryGetValue(KeyFor(settlement), out int val);
            return Math.Max(0, val);
        }

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

        public void SetReinforcements(Settlement settlement, int count)
        {
            if (settlement == null) return;
            var key = KeyFor(settlement);
            if (count <= 0) _reinforcements.Remove(key);
            else _reinforcements[key] = Math.Max(0, count);
        }

        public int GetRemainingCapacity(Settlement settlement, int cap)
        {
            if (settlement == null) return 0;
            if (cap <= 0) return int.MaxValue;
            var cur = GetReinforcements(settlement);
            return Math.Max(0, cap - cur);
        }

        // ----------------------
        // Persistence API - Elite Militia
        // ----------------------

        public int GetEliteReinforcements(Settlement settlement)
        {
            if (settlement == null) return 0;

            _eliteReinforcements ??= new Dictionary<string, int>();

            _eliteReinforcements.TryGetValue(KeyFor(settlement), out int val);
            return Math.Max(0, val);
        }

        public int AddEliteReinforcements(Settlement settlement, int amount, int cap)
        {
            if (settlement == null) return 0;
            if (amount <= 0) return 0;
            var key = KeyFor(settlement);
            _eliteReinforcements.TryGetValue(key, out int current);

            if (cap > 0)
            {
                var space = Math.Max(0, cap - current);
                var toAdd = Math.Min(space, amount);
                if (toAdd <= 0) return 0;
                _eliteReinforcements[key] = current + toAdd;
                return toAdd;
            }
            else
            {
                _eliteReinforcements[key] = current + amount;
                return amount;
            }
        }

        public int ReduceEliteReinforcements(Settlement settlement, int amount)
        {
            if (settlement == null) return 0;
            if (amount <= 0) return 0;
            var key = KeyFor(settlement);
            _eliteReinforcements.TryGetValue(key, out int current);
            var reduced = Math.Min(current, amount);
            var newVal = current - reduced;
            if (newVal <= 0) _eliteReinforcements.Remove(key);
            else _eliteReinforcements[key] = newVal;
            return reduced;
        }

        public void SetEliteReinforcements(Settlement settlement, int count)
        {
            if (settlement == null) return;
            var key = KeyFor(settlement);
            if (count <= 0) _eliteReinforcements.Remove(key);
            else _eliteReinforcements[key] = Math.Max(0, count);
        }

        public int GetRemainingEliteCapacity(Settlement settlement, int cap)
        {
            if (settlement == null) return 0;
            if (cap <= 0) return int.MaxValue;
            var cur = GetEliteReinforcements(settlement);
            return Math.Max(0, cap - cur);
        }

        /// <summary>
        /// Wipe all reinforcements for settlement (called when a settlement falls).
        /// </summary>
        public void RemoveAllReinforcements(Settlement settlement)
        {
            if (settlement == null) return;
            _reinforcements.Remove(KeyFor(settlement));
            _eliteReinforcements.Remove(KeyFor(settlement));
        }

        // ----------------------
        // Runtime siege-party bookkeeping
        // ----------------------

        /// <summary>
        /// Register a party stringId as created by our system for the given settlement's active siege.
        /// Type: militia vs elite (isElite = true for elite)
        /// </summary>
        public void RegisterSiegeParty(Settlement settlement, string partyStringId, bool isElite)
        {
            if (settlement == null || string.IsNullOrEmpty(partyStringId)) return;
            var key = KeyFor(settlement);
            var dict = isElite ? _openEliteSiegeParties : _openSiegeParties;
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<string>();
                dict[key] = list;
            }
            if (!list.Contains(partyStringId)) list.Add(partyStringId);
        }

        /// <summary>
        /// Get the list of party ids we generated for an active siege on the settlement (militia or elite).
        /// </summary>
        public IReadOnlyList<string> GetRegisteredSiegePartiesForSettlement(Settlement settlement, bool isElite)
        {
            if (settlement == null) return Array.Empty<string>();
            var dict = isElite ? _openEliteSiegeParties : _openSiegeParties;
            if (dict.TryGetValue(KeyFor(settlement), out var list) && list != null)
                return list;
            return Array.Empty<string>();
        }

        /// <summary>
        /// Called when a siege completes - reconcile survivors for all parties we created for this settlement's siege.
        /// </summary>
        // Note: method signature matches IMbEvent<Settlement, MobileParty, bool, MapEvent.BattleTypes>
        private void OnAfterSiegeCompleted(Settlement siegeSettlement, MobileParty attackerParty, bool attackersWon, TaleWorlds.CampaignSystem.MapEvents.MapEvent.BattleTypes battleType)
        {
            try
            {
                if (siegeSettlement == null) return;

                var key = KeyFor(siegeSettlement);

                // If attackers won (settlement captured), wipe the reinforcements
                if (attackersWon)
                {
                    RemoveAllReinforcements(siegeSettlement);
                    _openSiegeParties.Remove(key);
                    _openEliteSiegeParties.Remove(key);
                    return;
                }

                // Reconcile both militia and elite parties
                int totalSurvivorsMilitia = ReconcilePartyListForSettlement(siegeSettlement, _openSiegeParties);
                int totalSurvivorsElite = ReconcilePartyListForSettlement(siegeSettlement, _openEliteSiegeParties);

                // Persist survivors as the new stored reinforcement counts
                SetReinforcements(siegeSettlement, totalSurvivorsMilitia);
                SetEliteReinforcements(siegeSettlement, totalSurvivorsElite);

                // cleanup runtime records
                _openSiegeParties.Remove(key);
                _openEliteSiegeParties.Remove(key);

            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[BLT Reinforcement] OnAfterSiegeCompleted failed: {ex.Message}"));
            }
        }

        private int ReconcilePartyListForSettlement(Settlement siegeSettlement, Dictionary<string, List<string>> dict)
        {
            if (siegeSettlement == null) return 0;
            var key = KeyFor(siegeSettlement);
            if (!dict.TryGetValue(key, out var partyIds) || partyIds == null || partyIds.Count == 0)
                return 0;

            int totalSurvivors = 0;
            foreach (var id in partyIds.ToList())
            {
                try
                {
                    var party = MobileParty.All.FirstOrDefault(p => string.Equals(p.StringId, id, StringComparison.OrdinalIgnoreCase));
                    if (party == null) continue;

                    int survivors = 0;
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

            return totalSurvivors;
        }
    }
}
