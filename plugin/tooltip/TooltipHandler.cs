using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using SPT.Reflection.Utils;
using UnityEngine;
using static LootValuePlus.TooltipController;

namespace LootValuePlus
{
    // TODO: Only display flea market price if on trader sell/buy screen

    internal class ItemTooltipContext : IDisposable
    {

        public Item Item { get; private set; }
        public TooltipCfg TooltipCfg { get; private set; }
        public ItemState ItemState { get; private set; }
        public GameState GameState { get; private set; }
        public FleaPriceState FleaState { get; private set; }
        public TraderPriceState TraderState { get; private set; }
        public PriceState PriceState { get; private set; }
        public SellabilityState SellabilityState { get; private set; }
        public DisplayPriceState DisplayPriceState { get; private set; }
        public PricePerSlotAndKgState PricePerSlotAndKgState { get; private set; }

        internal ItemTooltipContext(Item item)
        {
            Item = item;
            GameState = new GameState();
            ItemState = new ItemState(item);
            TooltipCfg = new TooltipCfg(GameState, ItemState);
            FleaState = new FleaPriceState(ItemState, TooltipCfg, item);
            TraderState = new TraderPriceState(ItemState, item);
            PriceState = new PriceState(FleaState, TraderState);
            SellabilityState = new SellabilityState(TooltipCfg, PriceState, GameState, ItemState);
            DisplayPriceState = new DisplayPriceState(TooltipCfg, PriceState, GameState, SellabilityState);
            PricePerSlotAndKgState = new PricePerSlotAndKgState(ItemState, TooltipCfg, TraderState, FleaState, SellabilityState);
        }

        public void Dispose()
        {
            ItemState.Dispose();
            FleaState.Dispose();
            DisplayPriceState.Dispose();

            Item = null;
            GameState = null;
            TooltipCfg = null;
            FleaState = null;
            TraderState = null;
            PriceState = null;
            SellabilityState = null;
            DisplayPriceState = null;
            PricePerSlotAndKgState = null;
        }
    }

    internal record GameState
    {
        public static ISession Session => ClientAppUtils.GetMainApp().GetClientBackEndSession();

        public readonly bool HasFleaMarketAvailable;
        public readonly bool CanQuickSellOnCurrentScreen;
        public readonly bool IsInRaid;
        public readonly bool PressingAlt;

        public GameState()
        {
            HasFleaMarketAvailable = Session.RagFair.Available;
            CanQuickSellOnCurrentScreen = ScreenChangeController.CanQuickSellOnCurrentScreen();
            IsInRaid = Globals.HasRaidStarted();
            PressingAlt = Input.GetKey(KeyCode.LeftAlt);
        }


    }

    internal class ItemState : IDisposable
    {
        public readonly float MissingDurability;
        public readonly int UnitFleaPrice;
        public readonly int FullPrice;
        public readonly int StackAmount;
        public readonly bool IsEmpty;
        public readonly int UnitFleaPriceWithModifiers;
        public readonly int FullPriceWithModifiers;
        public readonly int Slots;
        public readonly bool IsWeapon;
        public readonly bool IsSoftArmorInsert; public readonly bool ContainsNonFleableItemsInside;
        public readonly bool ShouldSellToTraderDueToPriceOrCondition;
        public readonly bool IsPinned;
        public readonly bool IsLocked;
        public readonly bool CanSellOnFleaMarket;
        public readonly float TotalWeight;
        public readonly bool CanSellMultipleOfItem;

        private Item Item;
        private IEnumerable<Item> SimilarItemsInContainer;

        public ItemState(Item item)
        {
            Item = item;
            StackAmount = item.StackObjectsCount;
            IsEmpty = item.IsEmpty();
            var durability = ItemUtils.GetResourcePercentageOfItem(item);
            MissingDurability = 100 - durability * 100;
            var size = item.CalculateCellSize();
            Slots = size.X * size.Y;
            IsWeapon = ItemUtils.IsItemWeapon(item);
            IsSoftArmorInsert = ItemUtils.IsSoftArmorInsert(item);
            ContainsNonFleableItemsInside = FleaUtils.ContainsNonFleableItemsInside(item);
            ShouldSellToTraderDueToPriceOrCondition = TraderUtils.ShouldSellToTraderDueToPriceOrCondition(item);
            IsPinned = ItemUtils.IsItemPinned(item);
            IsLocked = ItemUtils.IsItemLocked(item);
            CanSellOnFleaMarket = item.Template.CanSellOnRagfair;
            TotalWeight = ItemUtils.CalculateWeightForItem(item);
            CanSellMultipleOfItem = FleaUtils.CanSellMultipleOfItem(item);
            SimilarItemsInContainer = ItemUtils.GetItemsSimilarToItemWithinSameContainer(item);
        }

