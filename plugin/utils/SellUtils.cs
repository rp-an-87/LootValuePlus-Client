using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SPT.Reflection.Utils;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using EFT;
using EFT.Communications;
using EFT.Trading;
// fix VVVVVVV
// using CurrencyUtil = GClass3130;
// using FleaRequirement = GClass2335;

namespace LootValuePlus
{

	internal static class FleaUtils

	{

		public static IEftSession Session => ClientAppUtils.GetClientApp().GetClientBackEndSession();

		public static bool HasFleaSlotToSell()
		{
			return LootValueMod.IgnoreFleaMaxOfferCount.Value || Session.RagFair.MyOffersCount < Session.RagFair.GetMaxOffersCount(Session.RagFair.MyRating);
		}

		public static int GetFleaValue(IEnumerable<Item> items)
		{
			var sellableItems = items.Where(item => item.Template.CanSellOnRagfair);
			var templateIds = sellableItems.Select(item => item.TemplateId.ToString());

			var price = Task.Run(() => FleaPriceCache.FetchPrice(templateIds)).Result;

			if (!price.HasValue)
			{
				return 0;
			}

			return price.Value;
		}

		public static int GetFleaValue(Item item)
		{

			if (!item.Template.CanSellOnRagfair)
			{
				return 0;
			}

			var price = Task.Run(() => FleaPriceCache.FetchPrice(item.TemplateId)).Result;

			if (!price.HasValue)
			{
				Globals.LogDebug($"Item {item.TemplateId} had no flea value.");
				return 0;
			}

			return (int) price.Value;
		}

		// A single weapon is not compared with the flea average of its model: SPT judges the sale chance against the sum of its parts
		// (the weapon at its handbook price, every part at its flea base price). That is often far below the average, so listing a gun
		// at the average barely sells. Needs the bulk price list; false when it hasn't been loaded.
		public static bool TryGetWeaponPartsPrice(Item item, out int price)
		{
			price = 0;
			if (!LootValueMod.UseWeaponPartsPrice.Value || !(item is Weapon))
			{
				return false;
			}

			double total = 0;
			foreach (var part in item.GetAllItems())
			{
				var partPrice = part == item ? FleaPriceCache.GetStaticPrice(part.TemplateId) : FleaPriceCache.GetBasePrice(part.TemplateId);
				if (part == item && partPrice == 0)
				{
					return false;
				}
				total += partPrice;
			}

			price = (int) total;
			return true;
		}

		public static int GetFleaMarketUnitPrice(Item item)
		{
			if (!item.Template.CanSellOnRagfair)
			{
				return 0;
			}

			if (TryGetWeaponPartsPrice(item, out var partsPrice))
			{
				return partsPrice;
			}

			int unitPrice = GetFleaValue(item);
			return unitPrice;
		}

		public static int GetFleaMarketUnitPriceWithModifiers(Item item)
		{
			int price = GetFleaMarketUnitPrice(item);

			bool applyConditionReduction = LootValueMod.ReducePriceInFleaForBrokenItem.Value;
			if (applyConditionReduction)
			{
				// the same condition the server judges the offer by (not the durability bar), see SellQuality
				price = (int)(price * SellQuality.GetPriceFactor(item));
			}

			return price;
		}

		public static int GetTotalPriceOfAllSimilarItemsWithinSameContainer(Item item)
		{
			var items = GetSimilarItemsForSale(item);
			var unitPrice = GetFleaMarketUnitPriceWithModifiers(item);
			return items.Select(i => unitPrice * i.StackObjectsCount).Sum();
		}

		public static bool IsItemFleaMarketPriceBelow(Item item, int priceThreshold, bool considerMultipleItems = false)
		{
			var unitPrice = GetFleaMarketUnitPriceWithModifiers(item);
			if (considerMultipleItems) // if you have multiple items that can be sold together, the price becomes the sum of each one individually
			{
				var price = GetTotalPriceOfAllSimilarItemsWithinSameContainer(item);
				return price < priceThreshold;
			}
			else
			{
				var price = unitPrice * item.StackObjectsCount;
				return price < priceThreshold;
			}
		}


