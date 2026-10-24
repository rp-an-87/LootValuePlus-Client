using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;

namespace LootValuePlus
{

	// The flea sell chance is decided by the server from the item's condition, not from what the durability bar shows.
	// This mirrors the server's ItemHelper.GetItemQualityModifierForItems and RagfairSellHelper.CalculateSellChance, so an offer can be
	// priced inside the 100% sale zone:
	//   chance = base * quality * m^4, with m = (average * quality / listed price) * multiplier, capped at 100
	internal static class SellQuality
	{
		private const float MaxSellChance = 100f;

		// Quality (0-1) the server compares the offer against: a weapon counts by its own condition only, anything else by the average
		// of the item and everything inside it (plates in armor, for example)
		public static float GetQuality(Item item)
		{
			if (item == null)
			{
				return 1f;
			}

			if (item is Weapon)
			{
				return GetItemQuality(item, out var weaponQuality) ? weaponQuality : 1f;
			}

			float sum = 0f;
			int counted = 0;
			foreach (var part in item.GetAllItems())
			{
				if (GetItemQuality(part, out var quality))
				{
					sum += quality;
					counted++;
				}
			}

			// like the server: rigs without plates and the like count as 100%
			return counted == 0 ? 1f : Mathf.Min(sum / counted, 1f);
		}

		// False for armor that has no durability of its own (the server skips those and averages their plates instead)
		private static bool GetItemQuality(Item item, out float quality)
		{
			quality = 1f;

			if (item.Template is ArmorTemplate armorTemplate && armorTemplate.MaxDurability == 0)
			{
				return false;
			}

			var medKit = item.GetItemComponent<MedKitComponent>();
			var repairable = item.GetItemComponent<RepairableComponent>();
			var food = item.GetItemComponent<FoodDrinkComponent>();
			var key = item.GetItemComponent<KeyComponent>();
			var resource = item.GetItemComponent<ResourceComponent>();
			var repairKit = item.GetItemComponent<RepairKitComponent>();

			if (medKit != null)
			{
				quality = medKit.RelativeValue;
			}
			else if (repairable != null)
			{
				// the server uses the square root of the durability fraction, so a half broken item counts as ~71%, not 50%
				var maxDurability = repairable.TemplateDurability;
				var fraction = maxDurability > 0 ? repairable.Durability / maxDurability : 0f;
				quality = fraction <= 0f ? 1f : Mathf.Sqrt(Mathf.Min(fraction, 1f));  // the server treats 0 durability as invalid and uses 100%
				return true;
			}
			else if (food != null)
			{
				quality = food.RelativeValue;
			}
			else if (key != null && key.NumberOfUsages > 0)
			{
				// keys count their uses upwards
				quality = key.RelativeValue;
			}
			else if (resource != null && resource.MaxResource > 0f && resource.Value < resource.MaxResource)
			{
				quality = resource.RelativeValue;
			}
			else if (repairKit != null)
			{
				var maxResource = repairKit.CG_Ctor();
				quality = maxResource > 0f ? repairKit.Resource / maxResource : 1f;
			}

			if (quality <= 0f)
			{
				quality = 0.01f;
			}

			return true;
		}

		// Highest price, relative to the server average for an item in perfect condition, that still has the full sale chance
		// for the given quality
		public static float GetCeilingFactor(float quality)
		{
			quality = Mathf.Clamp(quality, 0.01f, 1f);
			var baseChance = Mathf.Max(1f, LootValueMod.SellChanceBase.Value);
			return quality * LootValueMod.SellChanceMultiplier.Value * Mathf.Pow(baseChance * quality / MaxSellChance, 0.25f);
		}

		// The server's sell chance (percent) for an offer listed at listedPrice when the server average is averagePrice
		public static float PredictChance(float averagePrice, float quality, float listedPrice)
		{
			if (listedPrice <= 0f)
			{
				return MaxSellChance;
			}

			var m = averagePrice * quality / listedPrice * LootValueMod.SellChanceMultiplier.Value;
			return Mathf.Clamp(Mathf.Round(LootValueMod.SellChanceBase.Value * quality * Mathf.Pow(m, 4f)), 0f, MaxSellChance);
		}

		// The price factor to list at: the server compares against average * quality, but below perfect condition the sale chance
		// only stays at 100% a bit under that
		public static float GetPriceFactor(Item item)
		{
			var quality = GetQuality(item);
			return Mathf.Min(quality, GetCeilingFactor(quality));
		}
	}

}
