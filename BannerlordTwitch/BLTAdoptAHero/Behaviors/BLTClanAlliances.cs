using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.SaveSystem;
using BannerlordTwitch.Util;

namespace BLTAdoptAHero.Behaviors
{
    public class BLTClanAllianceBehavior : CampaignBehaviorBase
    {
        private Listeners listeners = new Listeners();
        public override void RegisterEvents() 
        {
            listeners.RegisterEvents();
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, () =>
            {
                Cleanup();
            });
        }

        public override void SyncData(IDataStore dataStore) 
        {
            //dataStore.SyncData("BLT_ClanAlliances", ref _alliances);
            //dataStore.SyncData("BLT_AllianceIds", ref _alliances);
        }

        // Main list of alliances
        private static List<ClanAlliance> _alliances = new();

        // Helper list of all clan IDs in any alliance
        private static List<string> _allianceClanIds = new();

        public class ClanAlliance
        {
            public string Name;
            public List<Clan> Members = new();
        }

        // ------------------------
        // Alliance operations
        // ------------------------

        public static string RegisterAlliance(Clan clan, string name)
        {
            if (_alliances.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
                return $"Clan alliance name {name} already exists";


            var alliance = _alliances.FirstOrDefault(a => a.Members.Contains(clan));
            if (alliance != null)
                return "Your clan is already in an alliance";

            var newAlliance = new ClanAlliance
            {
                Name = name,
                Members = new List<Clan> { clan }
            };

            _alliances.Add(newAlliance);
            _allianceClanIds.Add(clan.StringId);

            var vassals = VassalBehavior.Current.GetVassalClans(clan);
            if (vassals != null && vassals.Count > 0)
            {
                foreach (var c in vassals)
                {
                    newAlliance.Members.Add(c);
                    _allianceClanIds.Add(c.StringId);
                }
            }

            return $"Created {name} alliance!";
        }

        public static string LeaveAlliance(Clan clan)
        {

            var alliance = _alliances.FirstOrDefault(a => a.Members.Contains(clan));
            if (alliance == null)
                return "Your clan is not in an alliance";

            alliance.Members.Remove(clan);
            _allianceClanIds.Remove(clan.StringId);

            var vassals = VassalBehavior.Current.GetVassalClans(clan);
            if (vassals != null && vassals.Count > 0)
            {
                foreach (var c in vassals)
                {
                    alliance.Members.Remove(c);
                    _allianceClanIds.Remove(c.StringId);
                }
            }

            return $"Clan {clan.Name} left alliance {alliance.Name}";
        }

        public static string JoinAlliance(Clan clan, string name)
        {

            if (_allianceClanIds.Contains(clan.StringId))
                return "Your clan is already in an alliance";

            var alliance = _alliances
                .FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

            if (alliance == null)
                return $"No alliance named {name}";       

            alliance.Members.Add(clan);
            _allianceClanIds.Add(clan.StringId);

            var vassals = VassalBehavior.Current.GetVassalClans(clan);
            if (vassals != null && vassals.Count > 0)
            {
                foreach (var c in vassals)
                {
                    alliance.Members.Add(c);
                    _allianceClanIds.Add(c.StringId);
                }
            }

            return $"Joined {alliance.Name} alliance";
        }

        public static string AllianceInfo(Clan clan)
        {
            var alliance = GetAllianceOfClan(clan);
            if (alliance == null)
                return "Your clan is not in an alliance";

            var sb = new StringBuilder();
            sb.Append($"==={alliance.Name}===");
            sb.Append($" Strength:{(int)alliance.Members.Sum(c => c.CurrentTotalStrength)} |");
            var vassalsBehavior = VassalBehavior.Current;

            // List independent members (not vassals)
            var independentMembers = alliance.Members.Where(c => !vassalsBehavior.IsVassal(c)).ToList();
            sb.Append("Members: ");
            sb.AppendLine(independentMembers.Count > 0
                ? string.Join(", ", independentMembers.Select(c => c.Name))
                : "None");

            // List vassals separately
            var vassals = alliance.Members.Where(c => vassalsBehavior.IsVassal(c)).ToList();
            sb.Append("Vassals: ");
            sb.AppendLine(vassals.Count > 0
                ? string.Join(", ", vassals.Select(c => $"{c.Name} (Master: {vassalsBehavior.GetMasterClan(c)?.Name.ToString() ?? "Unknown"})"))
                : "None");

            return sb.ToString();
        }

        public void Cleanup()
        {
            var dead = Clan.All.Where(c => c.IsEliminated).ToHashSet();

            foreach (var alliance in _alliances)
                alliance.Members.RemoveAll(c => dead.Contains(c));

            _alliances.RemoveAll(a => a.Members.Count == 0);

            // Rebuild helper list
            _allianceClanIds = _alliances.SelectMany(a => a.Members.Select(c => c.StringId)).ToList();
        }

        // ------------------------
        // Helper methods for membership checks
        // ------------------------
        public static bool IsInAnyAlliance(Clan clan)
        {
            return _allianceClanIds.Contains(clan.StringId);
        }

        public static ClanAlliance GetAllianceOfClan(Clan clan)
        {
            return _alliances.FirstOrDefault(a => a.Members.Contains(clan));
        }

        public static bool IsInAlliance(Clan clan, string allianceName)
        {
            return _alliances.Any(a =>
                string.Equals(a.Name, allianceName, StringComparison.OrdinalIgnoreCase) &&
                a.Members.Contains(clan));
        }

        // ------------------------
        // Event Listeners
        // ------------------------
        private class Listeners
        {
            public void RegisterEvents()
            {
                CampaignEvents.WarDeclared.AddNonSerializedListener(this, (faction1, faction2, detail) =>
                {
                    if (!faction1.MapFaction.IsClan && !faction2.MapFaction.IsClan) return;
                    var clan1 = Clan.All.FirstOrDefault(c => c.StringId == faction1.StringId);
                    var clan2 = Clan.All.FirstOrDefault(c => c.StringId == faction2.StringId);
                    if (clan1 == null && clan2 == null) return;

                    if ((clan1 == null || !IsInAnyAlliance(clan1)) && (clan2 == null || !IsInAnyAlliance(clan2))) return;

                    if (clan1 != null && IsInAnyAlliance(clan1))
                    {
                        var alliance1 = GetAllianceOfClan(clan1);
                        foreach (var member in alliance1.Members)
                        {
                            var memberClan = Clan.All.FirstOrDefault(c => c == member);
                            if (VassalBehavior.Current.IsVassal(memberClan)) continue;
                            if (memberClan != null)
                                DeclareWarAction.ApplyByCallToWarAgreement(memberClan.MapFaction, faction2);
                        }
                    }

                    if (clan2 != null && IsInAnyAlliance(clan2))
                    {
                        var alliance2 = GetAllianceOfClan(clan2);
                        foreach (var member in alliance2.Members)
                        {
                            var memberClan = Clan.All.FirstOrDefault(c => c == member);
                            if (VassalBehavior.Current.IsVassal(memberClan)) continue;
                            if (memberClan != null)
                                DeclareWarAction.ApplyByCallToWarAgreement(memberClan.MapFaction, faction1);
                        }
                    }
                });

                CampaignEvents.MakePeace.AddNonSerializedListener(this, (faction1, faction2, detail) =>
                {
                    if (!faction1.MapFaction.IsClan && !faction2.MapFaction.IsClan) return;
                    var clan1 = Clan.All.FirstOrDefault(c => c.StringId == faction1.StringId);
                    var clan2 = Clan.All.FirstOrDefault(c => c.StringId == faction2.StringId);
                    if (clan1 == null && clan2 == null) return;

                    if ((clan1 == null || !IsInAnyAlliance(clan1)) && (clan2 == null || !IsInAnyAlliance(clan2))) return;


                    // Make peace for all members of clan1's alliance
                    if (clan1 != null && IsInAnyAlliance(clan1))
                    {
                        var alliance1 = GetAllianceOfClan(clan1);
                        foreach (var member in alliance1.Members)
                        {
                            var memberClan = Clan.All.FirstOrDefault(c => c == member);
                            if (VassalBehavior.Current.IsVassal(memberClan)) continue;
                            if (memberClan != null)
                                MakePeaceAction.Apply(memberClan.MapFaction, faction2);
                        }
                    }

                    // Make peace for all members of clan2's alliance
                    if (clan2 != null && IsInAnyAlliance(clan2))
                    {
                        var alliance2 = GetAllianceOfClan(clan2);
                        foreach (var member in alliance2.Members)
                        {
                            var memberClan = Clan.All.FirstOrDefault(c => c == member);
                            if (VassalBehavior.Current.IsVassal(memberClan)) continue;
                            if (memberClan != null)
                                MakePeaceAction.Apply(memberClan.MapFaction, faction1);
                        }
                    }
                });

                CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, (clan, oldKingdom, newKingdom, detail, notif) =>
                {
                    if (oldKingdom != null) return;
                    if (!IsInAnyAlliance(clan)) return;

                    string result = LeaveAlliance(clan);
                    Log.LogFeedEvent(result);
                });
                CampaignEvents.OnClanDestroyedEvent.AddNonSerializedListener(this, clan =>
                {
                    if (!IsInAnyAlliance(clan)) return;

                    LeaveAlliance(clan);
                });
                
            }
        }
        //public class BLTAlliancesSaveDefiner : SaveableTypeDefiner
        //{
        //    public BLTAlliancesSaveDefiner() : base(918273646) { } // Unique ID for this definer

        //    protected override void DefineContainerDefinitions()
        //    {
        //        // Save the list of alliances
        //        ConstructContainerDefinition(typeof(List<BLTClanAllianceBehavior.ClanAlliance>));

        //        // Save the helper list of all clan IDs
        //        ConstructContainerDefinition(typeof(List<string>));

        //        // Save inner lists in ClanAlliance if necessary
        //        ConstructContainerDefinition(typeof(List<Clan>));
        //    }
        //}
    }
}