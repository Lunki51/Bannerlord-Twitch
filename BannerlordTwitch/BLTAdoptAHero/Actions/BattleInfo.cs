using System;
using System.Linq;
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

            if (agent == null)
            {
                onFailure("Hero is not currently in battle!");
                return;
            }

            var state = BLTAdoptAHeroCommonMissionBehavior.Current.GetMissionState(adoptedHero);

            static float ActivePowerFractionRemaining(Hero hero)
            {
                var classDef = BLTAdoptAHeroCampaignBehavior.Current?.GetClass(hero);
                (float duration, float remaining) = classDef?.ActivePower?.DurationRemaining(hero) ?? (1, 0);
                return duration == 0 ? 0 : remaining / duration;
            }


            // Mounted info
            string mountInfo ="";
            if (agent.MountAgent != null)
            {
                mountInfo = $"{agent.MountAgent.Health}/{agent.MountAgent.HealthLimit}";
            }

            string weaponInfo = "Unarmed";
            var equipment = agent.Equipment;

            // Try Weapon0 first, then Weapon1
            EquipmentIndex usedSlot = EquipmentIndex.None;
            if (equipment[EquipmentIndex.Weapon0].Item != null)
                usedSlot = EquipmentIndex.Weapon0;
            else if (equipment[EquipmentIndex.Weapon1].Item != null)
                usedSlot = EquipmentIndex.Weapon1;

            if (usedSlot != EquipmentIndex.None)
            {
                var currentWeapon = equipment[usedSlot];
                var item = currentWeapon.Item; // <-- Get the actual ItemObject
                if (item != null)
                {
                    var itemType = item.ItemType;
                    string ammoInfo = "";

                    // Check for ranged/thrown weapons
                    if (itemType is ItemObject.ItemTypeEnum.Bow or ItemObject.ItemTypeEnum.Crossbow
                                    or ItemObject.ItemTypeEnum.Pistol or ItemObject.ItemTypeEnum.Musket
                                    or ItemObject.ItemTypeEnum.Thrown)
                    {
                        int ammo = equipment.GetAmmoAmount(usedSlot);
                        int maxAmmo = equipment.GetMaxAmmo(usedSlot);
                        ammoInfo = $" - Ammo: {ammo}/{maxAmmo}";
                    }

                    weaponInfo = $"{item.Name} ({itemType}){ammoInfo}";
                }
            }

            string message =
                $"Hero {adoptedHero.Name} Battle Info:\n" +
                $"HP: {agent.Health}/{agent.HealthLimit}\n";
            if (agent.MountAgent != null)
                message += $"- Mounted: {mountInfo}\n";

            message +=
                $"- Weapon: {weaponInfo}\n" +
                $"- Kills: {state.Kills}\n" +
                $"- Retinue: {state.RetinueKills}\n" +
                $"- Gold: {state.WonGold}\n" +
                $"- XP: {state.WonXP}\n" +
                $"- Power: {ActivePowerFractionRemaining(adoptedHero):0}";

            onSuccess(message);
        }
    }
}