        public bool IsStack()
        {
            return StackAmount > 1;
        }

        public bool IsDamaged()
        {
            return MissingDurability > 1.0f;
        }

        public int CountSimilarItems(bool includePinned, bool includeLocked)
        {
            return SimilarItemsInContainer
                .Where(o => o == Item || includePinned || !ItemUtils.IsItemPinned(o)) // if not including pinned items, we only keep non pinned items; we still keep the item itself regardless
                .Where(o => o == Item || includeLocked || !ItemUtils.IsItemLocked(o))  // if not including locked items, we only keep non locked items; we still keep the item itself regardless
                .Count();
        }

        public void Dispose()
        {
            Item = null;
            SimilarItemsInContainer = null;
        }
    }

    internal record TooltipCfg
    {

        public readonly bool ShowTooltipInRaid;
        public readonly bool ShouldShowPricesTooltipWhileInRaid;
        public readonly bool HideLowerPrice;
        public readonly bool HideLowerPriceInRaid;
        public readonly bool ShowFleaPriceBeforeAccess;
        public readonly bool ApplyConditionReduction;
        public readonly bool ShowNonVitalWeaponPartsFleaPrice;
        public readonly bool ShowContainedItemFleaPrices;
        public readonly bool ContainedItemFleaPricesOverridesKgAndSlotPrice;
        public readonly bool QuickSellEnabled;
        public readonly bool QuickSellUsesOneButton;
        public readonly bool CanSellPinnedItems;
        public readonly bool CanSellLockedItems;
        public readonly bool ShowFleaMarketEligibility;
        public readonly bool ShouldShowPricePerKgSlot;
        public readonly bool ShouldShowFleaMarketPrices;
        public readonly bool IsViewingContainedItemsPrice;
        public readonly bool ShowQuickSaleCommands;
        public readonly bool ShowNonVitalPartModsPrices;
        public readonly bool OverridePricePerKgSlotWithContainedItemsFleaValue;

        public TooltipCfg(GameState gameState, ItemState itemState)
        {
            ShowTooltipInRaid = LootValueMod.ShowFleaPricesInRaid.Value;
            ShouldShowPricesTooltipWhileInRaid = LootValueMod.ShowFleaPricesInRaid.Value;
            HideLowerPrice = LootValueMod.HideLowerPrice.Value;
            HideLowerPriceInRaid = LootValueMod.HideLowerPriceInRaid.Value;
            ShowFleaPriceBeforeAccess = LootValueMod.ShowFleaPriceBeforeAccess.Value;
            ApplyConditionReduction = LootValueMod.ReducePriceInFleaForBrokenItem.Value;
            ShowNonVitalWeaponPartsFleaPrice = LootValueMod.ShowNonVitalWeaponPartsFleaPrice.Value;
            ShowContainedItemFleaPrices = LootValueMod.ShowTotalFleaValueOfContainedItems.Value;
            ContainedItemFleaPricesOverridesKgAndSlotPrice = LootValueMod.TotalValueOfContainedItemsOverridesKgAndSlotPrice.Value;
            QuickSellEnabled = LootValueMod.EnableQuickSell.Value;
            QuickSellUsesOneButton = LootValueMod.OneButtonQuickSell.Value;
            CanSellPinnedItems = LootValueMod.AllowQuickSellPinned.Value;
            CanSellLockedItems = LootValueMod.AllowQuickSellLocked.Value;
            ShowFleaMarketEligibility = LootValueMod.ShowFleaMarketEligibility.Value;

            // aggregates
            ShouldShowFleaMarketPrices = gameState.HasFleaMarketAvailable || ShowFleaPriceBeforeAccess;
            IsViewingContainedItemsPrice = gameState.PressingAlt && ShowContainedItemFleaPrices && ShouldShowFleaMarketPrices;
            ShowQuickSaleCommands = QuickSellEnabled && !gameState.IsInRaid;
            ShowNonVitalPartModsPrices = ShowNonVitalWeaponPartsFleaPrice && ShouldShowFleaMarketPrices && itemState.IsWeapon;
            ShouldShowPricePerKgSlot = (LootValueMod.ShowPricePerKgAndPerSlotInRaid.Value && gameState.IsInRaid)
                                        || (LootValueMod.ShowPricePerKgAndPerSlotOutOfRaid.Value && !gameState.IsInRaid);
            OverridePricePerKgSlotWithContainedItemsFleaValue = ContainedItemFleaPricesOverridesKgAndSlotPrice && ShouldShowPricePerKgSlot;

        }

    }

