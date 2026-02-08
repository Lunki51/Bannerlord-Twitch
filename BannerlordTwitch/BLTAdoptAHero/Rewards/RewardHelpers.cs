using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Actions.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace BLTAdoptAHero
{
    public static class RewardHelpers
    {
        public enum RewardType
        {
            Weapon,
            Armor,
            Mount,
            Shield
        }

        public static (ItemObject item, ItemModifier modifier, EquipmentIndex slot) GenerateRewardType(
            RewardType rewardType, int tier, Hero hero, HeroClassDef heroClass,
            bool allowDuplicates, RandomItemModifierDef modifierDef, string customItemName, float customItemPower)
        {
            return rewardType switch
            {
                RewardType.Weapon => GenerateRewardTypeWeapon(tier, hero, heroClass, allowDuplicates, modifierDef, customItemName, customItemPower),
                RewardType.Armor => GenerateRewardTypeArmor(tier, hero, heroClass, allowDuplicates, modifierDef, customItemName, customItemPower),
                RewardType.Mount => GenerateRewardTypeMount(tier, hero, heroClass, allowDuplicates, modifierDef, customItemName, customItemPower),
                RewardType.Shield => GenerateRewardTypeShield(tier, hero, heroClass, allowDuplicates, modifierDef, customItemName, customItemPower),
                _ => throw new ArgumentOutOfRangeException(nameof(rewardType), rewardType, null)
            };
        }

        public static (ItemObject item, ItemModifier modifier, EquipmentIndex slot) GenerateCulturedRewardType(
            RewardType rewardType, int tier, Hero hero, HeroClassDef heroClass,
            bool allowDuplicates, RandomItemModifierDef modifierDef, string customItemName, float customItemPower, CultureObject culture)
        {
            return rewardType switch
            {
                RewardType.Weapon => GenerateCulturedRewardTypeWeapon(tier, hero, heroClass, allowDuplicates, modifierDef, customItemName, customItemPower, culture),
                RewardType.Armor => GenerateCulturedRewardTypeArmor(tier, hero, heroClass, allowDuplicates, modifierDef, customItemName, customItemPower, culture),
                RewardType.Mount => GenerateCulturedRewardTypeMount(tier, hero, heroClass, allowDuplicates, modifierDef, customItemName, customItemPower, culture),
                RewardType.Shield => GenerateCulturedRewardTypeShield(tier, hero, heroClass, allowDuplicates, modifierDef, customItemName, customItemPower, culture),
                _ => throw new ArgumentOutOfRangeException(nameof(rewardType), rewardType, null)
            };
        }

        public static string AssignCustomReward(Hero hero, ItemObject item, ItemModifier itemModifier, EquipmentIndex slot)
        {
            var element = new EquipmentElement(item, itemModifier);
            bool isCustom = BLTCustomItemsCampaignBehavior.Current.IsRegistered(itemModifier);

            // Always add custom items to hero's storage
            if (isCustom)
            {
                BLTAdoptAHeroCampaignBehavior.Current.AddCustomItem(hero, element);
            }

            // If a specific slot was provided, try to equip to that slot only
            if (slot != EquipmentIndex.None)
            {
                return AssignToSpecificSlot(hero, element, slot, isCustom);
            }
            // If no specific slot, intelligently find and fill all matching slots
            else
            {
                return AssignToAllMatchingSlots(hero, element, isCustom);
            }
        }

        private static string AssignToSpecificSlot(Hero hero, EquipmentElement element, EquipmentIndex slot, bool isCustom)
        {
            var currentItem = hero.BattleEquipment[slot];

            // If slot is empty or new item is better, equip it
            if (currentItem.IsEmpty || ShouldReplaceItem(currentItem, element))
            {
                hero.BattleEquipment[slot] = element;
                return "{=RczvXuxP}received {ItemName}"
                    .Translate(("ItemName", GetItemNameAndModifiers(element)));
            }
            else if (!isCustom)
            {
                // Sell non-custom items that aren't good enough
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, element.Item.Value * 5);
                return "{=bNX0NiQ9}sold {ItemName} for {ItemValue}{GoldIcon} (not needed)"
                    .Translate(
                        ("ItemName", element.GetModifiedItemName()),
                        ("ItemValue", element.Item.Value),
                        ("GoldIcon", Naming.Gold));
            }
            else
            {
                // Custom item stored but not equipped
                return "{=RczvXuxP}received {ItemName}"
                    .Translate(("ItemName", GetItemNameAndModifiers(element)))
                    + " (" + "{=}not equipped".Translate() + ")";
            }
        }

        private static string AssignToAllMatchingSlots(Hero hero, EquipmentElement element, bool isCustom)
        {
            var validSlots = GetValidSlotsForItemType(element.Item);
            int slotsEquipped = 0;
            int slotsReplaced = 0;

            foreach (var slot in validSlots)
            {
                var currentItem = hero.BattleEquipment[slot];

                // Only equip if slot is empty or new item is better
                if (currentItem.IsEmpty)
                {
                    hero.BattleEquipment[slot] = element;
                    slotsEquipped++;
                }
                else if (currentItem.Item.ItemType == element.Item.ItemType && ShouldReplaceItem(currentItem, element))
                {
                    hero.BattleEquipment[slot] = element;
                    slotsReplaced++;
                }
            }

            int totalSlots = slotsEquipped + slotsReplaced;

            if (totalSlots > 0)
            {
                string message = "{=RczvXuxP}received {ItemName}".Translate(("ItemName", GetItemNameAndModifiers(element)));
                if (totalSlots > 1)
                {
                    message += $" ({totalSlots} slots)";
                }
                return message;
            }
            else if (!isCustom)
            {
                // Sell non-custom items that couldn't be equipped
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, element.Item.Value * 5);
                return "{=bNX0NiQ9}sold {ItemName} for {ItemValue}{GoldIcon} (not needed)"
                    .Translate(
                        ("ItemName", element.GetModifiedItemName()),
                        ("ItemValue", element.Item.Value),
                        ("GoldIcon", Naming.Gold));
            }
            else
            {
                // Custom item stored but not equipped
                return "{=RczvXuxP}received {ItemName}"
                    .Translate(("ItemName", GetItemNameAndModifiers(element)))
                    + " (" + "{=}not equipped".Translate() + ")";
            }
        }

        private static List<EquipmentIndex> GetValidSlotsForItemType(ItemObject item)
        {
            var slots = new List<EquipmentIndex>();

            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:
                    slots.Add(EquipmentIndex.Head);
                    break;

                case ItemObject.ItemTypeEnum.BodyArmor:
                    slots.Add(EquipmentIndex.Body);
                    break;

                case ItemObject.ItemTypeEnum.LegArmor:
                    slots.Add(EquipmentIndex.Leg);
                    break;

                case ItemObject.ItemTypeEnum.HandArmor:
                    slots.Add(EquipmentIndex.Gloves);
                    break;

                case ItemObject.ItemTypeEnum.Cape:
                    slots.Add(EquipmentIndex.Cape);
                    break;

                case ItemObject.ItemTypeEnum.Horse:
                    slots.Add(EquipmentIndex.Horse);
                    break;

                case ItemObject.ItemTypeEnum.HorseHarness:
                    slots.Add(EquipmentIndex.HorseHarness);
                    break;

                case ItemObject.ItemTypeEnum.Shield:
                    // Shields can go in weapon slots 1-3
                    slots.Add(EquipmentIndex.Weapon1);
                    slots.Add(EquipmentIndex.Weapon2);
                    slots.Add(EquipmentIndex.Weapon3);
                    break;

                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Thrown:
                case ItemObject.ItemTypeEnum.Pistol:
                case ItemObject.ItemTypeEnum.Musket:
                    // Weapons can go in weapon slots 0-3
                    slots.Add(EquipmentIndex.Weapon0);
                    slots.Add(EquipmentIndex.Weapon1);
                    slots.Add(EquipmentIndex.Weapon2);
                    slots.Add(EquipmentIndex.Weapon3);
                    break;
            }

            return slots;
        }

        private static bool ShouldReplaceItem(EquipmentElement currentItem, EquipmentElement newItem)
        {
            // If items are not the same type, don't replace
            if (currentItem.Item.ItemType != newItem.Item.ItemType)
                return false;

            // Don't replace custom items with non-custom items
            bool currentIsCustom = BLTCustomItemsCampaignBehavior.Current.IsRegistered(currentItem.ItemModifier);
            bool newIsCustom = BLTCustomItemsCampaignBehavior.Current.IsRegistered(newItem.ItemModifier);

            if (currentIsCustom && !newIsCustom)
                return false;

            // Compare item effectiveness based on type
            return GetItemEffectiveness(newItem) > GetItemEffectiveness(currentItem);
        }

        private static float GetItemEffectiveness(EquipmentElement equipment)
        {
            if (equipment.IsEmpty)
                return 0f;

            var item = equipment.Item;

            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                case ItemObject.ItemTypeEnum.HorseHarness:
                    return GetArmorEffectiveness(equipment);

                case ItemObject.ItemTypeEnum.Horse:
                    return GetHorseEffectiveness(equipment);

                case ItemObject.ItemTypeEnum.Shield:
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Thrown:
                case ItemObject.ItemTypeEnum.Pistol:
                case ItemObject.ItemTypeEnum.Musket:
                    return GetWeaponEffectiveness(equipment);

                default:
                    return 0f;
            }
        }

        private static float GetArmorEffectiveness(EquipmentElement armor)
        {
            if (armor.IsEmpty) return 0f;

            var armorComponent = armor.Item.ArmorComponent;
            if (armorComponent == null) return 0f;

            float baseArmor = armorComponent.HeadArmor * 0.3f +
                              armorComponent.BodyArmor * 0.4f +
                              armorComponent.LegArmor * 0.2f +
                              armorComponent.ArmArmor * 0.1f;

            // Apply modifier bonus
            if (armor.ItemModifier != null)
            {
                baseArmor += armor.ItemModifier.ModifyArmor(0);
            }

            return baseArmor;
        }

        private static float GetHorseEffectiveness(EquipmentElement horse)
        {
            if (horse.IsEmpty) return 0f;

            var horseComponent = horse.Item.HorseComponent;
            if (horseComponent == null) return 0f;

            float baseEffectiveness = horseComponent.Speed * 2f +
                                      horseComponent.Maneuver * 1.5f +
                                      horseComponent.ChargeDamage +
                                      horseComponent.HitPoints * 0.1f;

            // Apply modifier bonuses
            if (horse.ItemModifier != null)
            {
                float speedMod = horse.ItemModifier.ModifyMountSpeed(100) - 100;
                float chargeMod = horse.ItemModifier.ModifyMountCharge(100) - 100;
                float hpMod = horse.ItemModifier.ModifyMountHitPoints(100) - 100;
                float maneuverMod = horse.ItemModifier.ModifyMountManeuver(100) - 100;

                baseEffectiveness *= (1f + (speedMod + chargeMod + hpMod + maneuverMod) / 400f);
            }

            return baseEffectiveness;
        }

        private static float GetWeaponEffectiveness(EquipmentElement weapon)
        {
            if (weapon.IsEmpty) return 0f;

            var weaponComponent = weapon.Item.WeaponComponent;
            if (weaponComponent == null) return 0f;

            var primaryWeapon = weaponComponent.PrimaryWeapon;

            // Base damage value
            float effectiveness = primaryWeapon.ThrustDamage + primaryWeapon.SwingDamage;

            // Apply modifier bonuses for damage
            if (weapon.ItemModifier != null)
            {
                effectiveness += weapon.ItemModifier.ModifyDamage(0);
            }

            // Factor in weapon handling
            effectiveness += primaryWeapon.Accuracy * 0.1f;

            float missileSpeed = primaryWeapon.MissileSpeed;
            if (weapon.ItemModifier != null)
            {
                missileSpeed += weapon.ItemModifier.ModifyMissileSpeed(0);
            }
            effectiveness += missileSpeed * 0.05f;

            // For shields, prioritize hit points
            if (weapon.Item.ItemType == ItemObject.ItemTypeEnum.Shield)
            {
                float shieldHP = primaryWeapon.MaxDataValue;
                if (weapon.ItemModifier != null)
                {
                    shieldHP += weapon.ItemModifier.ModifyHitPoints(0);
                }
                effectiveness = shieldHP * 0.5f;
            }

            // For throwing weapons, factor in stack amount
            if (weapon.Item.ItemType == ItemObject.ItemTypeEnum.Thrown)
            {
                float stackSize = primaryWeapon.MaxDataValue;
                if (weapon.ItemModifier != null)
                {
                    stackSize += weapon.ItemModifier.ModifyStackCount(0);
                }
                effectiveness *= (stackSize / 10f);
            }

            // For melee weapons, consider swing speed
            if (weapon.Item.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon ||
                weapon.Item.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon ||
                weapon.Item.ItemType == ItemObject.ItemTypeEnum.Polearm)
            {
                float speedBonus = weapon.ItemModifier?.ModifySpeed(0) ?? 0;
                effectiveness *= (1f + speedBonus * 0.01f);
            }

            return effectiveness;
        }

        private static (ItemObject item, ItemModifier modifier, EquipmentIndex slot) GenerateCulturedRewardTypeWeapon(
            int tier, Hero hero, HeroClassDef heroClass, bool allowDuplicateTypes, RandomItemModifierDef modifierDef,
            string customItemName, float customItemPower, CultureObject culture)
        {
            // List of heroes custom items, so we can avoid giving duplicates (it will include what they are carrying,
            // as all custom items are registered)
            var heroCustomWeapons = BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(hero);

            // List of heroes current weapons
            var heroWeapons = hero.BattleEquipment.YieldFilledWeaponSlots().ToList();

            var replaceableHeroWeapons = heroWeapons
                .Where(w =>
                    // Must be lower than the desired tier
                    (int)w.element.Item.Tier < tier
                    // Must not be a custom item
                    && !BLTCustomItemsCampaignBehavior.Current.IsRegistered(w.element.ItemModifier))
                .Select(w => (w.index, w.element.Item.GetEquipmentType()));


            // Weapon classes we can generate a reward for, with some heuristics to avoid some edge cases, and getting
            // duplicates
            var weaponClasses =
                (heroClass?.IndexedWeapons ?? replaceableHeroWeapons)
                .Where(s =>
                    // No shields, they aren't cool rewards and don't support any modifiers
                    // Shields DO support a modifier, shield hit points, which is useful against the massive archer swarms that players often have
                    // s.type != EquipmentType.Shield &&
                    // Exclude bolts if hero doesn't have a crossbow already
                    (s.type != EquipmentType.Bolts || heroWeapons.Any(i
                        => i.element.Item.WeaponComponent?.PrimaryWeapon?.AmmoClass == WeaponClass.Bolt))
                    // Exclude arrows if hero doesn't have a bow
                    && (s.type != EquipmentType.Arrows || heroWeapons.Any(i
                        => i.element.Item.WeaponComponent?.PrimaryWeapon?.AmmoClass == WeaponClass.Arrow))
                    // Exclude any weapons we already have enough custom versions of (if we have class then we can
                    // match the class count, otherwise we just limit it to 1), unless we are allowing duplicates
                    && (allowDuplicateTypes
                        || heroCustomWeapons.Count(i => i.Item.IsEquipmentType(s.type))
                        < (heroClass?.Weapons.Count(w => w == s.type) ?? 1))
                )
                .Shuffle()
                .ToList();

            if (!weaponClasses.Any())
            {
                return default;
            }

            // Tier > 5 indicates custom weapons with modifiers
            if (tier > 5)
            {
                // Custom "modified" item
                var (item, index) = weaponClasses
                    .Select(c => (
                        item: CreateCulturedCustomWeapon(hero, heroClass, c.type, culture),
                        index: c.index))
                    .FirstOrDefault(w => w.item != null);
                return item == null
                        ? default
                        : (item, modifierDef.Generate(item, customItemName, customItemPower), index)
                    ;
            }
            else
            {
                // Find a random item fitting the weapon class requirements
                var (item, index) = weaponClasses
                    .Select(c => (
                        item: EquipHero.FindRandomTieredEquipment(tier, hero,
                            heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                            EquipHero.FindFlags.IgnoreAbility | EquipHero.FindFlags.RequireExactTier,
                            i => i.IsEquipmentType(c.type), culture),
                        index: c.index))
                    .FirstOrDefault(w => w.item != null);
                return item == null || hero.BattleEquipment[index].Item?.Tier >= item.Tier
                    ? default
                    : (item, null, index);
            }
        }

        private static (ItemObject item, ItemModifier modifier, EquipmentIndex slot) GenerateCulturedRewardTypeArmor(int tier,
            Hero hero, HeroClassDef heroClass, bool allowDuplicateTypes, RandomItemModifierDef modifierDef,
            string customItemName, float customItemPower, CultureObject culture)
        {
            // List of custom items the hero already has, and armor they are wearing that is as good or better than
            // the tier we want 
            var heroBetterArmor = BLTAdoptAHeroCampaignBehavior.Current
                .GetCustomItems(hero)
                .Concat(hero.BattleEquipment.YieldFilledArmorSlots()
                    .Where(e => (int)e.Item.Tier >= tier));

            // Select randomly from the various armor types we can choose between
            var (index, itemType) = SkillGroup.ArmorIndexType
                // Exclude any armors we already have an equal or better version of, unless we are allowing duplicates
                .Where(i => allowDuplicateTypes
                            || heroBetterArmor.All(i2 => i2.Item.ItemType != i.itemType))
                .SelectRandom();

            if (index == default)
            {
                return default;
            }

            // Custom "modified" item
            if (tier > 5)
            {
                var armor = EquipHero.FindRandomTieredEquipment(5, hero,
                    heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                    EquipHero.FindFlags.IgnoreAbility,
                    o => o.ItemType == itemType, culture);
                return armor == null ? default : (armor, modifierDef.Generate(armor, customItemName, customItemPower), index);
            }
            else
            {
                var armor = EquipHero.FindRandomTieredEquipment(tier, hero,
                    heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                    EquipHero.FindFlags.IgnoreAbility | EquipHero.FindFlags.RequireExactTier,
                    o => o.ItemType == itemType, culture);
                // if no armor was found, or its the same tier as what we have then return null
                return armor == null || hero.BattleEquipment.YieldFilledArmorSlots()
                    .Any(i2 => i2.Item.Type == armor.Type && i2.Item.Tier >= armor.Tier)
                    ? default
                    : (armor, null, index);
            }
        }

        private static (ItemObject item, ItemModifier modifier, EquipmentIndex slot) GenerateCulturedRewardTypeMount(
    int tier, Hero hero, HeroClassDef heroClass, bool allowDuplicates, RandomItemModifierDef modifierDef,
    string customItemName, float customItemPower, CultureObject culture)
        {
            var currentMount = hero.BattleEquipment.Horse;
            // If we are generating is non custom reward, and the hero has a non custom mount already,
            // of equal or better tier, we don't replace it
            if (tier <= 5 && !currentMount.IsEmpty && (int)currentMount.Item.Tier >= tier)
            {
                return default;
            }

            // If the hero has a custom mount already, then we don't give them another, or any non custom one,
            // unless we are allowing duplicates
            if (!allowDuplicates
                && BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(hero)
                .Any(i => i.Item.ItemType == ItemObject.ItemTypeEnum.Horse))
            {
                return default;
            }

            bool IsCorrectMountFamily(ItemObject item)
            {
                // Must match hero class requirements
                return (heroClass == null
                        || heroClass.UseHorse && item.HorseComponent.Monster.FamilyType
                            is (int)EquipHero.MountFamilyType.horse
                        || heroClass.UseCamel && item.HorseComponent.Monster.FamilyType
                            is (int)EquipHero.MountFamilyType.camel)
                       // Must also not differ from current mount family type (or saddle can get messed up)
                       && (currentMount.IsEmpty
                           || currentMount.Item.HorseComponent.Monster.FamilyType
                           == item.HorseComponent.Monster.FamilyType
                       );
            }

            // Find mounts of the correct family type and tier
            var mountQuery = CampaignHelpers.AllItems
                .Where(item =>
                    item.IsMountable
                    // If we are making a custom mount then use any mount over Tier 2, otherwise match the tier exactly 
                    && (tier > 5 && (int)item.Tier >= 2 || (int)item.Tier == tier)
                    && IsCorrectMountFamily(item)
                );

            // Filter by culture if specified
            if (culture != null)
            {
                mountQuery = mountQuery.Where(item => item.Culture == culture);
            }

            var mount = mountQuery.SelectRandom();

            if (mount == null)
            {
                return default;
            }

            var modifier = tier > 5
                ? modifierDef.Generate(mount, customItemName, customItemPower)
                : null;
            return (mount, modifier, EquipmentIndex.Horse);
        }

        private static (ItemObject item, ItemModifier modifier, EquipmentIndex slot) GenerateCulturedRewardTypeShield(
            int tier, Hero hero, HeroClassDef heroClass, bool allowDuplicateTypes, RandomItemModifierDef modifierDef,
            string customItemName, float customItemPower, CultureObject culture)
        {
            // List of heroes custom items, so we can avoid giving duplicates (it will include what they are carrying,
            // as all custom items are registered)
            var heroCustomWeapons = BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(hero);

            // List of heroes current weapons
            var heroWeapons = hero.BattleEquipment.YieldFilledWeaponSlots().ToList();

            var replaceableHeroWeapons = heroWeapons
                .Where(w =>
                    // Must be lower than the desired tier
                    (int)w.element.Item.Tier < tier
                    // Must not be a custom item
                    && !BLTCustomItemsCampaignBehavior.Current.IsRegistered(w.element.ItemModifier))
                .Select(w => (w.index, w.element.Item.GetEquipmentType()));


            // Weapon classes we can generate a reward for, with some heuristics to avoid some edge cases, and getting
            // duplicates
            var weaponClasses =
                (heroClass?.IndexedWeapons ?? replaceableHeroWeapons)
                .Where(s =>
                    s.type == EquipmentType.Shield
                    // Exclude any weapons we already have enough custom versions of (if we have class then we can
                    // match the class count, otherwise we just limit it to 1), unless we are allowing duplicates
                    && (allowDuplicateTypes
                        || heroCustomWeapons.Count(i => i.Item.IsEquipmentType(s.type))
                        < (heroClass?.Weapons.Count(w => w == s.type) ?? 1))
                )
                .Shuffle()
                .ToList();

            if (!weaponClasses.Any())
            {
                return default;
            }

            // Tier > 5 indicates custom weapons with modifiers
            if (tier > 5)
            {
                // Custom "modified" item
                var (item, index) = weaponClasses
                    .Select(c => (
                        item: CreateCulturedCustomShield(hero, heroClass, culture),
                        index: c.index))
                    .FirstOrDefault(w => w.item != null);
                return item == null
                        ? default
                        : (item, modifierDef.Generate(item, customItemName, customItemPower), index)
                    ;
            }
            else
            {
                // Find a random item fitting the weapon class requirements
                var (item, index) = weaponClasses
                    .Select(c => (
                        item: EquipHero.FindRandomTieredEquipment(tier, hero,
                            heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                            EquipHero.FindFlags.IgnoreAbility | EquipHero.FindFlags.RequireExactTier,
                            i => i.IsEquipmentType(c.type), culture),
                        index: c.index))
                    .FirstOrDefault(w => w.item != null);
                return item == null || hero.BattleEquipment[index].Item?.Tier >= item.Tier
                    ? default
                    : (item, null, index);
            }
        }

        private static (ItemObject item, ItemModifier modifier, EquipmentIndex slot) GenerateRewardTypeWeapon(
            int tier, Hero hero, HeroClassDef heroClass, bool allowDuplicateTypes, RandomItemModifierDef modifierDef,
            string customItemName, float customItemPower)
        {
            // List of heroes custom items, so we can avoid giving duplicates (it will include what they are carrying,
            // as all custom items are registered)
            var heroCustomWeapons = BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(hero);

            // List of heroes current weapons
            var heroWeapons = hero.BattleEquipment.YieldFilledWeaponSlots().ToList();

            var replaceableHeroWeapons = heroWeapons
                .Where(w =>
                    // Must be lower than the desired tier
                    (int)w.element.Item.Tier < tier
                    // Must not be a custom item
                    && !BLTCustomItemsCampaignBehavior.Current.IsRegistered(w.element.ItemModifier))
                .Select(w => (w.index, w.element.Item.GetEquipmentType()));


            // Weapon classes we can generate a reward for, with some heuristics to avoid some edge cases, and getting
            // duplicates
            var weaponClasses =
                (heroClass?.IndexedWeapons ?? replaceableHeroWeapons)
                .Where(s =>
                    // No shields, they aren't cool rewards and don't support any modifiers
                    // Shields DO support a modifier, shield hit points, which is useful against the massive archer swarms that players often have
                    // s.type != EquipmentType.Shield &&
                    // Exclude bolts if hero doesn't have a crossbow already
                    (s.type != EquipmentType.Bolts || heroWeapons.Any(i
                        => i.element.Item.WeaponComponent?.PrimaryWeapon?.AmmoClass == WeaponClass.Bolt))
                    // Exclude arrows if hero doesn't have a bow
                    && (s.type != EquipmentType.Arrows || heroWeapons.Any(i
                        => i.element.Item.WeaponComponent?.PrimaryWeapon?.AmmoClass == WeaponClass.Arrow))
                    // Exclude any weapons we already have enough custom versions of (if we have class then we can
                    // match the class count, otherwise we just limit it to 1), unless we are allowing duplicates
                    && (allowDuplicateTypes
                        || heroCustomWeapons.Count(i => i.Item.IsEquipmentType(s.type))
                        < (heroClass?.Weapons.Count(w => w == s.type) ?? 1))
                )
                .Shuffle()
                .ToList();

            if (!weaponClasses.Any())
            {
                return default;
            }

            // Tier > 5 indicates custom weapons with modifiers
            if (tier > 5)
            {
                // Custom "modified" item
                var (item, index) = weaponClasses
                    .Select(c => (
                        item: CreateCustomWeapon(hero, heroClass, c.type),
                        index: c.index))
                    .FirstOrDefault(w => w.item != null);
                return item == null
                        ? default
                        : (item, modifierDef.Generate(item, customItemName, customItemPower), index)
                    ;
            }
            else
            {
                // Find a random item fitting the weapon class requirements
                var (item, index) = weaponClasses
                    .Select(c => (
                        item: EquipHero.FindRandomTieredEquipment(tier, hero,
                            heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                            EquipHero.FindFlags.IgnoreAbility | EquipHero.FindFlags.RequireExactTier,
                            i => i.IsEquipmentType(c.type)),
                        index: c.index))
                    .FirstOrDefault(w => w.item != null);
                return item == null || hero.BattleEquipment[index].Item?.Tier >= item.Tier
                    ? default
                    : (item, null, index);
            }
        }

        private static (ItemObject item, ItemModifier modifier, EquipmentIndex slot) GenerateRewardTypeArmor(int tier,
            Hero hero, HeroClassDef heroClass, bool allowDuplicateTypes, RandomItemModifierDef modifierDef,
            string customItemName, float customItemPower)
        {
            // List of custom items the hero already has, and armor they are wearing that is as good or better than
            // the tier we want 
            var heroBetterArmor = BLTAdoptAHeroCampaignBehavior.Current
                .GetCustomItems(hero)
                .Concat(hero.BattleEquipment.YieldFilledArmorSlots()
                    .Where(e => (int)e.Item.Tier >= tier));

            // Select randomly from the various armor types we can choose between
            var (index, itemType) = SkillGroup.ArmorIndexType
                // Exclude any armors we already have an equal or better version of, unless we are allowing duplicates
                .Where(i => allowDuplicateTypes
                            || heroBetterArmor.All(i2 => i2.Item.ItemType != i.itemType))
                .SelectRandom();

            if (index == default)
            {
                return default;
            }

            // Custom "modified" item
            if (tier > 5)
            {
                var armor = EquipHero.FindRandomTieredEquipment(5, hero,
                    heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                    EquipHero.FindFlags.IgnoreAbility,
                    o => o.ItemType == itemType);
                return armor == null ? default : (armor, modifierDef.Generate(armor, customItemName, customItemPower), index);
            }
            else
            {
                var armor = EquipHero.FindRandomTieredEquipment(tier, hero,
                    heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                    EquipHero.FindFlags.IgnoreAbility | EquipHero.FindFlags.RequireExactTier,
                    o => o.ItemType == itemType);
                // if no armor was found, or its the same tier as what we have then return null
                return armor == null || hero.BattleEquipment.YieldFilledArmorSlots()
                    .Any(i2 => i2.Item.Type == armor.Type && i2.Item.Tier >= armor.Tier)
                    ? default
                    : (armor, null, index);
            }
        }

        private static (ItemObject item, ItemModifier modifier, EquipmentIndex slot) GenerateRewardTypeMount(
            int tier, Hero hero, HeroClassDef heroClass, bool allowDuplicates, RandomItemModifierDef modifierDef,
            string customItemName, float customItemPower)
        {
            var currentMount = hero.BattleEquipment.Horse;
            // If we are generating is non custom reward, and the hero has a non custom mount already,
            // of equal or better tier, we don't replace it
            if (tier <= 5 && !currentMount.IsEmpty && (int)currentMount.Item.Tier >= tier)
            {
                return default;
            }

            // If the hero has a custom mount already, then we don't give them another, or any non custom one,
            // unless we are allowing duplicates
            if (!allowDuplicates
                && BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(hero)
                .Any(i => i.Item.ItemType == ItemObject.ItemTypeEnum.Horse))
            {
                return default;
            }

            bool IsCorrectMountFamily(ItemObject item)
            {
                // Must match hero class requirements
                return (heroClass == null
                        || heroClass.UseHorse && item.HorseComponent.Monster.FamilyType
                            is (int)EquipHero.MountFamilyType.horse
                        || heroClass.UseCamel && item.HorseComponent.Monster.FamilyType
                            is (int)EquipHero.MountFamilyType.camel)
                       // Must also not differ from current mount family type (or saddle can get messed up)
                       && (currentMount.IsEmpty
                           || currentMount.Item.HorseComponent.Monster.FamilyType
                           == item.HorseComponent.Monster.FamilyType
                       );
            }

            // Find mounts of the correct family type and tier
            var mount = CampaignHelpers.AllItems
                .Where(item =>
                    item.IsMountable
                    // If we are making a custom mount then use any mount over Tier 2, otherwise match the tier exactly 
                    && (tier > 5 && (int)item.Tier >= 2 || (int)item.Tier == tier)
                    && IsCorrectMountFamily(item)
                )
                .SelectRandom();

            if (mount == null)
            {
                return default;
            }

            var modifier = tier > 5
                ? modifierDef.Generate(mount, customItemName, customItemPower)
                : null;
            return (mount, modifier, EquipmentIndex.Horse);
        }

        private static (ItemObject item, ItemModifier modifier, EquipmentIndex slot) GenerateRewardTypeShield(
            int tier, Hero hero, HeroClassDef heroClass, bool allowDuplicateTypes, RandomItemModifierDef modifierDef,
            string customItemName, float customItemPower)
        {
            // List of heroes custom items, so we can avoid giving duplicates (it will include what they are carrying,
            // as all custom items are registered)
            var heroCustomWeapons = BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(hero);

            // List of heroes current weapons
            var heroWeapons = hero.BattleEquipment.YieldFilledWeaponSlots().ToList();

            var replaceableHeroWeapons = heroWeapons
                .Where(w =>
                    // Must be lower than the desired tier
                    (int)w.element.Item.Tier < tier
                    // Must not be a custom item
                    && !BLTCustomItemsCampaignBehavior.Current.IsRegistered(w.element.ItemModifier))
                .Select(w => (w.index, w.element.Item.GetEquipmentType()));


            // Weapon classes we can generate a reward for, with some heuristics to avoid some edge cases, and getting
            // duplicates
            var weaponClasses =
                (heroClass?.IndexedWeapons ?? replaceableHeroWeapons)
                .Where(s =>
                    s.type == EquipmentType.Shield
                    // Exclude any weapons we already have enough custom versions of (if we have class then we can
                    // match the class count, otherwise we just limit it to 1), unless we are allowing duplicates
                    && (allowDuplicateTypes
                        || heroCustomWeapons.Count(i => i.Item.IsEquipmentType(s.type))
                        < (heroClass?.Weapons.Count(w => w == s.type) ?? 1))
                )
                .Shuffle()
                .ToList();

            if (!weaponClasses.Any())
            {
                return default;
            }

            // Tier > 5 indicates custom weapons with modifiers
            if (tier > 5)
            {
                // Custom "modified" item
                var (item, index) = weaponClasses
                    .Select(c => (
                        item: CreateCustomShield(hero, heroClass),
                        index: c.index))
                    .FirstOrDefault(w => w.item != null);
                return item == null
                        ? default
                        : (item, modifierDef.Generate(item, customItemName, customItemPower), index)
                    ;
            }
            else
            {
                // Find a random item fitting the weapon class requirements
                var (item, index) = weaponClasses
                    .Select(c => (
                        item: EquipHero.FindRandomTieredEquipment(tier, hero,
                            heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                            EquipHero.FindFlags.IgnoreAbility | EquipHero.FindFlags.RequireExactTier,
                            i => i.IsEquipmentType(c.type)),
                        index: c.index))
                    .FirstOrDefault(w => w.item != null);
                return item == null || hero.BattleEquipment[index].Item?.Tier >= item.Tier
                    ? default
                    : (item, null, index);
            }
        }

        private static ItemObject CreateCustomShield(Hero hero, HeroClassDef heroClass)
        {
            // Get the highest tier we can for the weapon type
            var item = EquipHero.FindRandomTieredEquipment(6, hero,
                heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                EquipHero.FindFlags.IgnoreAbility,
                o => o.IsEquipmentType(EquipmentType.Shield));

            if (item == null)
            {
                item = EquipHero.FindRandomTieredEquipment(5, hero,
                heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                EquipHero.FindFlags.IgnoreAbility,
                o => o.IsEquipmentType(EquipmentType.Shield));
            }
            return item;
        }

        private static ItemObject CreateCustomWeapon(Hero hero, HeroClassDef heroClass, EquipmentType weaponType)
        {
            if (!CustomItems.CraftableEquipmentTypes.Contains(weaponType))
            {
                // Get the highest tier we can for the weapon type
                var item = EquipHero.FindRandomTieredEquipment(5, hero,
                    heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                    EquipHero.FindFlags.IgnoreAbility,
                    o => o.IsEquipmentType(weaponType));
                return item;
            }
            else
            {
                return CustomItems.CreateCraftedWeapon(hero, weaponType, 5);
            }
        }

        private static ItemObject CreateCulturedCustomShield(Hero hero, HeroClassDef heroClass, CultureObject culture)
        {
            // Get the highest tier we can for the weapon type
            var item = EquipHero.FindRandomTieredEquipment(6, hero,
                heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                EquipHero.FindFlags.IgnoreAbility,
                o => o.IsEquipmentType(EquipmentType.Shield), culture);

            if (item == null)
            {
                item = EquipHero.FindRandomTieredEquipment(5, hero,
                heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                EquipHero.FindFlags.IgnoreAbility,
                o => o.IsEquipmentType(EquipmentType.Shield), culture);
            }
            return item;
        }

        private static ItemObject CreateCulturedCustomWeapon(Hero hero, HeroClassDef heroClass, EquipmentType weaponType, CultureObject culture)
        {
            if (!CustomItems.CraftableEquipmentTypes.Contains(weaponType))
            {
                // Get the highest tier we can for the weapon type
                var item = EquipHero.FindRandomTieredEquipment(5, hero,
                    heroClass?.Mounted == true || !hero.BattleEquipment.Horse.IsEmpty,
                    EquipHero.FindFlags.IgnoreAbility,
                    o => o.IsEquipmentType(weaponType), culture);
                return item;
            }
            else
            {
                return CustomItems.CreateCulturedCraftedWeapon(hero, weaponType, 5, culture);
            }
        }

        public static string GetItemNameAndModifiers(EquipmentElement item)
            => item.GetModifiedItemName() + " (" + GetModifiersDescription(item.ItemModifier, item.Item) + ")";

        public static string GetModifiersDescription(ItemModifier itemModifier, ItemObject itemObject)
        {
            if (itemModifier == null)
            {
                return "{=}no modifiers".Translate();
            }

            bool isWeaponMelee = itemObject.Type is
                ItemObject.ItemTypeEnum.OneHandedWeapon or
                ItemObject.ItemTypeEnum.TwoHandedWeapon or
                ItemObject.ItemTypeEnum.Polearm;
            bool isWeaponRanged = itemObject.Type is
                ItemObject.ItemTypeEnum.Crossbow or
                ItemObject.ItemTypeEnum.Bow or
                ItemObject.ItemTypeEnum.Sling;
            bool isAmmo = itemObject.Type is
                ItemObject.ItemTypeEnum.Bolts or
                ItemObject.ItemTypeEnum.Arrows or
                ItemObject.ItemTypeEnum.SlingStones or
                ItemObject.ItemTypeEnum.Thrown;
            bool isThrown = itemObject.Type is ItemObject.ItemTypeEnum.Thrown;
            bool isShield = itemObject.Type is
                ItemObject.ItemTypeEnum.Shield;
            bool isArmor = itemObject.HasArmorComponent;
            bool isHorseArmor = itemObject.Type is ItemObject.ItemTypeEnum.HorseHarness;
            bool isHorse = itemObject.HorseComponent != null;

            var modifiers = new[]
                {
                    // Only armor modifies armor
                    (str: "{=}{Inc}{AMOUNT} Armor", mod: itemModifier.ModifyArmor(0), enabled: isArmor || isHorseArmor),
                    // Only shields can modify HP
                    (str: "{=}{Inc}{AMOUNT} HP", mod: itemModifier.ModifyHitPoints(0), enabled: isShield),
                    // Only non-ranged weapons can modify speed, and speed refers to swing/thrust/handling
                    (str: "{=}{Inc}{AMOUNT} Swing Speed", mod: itemModifier.ModifySpeed(0), enabled: isWeaponMelee),
                    (str: "{=}{Inc}{AMOUNT} Damage", mod: itemModifier.ModifyDamage(0), enabled: isWeaponMelee || isAmmo),
                    (str: "{=}{Inc}{AMOUNT} Missile Speed", mod: itemModifier.ModifyMissileSpeed(0), enabled: isThrown || isWeaponRanged),
                    (str: "{=}{Inc}{AMOUNT} Stack Count", mod: itemModifier.ModifyStackCount(0), enabled: isAmmo),
                    (str: "{=}{Inc}{AMOUNT}% Mount Speed", mod: itemModifier.ModifyMountSpeed(100) - 100, enabled: isHorse || isHorseArmor),
                    (str: "{=}{Inc}{AMOUNT}% Mount Charge", mod: itemModifier.ModifyMountCharge(100) - 100, enabled: isHorse || isHorseArmor),
                    (str: "{=}{Inc}{AMOUNT}% Mount HP", mod: itemModifier.ModifyMountHitPoints(100) - 100, enabled: isHorse),
                    (str: "{=}{Inc}{AMOUNT}% Mount Maneuver", mod: itemModifier.ModifyMountManeuver(100) - 100, enabled: isHorse || isHorseArmor),
                }
                .Where(x => x.mod != 0 && x.enabled)
                .Select(x => x.str.Translate(("Inc", Naming.Inc), ("AMOUNT", x.mod)))
                .ToList();

            if (!modifiers.Any())
            {
                return "{=}no modifiers".Translate();
            }

            return string.Join(" ", modifiers);
        }
    }
}