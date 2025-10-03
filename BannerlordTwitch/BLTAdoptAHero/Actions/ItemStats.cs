using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Localization;
using BLTAdoptAHero;


namespace BLTAdoptAHero
{
    [LocDisplayName("{=y4BMYwgn}Item stats"),
     LocDescription("{=bfsWG9GS}Shows detailed stats of hero equipment or custom items"),
     UsedImplicitly]
    public class ItemStats : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context,
                    context.ArgsErrorMessage("{=nHbTeHpp}invalid mode (use armor, inv, custom, store)".Translate()));
                return;
            }

            var argParts = context.Args.Trim().Split(' ').ToList();
            var mode = argParts[0].ToLower();

            switch (mode)
            {
                case "armor":
                    {
                        // Overall armor values from CharacterObject
                        int head = (int)(adoptedHero.CharacterObject.GetHeadArmorSum());
                        int body = (int)(adoptedHero.CharacterObject.GetBodyArmorSum());
                        int arm = (int)(adoptedHero.CharacterObject.GetArmArmorSum());
                        int leg = (int)(adoptedHero.CharacterObject.GetLegArmorSum());

                        string result = "{=2w64E4Oh}Overall Armor: Head: {head} - Body: {body} - Arm: {arm} - Leg: {leg}"
                            .Translate(("head", head), ("body", body), ("arm", arm), ("leg", leg));
                        onSuccess(result);
                        break;
                    }
                case "inv":
                    {
                        string result = "";

                        // parse optional slot number
                        int slotIndex = -1;
                        if (argParts.Count > 1 && int.TryParse(argParts[1], out int parsed))
                            slotIndex = parsed - 1; // user-facing 1 = index 0

                        var slots = adoptedHero.BattleEquipment.YieldFilledEquipmentSlots().ToList();

                        // filter to one slot if requested
                        var selectedSlots = (slotIndex >= 0 && slotIndex < slots.Count)
                        ? new List<(EquipmentElement element, EquipmentIndex index)> { slots[slotIndex] }
                        : slots;

                        var itemStrings = selectedSlots
                            .Select(slot =>
                            {
                                var item = slot.element.Item;
                                if (item == null) return null;

                                var element = slot.element;
                                string inv = $"{GetIcon(item)} {element.GetModifiedItemName()}";
                                if (item.HasWeaponComponent)
                                {
                                    var w = item.PrimaryWeapon;
                                    if (w != null)
                                    {
                                        if (w.IsRangedWeapon)
                                        {
                                            var rW = new System.Text.StringBuilder();

                                            rW.Append("{=6eAEEYD4} - Damage: {dmg} - MissileSpeed: {spd}"
                                                .Translate(("dmg", w.GetModifiedMissileDamage(element.ItemModifier)), ("spd", w.GetModifiedMissileSpeed(element.ItemModifier))));

                                            if ((int)w.GetModifiedStackCount(element.ItemModifier) > 1)
                                                rW.Append("{=6oAUemDk} - Stack: {stk}"
                                                    .Translate(("stk", (int)w.GetModifiedStackCount(element.ItemModifier))));

                                            inv += rW.ToString();
                                        }
                                        else if (w.IsMeleeWeapon)
                                        {
                                            inv += "{=4JDkt6Vj} - Damage(swing/thrust): {sw}/{th} - Speed(swing/thrust): {sps}/{spt} - Length: {len}"
                                                .Translate(("sw", w.GetModifiedSwingDamage(element.ItemModifier)), ("th", w.GetModifiedThrustDamage(element.ItemModifier)),
                                                           ("sps", w.GetModifiedSwingSpeed(element.ItemModifier)), ("spt", w.GetModifiedThrustDamage(element.ItemModifier)), ("len", w.WeaponLength));
                                        }
                                        else if (w.IsShield)
                                        {
                                            inv += "{=zEabmx65} - HP: {hp}".Translate(("hp", (1*element.GetModifiedMaximumHitPointsForUsage(0))));
                                        }
                                        else if (w.IsAmmo)
                                        {
                                            inv += "{=ACds6V3a} - Damage: {dmg} Speed: {spd} - Stack: {stk}"
                                        .Translate(("dmg", w.GetModifiedMissileDamage(element.ItemModifier)), ("spd", (int)w.GetModifiedMissileSpeed(element.ItemModifier)),
                                                   ("stk", (int)w.GetModifiedStackCount(element.ItemModifier)));
                                        }
                                        else if (item.BannerComponent?.BannerEffect != null)
                                        {
                                            inv += "{=wphkep40} - Effect: {eff}"
                                                .Translate(("eff", item.BannerComponent.BannerEffect.GetDescription((int)item.Tier)));
                                        }
                                    }
                                }
                                else if (item.HasArmorComponent)
                                {
                                    var a = item.ArmorComponent;
                                    var zC = new System.Text.StringBuilder();

                                    if (a.HeadArmor > 0)
                                        zC.Append("{=UFJ0MM9n} - Head: {val}".Translate(("val", element.GetModifiedHeadArmor())));
                                    if (a.BodyArmor > 0)
                                        zC.Append("{=1uh8yzId} - Torso: {val}".Translate(("val", element.GetModifiedBodyArmor())));
                                    if (a.ArmArmor > 0)
                                        zC.Append("{=6O4gnvEM} - Arms: {val}".Translate(("val", element.GetModifiedArmArmor())));
                                    if (a.LegArmor > 0)
                                        zC.Append("{=a96X89Z3} - Legs: {val}".Translate(("val", element.GetModifiedLegArmor())));

                                    inv += zC.ToString();
                                }
                                else if (item.HasHorseComponent)
                                {
                                    var h = item.HorseComponent;

                                    // Base stats
                                    int speed = h.Speed;
                                    int maneuver = h.Maneuver;
                                    int charge = h.ChargeDamage;
                                    int hp = h.HitPoints;

                                    // Apply item modifier if present
                                    if (element.ItemModifier != null)
                                    {
                                        speed = element.ItemModifier.ModifyMountSpeed(speed);
                                        maneuver = element.ItemModifier.ModifyMountManeuver(maneuver);
                                        charge = element.ItemModifier.ModifyMountCharge(charge);
                                        hp = element.ItemModifier.ModifyMountHitPoints(hp);
                                    }

                                    inv += "{=SoV9WfBM} - Speed: {spd} - Maneuver: {man} - ChargeDmg: {cdmg} - HP: {hp}"
                                        .Translate(("spd", speed),
                                                   ("man", maneuver),
                                                   ("cdmg", charge),
                                                   ("hp", hp));
                                }

                                return inv;
                            })
                            .Where(line => line != null);

                        if (itemStrings.Any())
                        {
                            result += " | " + string.Join(" | ", itemStrings);
                        }

                        const int maxLen = 480;
                        for (int i = 0; i < result.Length; i += maxLen)
                        {
                            string chunk = result.Substring(i, Math.Min(maxLen, result.Length - i));
                            onSuccess(chunk);
                        }
                        break;
                    }

                case "custom":
                    {
                        if (argParts.Count < 2)
                        {
                            onFailure("{=BClUoR9H}(custom item index)".Translate());
                            return;
                        }

                        (var element, string error) = BLTAdoptAHeroCampaignBehavior.Current
                            .FindCustomItemByIndex(adoptedHero, argParts[1]);

                        if (element.IsEqualTo(EquipmentElement.Invalid))
                        {
                            onFailure(error ?? "{=3ZKRp5OF}(unknown error)".Translate());
                            return;
                        }

                        var item = element.Item;
                        if (item == null)
                        {
                            onFailure("{=V4aRMn9L}(no item found)".Translate());
                            return;
                        }

                        string custom = $"{GetIcon(item)} {item.Name} | " +
                                        "{=rDvZf4JE}Type: {type}".Translate(("type", item.ItemType)) + " | " +
                                        "{=Sg11nEUe}Tier: {tier}".Translate(("tier", item.Tier));

                        if (item.HasWeaponComponent)
                        {
                            var w = item.PrimaryWeapon;
                            if (w != null)
                            {
                                if (w.IsRangedWeapon)
                                {
                                    var rW = new System.Text.StringBuilder();

                                    rW.Append("{=6eAEEYD4} - Damage: {dmg} - MissileSpeed: {spd}"
                                        .Translate(("dmg", w.GetModifiedMissileDamage(element.ItemModifier)), ("spd", w.GetModifiedMissileSpeed(element.ItemModifier))));

                                    if ((int)w.GetModifiedStackCount(element.ItemModifier) > 1 )
                                        rW.Append("{=6oAUemDk} - Stack: {stk}"
                                            .Translate(("stk", (int)w.GetModifiedStackCount(element.ItemModifier))));

                                    custom += rW.ToString();
                                }
                                else if (w.IsMeleeWeapon)
                                {
                                    custom += "{=4JDkt6Vj} - Damage(swing/thrust): {sw}/{th} - Speed(swing/thrust): {sps}/{spt} - Length: {len}"
                                        .Translate(("sw", w.GetModifiedSwingDamage(element.ItemModifier)), ("th", w.GetModifiedThrustDamage(element.ItemModifier)),
                                                   ("spd", w.GetModifiedSwingSpeed(element.ItemModifier)), ("spt", w.GetModifiedThrustDamage(element.ItemModifier)), ("len", w.WeaponLength));
                                }
                                else if (w.IsShield)
                                {
                                    custom += "{=zEabmx65} - Hp: {hp}".Translate(("hp", (1 * element.GetModifiedMaximumHitPointsForUsage(0))));
                                }
                                else if (w.IsAmmo)
                                {
                                    custom += "{=ACds6V3a} - Damage: {dmg} Speed: {spd} - Stack: {stk}"
                                        .Translate(("dmg", w.GetModifiedMissileDamage(element.ItemModifier)), ("spd", (int)w.GetModifiedMissileSpeed(element.ItemModifier)),
                                                   ("stk", (int)w.GetModifiedStackCount(element.ItemModifier)));
                                }
                            }
                        }
                        if (item.HasArmorComponent)
                        {
                            var a = item.ArmorComponent;
                            var zC = new System.Text.StringBuilder();

                            if (a.HeadArmor > 0)
                                zC.Append("{=UFJ0MM9n} - Head: {val}".Translate(("val", a.HeadArmor)));
                            if (a.BodyArmor > 0)
                                zC.Append("{=1uh8yzId} - Torso: {val}".Translate(("val", a.BodyArmor)));
                            if (a.ArmArmor > 0)
                                zC.Append("{=6O4gnvEM} - Arms: {val}".Translate(("val", a.ArmArmor)));
                            if (a.LegArmor > 0)
                                zC.Append("{=a96X89Z3} - Legs: {val}".Translate(("val", a.LegArmor)));

                            custom += zC.ToString();
                        }
                        else if (item.HasHorseComponent)
                        {
                            var h = item.HorseComponent;

                            // Base stats
                            int speed = h.Speed;
                            int maneuver = h.Maneuver;
                            int charge = h.ChargeDamage;
                            int hp = h.HitPoints;

                            // Apply item modifier if present
                            if (element.ItemModifier != null)
                            {
                                speed = element.ItemModifier.ModifyMountSpeed(speed);
                                maneuver = element.ItemModifier.ModifyMountManeuver(maneuver);
                                charge = element.ItemModifier.ModifyMountCharge(charge);
                                hp = element.ItemModifier.ModifyMountHitPoints(hp);
                            }

                            custom += "{=SoV9WfBM} - Speed: {spd} - Maneuver: {man} - ChargeDmg: {cdmg} - Hp: {hp}"
                                .Translate(("spd", speed),
                                           ("man", maneuver),
                                           ("cdmg", charge),
                                           ("hp", hp));
                        }
                        if (custom == $"{GetIcon(item)} {item.Name} | " +
                        "{=rDvZf4JE}Type: {type}".Translate(("type", item.ItemType)) + " | " +
                        "{=Sg11nEUe}Tier: {tier}".Translate(("tier", item.Tier)))
                        {
                            onFailure("{=N1GcPlOD}Item has no recognized components".Translate());
                        }
                        onSuccess(custom);
                        break;
                    }

                case "store":
                    {
                        // Require a slot index argument
                        if (argParts.Count < 2 || !int.TryParse(argParts[1], out int slotIndex))
                        {
                            onFailure("You must specify which equipment slot to store");
                            break;
                        }
                        slotIndex -= 1; // convert to zero-based index

                        var slots = adoptedHero.BattleEquipment.YieldFilledEquipmentSlots().ToList();
                        if (slotIndex < 0 || slotIndex >= slots.Count)
                        {
                            onFailure($"Invalid slot index. Must be between 1 and {slots.Count}");
                            break;
                        }

                        var (element, index) = slots[slotIndex];
                        var item = element.Item;
                        if (item == null)
                        {
                            onFailure("That slot is empty");
                            break;
                        }

                        var itemName = item.Name?.ToString() ?? "Custom item";

                        // 🔎 Check existing custom items for duplicate name
                        bool alreadyStored = BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(adoptedHero)
                            .Any(ci => ci.Item != null &&
                                       string.Equals(ci.Item.Name?.ToString(), itemName, StringComparison.OrdinalIgnoreCase));

                        if (!alreadyStored)
                        {
                            var dummy = BLTCustomItemsCampaignBehavior.Current.CreateDummyModifier(itemName);
                            var registeredElement = new EquipmentElement(item, dummy);

                            BLTAdoptAHeroCampaignBehavior.Current.AddCustomItem(adoptedHero, registeredElement);
                            onSuccess("{=StHrxSot}{item.Name} has been stored as a custom item!"
                                .Translate(("item.Name", itemName)));
                        }
                        else
                        {
                            onFailure("{=mOdh17Cj}{item.Name} is already stored as a custom item!"
                                .Translate(("item.Name", itemName)));
                        }

                        break;
                    }

                default:
                    onFailure("{=nHbTeHpp}invalid mode (use armor, inv, custom, store)".Translate());
                    break;
            }
        }
        

        private static string GetIcon(ItemObject item)
        {
            if (item == null) return "❔";
            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                    return "🗡";
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Thrown:
                    return "🏹";
                case ItemObject.ItemTypeEnum.Shield:
                    return "🛡";
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts:
                    return "➶";
                case ItemObject.ItemTypeEnum.Horse:
                    return "🐴";
                case ItemObject.ItemTypeEnum.HorseHarness:
                    return "🐎";
                case ItemObject.ItemTypeEnum.HeadArmor:
                    return "⛑️";
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.ChestArmor:
                    return "👕";
                case ItemObject.ItemTypeEnum.LegArmor:
                    return "🥾";
                case ItemObject.ItemTypeEnum.HandArmor:
                    return "🧤";
                case ItemObject.ItemTypeEnum.Cape:
                    return "🧣";
                case ItemObject.ItemTypeEnum.Banner:
                    return "⚑";
                default:
                    return "⚙️";
            }
        }
    }
}
