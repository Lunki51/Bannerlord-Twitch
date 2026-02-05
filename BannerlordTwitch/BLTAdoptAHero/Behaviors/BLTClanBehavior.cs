using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using BannerlordTwitch.Util;
using BannerlordTwitch.Helpers;
using BLTAdoptAHero;
using BannerlordTwitch.Rewards;

namespace BLTAdoptAHero
{
    public class BLTClanBehavior : CampaignBehaviorBase
    {
        private BLTFamily _bltFamily;
        private CampaignTime _lastFamilyInitTime;
        public BLTSocialSecurity SocialSecurity { get; } = new BLTSocialSecurity();
        //public BLTPartyCheck PartyCheck { get; } = new BLTPartyCheck();
        public BLTPrisoner _Prisoner { get; } = new BLTPrisoner();

        public override void RegisterEvents()
        {
            SocialSecurity.RegisterEvents();
            _Prisoner.RegisterEvents();
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, () =>
            {
                if (_bltFamily == null)
                {
                    _bltFamily = new BLTFamily();
                    _bltFamily.Initialize();
                    _lastFamilyInitTime = CampaignTime.Now;
                    _bltFamily.RegisterEvents();
                }
                else
                {
                    if ((CampaignTime.Now - _lastFamilyInitTime).ToDays >= 10)
                    {
                        _bltFamily.Initialize();
                        _lastFamilyInitTime = CampaignTime.Now;
                    }

                    _bltFamily.GiveDailyXpToFamily();
                    _bltFamily.AgeBLTChildren();
                }
            });
        }
        public override void SyncData(IDataStore dataStore)
        {

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
                    if (hero.Name.ToString().Contains(BLTAdoptAHeroModule.DevTag))
                    {
                        return true;
                    }
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
                    (!hero.Name.ToString().Contains(BLTAdoptAHeroModule.Tag) || !hero.Name.ToString().Contains(BLTAdoptAHeroModule.DevTag));

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

            public void AgeBLTChildren()
            {
                int growthRatePerDay = GlobalCommonConfig.Get().BLTChildAgeMult;
                growthRatePerDay -= 1;
                if (growthRatePerDay < 1)
                {
                    return;
                }
                var bltHeroes = Hero.AllAliveHeroes
                    .Where(h => h != null && h.IsAdopted())
                    .ToList();

                foreach (var bltHero in bltHeroes)
                {

                    foreach (var child in bltHero.Children)
                    {
                        if (child == null || child.IsDead)
                            continue;

                        if (child.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge)
                            child.SetBirthDay(child.BirthDay - CampaignTime.Days(growthRatePerDay));

                        foreach (var grandchild in child.Children)
                        {
                            if (grandchild == null || grandchild.IsDead || grandchild.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge)
                                continue;

                            // Apply growth rate
                            grandchild.SetBirthDay(grandchild.BirthDay - CampaignTime.Days(growthRatePerDay));

                        }
                    }
                }
            }

            // Equipment
            public void RegisterEvents()
            {
                CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, EquipBLTChildren);
                CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, UpdateEquipment);
            }

            private void UpdateEquipment()
            {
                var bltHeroes = Hero.AllAliveHeroes
                    .Where(h => h != null && h.IsAdopted())
                    .ToList();
                foreach (var bltHero in bltHeroes)
                {
                    var spouse = bltHero.Spouse;
                    if (spouse != null)
                    {                     
                        EquipBLTChildren(spouse);
                    }
                    foreach (var child in bltHero.Children)
                    {
                        EquipBLTChildren(child);
                        foreach (var grandchild in child.Children)
                        {
                            EquipBLTChildren(grandchild);
                        }
                    }
                }
            }

            private void EquipBLTChildren(Hero child)
            {
                if (child.IsChild || child.Culture == null)
                    return;
                if (child.IsDead)
                    return;

                float armor = child.CharacterObject.GetTotalArmorSum();
                if (armor < 100f)
                {
                    // Get standard noble armor from culture
                    var armorEquipment = GetStandardNobleArmor(child);
                    if (armorEquipment != null)
                    {
                        // Copy armor pieces only
                        child.BattleEquipment[EquipmentIndex.Head] = armorEquipment[EquipmentIndex.Head];
                        child.BattleEquipment[EquipmentIndex.Cape] = armorEquipment[EquipmentIndex.Cape];
                        child.BattleEquipment[EquipmentIndex.Body] = armorEquipment[EquipmentIndex.Body];
                        child.BattleEquipment[EquipmentIndex.Gloves] = armorEquipment[EquipmentIndex.Gloves];
                        child.BattleEquipment[EquipmentIndex.Leg] = armorEquipment[EquipmentIndex.Leg];
                    }
                }
                    
                // Equip weapons based on skills
                EquipWeaponsBySkill(child);

                // Equip horse if good riding skill
                if (child.GetSkillValue(DefaultSkills.Riding) > 100)
                {
                    EquipHorse(child);
                }
            }

            private Equipment GetStandardNobleArmor(Hero hero)
            {
                // Find noble equipment roster for culture

                var rosters = MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>();

                // 1) Try noble templates first
                var roster = rosters.FirstOrDefault(r =>
                    r.EquipmentCulture == hero.Culture &&
                    r.HasEquipmentFlags(EquipmentFlags.IsNobleTemplate) &&
                    r.HasEquipmentFlags(EquipmentFlags.IsCombatantTemplate));

                // 2) Fallback to combatant if no noble found
                if (roster == null)
                {
                    roster = rosters.FirstOrDefault(r =>
                        r.EquipmentCulture == hero.Culture &&
                        r.HasEquipmentFlags(EquipmentFlags.IsCombatantTemplate));
                }

                if (roster?.AllEquipments?.Count > 0)
                {
                    return roster.AllEquipments[MBRandom.RandomInt(roster.AllEquipments.Count)];
                }
                return null;
            }

            private void EquipWeaponsBySkill(Hero hero)
            {
                // Clear weapon slots
                for (int i = 0; i < 4; i++)
                {
                    hero.BattleEquipment[i] = EquipmentElement.Invalid;
                }

                // Get best melee skill
                var meleeSkills = new[]
                {
                    (skill: DefaultSkills.OneHanded, value: hero.GetSkillValue(DefaultSkills.OneHanded)),
                    (skill: DefaultSkills.TwoHanded, value: hero.GetSkillValue(DefaultSkills.TwoHanded)),
                    (skill: DefaultSkills.Polearm, value: hero.GetSkillValue(DefaultSkills.Polearm))
                }.OrderByDescending(s => s.value).ToList();

                var bestSkill = meleeSkills[0].skill;

                // Find weapon
                var weapon = CampaignHelpers.AllItems
                    .Where(i => i.RelevantSkill == bestSkill &&
                               i.Culture == hero.Culture &&
                               !i.NotMerchandise)
                    .OrderByDescending(i => i.Tier)
                    .FirstOrDefault();

                if (weapon != null)
                {
                    hero.BattleEquipment[EquipmentIndex.Weapon0] = new EquipmentElement(weapon);

                    // Add shield if one-handed
                    if (bestSkill == DefaultSkills.OneHanded)
                    {
                        var shield = CampaignHelpers.AllItems
                            .Where(i => i.ItemType == ItemObject.ItemTypeEnum.Shield &&
                                       i.Culture == hero.Culture &&
                                       !i.NotMerchandise)
                            .OrderByDescending(i => i.Tier)
                            .FirstOrDefault();

                        if (shield != null)
                        {
                            hero.BattleEquipment[EquipmentIndex.Weapon1] = new EquipmentElement(shield);
                        }
                    }
                }
            }

            private void EquipHorse(Hero hero)
            {
                var horse = CampaignHelpers.AllItems
                    .Where(i => i.ItemType == ItemObject.ItemTypeEnum.Horse &&
                               i.Culture == hero.Culture &&
                               !i.NotMerchandise &&
                               i.HorseComponent?.Monster?.FamilyType == 1)
                    .OrderByDescending(i => i.Tier)
                    .FirstOrDefault();

                if (horse != null)
                {
                    hero.BattleEquipment[EquipmentIndex.Horse] = new EquipmentElement(horse);

                    var saddle = CampaignHelpers.AllItems
                        .Where(i => i.ItemType == ItemObject.ItemTypeEnum.HorseHarness &&
                                   i.Culture == hero.Culture &&
                                   !i.NotMerchandise)
                        .OrderByDescending(i => i.Tier)
                        .FirstOrDefault();

                    if (saddle != null)
                    {
                        hero.BattleEquipment[EquipmentIndex.HorseHarness] = new EquipmentElement(saddle);
                    }
                }
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

                        if (leader.Gold <= 100000)
                            leader.Gold += 50000;
                        else if (clan.Influence <= 100 && !clan.IsUnderMercenaryService && clan.Kingdom != null)
                            clan.Influence += 250f;
                    }
                }
            }
        }

        public class BLTPrisoner
        {
            public void RegisterEvents()
            {
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            }
            private void OnDailyTick()
            {
                foreach (Hero hero in Hero.AllAliveHeroes)
                {
                    if (hero.IsAdopted() && hero.IsPrisoner)
                    {
                        if (hero.CaptivityStartTime.ElapsedDaysUntilNow >= 10)
                        {
                            EndCaptivityAction.ApplyByEscape(hero);
                        }
                    }
                }
            }
        }
    }
}