    internal class FleaPriceState : IDisposable
    {
        public readonly int UnitaryPrice;
        public readonly int StackPrice;
        public readonly int UnitaryPriceWithModifiers;
        public readonly int FleaPriceWithModifiers;
        public readonly bool HasPriceInFlea;
        public readonly int PricePerSlotWithModifiers;
        public readonly int PriceSumOfNonVitalMods;
        public readonly int PriceSumOfContainedItems;
        public readonly int FleaPriceOfSimilarItems;

        private TooltipCfg TooltipCfg;
        private ItemState ItemState;

        public FleaPriceState(ItemState itemState, TooltipCfg tooltipCfg, Item item)
        {
            TooltipCfg = tooltipCfg;
            ItemState = itemState;

            // unit price stuff
            UnitaryPrice = FleaUtils.GetFleaMarketUnitPrice(item);
            HasPriceInFlea = UnitaryPrice > 0;
            StackPrice = UnitaryPrice * itemState.StackAmount;

            // modifiers and stack count (i.e: final price)
            UnitaryPriceWithModifiers = FleaUtils.GetFleaMarketUnitPriceWithModifiers(item);
            FleaPriceWithModifiers = UnitaryPriceWithModifiers * itemState.StackAmount;
            PricePerSlotWithModifiers = FleaPriceWithModifiers / itemState.Slots;

            var containedItems = ItemUtils.GetContainedSellableItems(item);
            PriceSumOfContainedItems = containedItems.Select(ci => FleaUtils.GetFleaMarketUnitPriceWithModifiers(ci) * ci.StackObjectsCount).Sum();

            if (itemState.IsWeapon)
            {
                var nonVitalMods = ItemUtils.GetWeaponNonVitalMods(item);
                PriceSumOfNonVitalMods = FleaUtils.GetFleaValue(nonVitalMods);
            }
            else
            {
                PriceSumOfNonVitalMods = 0;
            }

            FleaPriceOfSimilarItems = FleaUtils.GetTotalPriceOfAllSimilarItemsWithinSameContainer(item);

        }


        public bool HasPriceSumOfNonVitalMods()
        {
            return PriceSumOfNonVitalMods > 0;
        }

        public bool ContainsItemsWithFleaValue()
        {
            return PriceSumOfContainedItems > 0;
        }

        public int GetDynamicUnitaryPrice()
        {
            // when viewing contained items, the unitary price is treated as the contained items.
            if (ShouldOverrideItemPriceWithContainedItems())
            {
                return PriceSumOfContainedItems;
            }
            else
            {
                return UnitaryPriceWithModifiers;
            }

        }

        public int GetDynamicPricePerSlotWithModifiers()
        {
            // when viewing contained items, the price per slot is based on the total price of contained items.
            if (ShouldOverrideItemPriceWithContainedItems())
            {
                return PriceSumOfContainedItems / ItemState.Slots;
            }
            else
            {
                return PricePerSlotWithModifiers;
            }

        }

        public bool IsViewingContainedItems()
        {
            return TooltipCfg.IsViewingContainedItemsPrice
                && ContainsItemsWithFleaValue();
        }

        private bool ShouldOverrideItemPriceWithContainedItems()
        {
            return IsViewingContainedItems()
                && TooltipCfg.ContainedItemFleaPricesOverridesKgAndSlotPrice;
        }

        public void Dispose()
        {
            TooltipCfg = null;
            ItemState = null;
        }
    }

    internal record TraderPriceState
    {
        public readonly int TraderOfferPrice;
        public readonly bool HasTraderOffer;
        public readonly int PricePerSlot;
        public readonly int UnitaryPrice;

        public TraderPriceState(ItemState itemState, Item item)
        {
            TraderOfferPrice = TraderUtils.GetBestTraderPrice(item);
            HasTraderOffer = TraderOfferPrice > 0;
            PricePerSlot = TraderOfferPrice / itemState.Slots;
            UnitaryPrice = TraderOfferPrice / itemState.StackAmount;
        }
    }

    internal record PriceState
    {

        public readonly bool IsTraderPriceHigher;
        public readonly bool IsFleaPriceHigher;
        public readonly bool ItemsContainedHaveValue;
        public readonly bool HasPrice;
        public readonly bool HasFleaPrice;
        public readonly bool HasTraderPrice;