		public static bool ContainsNonFleableItemsInside(Item item)
		{
			// the rounds in an ammo box don't count: the box is sold as it is and the game itself only looks at the box
			return item.GetAllItems().Any(i => i.Template.CanSellOnRagfair == false && !(i.Parent?.Container?.ParentItem is AmmoBox));
		}

		public static bool CanBeSoldInFleaRightNow(Item item, bool displayWarning = true)
		{

			if (!Session.RagFair.Available)
			{

				if (displayWarning)
					NotificationManager.DisplayWarningNotification("Quicksell: Flea market is not available yet.");

				return false;
			}

			// we need to check if the base item is sellable
			if (!item.Template.CanSellOnRagfair)
			{

				if (displayWarning)
					NotificationManager.DisplayWarningNotification("Quicksell: Item is banned from flea market.");

				return false;
			}

			if (!HasFleaSlotToSell())
			{

				if (displayWarning)
					NotificationManager.DisplayWarningNotification("Quicksell: Maximum number of flea offers reached.");

				return false;
			}

			if (item.IsNotEmpty())
			{

				if (displayWarning)
					NotificationManager.DisplayWarningNotification("Quicksell: Item is not empty.");

				return false;

			}

			if (ContainsNonFleableItemsInside(item))
			{

				if (displayWarning)
					NotificationManager.DisplayWarningNotification("Quicksell: Item contains banned fleamarket items.");

				return false;
			}

			// fallback as any other reason will get caught here
			if (!item.CanSellOnRagfair)
			{
				if (displayWarning)
					NotificationManager.DisplayWarningNotification("Quicksell: Item can't be sold right now.");

				return false;
			}

			if (ItemUtils.IsSoftArmorInsert(item))
			{
				if (displayWarning)
					NotificationManager.DisplayWarningNotification("Quicksell: Item can't be removed from the armor.");

				return false;
			}

			var canSellPinnedItems = LootValueMod.AllowQuickSellPinned.Value;
			var canSellLockedItems = LootValueMod.AllowQuickSellLocked.Value;

			if (item.PinLockState == EItemPinLockState.Pinned && !canSellPinnedItems)
			{
				if (displayWarning)
					NotificationManager.DisplayWarningNotification("Quicksell: Item is pinned.");

				return false;
			}

			if (item.PinLockState == EItemPinLockState.Locked && !canSellLockedItems)
			{
				if (displayWarning)
					NotificationManager.DisplayWarningNotification("Quicksell: Item is locked.");

				return false;
			}

			return true;
		}

		public static bool CanSellMultipleOfItem(Item item)
		{
			bool sellMultipleEnabled = LootValueMod.SellSimilarItems.Value;
			bool sellMultipleOnlyFiR = LootValueMod.SellOnlySimilarItemsFiR.Value;
			bool isItemFindInRaid = item.MarkedAsSpawnedInSession;

			if (!sellMultipleEnabled)
			{
				return false;
			}


			if (sellMultipleOnlyFiR && !isItemFindInRaid)
			{
				return false;
			}

			return true;
		}


		public static void SellFleaItemOrMultipleItemsIfEnabled(Item item)
		{
			if (!CanSellMultipleOfItem(item))
			{
				SellToFlea(item);
				return;
			}

			var similarBundledItems = GetSimilarItemsForSale(item);
			SellToFlea(item, similarBundledItems);
		}

		public static void SellToFlea(Item itemToCheck, IEnumerable<Item> itemsToSell)
		{
			if (!CanBeSoldInFleaRightNow(itemToCheck))
			{
				return;
			}

			var price = GetFleaMarketUnitPriceWithModifiers(itemToCheck);
			var ids = itemsToSell.Select(i => i.Id).ToArray();

			ApplyFleaOffer(price, ids, GetCeilingPrice(itemToCheck), itemToCheck);
		}

		public static void SellToFlea(Item itemToSell)
		{
			if (!CanBeSoldInFleaRightNow(itemToSell))
			{
				return;
			}

			var price = GetFleaMarketUnitPriceWithModifiers(itemToSell);
			var ids = new string[1] { itemToSell.Id };

			ApplyFleaOffer(price, ids, GetCeilingPrice(itemToSell), itemToSell);
		}

		private static readonly Random random = new Random();

