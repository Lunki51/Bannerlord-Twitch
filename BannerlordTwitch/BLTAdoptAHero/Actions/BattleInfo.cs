using System;
using System.Linq;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using BLTAdoptAHero;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=TESTING}BattleInfo"),
     LocDescription("{=TESTING}Shows hero battle info"),
     UsedImplicitly]
    public class BattleInfo : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (Mission.Current == null)
            {
                onFailure("{=TESTING}No mission!".Translate());
                return;
            }

            var missionBehavior = BLTAdoptAHeroCommonMissionBehavior.Current;
            if (missionBehavior == null)
            {
                onFailure("Mission behavior not found!");
                return;
            }

            var agent = adoptedHero.GetAgent();
            var state = BLTAdoptAHeroCommonMissionBehavior.Current.GetMissionState(adoptedHero);
            var state2 = BLTSummonBehavior.Current.GetHeroSummonState(adoptedHero);

            if (agent == null)
            {
                onFailure($"Hero is not currently in battle!({state2.CooldownRemaining}s)");
                return;
            }




            static float ActivePowerFraction(Hero hero)
            {
                var classDef = BLTAdoptAHeroCampaignBehavior.Current?.GetClass(hero);
                if (classDef?.ActivePower == null)
                    return 0f;

                // Check if power is active
                if (!classDef.ActivePower.IsActive(hero))
                    return 0f;

                var (duration, remaining) = classDef.ActivePower.DurationRemaining(hero);

                return duration > 0 ? remaining / duration : 0f;
            }


            // Mounted info
            string mountInfo = "";
            if (agent.MountAgent != null)
            {
                mountInfo = $"{agent.MountAgent.Health}/{agent.MountAgent.HealthLimit}";
            }


            var equipment = agent.Equipment;
            // --- Main hand ---
            var mainIndex = agent.GetPrimaryWieldedItemIndex();
            var mainItemObj = mainIndex != EquipmentIndex.None ? equipment[mainIndex].Item : null;
            string weaponInfo = "Unarmed";

            if (mainItemObj != null)
            {
                string ammoInfo = "";
                if (mainItemObj.ItemType == ItemObject.ItemTypeEnum.Bow
                    || mainItemObj.ItemType == ItemObject.ItemTypeEnum.Crossbow
                    || mainItemObj.ItemType == ItemObject.ItemTypeEnum.Pistol
                    || mainItemObj.ItemType == ItemObject.ItemTypeEnum.Musket
                    || mainItemObj.ItemType == ItemObject.ItemTypeEnum.Thrown)
                {
                    int ammo = equipment.GetAmmoAmount(mainIndex);
                    int maxAmmo = equipment.GetMaxAmmo(mainIndex);
                    ammoInfo = $" - Ammo: {ammo}/{maxAmmo}";
                }

                weaponInfo = $"{mainItemObj.Name} ({mainItemObj.ItemType}){ammoInfo}";
            }

            // --- Off-hand ---
            var offIndex = agent.GetOffhandWieldedItemIndex();
            var offItemObj = offIndex != EquipmentIndex.None ? equipment[offIndex].Item : null;

            if (offItemObj != null)
                //if (offItemObj.ItemType == ItemObject.ItemTypeEnum.Shield)
                //{
                //    int shp = offItemObj.ItemComponent..
                //}
                weaponInfo += $" + {offItemObj.Name} ({offItemObj.ItemType})";
            var weaponSlots = new[]
            {
                EquipmentIndex.Weapon0,
                EquipmentIndex.Weapon1,
                EquipmentIndex.Weapon2,
                EquipmentIndex.Weapon3,
                EquipmentIndex.ExtraWeaponSlot
            };
            // --- Other ranged/thrown weapons not in main-hand ---
            var addedThrownNames = new HashSet<string>();
            foreach (EquipmentIndex slot in weaponSlots)
            {
                if (slot == mainIndex || slot == offIndex)
                    continue;

                var element = equipment[slot];
                if (element.Item == null)
                    continue;

                var item = element.Item;

                // Only consider ranged or thrown weapons
                switch (item.ItemType)
                {
                    case ItemObject.ItemTypeEnum.Bow:
                    case ItemObject.ItemTypeEnum.Crossbow:
                    case ItemObject.ItemTypeEnum.Sling:
                    case ItemObject.ItemTypeEnum.Pistol:
                    case ItemObject.ItemTypeEnum.Musket:
                    case ItemObject.ItemTypeEnum.Thrown:
                        {
                            string nameKey = item.Name.ToString();

                            // If thrown and same name already added → skip
                            if (item.ItemType == ItemObject.ItemTypeEnum.Thrown &&
                                addedThrownNames.Contains(nameKey))
                                break;

                            if (item.ItemType == ItemObject.ItemTypeEnum.Thrown)
                                addedThrownNames.Add(nameKey);

                            int ammo = equipment.GetAmmoAmount(slot);
                            int maxAmmo = equipment.GetMaxAmmo(slot);

                            weaponInfo += $" + {item.Name} ({item.ItemType}) - Ammo: {ammo}/{maxAmmo}";
                            break;
                        }
                }
            }

            string message =
                $"Class: {adoptedHero.GetClass()?.Name.ToString() ?? "No class"}\n" +
                $"- HP: {(int)agent.Health}/{(int)agent.HealthLimit}\n";
            if (agent.MountAgent != null)
                message += $"- Mount HP: {mountInfo}\n";

            message +=
                $"- Weapon: {weaponInfo}\n" +
                $"- Kills: {state.Kills}\n" +
                $"- Retinue({state2.ActiveRetinue}): {state.RetinueKills}\n" +
                $"- Gold: {state.WonGold}\n" +
                $"- XP: {state.WonXP}\n" +
                $"- Power: { ActivePowerFraction(adoptedHero) * 100:0}% ";

            onSuccess(message);
        }
    }
}