        public PriceState(FleaPriceState fleaPriceState, TraderPriceState traderPriceState)
        {
            IsTraderPriceHigher = traderPriceState.TraderOfferPrice > fleaPriceState.FleaPriceWithModifiers;
            IsFleaPriceHigher = !IsTraderPriceHigher;

            ItemsContainedHaveValue = fleaPriceState.ContainsItemsWithFleaValue();

            HasFleaPrice = fleaPriceState.HasPriceInFlea;
            HasTraderPrice = traderPriceState.HasTraderOffer;
            HasPrice = fleaPriceState.HasPriceInFlea || traderPriceState.HasTraderOffer;
        }
    }

    internal record SellabilityState
    {
        public bool CanBeSoldToTrader { get; private set; }
        public bool CanBeSoldToFlea { get; private set; }
        public Buyer OneClickBuyer  { get; private set; }
        public bool SellingToTraderDueConditional { get; private set; }

        public SellabilityState(TooltipCfg tooltipCfg, PriceState priceState, GameState gameState, ItemState itemState)
        {
            DetermineSellability(tooltipCfg, priceState, gameState, itemState);
            DetermineOneClickBuyer(priceState, gameState, itemState);
        }

        private void DetermineSellability(TooltipCfg tooltipCfg, PriceState priceState, GameState gameState, ItemState itemState)
        {
            // determine where it can be sold for two click sell logic
            CanBeSoldToTrader = true;
            CanBeSoldToFlea = true;

            if (!gameState.HasFleaMarketAvailable)
            {
                CanBeSoldToFlea = false;
            }

            if (!priceState.HasFleaPrice)
            {
                CanBeSoldToFlea = false;
            }

            if (itemState.ContainsNonFleableItemsInside)
            {
                CanBeSoldToFlea = false;
            }

            if (!priceState.HasTraderPrice)
            {
                CanBeSoldToTrader = false;
            }

            if (!itemState.IsEmpty)
            {
                CanBeSoldToFlea = false;
                CanBeSoldToTrader = false;
            }

            if (itemState.IsPinned && !tooltipCfg.CanSellPinnedItems)
            {
                CanBeSoldToFlea = false;
                CanBeSoldToTrader = false;
            }

            if (itemState.IsLocked && !tooltipCfg.CanSellLockedItems)
            {
                CanBeSoldToFlea = false;
                CanBeSoldToTrader = false;
            }

            if (!itemState.CanSellOnFleaMarket)
            {
                CanBeSoldToFlea = false;
            }


        }

        private void DetermineOneClickBuyer(PriceState priceState, GameState gameState, ItemState itemState)
        {
            // one click sell logic
            if (priceState.IsTraderPriceHigher)
            {
                OneClickBuyer = Buyer.TRADER;
            }

            if (priceState.IsFleaPriceHigher)
            {
                OneClickBuyer = Buyer.FLEA;
            }

            if (!gameState.HasFleaMarketAvailable)
            {
                OneClickBuyer = Buyer.TRADER;
                SellingToTraderDueConditional = false;
            }

            var shouldSellToTraderDueToPriceOrCondition = itemState.ShouldSellToTraderDueToPriceOrCondition;
            if (shouldSellToTraderDueToPriceOrCondition)
            {
                OneClickBuyer = Buyer.TRADER;
                SellingToTraderDueConditional = true;
            }

            if (!priceState.HasFleaPrice)
            {
                OneClickBuyer = Buyer.TRADER;
                SellingToTraderDueConditional = false;
            }

            if (!itemState.CanSellOnFleaMarket)
            {
                OneClickBuyer = Buyer.TRADER;
                SellingToTraderDueConditional = false;
            }
        }

        internal enum Buyer
        {
            TRADER, FLEA
        }

        public bool IsSellable()
        {
            return CanBeSoldToFlea || CanBeSoldToTrader;
        }

        public bool TraderBuys()
        {
            return OneClickBuyer == Buyer.TRADER;
        }

        public bool FleaBuys()
        {
            return OneClickBuyer == Buyer.FLEA;
        }

    }

    internal record DisplayPriceState : IDisposable
    {

        public MainDisplayPriceFlag MainDisplayPriceFlags { get; private set; }

        public DisplayPriceState(TooltipCfg tooltipCfg, PriceState priceState, GameState gameState, SellabilityState sellabilityState)
        {
            InitDisplayPriceState(tooltipCfg, priceState, gameState);
        }

