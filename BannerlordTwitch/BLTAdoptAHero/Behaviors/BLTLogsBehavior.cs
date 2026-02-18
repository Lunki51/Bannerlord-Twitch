using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BannerlordTwitch;
using BannerlordTwitch.Annotations;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.SaveSystem;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Actions;
using BLTAdoptAHero.Achievements;
using BLTAdoptAHero.UI;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using TaleWorlds.CampaignSystem.Party;

namespace BLTAdoptAHero.Behaviors
{
    public class BLTLogsBehavior : CampaignBehaviorBase
    {
        // Helpers
        public static List<Hero> _heroes = BLTAdoptAHeroCampaignBehavior.GetAllAdoptedHeroes().ToList();
        public static List<Clan> _clans = Clan.All.Where(c => c.Leader != null && c.Leader.IsAdopted()).ToList();

        public HeroLogs heroLogs { get; } = new HeroLogs();
        public ClanLogs clanLogs { get; } = new ClanLogs();
        public KingdomLogs kingdomLogs { get; } = new KingdomLogs();
        public FiefLogs fiefLogs { get; } = new FiefLogs();

        public override void RegisterEvents()
        {
            heroLogs.RegisterEvents();
            clanLogs.RegisterEvents();
            kingdomLogs.RegisterEvents();
            fiefLogs.RegisterEvents();

            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, () =>
            {
                var heroesToClean = heroLogs._heroLogs.Keys.ToList();
                foreach (var heroId in heroesToClean)
                {
                    HeroLogsCleanup(heroId);
                }

                var clansToClean = clanLogs._clanLogs.Keys.ToList();
                foreach (var clanId in clansToClean)
                {
                    ClanLogsCleanup(clanId);
                }

                var kingdomsToClean = kingdomLogs._kingdomLogs.Keys.ToList();
                foreach (var kingdomId in kingdomsToClean)
                {
                    KingdomLogsCleanup(kingdomId);
                }
            });
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("BLT_HeroLogs", ref heroLogs._heroLogs);
            dataStore.SyncData("BLT_ClanLogs", ref clanLogs._clanLogs);
            dataStore.SyncData("BLT_KingdomLogs", ref kingdomLogs._kingdomLogs);
            dataStore.SyncData("BLT_FiefLogs", ref fiefLogs._fiefLogs);
        }

        #region Hero
        public class HeroLogs
        {
            private int maxLogs = CampaignLogs.CurrentSettings?.hLogs ?? 0;
            public Dictionary<string, List<string>> _heroLogs = new();
            public void RegisterEvents()
            {
                if (maxLogs == 0) return;
                //Battle results
                CampaignEvents.MapEventEnded.AddNonSerializedListener(this, mapEvent =>
                {
                    string eventType = mapEvent.EventType switch
                    {
                        MapEvent.BattleTypes.FieldBattle => "Field battle",
                        MapEvent.BattleTypes.Raid => "Raid",
                        MapEvent.BattleTypes.Siege => "Siege",
                        MapEvent.BattleTypes.Hideout => "Hideout battle",
                        MapEvent.BattleTypes.SallyOut => "Sally out",
                        MapEvent.BattleTypes.SiegeOutside => "Outside siege",
                        _ => "unknown battle"
                    };
                    foreach (var p in mapEvent.InvolvedParties)
                    {
                        var hero = p.MobileParty?.LeaderHero;
                        if (hero == null || !hero.IsAdopted())
                            continue;

                        var date = mapEvent.BattleStartTime;
                        var heroSide = hero.PartyBelongedTo.MapEventSide;
                        bool won = mapEvent.Winner == heroSide;
                        var enemySide = heroSide.OtherSide;

                        string enemyPartyName = enemySide.LeaderParty?.Name?.ToString() ?? "unknown party";
                        string enemyFactionName = enemySide.LeaderParty?.MapFaction?.Name?.ToString() ?? "unknown faction";

                        string battleLog = $"[{date}]{eventType} against {enemyPartyName} ({enemyFactionName})({heroSide.HealthyTroopCountAtMapEventStart} vs {enemySide.HealthyTroopCountAtMapEventStart}) - {(won ? "Victory" : "Defeat")}";

                        if (!_heroLogs.TryGetValue(hero.StringId, out var logs))
                        {
                            logs = new List<string>();
                            _heroLogs[hero.StringId] = logs;
                        }
                        if (logs.Count >= maxLogs)
                            logs.RemoveAt(0);
                        logs.Add(battleLog);
                    }
                    
                });

                //Imprison
                CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, (party, hero) =>
                { 
                    if (hero.IsAdopted())
                    {
                        var date = CampaignTime.Now;

                        string prisonLog = $"[{date}]Taken prisoner {(party != null ? $"by {party.Name}" : "")}";

                        if (!_heroLogs.TryGetValue(hero.StringId, out var logs))
                        {
                            logs = new List<string>();
                            _heroLogs[hero.StringId] = logs;
                        }
                        if (logs.Count >= maxLogs)
                            logs.RemoveAt(0);
                        logs.Add(prisonLog);
                    }
                });