		// Randomizes the listing price by +/- FleaSellRandomVariancePercent around the average flea price.
		private static int ApplyRandomVariance(int price)
		{
			int variance = LootValueMod.FleaSellRandomVariancePercent.Value;
			if (variance <= 0 || price <= 0)
			{
				return price;
			}

			double factor = 1 + ((random.NextDouble() * 2) - 1) * variance / 100.0;
			return Math.Max(1, (int)Math.Round(price * factor));
		}

		// Highest unit price that still has the full sale chance, or int.MaxValue when the condition reduction is off (then the
		// listing price is not tied to the condition at all)
		private static int GetCeilingPrice(Item item)
		{
			if (!LootValueMod.ReducePriceInFleaForBrokenItem.Value)
			{
				return int.MaxValue;
			}

			var ceiling = GetFleaMarketUnitPrice(item) * (double) SellQuality.GetCeilingFactor(SellQuality.GetQuality(item));
			return ceiling >= int.MaxValue ? int.MaxValue : Math.Max(1, (int) Math.Floor(ceiling));
		}

		// Debug log: how far the cached average is from the server's average right now, and the sale chance that gives this listing
		private static void LogListing(Item item, int listedPrice)
		{
			try
			{
				var templateId = item.TemplateId;
				FleaPriceCache.cache.TryGetValue(templateId, out var cached);
				var live = FleaPriceCache.QueryLivePrice(templateId);
				// what the server compares the offer with: the parts price for a single weapon, else the average
				var target = TryGetWeaponPartsPrice(item, out var partsPrice) ? partsPrice : live;
				var quality = SellQuality.GetQuality(item);
				var age = cached == null ? -1 : (int) (DateTime.Now - cached.lastUpdate).TotalSeconds;
				var drift = cached == null || cached.price <= 0 ? 0 : (live - cached.price) * 100.0 / cached.price;
				Globals.logger.LogInfo($"Flea listing {item.ShortName.Localized()} ({templateId}): cached average {cached?.price} (age {age}s), server average now {live} ({drift:+0.0;-0.0}%), " +
					$"compared against {target}{(target != live ? " (parts price)" : "")}, quality {quality:0.###}, listed at {listedPrice}, expected sale chance {SellQuality.PredictChance(target, quality, listedPrice)}%");
			}
			catch (Exception ex)
			{
				Globals.logger.LogWarning($"Could not log the flea listing: {ex.Message}");
			}
		}

		private static void ApplyFleaOffer(int price, string[] itemIds, int ceilingPrice, Item item)
		{
			// the random variance may lower the price, but never lifts it out of the 100% sale zone
			price = Math.Min(ApplyRandomVariance(price), ceilingPrice);
			if (LootValueMod.DebugLogs.Value)
			{
				LogListing(item, price);
			}
			var offerRequeriment = new BarterTemplate()
			{
				count = price,
				_tpl = "5449016a4bdc2d6f028b456f" //id of ruble
			};

			Globals.logger.LogInfo($"Invoking Session.RagFair.AddOffer(false, '{itemIds.EnumerableToString(", ")}', ({offerRequeriment.ToPrettyJson()}), null)");
			Session.RagFair.AddOffer(false, itemIds, [offerRequeriment], null);
		}

		private static IEnumerable<Item> GetSimilarItemsForSale(Item item)
		{
			var includePinned = LootValueMod.AllowQuickSellPinned.Value; // we wanna exclude pinned items from the total calculation if this is disabled
			var includeLocked = LootValueMod.AllowQuickSellLocked.Value; // we wanna exclude locked items from the total calculation if this is disabled
			return ItemUtils.GetItemsSimilarToItemWithinSameContainer(item)
				.Where(o => o == item || includePinned || !ItemUtils.IsItemPinned(o)) // if not including pinned items, we only keep non pinned items; we still keep the item itself regardless
				.Where(o => o == item || includeLocked || !ItemUtils.IsItemLocked(o)); // if not including locked items, we only keep non locked items; we still keep the item itself regardless

		}


	}

	internal static class TraderUtils
	{

		public static IEftSession Session => ClientAppUtils.GetClientApp().GetClientBackEndSession();

		public static int GetBestTraderPrice(Item item)
		{
			var offer = GetBestTraderOffer(item);
			if (offer == null)
			{
				return 0;
			}
			return offer.RoublePrice;
		}

