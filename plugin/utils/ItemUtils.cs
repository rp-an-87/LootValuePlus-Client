using System.Linq;
using EFT.InventoryLogic;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EFT.Visual;
using HarmonyLib;
using System;

namespace LootValuePlus
{

	internal class ItemUtils
	{

		public static float GetResourcePercentageOfItem(Item item)
		{
			if (item == null)
			{
				return 1.0f;
			}

			if (item.GetItemComponent<RepairableComponent>() != null)
			{
				var repairable = item.GetItemComponent<RepairableComponent>();

				var actualMax = repairable.TemplateDurability;
				var currentDurability = repairable.Durability;
				var currentPercentage = currentDurability / actualMax;
				return currentPercentage;

			}
			else if (item.GetItemComponent<MedKitComponent>() != null)
			{

				return item.GetItemComponent<MedKitComponent>().RelativeValue;

			}
			else if (item.GetItemComponent<FoodDrinkComponent>() != null)
			{

				return item.GetItemComponent<FoodDrinkComponent>().RelativeValue;

			}
			else if (item.GetItemComponent<ResourceComponent>() != null)
			{

				// some barter items are considered resources, although they have no max value / value or anything. Must be a leftover from BSG
				if (item.GetItemComponent<ResourceComponent>().MaxResource.ApproxEquals(0.0f))
				{
					return 1.0f;
				}

				return item.GetItemComponent<ResourceComponent>().RelativeValue;

			}
			else if (item.GetItemComponent<RepairKitComponent>() != null)
			{

				var component = item.GetItemComponent<RepairKitComponent>();
				var currentResource = component.Resource;
				// method 0 returns max value of template
				var maxResource = component.method_0();
				return currentResource / maxResource;

			}
			else if (item.GetItemComponent<ArmorHolderComponent>() != null)
			{
				var component = item.GetItemComponent<ArmorHolderComponent>();

				if (component.LockedArmorPlates.Count() == 0)
				{
					return 1.0f;
				}

				var maxDurabilityOfAllBasePlates = component.LockedArmorPlates.Sum(plate => plate.Armor.Repairable.TemplateDurability);
				var currentDurabilityOfAllBasePlates = component.LockedArmorPlates.Sum(plate => plate.Armor.Repairable.Durability);
				var currentPercentage = currentDurabilityOfAllBasePlates / maxDurabilityOfAllBasePlates;
				return currentPercentage;
			}

			return 1.0f;

		}

		public static bool IsWeaponNonOperational(Item item)
		{
			if (!(item is Weapon weapon))
			{
				return false;
			}
			return weapon.MissingVitalParts.Any<Slot>();
		}

		public static IEnumerable<Item> GetWeaponNonVitalMods(Item item)
		{
			if (!(item is Weapon weapon))
			{
				return new Collection<Item>();
			}

			var mods = weapon.Mods;
			var vitalItems = weapon.VitalParts
									.Select(slot => slot.ContainedItem) // get all vital items from their respective slot
									.Where(contained => contained != null); // only keep those not null

			var nonVitalMods = mods.Where(mod => !vitalItems.Contains(mod));
			return nonVitalMods;
		}

		public static IEnumerable<Item> GetContainedSellableItems(Item item)
		{
			return item.GetAllItems()
					.Where(i => i.CanSellOnRagfair)
					.Where(i => !IsSoftArmorInsert(i))
					.Where(i => i != item);
		}

		public static bool IsSoftArmorInsert(Item item)
		{
			return TemplateIdToObjectMappingsClass.TypeTable[item.Template.ParentId] == typeof(BuiltInInsertsItemClass);
		}

		public static bool IsItemWeapon(Item item)
		{
			if (item is Weapon)
			{
				return true;
			}

			return false;
		}

		public static bool IsItemArmoredRig(Item item)
		{
			if (item is VestItemClass)
			{
				return true;
			}
			return false;
		}

		// This seems to work to Everything but armored rigs
		// If the item is not empty, it will not properly calculate the trader offer price, as it will include all items inside too
		// For that reason, we clone the base item, and that will not clone it's internal inventory
		// For some reason the price calculation works for cloned armors, weapons, helmets, but not for armored rigs
		// It's not really that important as armored rigs are almost always more expensive in the flea market anyways
		public static Item CloneItemSafely(Item item)
		{
			var clone = item.CloneVisibleItem();
			clone.UnlimitedCount = false;
			return clone;
		}


		public static bool IsItemBelowDurability(Item hoveredItem, int durabilityThreshold)
		{
			var currentDurability = GetResourcePercentageOfItem(hoveredItem) * 100;
			return currentDurability < durabilityThreshold;
		}


		public static bool ItemBelongsToTraderOrFleaMarketOrMail(Item item)
		{

			if (item == null)
			{
				return false;
			}

			if (item.Owner == null)
			{
				return false;
			}


			var ownerType = item.Owner.OwnerType;
			if (EOwnerType.Trader.Equals(ownerType))
			{
				return true;
			}
			else if (EOwnerType.RagFair.Equals(ownerType))
			{
				return true;
			}
			else if (EOwnerType.Mail.Equals(ownerType))
			{
				return true;
			}

			return false;

		}

		public static bool IsItemInPlayerInventory(Item item)
		{
			var ownerType = item?.Owner?.OwnerType;
			if (ownerType == null)
			{
				return false;
			}

			return EOwnerType.Profile.Equals(ownerType);
		}

		/**
		* Includes original item, always, even if locked/pinned!
		*/
		public static IEnumerable<Item> GetItemsSimilarToItemWithinSameContainer(Item item)
		{

			if (item?.Parent?.Container == null)
			{
				return [];
			}

			var itemsOfParent = item.Parent?.Container?.Items ?? [];

			return itemsOfParent
				.Where(o => item.Compare(o)) // select all same items
				.Where(o => o.MarkedAsSpawnedInSession == item.MarkedAsSpawnedInSession); // all must have same FiR status
		}

		public static bool IsItemLocked(Item item)
		{
			return item.PinLockState == EItemPinLockState.Locked;
		}

		public static bool IsItemPinned(Item item)
		{
			return item.PinLockState == EItemPinLockState.Pinned;
		}

		/**
		* Includes original item!
		*/
		public static int CountItemsSimilarToItemWithinSameContainer(Item item)
		{
			return GetItemsSimilarToItemWithinSameContainer(item).Count();
		}

        public static float GetTemplateWeight(Item item)
        {
			// this is so we handle the base weight of equipment with extra things inside
			return CloneItemSafely(item).TotalWeight / Math.Max(item.StackObjectsCount, 1);
        }
    }


}