                //Release
                CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, (hero, party, faction, detail, wasBattle) =>
                {
                    if (hero.IsAdopted())
                    {
                        string reason = detail switch
                        {
                            EndCaptivityDetail.Ransom => "ransom",
                            EndCaptivityDetail.ReleasedAfterPeace => "peace",
                            EndCaptivityDetail.ReleasedAfterBattle => "battle",
                            EndCaptivityDetail.ReleasedAfterEscape => "escape",
                            EndCaptivityDetail.Death => "death",
                            _ => "compensation"
                        };
                        var date = CampaignTime.Now;
                        string releaseLog = $"[{date}]Released {(faction?.Name != null ? $"from {faction.Name}" : "")} by {reason}";

                        if (!_heroLogs.TryGetValue(hero.StringId, out var logs))
                        {
                            logs = new List<string>();
                            _heroLogs[hero.StringId] = logs;
                        }
                        if (logs.Count >= maxLogs)
                            logs.RemoveAt(0);
                        logs.Add(releaseLog);
                    }
                });
            }
        }
        #endregion

        #region Clan
        public class ClanLogs
        {
            private int maxLogs = CampaignLogs.CurrentSettings?.cLogs ?? 0;
            public Dictionary<string, List<string>> _clanLogs = new();
            public void RegisterEvents()
            {
                if (maxLogs == 0) return;
                //Births
                CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, (mother, newborns, dead) => 
                {
                    if (newborns.Count == 0) return;

                    var clan = mother.Clan;
                    if (!_clans.Contains(clan)) return;

                    var date = CampaignTime.Now;

                    string fatherName = newborns[0].Father.FirstName.ToString() ?? "Unknown";
                    string motherName = mother.FirstName.ToString() ?? "Unknown";

                    string childrenNames = newborns.Count switch
                    {
                        1 => newborns[0].Name.ToString(),
                        2 => $"{newborns[0].Name} and {newborns[1].Name}",
                        _ => string.Join(", ", newborns.Take(newborns.Count - 1).Select(c => c.Name.ToString()))
                             + $", and {newborns.Last().Name}"
                    };

                    string birthType = newborns.Count > 1 ? $" ({newborns.Count} children)" : "";

                    string birthLog = $"[{date}]{childrenNames}{birthType} has been born to {fatherName} and {motherName}";

                    if (!_clanLogs.TryGetValue(clan.StringId, out var logs))
                    {
                        logs = new List<string>();
                        _clanLogs[clan.StringId] = logs;
                    }
                    if (logs.Count >= maxLogs)
                        logs.RemoveAt(0);
                    logs.Add(birthLog);
                });

                //Marriages
                CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, (hero1, hero2, notif) => 
                {
                    if (_clans.Contains(hero1.Clan) && _clans.Contains(hero2.Clan)) return;
                    var date = CampaignTime.Now;
                    var clan1 = hero1.Clan;
                    var clan2 = hero2.Clan;

                    if (_clans.Contains(clan1))
                    {
                        string marryLog1 = $"[{date}]{hero1.Name} has married {hero2.Name} of {clan2.Name}";
                        if (!_clanLogs.TryGetValue(clan1.StringId, out var logs))
                        {
                            logs = new List<string>();
                            _clanLogs[clan1.StringId] = logs;
                        }
                        if (logs.Count >= maxLogs)
                            logs.RemoveAt(0);
                        logs.Add(marryLog1);
                    }

                    if (_clans.Contains(clan2))
                    {
                        string marryLog2 = $"[{date}]{hero2.Name} has married {hero1.Name} of {clan1.Name}";
                        if (!_clanLogs.TryGetValue(clan2.StringId, out var logs))
                        {
                            logs = new List<string>();
                            _clanLogs[clan2.StringId] = logs;
                        }
                        if (logs.Count >= maxLogs)
                            logs.RemoveAt(0);
                        logs.Add(marryLog2);
                    }
                });

                //Deaths
                CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, (victim, killer, DetachmentData, notif) => 
                {
                    if (!_clans.Contains(victim.Clan)) return;
                    var date = CampaignTime.Now;

                    string deathLog = $"[{date}]{victim.Name} {(killer == null ? "has died" : $"was killed by {killer.Name}{(killer.Clan == null ? "" : $" of {killer.Clan.Name}")}")}";

                    if (!_clanLogs.TryGetValue(victim.Clan.StringId, out var logs))
                    {
                        logs = new List<string>();
                        _clanLogs[victim.Clan.StringId] = logs;
                    }
                    if (logs.Count >= maxLogs)
                        logs.RemoveAt(0);
                    logs.Add(deathLog);
                });
            }
        }
        #endregion

        #region Kingdom
        public class KingdomLogs
        {
            private int maxLogs = CampaignLogs.CurrentSettings?.kLogs ?? 0;
            public Dictionary<string, List<string>> _kingdomLogs = new();
            public void RegisterEvents()
            {
                if (maxLogs == 0) return;
                //War
                CampaignEvents.WarDeclared.AddNonSerializedListener(this, (faction1, faction2, detail) => 
                {
                    if (!faction1.IsKingdomFaction && !faction2.IsKingdomFaction) return;
                    var date = CampaignTime.Now;
                    if (faction1.IsKingdomFaction)
                    {
                        Kingdom kingdom1 = Kingdom.All.FirstOrDefault(k => k.MapFaction == faction1);
                        string warLog1 = $"[{date}]Declared war on {faction2.Name}";

                        if (!_kingdomLogs.TryGetValue(kingdom1.StringId, out var logs))
                        {
                            logs = new List<string>();
                            _kingdomLogs[kingdom1.StringId] = logs;
                        }
                        if (logs.Count >= maxLogs)
                            logs.RemoveAt(0);
                        logs.Add(warLog1);
                    }
                    if (faction2.IsKingdomFaction)
                    {
                        Kingdom kingdom2 = Kingdom.All.FirstOrDefault(k => k.MapFaction == faction2);
                        string warLog2 = $"[{date}]{faction1.Name} has declared war on your kingdom";

                        if (!_kingdomLogs.TryGetValue(kingdom2.StringId, out var logs))
                        {
                            logs = new List<string>();
                            _kingdomLogs[kingdom2.StringId] = logs;
                        }
                        if (logs.Count >= maxLogs)
                            logs.RemoveAt(0);
                        logs.Add(warLog2);
                    }                            
                });

                //Peace
                CampaignEvents.MakePeace.AddNonSerializedListener(this, (faction1, faction2, detail) =>
                {
                    if (!faction1.IsKingdomFaction && !faction2.IsKingdomFaction) return;
                    var date = CampaignTime.Now;
                    if (faction1.IsKingdomFaction)
                    {
                        Kingdom kingdom1 = Kingdom.All.FirstOrDefault(k => k.MapFaction == faction1);
                        string peaceLog1 = $"[{date}]Made peace with {faction2.Name}";

                        if (!_kingdomLogs.TryGetValue(kingdom1.StringId, out var logs))
                        {
                            logs = new List<string>();
                            _kingdomLogs[kingdom1.StringId] = logs;
                        }
                        if (logs.Count >= maxLogs)
                            logs.RemoveAt(0);
                        logs.Add(peaceLog1);
                    }
                    if (faction2.IsKingdomFaction)
                    {
                        Kingdom kingdom2 = Kingdom.All.FirstOrDefault(k => k.MapFaction == faction2);
                        string peaceLog2 = $"[{date}]Made peace with {faction1.Name}";

                        if (!_kingdomLogs.TryGetValue(kingdom2.StringId, out var logs))
                        {
                            logs = new List<string>();
                            _kingdomLogs[kingdom2.StringId] = logs;
                        }
                        if (logs.Count >= maxLogs)
                            logs.RemoveAt(0);
                        logs.Add(peaceLog2);
                    }
                });

                //Settlement owners
                CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, (fief, claim, newOwner, oldOwner, capturerHero, detail) =>
                {
                    var oldKingdom = oldOwner.Clan.Kingdom;
                    var newKingdom = newOwner.Clan.Kingdom;
                    if (oldKingdom == null && newKingdom == null) return;
                    if (oldKingdom == newKingdom) return;

                    string reason = detail switch
                    {
                        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.Default => "Default",
                        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege => "Siege",
                        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByBarter => "Barter",
                        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByLeaveFaction => "Leave",
                        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByKingDecision => "King",
                        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByGift => "Gift",
                        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByRebellion => "Rebellion",
                        _ => "Clan Destruction"
                    };
                    var date = CampaignTime.Now;
                    if (oldKingdom != null)
                    {
                        string kingdomOwnerLog1 = $"[{date}]{fief.Name} has been lost by: {reason}{(newOwner.MapFaction != null ? $" to {newOwner.MapFaction.Name}" : "")}";

                        if (!_kingdomLogs.TryGetValue(oldKingdom.StringId, out var logs))
                        {
                            logs = new List<string>();
                            _kingdomLogs[oldKingdom.StringId] = logs;
                        }
                        if (logs.Count >= maxLogs)
                            logs.RemoveAt(0);
                        logs.Add(kingdomOwnerLog1);
                    }
                    if (newKingdom != null)
                    {
                        string kingdomOwnerLog2 = $"[{date}]{fief.Name} has been obtained by: {reason}{(oldOwner.MapFaction != null ? $" from {oldOwner.MapFaction?.Name}" : "")}";

                        if (!_kingdomLogs.TryGetValue(newKingdom.StringId, out var logs))
                        {
                            logs = new List<string>();
                            _kingdomLogs[newKingdom.StringId] = logs;
                        }
                        if (logs.Count >= maxLogs)
                            logs.RemoveAt(0);
                        logs.Add(kingdomOwnerLog2);
                    }
                });
            }
        }
        #endregion

        #region Fief
        public class FiefLogs
        {
            private int maxLogs = CampaignLogs.CurrentSettings?.fLogs ?? 0;
            public Dictionary<string, List<string>> _fiefLogs = new();
            public void RegisterEvents()
            {
                if (maxLogs == 0) return;
                //Settlement owners
                CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, (fief, claim, newOwner, oldOwner, capturerHero, detail) =>
                {
                    var date = CampaignTime.Now;
                    var newClan = newOwner.Clan;
                    var oldClan = oldOwner.Clan;
                    var newKingdom = newClan.Kingdom;
                    var oldKingdom = oldClan.Kingdom;
                    if (oldClan == newClan) return;

                    string fiefOwnerLog = $"[{date}]Ownership changed from {oldClan.Name}{(oldKingdom != null ? $"({oldKingdom.Name})" : "")} to {newClan.Name}{(newKingdom != null ? $"({newKingdom.Name})" : "")}";
                    if (!_fiefLogs.TryGetValue(fief.Town.StringId, out var logs))
                    {
                        logs = new List<string>();
                        _fiefLogs[fief.Town.StringId] = logs;
                    }
                    if (logs.Count >= maxLogs)
                        logs.RemoveAt(0);
                    logs.Add(fiefOwnerLog);
                });

                //Sieges
                CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, (siegeEvent) =>
                {
                    var date = CampaignTime.Now;
                    var attackers = siegeEvent.BesiegerCamp.MapFaction;
                    var town = siegeEvent.BesiegedSettlement.Town;
                    var defendCount = siegeEvent.BesiegedSettlement.Parties.Sum(p => p.MemberRoster.TotalHealthyCount);
                    var attackCount = siegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType().Sum(p => p.MemberRoster.TotalHealthyCount);

                    string siegeLog = $"[{date}]Sieged by {attackers.Name} ({attackCount} attackers vs {defendCount} defenders).";
                    if (!_fiefLogs.TryGetValue(town.StringId, out var logs))
                    {
                        logs = new List<string>();
                        _fiefLogs[town.StringId] = logs;
                    }
                    if (logs.Count >= maxLogs)
                        logs.RemoveAt(0);
                    logs.Add(siegeLog);
                });
            }
        }
        #endregion

        #region CleanUp
        private void HeroLogsCleanup(string heroId)
        {
            var hero = Hero.FindFirst(h => h.StringId == heroId);
            if (hero == null || hero.IsDead || !hero.IsAdopted())
            {
                heroLogs._heroLogs.Remove(heroId);
            }
        }

        private void ClanLogsCleanup(string clanId)
        {
            var clan = Clan.FindFirst(c => c.StringId == clanId);
            if (clan == null || !_clans.Contains(clan) || clan.IsEliminated)
            {
                clanLogs._clanLogs.Remove(clanId);
            }
        }

        private void KingdomLogsCleanup(string kingdomId)
        {
            var kingdom = Kingdom.All.FirstOrDefault(k => k.StringId == kingdomId);
            if (kingdom == null || kingdom.IsEliminated)
            {
                kingdomLogs._kingdomLogs.Remove(kingdomId);
            }
        }
        #endregion

        public class BLTLogsSaveDefiner : SaveableTypeDefiner
        {
            public BLTLogsSaveDefiner() : base(918273645) { }

            protected override void DefineContainerDefinitions()
            {
                ConstructContainerDefinition(typeof(Dictionary<string, List<string>>));
                ConstructContainerDefinition(typeof(List<string>));
            }
        }
    }
}