		public static TraderOffer GetBestTraderOffer(Item item)
		{
			if (!Session.Profile.Examined(item))
			{
				return null;
			}

			var clone = ItemUtils.CloneItemSafely(item);

			var bestOffer =
				Session.Traders
					.Where(trader => trader.Info.Available && !trader.Info.Disabled && trader.Info.Unlocked && !trader.Settings.AvailableInRaid)
					.Select(trader => GetTraderOffer(clone, trader))
					.Where(offer => offer != null)
					.Where(offer => LootValueMod.AllowForeignCurrencyTraders.Value || offer.IsRoubleOffer)
					.OrderByDescending(offer => offer.RoublePrice)
					.FirstOrDefault();

			return bestOffer;
		}

		private static TraderOffer GetTraderOffer(Item item, Trader trader)
		{
			var result = trader.GetUserItemPrice(item);

			if (result == null)
			{
				return null;
			}

			return result.HasValue ? new TraderOffer(
				trader.Id,
				trader.LocalizedName,
				result.Value.Amount,
				CurrencyUtil.GetCurrencyCharById(result.Value.CurrencyId.Value),
				trader._supplyData.CurrencyCourses[result.Value.CurrencyId.Value],
				item.StackObjectsCount
			) : null;
		}

		public static bool ShouldSellToTraderDueToPriceOrCondition(Item item)
		{
			var flags = DurabilityOrProfitConditionFlags.GetDurabilityOrProfitConditionFlagsForItem(item);
			return flags.ShouldSellItemToTraderDueToTriggeredConditions();
		}

		public static void SellToTrader(Item item)
		{
			try
			{
				TraderOffer bestTraderOffer = GetBestTraderOffer(item);

				if (bestTraderOffer == null)
				{
					NotificationManager.DisplayWarningNotification("Quicksell Error: No trader will purchase this item.");
					return;
				}

				if (item.IsNotEmpty())
				{
					NotificationManager.DisplayWarningNotification("Quicksell: item is not empty.");
					return;
				}

				var canSellPinnedItems = LootValueMod.AllowQuickSellPinned.Value;
				var canSellLockedItems = LootValueMod.AllowQuickSellLocked.Value;

				if (item.PinLockState == EItemPinLockState.Pinned && !canSellPinnedItems)
				{
					NotificationManager.DisplayWarningNotification("Quicksell: Item is pinned.");
					return;
				}

				if (item.PinLockState == EItemPinLockState.Locked && !canSellLockedItems)
				{
					NotificationManager.DisplayWarningNotification("Quicksell: Item is locked.");
					return;
				}


				SellToTrader(item, bestTraderOffer);
			}
			catch (Exception ex)
			{
				Globals.logger.LogInfo($"Something fucked up: {ex.Message}");
				Globals.logger.LogInfo($"{ex.InnerException.Message}");
			}
		}

