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
                $"Class: {adoptedHero.GetClass()?.Name.ToString() ?? "No class"}\n"+
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