        private void InitDisplayPriceState(TooltipCfg tooltipCfg, PriceState priceState, GameState gameState)
        {
            MainDisplayPriceFlags |= MainDisplayPriceFlag.FLEA;
            MainDisplayPriceFlags |= MainDisplayPriceFlag.TRADER;

            if (tooltipCfg.HideLowerPrice && priceState.IsFleaPriceHigher)
            {
                MainDisplayPriceFlags &= ~MainDisplayPriceFlag.TRADER;
            }
            if (tooltipCfg.HideLowerPriceInRaid && gameState.IsInRaid && priceState.IsFleaPriceHigher)
            {
                MainDisplayPriceFlags &= ~MainDisplayPriceFlag.TRADER;
            }
            if (!priceState.HasTraderPrice)
            {
                MainDisplayPriceFlags &= ~MainDisplayPriceFlag.TRADER;
            }

            if (tooltipCfg.HideLowerPrice && priceState.IsTraderPriceHigher)
            {
                MainDisplayPriceFlags &= ~MainDisplayPriceFlag.FLEA;
            }
            if (tooltipCfg.HideLowerPriceInRaid && gameState.IsInRaid && priceState.IsTraderPriceHigher)
            {
                MainDisplayPriceFlags &= ~MainDisplayPriceFlag.FLEA;
            }
            if (!priceState.HasFleaPrice)
            {
                MainDisplayPriceFlags &= ~MainDisplayPriceFlag.FLEA;
            }
            if (!tooltipCfg.ShouldShowFleaMarketPrices)
            {
                MainDisplayPriceFlags &= ~MainDisplayPriceFlag.FLEA;
            }
        }

        [Flags]
        internal enum MainDisplayPriceFlag
        {
            NONE = 0,
            TRADER = 1 << 0,
            FLEA = 1 << 1
        }


        public bool CanDisplay(MainDisplayPriceFlag flag)
        {
            return MainDisplayPriceFlags.HasFlag(flag);
        }

        public void Dispose()
        {
            MainDisplayPriceFlags &= ~MainDisplayPriceFlag.FLEA;
            MainDisplayPriceFlags &= ~MainDisplayPriceFlag.TRADER;
        }

    }

    internal record PricePerSlotAndKgState
    {
        public readonly int PricePerSlot;
        public readonly int PricePerKg;
        private readonly int UnitaryPrice;

        internal PricePerSlotAndKgState(ItemState itemState, TooltipCfg tooltipCfg, TraderPriceState traderState, FleaPriceState fleaState, SellabilityState sellabilityState)
        {
            var displayPrice = InitPricePerSlotDisplayPrice(tooltipCfg, fleaState, sellabilityState);
            if (displayPrice == PricePerSlotDisplay.TRADER)
            {
                PricePerSlot = traderState.PricePerSlot;
                UnitaryPrice = traderState.UnitaryPrice;
            }
            else
            {
                // use the dynamic one as this gets replaced by the contained items if the options match
                PricePerSlot = fleaState.GetDynamicPricePerSlotWithModifiers();
                UnitaryPrice = fleaState.GetDynamicUnitaryPrice();
            }

            PricePerKg = (int)(UnitaryPrice * itemState.StackAmount / itemState.TotalWeight);
            if (itemState.TotalWeight.ApproxEquals(0.0f))
            {
                PricePerKg = 0;
            }

        }

        private PricePerSlotDisplay InitPricePerSlotDisplayPrice(TooltipCfg tooltipCfg, FleaPriceState fleaState, SellabilityState sellabilityState)
        {

            PricePerSlotDisplay selected;
            if (sellabilityState.FleaBuys())
            {
                selected = PricePerSlotDisplay.FLEA;
            }
            else
            {
                selected = PricePerSlotDisplay.TRADER;
            }

            // If viewing contained item flea prices, always display p/slot and p/kg of contained items value, if feature override is enabled.
            if (tooltipCfg.OverridePricePerKgSlotWithContainedItemsFleaValue
                && fleaState.IsViewingContainedItems())
            {
                selected = PricePerSlotDisplay.FLEA;
            }

            return selected;

        }

        internal enum PricePerSlotDisplay
        {
            TRADER,
            FLEA
        }

    }



    internal class ItemTooltipHandler : IDisposable
    {
        private ItemTooltipContext Ctx;

        internal ItemTooltipHandler(Item item)
        {
            Ctx = new ItemTooltipContext(item);
        }

        public void Dispose()
        {
            Ctx.Dispose();
            Ctx = null;
        }

        public static bool ShouldModifyTooltipForItem(Item item)
        {
            if (item == null || GameTooltipContext.Tooltip == null)
                return false;

            if (!LootValueMod.ShowPrices.Value)
                return false;

            if (!ScreenChangeController.CanShowItemPriceTooltipsOnCurrentScreen())
                return false;

            if (ItemUtils.ItemBelongsToTraderOrFleaMarketOrMail(item))
                return false;

            if (ClickItemController.itemSells.Contains(item?.Id))
                return false;

            return true;
        }