		public static void SellToTrader(Item item, TraderOffer bestTraderOffer)
		{
			Trader tc = Session.GetTrader(bestTraderOffer.TraderId);

			FenceTrader.CG_SellItems @class = new FenceTrader.CG_SellItems();
			@class.source = new TaskCompletionSource<bool>();

			var itemRef = new EFT.Trading.TradingItemReference
			{
				Item = item,
				Count = item.StackObjectsCount
			};

			Session.ConfirmSell(tc.Id, [itemRef], bestTraderOffer.Price, new Callback(@class.method_0));
			Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.TradeOperationComplete);
		}

		


	}


	public sealed class TraderOffer
	{
		public string TraderId;
		public string TraderName;
		public int Price;
		public string Currency;
		public double Course;
		public int Count;

		public TraderOffer(string traderId, string traderName, int price, string currency, double course, int count)
		{
			TraderId = traderId;
			TraderName = traderName;
			Price = price;
			Currency = currency;
			Course = course;
			Count = count;
		}

		public const string RoubleSymbol = "₽";

		public bool IsRoubleOffer => Currency == RoubleSymbol;

		// Price converted to roubles, used for comparing against flea and between traders.
		// Price stays in the trader's own currency, as that is what ConfirmSell expects.
		public int RoublePrice => IsRoubleOffer ? Price : (int)Math.Round(Price * Course);
	}


	internal class DurabilityOrProfitConditionFlags
	{

		public bool shouldSellToTraderDueToBeingNonOperational;
		public bool shouldSellNonOperationalToFleaDueToHighProfit;
		public bool shouldSellToTraderBecauseDurabilityIsTooLow;
		public bool shouldSellToTraderBecauseFleaProfitIsTooLow;


		public DurabilityOrProfitConditionFlags(
			bool shouldSellToTraderDueToBeingNonOperational,
			bool shouldSellNonOperationalToFleaDueToHighProfit,
			bool shouldSellToTraderBecauseDurabilityIsTooLow,
			bool shouldSellToTraderBecauseFleaProfitIsTooLow
		)
		{
			this.shouldSellToTraderDueToBeingNonOperational = shouldSellToTraderDueToBeingNonOperational;
			this.shouldSellNonOperationalToFleaDueToHighProfit = shouldSellNonOperationalToFleaDueToHighProfit;
			this.shouldSellToTraderBecauseDurabilityIsTooLow = shouldSellToTraderBecauseDurabilityIsTooLow;
			this.shouldSellToTraderBecauseFleaProfitIsTooLow = shouldSellToTraderBecauseFleaProfitIsTooLow;
		}

		public static DurabilityOrProfitConditionFlags GetDurabilityOrProfitConditionFlagsForItem(Item item)
		{
			bool sellNonOperationalWeaponsToTraderEnabled = LootValueMod.SellToTraderIfWeaponIsNonOperational.Value;
			int nonOperationalFleaValueThreshold = LootValueMod.SellToFleaIfWeaponIsNonOperationalAboveThreshold.Value;
			bool shouldSellToTraderDueToBeingNonOperational = ItemUtils.IsWeaponNonOperational(item) && sellNonOperationalWeaponsToTraderEnabled;
			bool shouldSellNonOperationalToFleaDueToHighProfit = nonOperationalFleaValueThreshold > 0 && !FleaUtils.IsItemFleaMarketPriceBelow(item, nonOperationalFleaValueThreshold, false);

			bool sellTooLowDurabilityItemsToTraderEnabled = LootValueMod.SellToTraderBelowDurabilityThresholdEnabled.Value;
			int durabilityThreshold = LootValueMod.SellToTraderDurabilityThreshold.Value;
			bool shouldSellToTraderBecauseDurabilityIsTooLow = ItemUtils.IsItemBelowDurability(item, durabilityThreshold) && sellTooLowDurabilityItemsToTraderEnabled;

			bool sellTooCheapFleaItemsToTraderEnabled = LootValueMod.SellToTraderBelowProfitThresholdEnabled.Value;
			int fleaMarketProfitThreshold = LootValueMod.SellToTraderProfitThreshold.Value;
			bool shouldSellToTraderBecauseFleaProfitIsTooLow = FleaUtils.IsItemFleaMarketPriceBelow(item, fleaMarketProfitThreshold, FleaUtils.CanSellMultipleOfItem(item)) && sellTooCheapFleaItemsToTraderEnabled;


			return new DurabilityOrProfitConditionFlags(shouldSellToTraderDueToBeingNonOperational, shouldSellNonOperationalToFleaDueToHighProfit, shouldSellToTraderBecauseDurabilityIsTooLow, shouldSellToTraderBecauseFleaProfitIsTooLow);
		}

		public bool ShouldSellDueToBeingWeaponAndNonOperational()
		{
			if (this.shouldSellToTraderDueToBeingNonOperational && !this.shouldSellNonOperationalToFleaDueToHighProfit)
			{
				return true;
			}
			return false;
		}

		public bool IsBelowDurabilityThresholdOrBelowFleaProfitThreshold()
		{
			return this.shouldSellToTraderBecauseDurabilityIsTooLow || this.shouldSellToTraderBecauseFleaProfitIsTooLow;
		}

		public bool ShouldSellItemToTraderDueToTriggeredConditions()
		{

			return this.IsBelowDurabilityThresholdOrBelowFleaProfitThreshold() || this.ShouldSellDueToBeingWeaponAndNonOperational();
		}

		

	}

}