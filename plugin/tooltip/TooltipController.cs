using System;
using System.Linq;
using System.Reflection;
using SPT.Reflection.Patching;
using EFT.InventoryLogic;
using EFT.UI;
using static LootValuePlus.TooltipUtils;
using SPT.Reflection.Utils;
using EFT.UI.Screens;
using UnityEngine;

namespace LootValuePlus
{

	internal class TooltipController
	{

		private static SimpleTooltip tooltip;

		public static ISession Session => ClientAppUtils.GetMainApp().GetClientBackEndSession();

		public static void SetupTooltip(SimpleTooltip _tooltip, ref float delay)
		{
			tooltip = _tooltip;
			delay = 0;
		}

		public static void ClearTooltip()
		{
			tooltip?.Close();
			tooltip = null;
		}


		internal class ShowTooltipPatch : ModulePatch
		{

			protected override MethodBase GetTargetMethod()
			{
				return typeof(SimpleTooltip)
					.GetMethods(BindingFlags.Instance | BindingFlags.Public)
					.Where(x => x.Name == "Show")
					.ToList()[0];
			}

			[PatchPrefix]
			private static void Prefix(ref string text, ref Vector2? offset, ref float delay, SimpleTooltip __instance)
			{
				SetupTooltip(__instance, ref delay);

				var item = HoverItemController.hoveredItem;
				if (item == null || tooltip == null)
				{
					return;
				}

				bool pricesTooltipEnabled = LootValueMod.ShowPrices.Value;
				if (pricesTooltipEnabled == false)
				{
					return;
				}

				bool canQuickSellOnCurrentScreen = ScreenChangeController.CanQuickSellOnCurrentScreen();
				bool shouldShowPricesTooltipwhileInRaid = LootValueMod.ShowFleaPricesInRaid.Value;
				bool hideLowerPrice = LootValueMod.HideLowerPrice.Value;
				bool hideLowerPriceInRaid = LootValueMod.HideLowerPriceInRaid.Value;
				bool showFleaPriceBeforeAccess = LootValueMod.ShowFleaPriceBeforeAccess.Value;
				bool hasFleaMarketAvailable = Session.RagFair.Available;

				bool isInRaid = Globals.HasRaidStarted();

				if (!shouldShowPricesTooltipwhileInRaid && isInRaid)
				{
					return;
				}

				var shouldShowItemPriceTooltipBasedOnCurrentScreen = ScreenChangeController.CanShowItemPriceTooltipsOnCurrentScreen();
				if (ItemUtils.ItemBelongsToTraderOrFleaMarketOrMail(item) || !shouldShowItemPriceTooltipBasedOnCurrentScreen)
				{
					return;
				}

				var durability = ItemUtils.GetResourcePercentageOfItem(item);
				var missingDurability = 100 - durability * 100;

				int stackAmount = item.StackObjectsCount;
				bool isItemEmpty = item.IsEmpty();
				bool applyConditionReduction = LootValueMod.ReducePriceInFleaForBrokenItem.Value;

				int finalFleaPrice = FleaUtils.GetFleaMarketUnitPriceWithModifiers(item) * stackAmount;
				bool canBeSoldToFlea = finalFleaPrice > 0;

				var finalTraderPrice = TraderUtils.GetBestTraderPrice(item);
				bool canBeSoldToTrader = finalTraderPrice > 0;

				// determine price per slot for each sale type				
				var size = item.CalculateCellSize();
				int slots = size.X * size.Y;

				int pricePerSlotTrader = finalTraderPrice / slots;
				int pricePerSlotFlea = finalFleaPrice / slots;


				bool isTraderPriceHigherThanFlea = finalTraderPrice > finalFleaPrice;
				bool isFleaPriceHigherThanTrader = finalFleaPrice > finalTraderPrice;
				bool sellToTrader = isTraderPriceHigherThanFlea;
				bool sellToFlea = !sellToTrader;

				// If both trader and flea are 0, then the item is not purchasable.
				if (!canBeSoldToTrader && !canBeSoldToFlea)
				{
					AppendFullLineToTooltip(ref text, "(Item can't be sold)", 11, "#AA3333");
					return;
				}

				var fleaPricesForWeaponMods = 0;
				var shouldShowNonVitalModsPartsOfItem = LootValueMod.ShowNonVitalWeaponPartsFleaPrice.Value;
				if (shouldShowNonVitalModsPartsOfItem && ItemUtils.IsItemWeapon(item))
				{

					var nonVitalMods = ItemUtils.GetWeaponNonVitalMods(item);
					fleaPricesForWeaponMods = FleaUtils.GetFleaValue(nonVitalMods);
				}

				// TODO: add another thing that fetches price of all items within item if pressing a modifier, which should not apply to weapons

				if (sellToFlea && !hasFleaMarketAvailable)
				{
					sellToFlea = false;
					sellToTrader = true;
					isTraderPriceHigherThanFlea = true;
					isFleaPriceHigherThanTrader = false;
					canBeSoldToFlea = false;
					AppendFullLineToTooltip(ref text, $"(Flea market is not available)", 11, "#AAAA33");
				}

				var quickSellEnabled = LootValueMod.EnableQuickSell.Value;
				var quickSellUsesOneButton = LootValueMod.OneButtonQuickSell.Value;
				var showQuickSaleCommands = quickSellEnabled && !isInRaid;
				var shouldSellToTraderDueToPriceOrCondition = TraderUtils.ShouldSellToTraderDueToPriceOrCondition(item);

				if (sellToFlea
						&& shouldSellToTraderDueToPriceOrCondition
						&& !isInRaid
						&& quickSellUsesOneButton
						&& canQuickSellOnCurrentScreen)
				{
					isTraderPriceHigherThanFlea = true;
					isFleaPriceHigherThanTrader = false;
					sellToTrader = true;
					sellToFlea = false;

					var reason = GetReasonForItemToBeSoldToTrader(item);
					AppendFullLineToTooltip(ref text, $"(Selling to <b>Trader</b> {reason})", 11, "#AAAA33");
				}

				var showTraderPrice = true;
				if (hideLowerPrice && isFleaPriceHigherThanTrader)
				{
					showTraderPrice = false;
				}
				if (hideLowerPriceInRaid && isInRaid && isFleaPriceHigherThanTrader)
				{
					showTraderPrice = false;
				}
				if (finalTraderPrice == 0)
				{
					showTraderPrice = false;
				}

				if (canBeSoldToTrader || canBeSoldToFlea)
				{
					AppendSeparator(ref text, appendNewLineAfter: false);
				}

				// append trader price on tooltip
				if (showTraderPrice)
				{
					AppendNewLineToTooltipText(ref text);

					// append trader price
					var traderName = $"Trader: ";
					var traderNameColor = sellToTrader ? "#ffffff" : "#444444";
					var traderPricePerSlotColor = sellToTrader ? SlotColoring.GetColorFromValuePerSlots(pricePerSlotTrader) : "#444444";
					var fontSize = sellToTrader ? 14 : 10;

					StartSizeTag(ref text, fontSize);

					AppendTextToToolip(ref text, traderName, traderNameColor);
					AppendTextToToolip(ref text, $"₽ {finalTraderPrice.FormatNumber()}", traderPricePerSlotColor);

					if (stackAmount > 1)
					{
						var unitPrice = $" (₽ {(finalTraderPrice / stackAmount).FormatNumber()} e.)";
						AppendTextToToolip(ref text, unitPrice, "#333333");
					}

					EndSizeTag(ref text);

				}

				var showFleaPrice = true;
				if (hideLowerPrice && isTraderPriceHigherThanFlea)
				{
					showFleaPrice = false;
				}
				if (hideLowerPriceInRaid && isInRaid && isTraderPriceHigherThanFlea)
				{
					showFleaPrice = false;
				}
				if (finalFleaPrice == 0)
				{
					showFleaPrice = false;
				}
				if (!hasFleaMarketAvailable && !showFleaPriceBeforeAccess)
				{
					showFleaPrice = false;
				}

				// append flea price on the tooltip
				if (showFleaPrice)
				{
					AppendNewLineToTooltipText(ref text);

					// append flea price
					var fleaName = $"Flea: ";
					var fleaNameColor = sellToFlea ? "#ffffff" : "#444444";
					var fleaPricePerSlotColor = sellToFlea ? SlotColoring.GetColorFromValuePerSlots(pricePerSlotFlea) : "#444444";
					var fontSize = sellToFlea ? 14 : 10;

					StartSizeTag(ref text, fontSize);

					AppendTextToToolip(ref text, fleaName, fleaNameColor);
					AppendTextToToolip(ref text, $"₽ {finalFleaPrice.FormatNumber()}", fleaPricePerSlotColor);

					if (applyConditionReduction)
					{
						if (missingDurability >= 1.0f)
						{
							var missingDurabilityText = $" (-{(int)missingDurability}%)";
							AppendTextToToolip(ref text, missingDurabilityText, "#AA1111");
						}
					}


					if (stackAmount > 1)
					{
						var unitPrice = $" (₽ {FleaUtils.GetFleaMarketUnitPriceWithModifiers(item).FormatNumber()} e.)";
						AppendTextToToolip(ref text, unitPrice, "#333333");
					}

					EndSizeTag(ref text);

					// Only show this out of raid
					if (!isInRaid && !isTraderPriceHigherThanFlea)
					{
						if (FleaUtils.ContainsNonFleableItemsInside(item))
						{
							AppendFullLineToTooltip(ref text, "(Contains banned flea items inside)", 11, "#AA3333");
							canBeSoldToFlea = false;
						}

					}

					var canSellPinnedItems = LootValueMod.AllowQuickSellPinned.Value;
					var canSellLockedItems = LootValueMod.AllowQuickSellLocked.Value;

					if (item.PinLockState == EItemPinLockState.Pinned && !canSellPinnedItems)
					{
						AppendFullLineToTooltip(ref text, "(Item is pinned)", 11, "#AA3333");
						canBeSoldToFlea = false;
					}
					
					if ( item.PinLockState == EItemPinLockState.Locked && !canSellLockedItems)
					{
						AppendFullLineToTooltip(ref text, "(Item is locked)", 11, "#AA3333");
						canBeSoldToFlea = false;
					}

				}

				if (fleaPricesForWeaponMods > 0 && hasFleaMarketAvailable)
				{
					AppendNewLineToTooltipText(ref text);
					var color = SlotColoring.GetColorFromTotalValue(fleaPricesForWeaponMods);
					StartSizeTag(ref text, 12);
					AppendTextToToolip(ref text, $"₽ {fleaPricesForWeaponMods.FormatNumber()} ", color);
					AppendTextToToolip(ref text, $"in parts (flea)", "#555555");
					EndSizeTag(ref text);
				}

				// TODO: add a (configurable) HOLD key modifier to show ALL parts of weapon, not just the non vital mods

				if (!isInRaid)
				{
					if (!isItemEmpty)
					{
						AppendFullLineToTooltip(ref text, "(Item is not empty)", 11, "#AA3333");
						canBeSoldToFlea = false;
						canBeSoldToTrader = false;
					}
				}

				var shouldShowFleaMarketEligibility = LootValueMod.ShowFleaMarketEligibility.Value;
				if (shouldShowFleaMarketEligibility && !item.Template.CanSellOnRagfair)
				{
					AppendFullLineToTooltip(ref text, "(Item is banned from flea market)", 11, "#AA3333");
				}

				var shouldShowPricePerSlotAndPerKgInRaid = LootValueMod.ShowPricePerKgAndPerSlotInRaid.Value;
				var shouldShowPricePerSlotAndPerKgOutRaid = LootValueMod.ShowPricePerKgAndPerSlotOutOfRaid.Value;
				var shouldShowPricePerKgWeight = (isInRaid && shouldShowPricePerSlotAndPerKgInRaid) || (!isInRaid && shouldShowPricePerSlotAndPerKgOutRaid);

				if (shouldShowPricePerKgWeight)
				{
					var pricePerSlot = sellToTrader ? pricePerSlotTrader : pricePerSlotFlea;
					var unitPrice = sellToTrader ? (finalTraderPrice / stackAmount) : FleaUtils.GetFleaMarketUnitPriceWithModifiers(item);
					var pricePerWeight = (int)(unitPrice / item.TotalWeight);

					if (item.TotalWeight.ApproxEquals(0.0f))
					{
						pricePerWeight = 0;
					}

					AppendSeparator(ref text);
					StartSizeTag(ref text, 11);
					AppendTextToToolip(ref text, $"₽ / KG\t{pricePerWeight.FormatNumber()}", "#555555");
					AppendNewLineToTooltipText(ref text);
					AppendTextToToolip(ref text, $"₽ / SLOT\t{pricePerSlot.FormatNumber()}", "#555555");
					EndSizeTag(ref text);
				}

				if (showQuickSaleCommands && canQuickSellOnCurrentScreen)
				{
					if (quickSellUsesOneButton)
					{

						bool canBeSold = (sellToFlea && canBeSoldToFlea) ||
														 (sellToTrader && canBeSoldToTrader);

						if (canBeSold)
						{
							AppendSeparator(ref text);
							AppendTextToToolip(ref text, $"Sell with Alt+Shift+Click", "#888888");
							if (canBeSoldToFlea && sellToFlea)
							{
								AddMultipleItemsSaleSection(ref text, item);
							}
						}

					}
					else
					{
						if (canBeSoldToFlea || canBeSoldToTrader)
						{
							AppendSeparator(ref text);
						}

						if (canBeSoldToTrader)
						{
							AppendTextToToolip(ref text, $"Sell to Trader with Alt+Shift+Left Click", "#888888");
						}

						if (canBeSoldToFlea && canBeSoldToTrader)
						{
							AppendNewLineToTooltipText(ref text);
						}

						if (canBeSoldToFlea)
						{
							AppendTextToToolip(ref text, $"List to Flea with Alt+Shift+Right Click", "#888888");
							AddMultipleItemsSaleSection(ref text, item);
						}
					}

				}


			}

