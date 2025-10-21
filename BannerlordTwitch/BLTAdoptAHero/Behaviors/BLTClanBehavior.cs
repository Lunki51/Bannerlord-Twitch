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
    public class BLTClanBehavior : CampaignBehaviorBase
    {
        public BLTMarriageBehavior MarriageBehavior { get; } = new BLTMarriageBehavior();
        private BLTFamily _bltFamily;
        private CampaignTime _lastFamilyInitTime;
        public BLTSocialSecurity SocialSecurity { get; } = new BLTSocialSecurity();
        public BLTPartyCheck PartyCheck { get; } = new BLTPartyCheck();

        public override void RegisterEvents()
        {
            MarriageBehavior.RegisterEvents();
            SocialSecurity.RegisterEvents();
            PartyCheck.RegisterEvents();
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, () =>
            {
                if (_bltFamily == null)
                {
                    _bltFamily = new BLTFamily();
                    _bltFamily.Initialize();
                    _lastFamilyInitTime = CampaignTime.Now;
                }
                else
                {
                    if ((CampaignTime.Now - _lastFamilyInitTime).ToDays >= 10)
                    {
                        _bltFamily.Initialize();
                        _lastFamilyInitTime = CampaignTime.Now;
                    }

                    _bltFamily.GiveDailyXpToFamily();
                }
            });
        }
        public override void SyncData(IDataStore dataStore)
        {

        }

        public class BLTMarriageBehavior
        {
            private readonly Dictionary<Hero, Clan> _previousClan = new();
            public void RegisterEvents()
            {
                CampaignEvents.OnHeroChangedClanEvent.AddNonSerializedListener(this, OnHeroChangedClanEvent);
                CampaignEvents.HeroesMarried.AddNonSerializedListener(this, OnHeroesMarried);
            }
            private void OnHeroChangedClanEvent(Hero hero, Clan oldClan)
            {
                if (hero != null && !_previousClan.ContainsKey(hero))
                    _previousClan[hero] = oldClan;
            }
            private void OnHeroesMarried(Hero hero1, Hero hero2, bool showNotification)
            {
                _previousClan.TryGetValue(hero1, out Clan old1);
                _previousClan.TryGetValue(hero2, out Clan old2);

                bool h1WasBlt = old1?.Name.ToString().Contains("[BLT Clan]") ?? false;
                bool h2WasBlt = old2?.Name.ToString().Contains("[BLT Clan]") ?? false;

                if (h1WasBlt && !h2WasBlt && !hero2.IsClanLeader)
                {
                    hero1.Clan = old1;
                    hero2.Clan = old1;
                }
                else if (h2WasBlt && !h1WasBlt && !hero1.IsClanLeader)
                {
                    hero1.Clan = old2;
                    hero2.Clan = old2;
                }

                _previousClan.Remove(hero1);
                _previousClan.Remove(hero2);
            }
        }

        public class BLTFamily
        {
            private const int DailyXpAmount = 1500;
            public List<BLTFamilyData> FamilyList { get; private set; } = new();
            public void Initialize()
            {
                BuildFamilyList();
            }
            private void BuildFamilyList()
            {
                bool IsValidFamilyMember(Hero hero) =>
                    hero != null &&
                    !IsBltHero(hero) &&
                    hero.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge &&
                    hero.Occupation == Occupation.Lord &&
                    hero.Clan != null &&
                    hero.IsAlive;

                bool IsBltHero(Hero hero)
                {
                    if (hero == null || hero.Name == null)
                        return false;
                    return hero.Name.ToString().Contains(BLTAdoptAHeroModule.Tag);
                }

                // Filter only BLT heroes once
                var bltHeroes = Hero.AllAliveHeroes
                    .Where(hero => hero != null && IsBltHero(hero))
                    .ToList();

                // Build a dictionary for quick lookup/update instead of searching list repeatedly
                var familyDict = FamilyList.ToDictionary(f => f.BltHero, f => f);

                foreach (var hero in bltHeroes)
                {
                    if (!familyDict.TryGetValue(hero, out var familyData))
                    {
                        familyData = new BLTFamilyData { BltHero = hero };
                        familyDict[hero] = familyData;
                    }

                    var familyMembers = new List<Hero>();
                    var relatives = new Hero[] { hero.Spouse, hero.Father, hero.Mother }
                        .Where(IsValidFamilyMember);
                    var children = hero.Children.Where(IsValidFamilyMember);

                    familyMembers.AddRange(relatives);
                    familyMembers.AddRange(children);

                    familyData.FamilyMembers = familyMembers;
                }
                FamilyList = familyDict.Values.ToList();
                //Log.Trace("familyList");
            }
            public void GiveDailyXpToFamily()
            {
                bool IsEligibleForXp(Hero hero) =>
                    hero != null &&
                    !hero.IsDead &&
                    hero.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge &&
                    !hero.Name.ToString().Contains(BLTAdoptAHeroModule.Tag);

                SkillObject GetRelevantSkill(Hero hero)
                {
                    var equippedSkills = hero.BattleEquipment
                        .YieldFilledWeaponSlots()
                        .SelectMany(slot => slot.element.Item.Weapons?.Select(w => w.RelevantSkill) ?? Enumerable.Empty<SkillObject>())
                        .Where(skill => skill != null)
                        .Distinct()
                        .ToList();

                    return equippedSkills.Any() ? equippedSkills.GetRandomElement() : null;
                }

                void GiveXpToHero(Hero target)
                {
                    var relevantSkill = GetRelevantSkill(target) ?? DefaultSkills.Athletics;
                    target.AddSkillXp(relevantSkill, DailyXpAmount);
                    //Log.Trace("relevant");

                    var supportSkills = new[]
                    {
                        DefaultSkills.Leadership,
                        DefaultSkills.Steward,
                        DefaultSkills.Medicine,
                        DefaultSkills.Engineering,
                        DefaultSkills.Tactics,
                        DefaultSkills.Scouting,
                        DefaultSkills.Charm
                    };

                    target.AddSkillXp(supportSkills.GetRandomElement(), DailyXpAmount);
                    //Log.Trace("support");
                }

                foreach (var family in FamilyList)
                {
                    if (family.BltHero == null || family.BltHero.IsDead)
                        continue;

                    foreach (var member in family.FamilyMembers)
                    {
                        if (IsEligibleForXp(member))
                            GiveXpToHero(member);
                    }
                }
            }

            public class BLTFamilyData
            {
                public Hero BltHero { get; set; }
                public List<Hero> FamilyMembers { get; set; } = new List<Hero>();
            }
        }

        public class BLTSocialSecurity
        {
            public void RegisterEvents()
            {
                CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
            }

            private void OnWeeklyTick()
            {
                foreach (var clan in Clan.All)
                {
                    if (clan == null
                        || !clan.IsInitialized
                        || clan.IsEliminated
                        || clan.IsBanditFaction
                        || clan.IsMinorFaction
                        || clan.IsClanTypeMercenary
                        || clan.IsRebelClan)
                        continue;

                    var leader = clan.Leader;
                    if (leader == null || !leader.IsAlive)
                        continue;

                    if (leader.IsAdopted())
                    {
                        clan.Renown += 5f;

                        if (leader.Gold <= 0)
                            leader.Gold = 25000;
                        else if (clan.Influence <= 0 && !clan.IsUnderMercenaryService && clan.Kingdom != null)
                            clan.Influence = 100f;
                    }
                }
            }
        }
        //public class FIefs
        //{
        //    ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail
        //}
        public class BLTPartyCheck
        {
            public void RegisterEvents()
            {
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            }

            //public override void SyncData(IDataStore dataStore) { }

            private void OnDailyTick()
            {

                foreach (var party in MobileParty.All)
                {
                    if (party?.LeaderHero == null)
                        continue;

                    Hero leader = party.LeaderHero;

                    // Only apply to adopted heroes
                    if (!HeroExtensions.IsAdopted(leader))
                        continue;

                    CheckAndFixParty(party);
                }

            }

            private void CheckAndFixParty(MobileParty party)
            {
                var ai = party.Ai;
                if (ai == null)
                    return;

                bool isHolding = ai.DefaultBehavior == AiBehavior.Hold && (CampaignTime.Now - party.StationaryStartTime).ToHours > CampaignTime.HoursInDay;

                bool isDisabled = ai.IsDisabled;
                bool isStuck = ai.ForceAiNoPathMode || ai.Path == null || ai.NeedTargetReset;


                if (isHolding || isDisabled || isStuck)
                {
                    Log.LogFeedEvent(
                        $"[BLT] Resetting AI for {party.Name} (Behavior: {ai.DefaultBehavior}, Disabled: {ai.IsDisabled})");

                    ai.DisableAi();
                    ai.EnableAi();
                    ai.RethinkAtNextHourlyTick = true;
                    ai.CheckPartyNeedsUpdate();
                }
            }

        }
    }
}