        public void Handle(ref string text)
        {
            var exitEarly = HandleEarlyExitConditions(ref text);
            if (exitEarly)
                return;

            HandleFleaMarketAvailabilityMessage(ref text);
            HandleSellToTraderInsteadMessage(ref text);
            HandleItemFleaAndTraderPricesSection(ref text);
            HandleNonVitalAttachmentPricesMessage(ref text);
            HandleContainedItemsPricesMessage(ref text);
            HandleContainsBannedItemsMessage(ref text);
            HandleOutOfRaidItemStateMessages(ref text);
            HandleBannedBaseItemMessage(ref text);
            HandlePricePerKgAndSlotSection(ref text);
            HandleQuickSaleSection(ref text);

        }

        private bool HandleEarlyExitConditions(ref string text)
        {
            if (!Ctx.TooltipCfg.ShowTooltipInRaid && Ctx.GameState.IsInRaid)
                return true;

            if (Ctx.ItemState.IsSoftArmorInsert)
            {
                TooltipUtils.AppendFullLineToTooltip(ref text, "(Item can't be sold)", 11, "#AA3333");
                return true;
            }

            // If both trader and flea are 0, then the item is not purchasable.
            if (!Ctx.PriceState.HasPrice)
            {
                TooltipUtils.AppendFullLineToTooltip(ref text, "(Item can't be sold)", 11, "#AA3333");
                return true;
            }

            return false;
        }

        private void HandleFleaMarketAvailabilityMessage(ref string text)
        {
            if (Ctx.PriceState.IsFleaPriceHigher && !Ctx.GameState.HasFleaMarketAvailable)
            {
                TooltipUtils.AppendFullLineToTooltip(ref text, $"(Flea market is not available)", 11, "#AAAA33");
            }
        }