			private static void AddMultipleItemsSaleSection(ref string text, Item item)
			{
				bool canSellSimilarItems = FleaUtils.CanSellMultipleOfItem(item);
				if (canSellSimilarItems)
				{
					// append only if more than 1 item will be sold due to the flea market action
					var includePinned = LootValueMod.AllowQuickSellPinned.Value;
					var includeLocked = LootValueMod.AllowQuickSellLocked.Value;
					var amountOfItems = ItemUtils.CountItemsSimilarToItemWithinSameContainer(item, includePinned, includeLocked);
					if (amountOfItems > 1)
					{
						var totalPrice = FleaUtils.GetTotalPriceOfAllSimilarItemsWithinSameContainer(item);
						AppendFullLineToTooltip(ref text, $"(Will list {amountOfItems} similar items in flea for ₽ {totalPrice.FormatNumber()})", 10, "#555555");
					}

				}
			}

			private static string GetReasonForItemToBeSoldToTrader(Item item)
			{
				var flags = DurabilityOrProfitConditionFlags.GetDurabilityOrProfitConditionFlagsForItem(item);
				if (flags.shouldSellToTraderDueToBeingNonOperational)
				{
					return "due to being non operational";
				}
				else if (flags.shouldSellToTraderBecauseDurabilityIsTooLow)
				{
					return "due to low durability";
				}
				else if (flags.shouldSellToTraderBecauseFleaProfitIsTooLow)
				{
					return "due to low flea profit";
				}
				return "due to no reason :)";
			}

		}

	}



}