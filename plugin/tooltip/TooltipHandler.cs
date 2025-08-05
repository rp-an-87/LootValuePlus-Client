using System;
using System.Linq;
using EFT.InventoryLogic;
using SPT.Reflection.Utils;
using UnityEngine;
using static LootValuePlus.TooltipController;

namespace LootValuePlus
{

    internal class ItemTooltipContext
    {

        public Item Item { get; }
        public TooltipCfg TooltipCfg { get; }
        public ItemState ItemState { get; }
        public GameState GameState { get; }
        public FleaPriceState FleaState { get; }
        public TraderPriceState TraderState { get; }
        public PriceState PriceState { get; }
        public SellabilityState SellabilityState { get; }
        public DisplayPriceState DisplayPriceState { get; }

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
            DisplayPriceState = new DisplayPriceState(TooltipCfg, PriceState, GameState);
        }

        

    }

    internal class GameState
    {

        public static ISession Session => ClientAppUtils.GetMainApp().GetClientBackEndSession();

        public GameState()
        {
            HasFleaMarketAvailable = Session.RagFair.Available;
            CanQuickSellOnCurrentScreen = ScreenChangeController.CanQuickSellOnCurrentScreen();
            IsInRaid = Globals.HasRaidStarted();
            PressingAlt = Input.GetKey(KeyCode.LeftAlt);
        }

        public bool HasFleaMarketAvailable { get; }
        public bool CanQuickSellOnCurrentScreen { get; }
        public bool IsInRaid { get; }
        public bool PressingAlt { get; }
    }


    internal class TooltipCfg
    {

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

            // aggregates
            ShouldShowFleaMarketPrices = gameState.HasFleaMarketAvailable || ShowFleaPriceBeforeAccess;
            IsViewingContainedItemsPrice = gameState.PressingAlt && ShowContainedItemFleaPrices; 
            ShowQuickSaleCommands = QuickSellEnabled && !gameState.IsInRaid;
            ShowNonVitalPartModsPrices = ShowNonVitalWeaponPartsFleaPrice && ShouldShowFleaMarketPrices && itemState.IsWeapon;
            ShouldShowPricePerKgSlot = (LootValueMod.ShowPricePerKgAndPerSlotInRaid.Value && gameState.IsInRaid)
                                        || (LootValueMod.ShowPricePerKgAndPerSlotOutOfRaid.Value && !gameState.IsInRaid);
            ShowOverridenPricePerKgSlot = ContainedItemFleaPricesOverridesKgAndSlotPrice && ShouldShowPricePerKgSlot;

        }

        public bool ShowTooltipInRaid { get; }
        public bool ShouldShowPricesTooltipWhileInRaid { get; }
        public bool HideLowerPrice { get; }
        public bool HideLowerPriceInRaid { get; }
        public bool ShowFleaPriceBeforeAccess { get; }
        public bool ApplyConditionReduction { get; }
        public bool ShowNonVitalWeaponPartsFleaPrice { get; }
        public bool ShowContainedItemFleaPrices { get; }
        public bool ContainedItemFleaPricesOverridesKgAndSlotPrice { get; }
        public bool QuickSellEnabled { get; }
        public bool QuickSellUsesOneButton { get; }
        public bool CanSellPinnedItems { get; }
        public bool CanSellLockedItems { get; }
        public bool ShouldShowPricePerKgSlot { get; }
        public bool ShouldShowFleaMarketPrices { get; }
        public bool IsViewingContainedItemsPrice { get; }
        public bool ShowQuickSaleCommands { get; }
        public bool ShowNonVitalPartModsPrices { get; }
        public bool ShowOverridenPricePerKgSlot { get; }
    }

    internal class ItemState
    {

        public ItemState(Item item)
        {
            ItemBelongsToTraderOrFlearMarketOrMail = ItemUtils.ItemBelongsToTraderOrFleaMarketOrMail(item);
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
        }

        public float MissingDurability { get; }
        public int UnitFleaPrice { get; }
        public int FullPrice { get; }
        public bool ItemBelongsToTraderOrFlearMarketOrMail { get; }
        public int StackAmount { get; }
        public bool IsEmpty { get; }
        public int UnitFleaPriceWithModifiers { get; }
        public int FullPriceWithModifiers { get; }
        public int Slots { get; }
        public bool IsWeapon { get; }
        public bool IsSoftArmorInsert { get; }
        public bool ContainsNonFleableItemsInside { get; }
        public bool ShouldSellToTraderDueToPriceOrCondition { get; set; }
        public bool IsPinned { get; }
        public bool IsLocked { get; }
        public bool CanSellOnFleaMarket { get; }

        public bool IsStack()
        {
            return StackAmount > 1;
        }

        public bool IsDamaged()
        {
            return MissingDurability > 1.0f;
        }
    }

    internal class FleaPriceState
    {
        public FleaPriceState(ItemState itemState, TooltipCfg cfg, Item item)
        {
            // unit price stuff
            UnitaryPrice = FleaUtils.GetFleaMarketUnitPrice(item);
            HasPriceInFlea = UnitaryPrice > 0;
            StackPrice = UnitaryPrice * itemState.StackAmount;

            // modifiers and stack count (i.e: final price)
            UnitaryPriceWithModifiers = FleaUtils.GetFleaMarketUnitPriceWithModifiers(item);
            FleaPriceWithModifiers = UnitaryPriceWithModifiers * itemState.StackAmount;

            var containedItems = ItemUtils.GetContainedSellableItems(item);
            PriceSumOfContainedItems = containedItems.Select(ci => FleaUtils.GetFleaMarketUnitPriceWithModifiers(ci) * ci.StackObjectsCount).Sum();

            if (cfg.IsViewingContainedItemsPrice && cfg.ContainedItemFleaPricesOverridesKgAndSlotPrice && PriceSumOfContainedItems > 0)
            {
                DynamicPricePerSlotWithModifiers = PriceSumOfContainedItems / itemState.Slots;
            }
            else
            {

                DynamicPricePerSlotWithModifiers = FleaPriceWithModifiers / itemState.Slots;
            }

            StaticPricePerSlotWithModifiers = FleaPriceWithModifiers / itemState.Slots;

            if (itemState.IsWeapon)
            {
                var nonVitalMods = ItemUtils.GetWeaponNonVitalMods(item);
                PriceSumOfNonVitalMods = FleaUtils.GetFleaValue(nonVitalMods);
            }
            else
            {
                PriceSumOfNonVitalMods = 0;
            }


        }

        public int UnitaryPrice { get; }
        public int StackPrice { get; }
        public int UnitaryPriceWithModifiers { get; }
        public int FleaPriceWithModifiers { get; }
        public bool HasPriceInFlea { get; }
        public int DynamicPricePerSlotWithModifiers { get; }
        public int PriceSumOfNonVitalMods { get; }
        public int PriceSumOfContainedItems { get; }
        public int StaticPricePerSlotWithModifiers { get; }
    }

    internal class TraderPriceState
    {
        public TraderPriceState(ItemState itemState, Item item)
        {
            TraderOfferPrice = TraderUtils.GetBestTraderPrice(item);
            HasTraderOffer = TraderOfferPrice > 0;
            PricePerSlot = TraderOfferPrice * itemState.Slots;
        }

        public int TraderOfferPrice { get; }
        public bool HasTraderOffer { get; }
        public int PricePerSlot { get; }
    }

    internal class PriceState
    {

        public PriceState(FleaPriceState fleaPriceState, TraderPriceState traderPriceState)
        {
            IsTraderPriceHigher = traderPriceState.TraderOfferPrice > fleaPriceState.FleaPriceWithModifiers;
            IsFleaPriceHigher = !IsTraderPriceHigher;

            HasFleaPrice = fleaPriceState.HasPriceInFlea;
            HasTraderPrice = traderPriceState.HasTraderOffer;
            HasPrice = fleaPriceState.HasPriceInFlea || traderPriceState.HasTraderOffer;
        }

        public bool IsTraderPriceHigher { get; }
        public bool IsFleaPriceHigher { get; }
        public bool HasPrice { get; }
        public bool HasFleaPrice { get; }
        public bool HasTraderPrice { get; }
    }

    internal class SellabilityState
    {

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


        public bool CanBeSoldToTrader { get; private set; }
        public bool CanBeSoldToFlea { get; private set; }
        public Buyer OneClickBuyer { get; private set; }
        public bool SellingToTraderDueConditional { get; private set; }

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

    internal class DisplayPriceState
    {

        public DisplayPriceState(TooltipCfg tooltipCfg, PriceState priceState, GameState gameState)
        {

            DisplayPrice |= DisplayPriceType.FLEA;
            DisplayPrice |= DisplayPriceType.TRADER;

            if (tooltipCfg.HideLowerPrice && priceState.IsFleaPriceHigher)
            {
                DisplayPrice &= ~DisplayPriceType.TRADER;
            }
            if (tooltipCfg.HideLowerPriceInRaid && gameState.IsInRaid && priceState.IsFleaPriceHigher)
            {
                DisplayPrice &= ~DisplayPriceType.TRADER;
            }
            if (!priceState.HasTraderPrice)
            {
                DisplayPrice &= ~DisplayPriceType.TRADER;
            }

            if (tooltipCfg.HideLowerPrice && priceState.IsTraderPriceHigher)
            {
                DisplayPrice &= ~DisplayPriceType.FLEA;
            }
            if (tooltipCfg.HideLowerPriceInRaid && gameState.IsInRaid && priceState.IsTraderPriceHigher)
            {
                DisplayPrice &= ~DisplayPriceType.FLEA;
            }
            if (!priceState.HasFleaPrice)
            {
                DisplayPrice &= ~DisplayPriceType.FLEA;
            }
            if (!tooltipCfg.ShouldShowFleaMarketPrices)
            {
                DisplayPrice &= ~DisplayPriceType.FLEA;
            }

        }

        public DisplayPriceType DisplayPrice { get; }

        [Flags]
        internal enum DisplayPriceType
        {
            NONE = 0,
            TRADER = 1 << 0,
            FLEA = 1 << 1
        }


        public bool CanDisplay(DisplayPriceType type)
        {
            return DisplayPrice.HasFlag(type);
        }


    }



    internal class ItemTooltipHandler
    {

        private readonly ItemTooltipContext Ctx;
        internal ItemTooltipHandler(Item item)
        {
            Ctx = new ItemTooltipContext(item);
        }
        
        public static bool ShouldModifyTooltipForItem(Item item)
        {
            if (item == null || GameTooltipContext.Tooltip == null)
                return false;

            if (!LootValueMod.ShowPrices.Value)
                return false;

            if (!ScreenChangeController.CanShowItemPriceTooltipsOnCurrentScreen())
                return false;

            return true;
        }

        public void handle(ref string text)
        {
            var exitEarly = HandleEarlyExitConditions(ref text);
            if (exitEarly)
                return;

            HandleFleaMarketAvailabilityMessage(ref text);
            HandleSellToTraderInsteadMessage(ref text);
            HandleItemFleaAndTraderPrices(ref text);
            HandleNonVitalAttachmentPrices(ref text);
            HandleContainedItemsPrices(ref text);
            HandleContainsBannedItemsMessage(ref text);
            HandleOutOfRaidPointerMessages(ref text);




            /* var shouldShowFleaMarketEligibility = LootValueMod.ShowFleaMarketEligibility.Value;
            if (shouldShowFleaMarketEligibility && !item.Template.CanSellOnRagfair)
            {
                AppendFullLineToTooltip(ref text, "(Item is banned from flea market)", 11, "#AA3333");
            }

            if (shouldShowPricePerKgWeight)
            {
                var overrideWithContained = overrideWeightAndSlotPriceWithContainedPrice && fleaPricesForContainedItems > 0 && shouldShowFleaMarketPrices;
                var pricePerSlot = sellToTrader ? pricePerSlotTrader : pricePerSlotFlea;
                if (overrideWithContained)
                {
                    pricePerSlot = pricePerSlotFlea;
                }

                var unitPrice = sellToTrader
                    ? finalTraderPrice / stackAmount
                    : FleaUtils.GetFleaMarketUnitPriceWithModifiers(item);

                if (overrideWithContained)
                {
                    unitPrice = fleaPricesForContainedItems;
                }

                var pricePerWeight = (int)(unitPrice * item.StackObjectsCount / item.TotalWeight);
                if (item.TotalWeight.ApproxEquals(0.0f))
                {
                    pricePerWeight = 0;
                }

                AppendSeparator(ref text);
                StartSizeTag(ref text, 11);
                AppendTextToToolip(ref text, $"₽ / KG\t{pricePerWeight.FormatNumber()}", "#555555");
                if (overrideWithContained)
                {
                    AppendTextToToolip(ref text, "*", "#2f485b");
                }
                AppendNewLineToTooltipText(ref text);
                AppendTextToToolip(ref text, $"₽ / SLOT\t{pricePerSlot.FormatNumber()}", "#555555");
                if (overrideWithContained)
                {
                    AppendTextToToolip(ref text, "*", "#2f485b");
                }
                EndSizeTag(ref text);
            }

            if (showQuickSaleCommands && canQuickSellOnCurrentScreen)
            {
                if (quickSellUsesOneButton)
                {

                    bool canBeSold = (sellToFlea && canBeSoldToFlea) || (sellToTrader && canBeSoldToTrader);
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
            } */











        }



        /* private static void AddMultipleItemsSaleSection(ref string text, Item item)
        {
            bool canSellSimilarItems = FleaUtils.CanSellMultipleOfItem(item);
            if (canSellSimilarItems)
            {
                var includePinned = LootValueMod.AllowQuickSellPinned.Value;
                var includeLocked = LootValueMod.AllowQuickSellLocked.Value;
                var amountOfItems = ItemUtils.CountItemsSimilarToItemWithinSameContainer(item, includePinned, includeLocked);
                // append only if more than 1 item will be sold due to the flea market action
                if (amountOfItems > 1)
                {
                    var totalPrice = FleaUtils.GetTotalPriceOfAllSimilarItemsWithinSameContainer(item);
                    AppendFullLineToTooltip(ref text, $"(Will list {amountOfItems} similar items in flea for ₽ {totalPrice.FormatNumber()})", 10, "#555555");
                }

            }
        } */



        /**
            ---> New stuff
        */
        private bool HandleEarlyExitConditions(ref string text)
        {
            if (!Ctx.TooltipCfg.ShowTooltipInRaid && Ctx.GameState.IsInRaid)
                return true;

            if (Ctx.ItemState.ItemBelongsToTraderOrFlearMarketOrMail)
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

            if (!Ctx.GameState.IsInRaid
              && Ctx.TooltipCfg.QuickSellUsesOneButton
              && Ctx.SellabilityState.SellingToTraderDueConditional == true
              && Ctx.SellabilityState.CanBeSoldToFlea
              && Ctx.SellabilityState.OneClickBuyer == SellabilityState.Buyer.TRADER)
            {
                var reason = GetReasonForItemToBeSoldToTrader(Ctx.Item);
                TooltipUtils.AppendFullLineToTooltip(ref text, $"(Selling to <b>Trader</b> {reason})", 11, "#AAAA33");
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


        private void HandleItemFleaAndTraderPrices(ref string text)
        {

            if (Ctx.SellabilityState.IsSellable())
            {
                TooltipUtils.AppendSeparator(ref text, appendNewLineAfter: false);
            }

            // append trader price on tooltip
            if (Ctx.DisplayPriceState.CanDisplay(DisplayPriceState.DisplayPriceType.TRADER))
            {
                HandleTraderPriceDisplay(ref text);
            }

            // append flea price on tooltip
            if (Ctx.DisplayPriceState.CanDisplay(DisplayPriceState.DisplayPriceType.FLEA))
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
            var pricePerSlot = Ctx.FleaState.StaticPricePerSlotWithModifiers;
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

        private void HandleNonVitalAttachmentPrices(ref string text)
        {
            if (!Ctx.TooltipCfg.ShowNonVitalPartModsPrices)
                return;

            var priceSum = Ctx.FleaState.PriceSumOfNonVitalMods;
            var color = SlotColoring.GetColorFromTotalValue(priceSum);

            TooltipUtils.AppendNewLineToTooltipText(ref text);
            TooltipUtils.StartSizeTag(ref text, 12);
            TooltipUtils.AppendTextToToolip(ref text, $"₽ {priceSum.FormatNumber()} ", color);
            TooltipUtils.AppendTextToToolip(ref text, "in non-vital parts (flea)", "#555555");
            TooltipUtils.EndSizeTag(ref text);
        }

        private void HandleContainedItemsPrices(ref string text)
        {
            if (!Ctx.TooltipCfg.IsViewingContainedItemsPrice)
                return;

            if (!Ctx.TooltipCfg.ShouldShowFleaMarketPrices)
                return;

            var fleaPricesForContainedItems = Ctx.FleaState.PriceSumOfContainedItems;

            if (fleaPricesForContainedItems == 0)
                return;

            var color = SlotColoring.GetColorFromTotalValue(fleaPricesForContainedItems);

            TooltipUtils.AppendNewLineToTooltipText(ref text);
            TooltipUtils.StartSizeTag(ref text, 12);
            TooltipUtils.AppendTextToToolip(ref text, $"₽ {fleaPricesForContainedItems.FormatNumber()}", color);
            if (Ctx.TooltipCfg.ShowOverridenPricePerKgSlot)
            {
                TooltipUtils.AppendTextToToolip(ref text, "*", "#2f485b");
            }
            TooltipUtils.AppendTextToToolip(ref text, " of items inside (flea)", "#555555");
            TooltipUtils.EndSizeTag(ref text);
        }

        private void HandleContainsBannedItemsMessage(ref string text)
        {
            if (!Ctx.GameState.IsInRaid)
                return;

            if (Ctx.TooltipCfg.QuickSellUsesOneButton && Ctx.SellabilityState.TraderBuys())
                return;

            if (Ctx.TooltipCfg.ShouldShowFleaMarketPrices)
                return;

            if (!Ctx.ItemState.ContainsNonFleableItemsInside)
                return;

            TooltipUtils.AppendFullLineToTooltip(ref text, "(Contains banned flea items)", 11, "#AA3333");
        }

        private void HandleOutOfRaidPointerMessages(ref string text)
        {
            if (!Ctx.GameState.IsInRaid)
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
    }


}