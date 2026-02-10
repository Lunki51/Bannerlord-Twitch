using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=EquipCustom}Equip Custom Item"),
     LocDescription("{=EquipCustomDesc}Equip a custom item from your inventory to all matching slots"),
     UsedImplicitly]
    public class EquipCustomItemAction : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=EquipCustomEnabled}Enabled"),
             LocCategory("General", "{=EquipCustomGeneral}General"),
             LocDescription("{=EquipCustomEnabledDesc}Enable this action"),
             PropertyOrder(1), UsedImplicitly]
            public bool Enabled { get; set; } = true;

            [LocDisplayName("{=EquipCustomGoldCost}Gold Cost"),
             LocCategory("General", "{=EquipCustomGeneral}General"),
             LocDescription("{=EquipCustomGoldCostDesc}Cost in gold to equip a custom item"),
             PropertyOrder(2), UsedImplicitly]
            public int GoldCost { get; set; } = 0;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                if (Enabled)
                {
                    generator.Value("<strong>Enabled:</strong> Yes");
                    if (GoldCost > 0)
                    {
                        generator.Value("<strong>Gold Cost:</strong> {cost}{icon}"
                            .Translate(("cost", GoldCost.ToString()), ("icon", Naming.Gold)));
                    }
                    generator.Value("<strong>Usage:</strong> !equipcustom [item name or number]");
                    generator.Value("Use without arguments to list your custom items");
                }
                else
                {
                    generator.Value("<strong>Enabled:</strong> No");
                }
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(
            Hero adoptedHero,
            ReplyContext context,
            object config,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            if (config is not Settings settings) return;

            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (!settings.Enabled)
            {
                onFailure("{=EquipCustomDisabled}This action is disabled".Translate());
                return;
            }

            if (Mission.Current != null)
            {
                onFailure("{=EquipCustomInMission}You cannot use this action during a mission!".Translate());
                return;
            }

            if (adoptedHero.HeroState == Hero.CharacterStates.Prisoner)
            {
                onFailure("{=EquipCustomPrisoner}You cannot use this action while imprisoned!".Translate());
                return;
            }

            // Check gold cost
            if (settings.GoldCost > 0 &&
                BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.GoldCost)
            {
                onFailure(Naming.NotEnoughGold(settings.GoldCost,
                    BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            // Get hero's custom items
            var customItems = BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(adoptedHero);

            if (customItems == null || !customItems.Any())
            {
                onFailure("{=EquipCustomNone}You don't have any custom items!".Translate());
                return;
            }

            // If no arguments, list custom items
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ListCustomItems(adoptedHero, customItems, onSuccess);
                return;
            }

            // Try to find the item by name or index
            EquipmentElement? itemToEquip = FindCustomItem(customItems, context.Args.Trim());

            if (!itemToEquip.HasValue)
            {
                onFailure("{=EquipCustomNotFound}Custom item '{itemName}' not found! Use !equipcustom to see your items."
                    .Translate(("itemName", context.Args.Trim())));
                return;
            }

            try
            {
                // Deduct gold cost
                if (settings.GoldCost > 0)
                {
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.GoldCost, true);
                }

                // Equip the item to all matching slots (no stat comparison - user choice)
                int slotsEquipped = EquipCustomItemToAllSlots(adoptedHero, itemToEquip.Value);

                if (slotsEquipped > 0)
                {
                    string itemName = RewardHelpers.GetItemNameAndModifiers(itemToEquip.Value);
                    string message = slotsEquipped == 1
                        ? "{=EquipCustomSuccess}Equipped {itemName}!"
                            .Translate(("itemName", itemName))
                        : "{=EquipCustomSuccessMulti}Equipped {itemName} to {count} slots!"
                            .Translate(("itemName", itemName), ("count", slotsEquipped.ToString()));

                    onSuccess(message);

                    Log.ShowInformation(
                        "{=EquipCustomLog}{heroName} equipped {itemName}!"
                            .Translate(("heroName", adoptedHero.Name.ToString()), ("itemName", itemName)),
                        adoptedHero.CharacterObject);
                }
                else
                {
                    onFailure("{=EquipCustomNoSlots}Could not find any suitable equipment slots for this item!".Translate());
                }
            }
            catch (Exception ex)
            {
                onFailure($"Failed to equip item: {ex.Message}");
                Log.Error($"EquipCustomItem error: {ex}");
            }
        }

        private void ListCustomItems(Hero hero, List<EquipmentElement> customItems, Action<string> onSuccess)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{=EquipCustomList}Your custom items:".Translate());

            for (int i = 0; i < customItems.Count; i++)
            {
                var item = customItems[i];
                string itemName = RewardHelpers.GetItemNameAndModifiers(item);
                sb.AppendLine($"{i + 1}. {itemName} ({item.Item.ItemType})");
            }

            sb.AppendLine();
            sb.AppendLine("{=EquipCustomListHelp}Use: !equipcustom [item name or number]".Translate());

            onSuccess(sb.ToString().TrimEnd());
        }

        private EquipmentElement? FindCustomItem(List<EquipmentElement> customItems, string searchTerm)
        {
            // Try to parse as index (1-based)
            if (int.TryParse(searchTerm, out int index))
            {
                if (index >= 1 && index <= customItems.Count)
                {
                    return customItems[index - 1];
                }
            }

            // Search by name (case-insensitive, partial match)
            searchTerm = searchTerm.ToLower();
            return customItems.FirstOrDefault(item =>
                item.GetModifiedItemName().ToString().ToLower().Contains(searchTerm));
        }

        private int EquipCustomItemToAllSlots(Hero hero, EquipmentElement customItem)
        {
            int slotsEquipped = 0;
            var equipment = hero.BattleEquipment;

            // Determine which slots can hold this item type
            var validSlots = GetValidSlotsForItem(customItem.Item);

            foreach (var slot in validSlots)
            {
                var currentItem = equipment[slot];

                // If slot is empty OR has same item type, equip the custom item
                // No stat comparison - this is a manual user choice
                if (currentItem.IsEmpty || currentItem.Item.ItemType == customItem.Item.ItemType)
                {
                    equipment[slot] = customItem;
                    slotsEquipped++;
                }
            }

            return slotsEquipped;
        }

        private List<EquipmentIndex> GetValidSlotsForItem(ItemObject item)
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

                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts:
                case ItemObject.ItemTypeEnum.Bullets:
                    // Ammo typically doesn't get equipped via this system
                    break;
            }

            return slots;
        }
    }
}