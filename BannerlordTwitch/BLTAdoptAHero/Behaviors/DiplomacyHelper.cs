using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BannerlordTwitch.Rewards;

namespace BLTAdoptAHero
{
    public class DiplomacyHelper : CampaignBehaviorBase
    {
        private readonly Dictionary<(IFaction, IFaction), bool> _blockedPeaceWars
            = new();

        // persistent backing store
        private List<(string, string)> _blockedPeaceIds = new();

        public override void RegisterEvents()
        {
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
            //CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener();
            //CampaignEvents.OnClanDestroyedEvent.AddNonSerializedListener();
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("BlockedPeaceWars", ref _blockedPeaceIds);

            if (dataStore.IsLoading)
            {
                _blockedPeaceWars.Clear();

                foreach (var (id1, id2) in _blockedPeaceIds)
                {
                    IFaction f1 = FindFaction(id1);
                    IFaction f2 = FindFaction(id2);

                    if (f1 != null && f2 != null)
                        _blockedPeaceWars[MakeKey(f1, f2)] = true;
                }
            }
            else if (dataStore.IsSaving)
            {
                _blockedPeaceIds.Clear();

                foreach (var key in _blockedPeaceWars.Keys)
                {
                    _blockedPeaceIds.Add((
                        GetFactionId(key.Item1),
                        GetFactionId(key.Item2)
                    ));
                }
            }
        }

        private void OnWarDeclared(
            IFaction faction1,
            IFaction faction2,
            DeclareWarAction.DeclareWarDetail declareWarDetail)
        {
            if (declareWarDetail != DeclareWarAction.DeclareWarDetail.CausedByRebellion &&
                declareWarDetail != DeclareWarAction.DeclareWarDetail.CausedByKingdomCreation)
                return;

            if (!IsAdoptedLeader(faction1) && !IsAdoptedLeader(faction2))
                return;

            _blockedPeaceWars[MakeKey(faction1, faction2)] = true;
        }

        private void OnMakePeace(
            IFaction faction1,
            IFaction faction2,
            MakePeaceAction.MakePeaceDetail peaceDetail)
        {
            _blockedPeaceWars.Remove(MakeKey(faction1, faction2));
        }

        public bool IsPeaceBlocked(IFaction faction1, IFaction faction2)
            => _blockedPeaceWars.ContainsKey(MakeKey(faction1, faction2));

        private static bool IsAdoptedLeader(IFaction faction)
            => faction?.Leader != null && faction.Leader.IsAdopted();

        private static (IFaction, IFaction) MakeKey(IFaction a, IFaction b)
            => a.GetHashCode() <= b.GetHashCode() ? (a, b) : (b, a);

        private static string GetFactionId(IFaction faction)
            => (faction as Kingdom)?.StringId;

        private static IFaction FindFaction(string id)
            => id == null ? null : Kingdom.All.FirstOrDefault(k => k.StringId == id);
    }

}
