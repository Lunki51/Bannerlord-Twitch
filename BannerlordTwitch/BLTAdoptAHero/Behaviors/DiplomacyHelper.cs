using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace BLTAdoptAHero
{
    public class DiplomacyHelper : CampaignBehaviorBase
    {
        private readonly Dictionary<(IFaction, IFaction), bool> _blockedPeaceWars = new();

        // 1. Changed to two simple lists. 
        // TaleWorlds' serializer handles List<string> perfectly, but struggles with Tuples.
        private List<string> _savedFaction1Ids = new List<string>();
        private List<string> _savedFaction2Ids = new List<string>();

        public override void RegisterEvents()
        {
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Sync the two separate lists
            dataStore.SyncData("_blockedPeaceFaction1Ids", ref _savedFaction1Ids);
            dataStore.SyncData("_blockedPeaceFaction2Ids", ref _savedFaction2Ids);

            if (dataStore.IsLoading)
            {
                _blockedPeaceWars.Clear();

                // Safety check to ensure lists are synced
                if (_savedFaction1Ids.Count == _savedFaction2Ids.Count)
                {
                    for (int i = 0; i < _savedFaction1Ids.Count; i++)
                    {
                        var id1 = _savedFaction1Ids[i];
                        var id2 = _savedFaction2Ids[i];

                        IFaction f1 = FindFaction(id1);
                        IFaction f2 = FindFaction(id2);

                        if (f1 != null && f2 != null)
                            _blockedPeaceWars[MakeKey(f1, f2)] = true;
                    }
                }
            }
            else if (dataStore.IsSaving)
            {
                _savedFaction1Ids.Clear();
                _savedFaction2Ids.Clear();

                foreach (var key in _blockedPeaceWars.Keys)
                {
                    // Ensure we have valid IDs before adding
                    string id1 = GetFactionId(key.Item1);
                    string id2 = GetFactionId(key.Item2);

                    if (id1 != null && id2 != null)
                    {
                        _savedFaction1Ids.Add(id1);
                        _savedFaction2Ids.Add(id2);
                    }
                }
            }
        }

        private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
        {
            if (declareWarDetail != DeclareWarAction.DeclareWarDetail.CausedByRebellion &&
                declareWarDetail != DeclareWarAction.DeclareWarDetail.CausedByKingdomCreation)
                return;

            if (!IsAdoptedLeader(faction1) && !IsAdoptedLeader(faction2))
                return;

            _blockedPeaceWars[MakeKey(faction1, faction2)] = true;
        }

        private void OnMakePeace(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail peaceDetail)
        {
            _blockedPeaceWars.Remove(MakeKey(faction1, faction2));
        }

        public bool IsPeaceBlocked(IFaction faction1, IFaction faction2)
            => _blockedPeaceWars.ContainsKey(MakeKey(faction1, faction2));

        private static bool IsAdoptedLeader(IFaction faction)
            => faction?.Leader != null && faction.Leader.IsAdopted();

        private static (IFaction, IFaction) MakeKey(IFaction a, IFaction b)
            => a.GetHashCode() <= b.GetHashCode() ? (a, b) : (b, a);

        // 2. FIXED: Cast to MBObjectBase instead of Kingdom. 
        // This handles both Clans (Rebellions) and Kingdoms.
        private static string GetFactionId(IFaction faction)
            => (faction as MBObjectBase)?.StringId;

        // 3. FIXED: Search both Kingdoms AND Clans.
        private static IFaction FindFaction(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            // Try finding a Kingdom first
            IFaction kingdom = Kingdom.All.FirstOrDefault(k => k.StringId == id);
            if (kingdom != null) return kingdom;

            // If not found, try finding a Clan
            return Clan.All.FirstOrDefault(c => c.StringId == id);
        }
    }
}