        private void HandleSellToTraderInsteadMessage(ref string text)
        {
            if (Ctx.GameState.IsInRaid)
                return;

            if (!Ctx.TooltipCfg.QuickSellUsesOneButton)
                return;

            if (!Ctx.SellabilityState.SellingToTraderDueConditional)
                return;

            if (Ctx.SellabilityState.OneClickBuyer != SellabilityState.Buyer.TRADER)
                return;

            var reason = GetReasonForItemToBeSoldToTrader(Ctx.Item);
            TooltipUtils.AppendFullLineToTooltip(ref text, $"(Selling to <b>Trader</b> {reason})", 11, "#AAAA33");
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


        private void HandleItemFleaAndTraderPricesSection(ref string text)
        {

            if (Ctx.PriceState.HasPrice)
            {
                TooltipUtils.AppendSeparator(ref text, appendNewLineAfter: false);
            }

            // append trader price on tooltip
            if (Ctx.DisplayPriceState.CanDisplay(DisplayPriceState.MainDisplayPriceFlag.TRADER))
            {
                HandleTraderPriceDisplay(ref text);
            }

            // append flea price on tooltip
            if (Ctx.DisplayPriceState.CanDisplay(DisplayPriceState.MainDisplayPriceFlag.FLEA))
            {
                HandleFleaPriceDisplay(ref text);
            }

        }



        private void HandleTraderPriceDisplay(ref string text)
        {
            TooltipUtils.AppendNewLineToTooltipText(ref text);

            // append trader price
            var traderBuys = Ctx.SellabilityState.TraderBuys();
            var pricePerSlot = Ctx.TraderState.PricePerSlot;
            var traderPrice = Ctx.TraderState.TraderOfferPrice;

            var traderName = $"Trader: ";
            var traderNameColor = traderBuys ? "#ffffff" : "#444444";
            var traderPricePerSlotColor = traderBuys ? SlotColoring.GetColorFromValuePerSlots(pricePerSlot) : "#444444";
            var fontSize = traderBuys ? 14 : 10;

            TooltipUtils.StartSizeTag(ref text, fontSize);

            TooltipUtils.AppendTextToToolip(ref text, traderName, traderNameColor);
            TooltipUtils.AppendTextToToolip(ref text, $"₽ {traderPrice.FormatNumber()}", traderPricePerSlotColor);

            if (Ctx.ItemState.IsStack())
            {
                var unitPrice = $" (₽ {(traderPrice / Ctx.ItemState.StackAmount).FormatNumber()} e.)";
                TooltipUtils.AppendTextToToolip(ref text, unitPrice, "#333333");
            }

            TooltipUtils.EndSizeTag(ref text);
        }

        private void HandleFleaPriceDisplay(ref string text)
        {
            TooltipUtils.AppendNewLineToTooltipText(ref text);

            // append flea price
            var fleaBuys = Ctx.SellabilityState.FleaBuys();
            var pricePerSlot = Ctx.FleaState.PricePerSlotWithModifiers;
            var fleaPrice = Ctx.FleaState.FleaPriceWithModifiers;

            var fleaName = $"Flea: ";
            var fleaNameColor = fleaBuys ? "#ffffff" : "#444444";
            var fleaPricePerSlotColor = fleaBuys ? SlotColoring.GetColorFromValuePerSlots(pricePerSlot) : "#444444";
            var fontSize = fleaBuys ? 14 : 10;

            TooltipUtils.StartSizeTag(ref text, fontSize);

            TooltipUtils.AppendTextToToolip(ref text, fleaName, fleaNameColor);
            TooltipUtils.AppendTextToToolip(ref text, $"₽ {fleaPrice.FormatNumber()}", fleaPricePerSlotColor);

            if (Ctx.TooltipCfg.ApplyConditionReduction)
            {
                if (Ctx.ItemState.IsDamaged())
                {
                    var missingDurabilityText = $" (-{(int)Ctx.ItemState.MissingDurability}%)";
                    TooltipUtils.AppendTextToToolip(ref text, missingDurabilityText, "#AA1111");
                }
            }


            if (Ctx.ItemState.IsStack())
            {
                var unitPrice = $" (₽ {Ctx.FleaState.UnitaryPriceWithModifiers.FormatNumber()} e.)";
                TooltipUtils.AppendTextToToolip(ref text, unitPrice, "#333333");
            }

            TooltipUtils.EndSizeTag(ref text);
        }

        private void HandleNonVitalAttachmentPricesMessage(ref string text)
        {
            if (!Ctx.TooltipCfg.ShowNonVitalPartModsPrices)
                return;

            if (!Ctx.FleaState.HasPriceSumOfNonVitalMods())
                return;

            var priceSum = Ctx.FleaState.PriceSumOfNonVitalMods;
            var color = SlotColoring.GetColorFromTotalValue(priceSum);

            TooltipUtils.AppendNewLineToTooltipText(ref text);
            TooltipUtils.StartSizeTag(ref text, 12);
            TooltipUtils.AppendTextToToolip(ref text, $"₽ {priceSum.FormatNumber()} ", color);
            TooltipUtils.AppendTextToToolip(ref text, "in non-vital parts (flea)", "#555555");
            TooltipUtils.EndSizeTag(ref text);
        }

        private void HandleContainedItemsPricesMessage(ref string text)
        {
            if (!Ctx.FleaState.IsViewingContainedItems())
                return;

            var fleaPricesForContainedItems = Ctx.FleaState.PriceSumOfContainedItems;
            var color = SlotColoring.GetColorFromTotalValue(fleaPricesForContainedItems);

            TooltipUtils.AppendNewLineToTooltipText(ref text);
            TooltipUtils.StartSizeTag(ref text, 12);
            TooltipUtils.AppendTextToToolip(ref text, $"₽ {fleaPricesForContainedItems.FormatNumber()}", color);
            if (Ctx.TooltipCfg.OverridePricePerKgSlotWithContainedItemsFleaValue)
            {
                TooltipUtils.AppendTextToToolip(ref text, "*", "#2f485b");
            }
            TooltipUtils.AppendTextToToolip(ref text, " of items inside (flea)", "#555555");
            TooltipUtils.EndSizeTag(ref text);
        }

        private void HandleContainsBannedItemsMessage(ref string text)
        {
            if (Ctx.GameState.IsInRaid)
                return;

            if (Ctx.TooltipCfg.QuickSellUsesOneButton && Ctx.SellabilityState.TraderBuys())
                return;

            if (!Ctx.TooltipCfg.ShouldShowFleaMarketPrices)
                return;

            if (!Ctx.ItemState.ContainsNonFleableItemsInside)
                return;

            TooltipUtils.AppendFullLineToTooltip(ref text, "(Contains banned flea items)", 11, "#AA3333");
        }

        private void HandleOutOfRaidItemStateMessages(ref string text)
        {
            if (Ctx.GameState.IsInRaid)
                return;

            if (!Ctx.ItemState.IsEmpty)
            {
                TooltipUtils.AppendFullLineToTooltip(ref text, "(Item is not empty)", 11, "#AA3333");
            }

            if (Ctx.ItemState.IsPinned && !Ctx.TooltipCfg.CanSellPinnedItems)
            {
                TooltipUtils.AppendFullLineToTooltip(ref text, "(Item is pinned)", 11, "#AA3333");
            }

            if (Ctx.ItemState.IsLocked && !Ctx.TooltipCfg.CanSellLockedItems)
            {
                TooltipUtils.AppendFullLineToTooltip(ref text, "(Item is locked)", 11, "#AA3333");
            }

        }

        private void HandleBannedBaseItemMessage(ref string text)
        {

            if (!Ctx.TooltipCfg.ShowFleaMarketEligibility)
                return;

            if (Ctx.ItemState.CanSellOnFleaMarket)
                return;

            TooltipUtils.AppendFullLineToTooltip(ref text, "(Item is banned from flea market)", 11, "#AA3333");
        }

        private void HandlePricePerKgAndSlotSection(ref string text)
        {
            if (!Ctx.TooltipCfg.ShouldShowPricePerKgSlot)
                return;

            TooltipUtils.AppendSeparator(ref text);
            TooltipUtils.StartSizeTag(ref text, 11);
            TooltipUtils.AppendTextToToolip(ref text, $"₽ / KG\t{Ctx.PricePerSlotAndKgState.PricePerKg.FormatNumber()}", "#555555");
            AppendTrailingAsterixIfOverrideTrue(ref text);
            TooltipUtils.AppendNewLineToTooltipText(ref text);
            TooltipUtils.AppendTextToToolip(ref text, $"₽ / SLOT\t{Ctx.PricePerSlotAndKgState.PricePerSlot.FormatNumber()}", "#555555");
            AppendTrailingAsterixIfOverrideTrue(ref text);
            TooltipUtils.EndSizeTag(ref text);
        }

        private void AppendTrailingAsterixIfOverrideTrue(ref string text)
        {
            if (Ctx.TooltipCfg.OverridePricePerKgSlotWithContainedItemsFleaValue
                && Ctx.FleaState.IsViewingContainedItems())
            {
                TooltipUtils.AppendTextToToolip(ref text, "*", "#2f485b");
            }
        }

        private void HandleQuickSaleSection(ref string text)
        {

            if (!Ctx.TooltipCfg.ShowQuickSaleCommands)
                return;

            if (!Ctx.GameState.CanQuickSellOnCurrentScreen)
                return;

            if (Ctx.TooltipCfg.QuickSellUsesOneButton)
            {
                HandleOneButtonQuickSaleCommands(ref text);
            }
            else
            {
                HandleMultiButtonQuickSaleCommands(ref text);
            }

        }

        private void HandleOneButtonQuickSaleCommands(ref string text)
        {
            if (!Ctx.SellabilityState.IsSellable())
                return;

            TooltipUtils.AppendSeparator(ref text);
            TooltipUtils.AppendTextToToolip(ref text, $"Sell with Alt+Shift+Click", "#888888");

            if (Ctx.SellabilityState.FleaBuys())
            {
                AddMultipleItemsSaleSection(ref text);
            }
        }

        private void HandleMultiButtonQuickSaleCommands(ref string text)
        {

            if (Ctx.SellabilityState.IsSellable())
            {
                TooltipUtils.AppendSeparator(ref text);
            }

            if (Ctx.SellabilityState.CanBeSoldToTrader)
            {
                TooltipUtils.AppendTextToToolip(ref text, $"Sell to Trader with Alt+Shift+Left Click", "#888888");
            }

            if (Ctx.SellabilityState.CanBeSoldToTrader && Ctx.SellabilityState.CanBeSoldToFlea)
            {
                TooltipUtils.AppendNewLineToTooltipText(ref text);
            }

            if (Ctx.SellabilityState.CanBeSoldToFlea)
            {
                TooltipUtils.AppendTextToToolip(ref text, $"List to Flea with Alt+Shift+Right Click", "#888888");
                AddMultipleItemsSaleSection(ref text);
            }
        }

        private void AddMultipleItemsSaleSection(ref string text)
        {
            if (!Ctx.ItemState.CanSellMultipleOfItem)
                return;

            var includePinned = Ctx.TooltipCfg.CanSellPinnedItems;
            var includeLocked = Ctx.TooltipCfg.CanSellLockedItems;
            var amountOfItems = Ctx.ItemState.CountSimilarItems(includePinned, includeLocked);
            if (amountOfItems <= 1)
                return;

            var totalPrice = Ctx.FleaState.FleaPriceOfSimilarItems;
            TooltipUtils.AppendFullLineToTooltip(ref text, $"(Will list {amountOfItems} similar items in flea for ₽ {totalPrice.FormatNumber()})", 10, "#555555");

        }


    }


}