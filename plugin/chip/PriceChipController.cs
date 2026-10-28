using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Bootstrap;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using SPT.Reflection.Patching;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LootValuePlus
{
    internal enum ChipCorner
    {
        BottomLeft,
        BottomRight,
        TopLeft,
        TopRight
    }

    internal enum ChipNumber
    {
        FullPrice,
        PerSlot
    }

    // The tooltip's price as a small chip on the item icon. Everything it shows comes from ItemTooltipContext, so the chip and the
    // tooltip follow the same rules; the chip only picks the number, the colour and where to put it.
    internal static class PriceChipController
    {
        private const string ChipName = "LootValuePlus.PriceChip";
        private const string PriceIndexGuid = "com.fums.priceindex";  // draws in the same corner, so we stay off when it is installed
        private const int MaxCachedItems = 5000;

        internal static GridItemView HoveredView { get; private set; }
        internal static int SettingsVersion { get; private set; }
        internal static bool EverDrawn { get; private set; }
        internal static bool Hidden { get; private set; }

        private struct ChipData
        {
            public bool Show;
            public int Stack;
            public int Price;
            public int PerSlot;
            public bool HasContents;
            public int ContentsPrice;
            public int ContentsPerSlot;
        }

        // Prices are only worked out once per item and state of the world. Containers and weapons with parts inside are not kept
        // here: their contents change without the stack count changing.
        private static readonly Dictionary<string, ChipData> Cache = new Dictionary<string, ChipData>();
        private static long cacheRefreshTicks;
        private static int cacheSettingsVersion = -1;
        private static bool cacheInRaid;

        private static bool? priceIndexInstalled;
        private static bool failureLogged;

        internal static void SettingsChanged()
        {
            SettingsVersion++;
        }

        internal static void ToggleHidden()
        {
            Hidden = !Hidden;
            RedrawAll();
        }

        // Tooltip and chip options and the price cache all change what a chip shows; one stamp covers them.
        private static void DropCacheIfStale()
        {
            var ticks = FleaPriceCache.LastGlobalRefresh.Ticks;
            var inRaid = Globals.HasRaidStarted();
            if (ticks == cacheRefreshTicks && SettingsVersion == cacheSettingsVersion && inRaid == cacheInRaid && Cache.Count < MaxCachedItems)
                return;

            Cache.Clear();
            cacheRefreshTicks = ticks;
            cacheSettingsVersion = SettingsVersion;
            cacheInRaid = inRaid;
        }

        internal static void SetHovered(GridItemView view)
        {
            var previous = HoveredView;
            HoveredView = view;

            if (!LootValueMod.ShowPriceChip.Value || !LootValueMod.ChipAltShowsContents.Value || !Input.GetKey(KeyCode.LeftAlt))
                return;

            if (previous != null && previous != view)
                Apply(previous);
            if (view != null)
                Apply(view);
        }

        internal static void RedrawAll()
        {
            if (!LootValueMod.ShowPriceChip.Value && !EverDrawn)
                return;

            foreach (var view in UnityEngine.Object.FindObjectsOfType<GridItemView>())
            {
                Apply(view);
            }
        }

        internal static void Apply(GridItemView view)
        {
            try
            {
                ApplyInternal(view);
            }
            catch (Exception e)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    Globals.logger.LogWarning($"Price chip failed, further failures are not logged: {e}");
                }
                Hide(view);
            }
        }

        private static void ApplyInternal(GridItemView view)
        {
            var item = view.Item;
            if (!Wanted(view, item))
            {
                Hide(view);
                return;
            }

            var data = Resolve(item);
            if (!data.Show)
            {
                Hide(view);
                return;
            }

            // only the item under the pointer switches to its contents, like the tooltip does while Alt is held
            var contents = data.HasContents && view == HoveredView && LootValueMod.ChipAltShowsContents.Value && Input.GetKey(KeyCode.LeftAlt);
            if (view == HoveredView && Input.GetKey(KeyCode.LeftAlt))
            {
                Globals.LogDebug($"Price chip, Alt over {item.ShortName}: contents={data.HasContents} ({data.ContentsPrice}), setting={LootValueMod.ChipAltShowsContents.Value}, shown as contents={contents}");
            }
            var perSlot = contents ? data.ContentsPerSlot : data.PerSlot;
            var shown = LootValueMod.ChipNumberShown.Value == ChipNumber.PerSlot ? perSlot : (contents ? data.ContentsPrice : data.Price);
            if (shown <= 0 || shown < LootValueMod.ChipMinimumPrice.Value)
            {
                Hide(view);
                return;
            }

            // the colour is always the tier of the price per slot, whichever number is shown
            Color color;
            if (!ColorUtility.TryParseHtmlString(SlotColoring.GetColorFromValuePerSlots(perSlot), out color))
            {
                color = Color.white;
            }

            Draw(view, FormatCompact(shown), color);
        }

        private static bool Wanted(GridItemView view, Item item)
        {
            if (!LootValueMod.ShowPriceChip.Value || Hidden || item == null)
                return false;

            if (!priceIndexInstalled.HasValue)
            {
                priceIndexInstalled = Chainloader.PluginInfos.ContainsKey(PriceIndexGuid);
            }
            if (priceIndexInstalled.Value)
                return false;

            // an unsearched item is a blank tile; and without the global cache every chip would fetch its price on its own
            if (!view.IsSearched || !LootValueMod.EnableGlobalCache.Value || FleaPriceCache.cache.Count == 0)
                return false;

            if (!ScreenChangeController.CanShowPriceChipOnCurrentScreen())
                return false;

            if (ItemUtils.ItemBelongsToTraderOrFleaMarketOrMail(item) || ClickItemController.itemSells.Contains(item.Id))
                return false;

            // our own inventory (stash, worn gear, bags) belongs to the player's inventory controller; containers, corpses and ground loot have their own
            if (item.Owner is InventoryController ? !LootValueMod.ChipShowOnOwn.Value : !LootValueMod.ChipShowOnLoot.Value)
                return false;

            if (Globals.HasRaidStarted() ? !LootValueMod.ChipShowInRaid.Value : !LootValueMod.ChipShowOutOfRaid.Value)
                return false;

            if (!LootValueMod.ChipShowOnWorn.Value && item.Owner is InventoryController && item.CurrentAddress is SlotItemAddress)
                return false;

            if (!LootValueMod.ChipShowAmmo.Value && item is Ammo)
                return false;

            if ((!LootValueMod.ChipShowPinned.Value && ItemUtils.IsItemPinned(item)) || (!LootValueMod.ChipShowLocked.Value && ItemUtils.IsItemLocked(item)))
                return false;

            if (!LootValueMod.ChipShowInsured.Value && view.InsuredIcon != null && view.InsuredIcon.activeSelf)
                return false;

            return true;
        }

        private static ChipData Resolve(Item item)
        {
            DropCacheIfStale();

            var cacheable = item.IsEmpty();
            ChipData data;
            if (cacheable && Cache.TryGetValue(item.Id, out data) && data.Stack == item.StackObjectsCount)
                return data;

            data = Compute(item);
            if (cacheable)
            {
                Cache[item.Id] = data;
            }
            return data;
        }

        private static ChipData Compute(Item item)
        {
            var data = new ChipData { Stack = item.StackObjectsCount };

            using (var ctx = new ItemTooltipContext(item))
            {
                if (ctx.ItemState.IsSoftArmorInsert || !ctx.PriceState.HasPrice)
                    return data;

                if (!ctx.TooltipCfg.ShowTooltipInRaid && ctx.GameState.IsInRaid)
                    return data;

                // the same source the tooltip highlights, unless the tooltip's own options hide it
                var canTrader = ctx.DisplayPriceState.CanDisplay(DisplayPriceState.MainDisplayPriceFlag.TRADER);
                var canFlea = ctx.DisplayPriceState.CanDisplay(DisplayPriceState.MainDisplayPriceFlag.FLEA);
                var useTrader = ctx.SellabilityState.TraderBuys();
                if (useTrader ? !canTrader : !canFlea)
                {
                    useTrader = !useTrader;
                    if (useTrader ? !canTrader : !canFlea)
                        return data;
                }

                data.Price = useTrader ? ctx.TraderState.TraderOfferPrice : ctx.FleaState.FleaPriceWithModifiers;
                data.PerSlot = useTrader ? ctx.TraderState.PricePerSlot : ctx.FleaState.PricePerSlotWithModifiers;

                data.HasContents = ctx.TooltipCfg.ShowContainedItemFleaPrices
                    && ctx.TooltipCfg.ShouldShowFleaMarketPrices
                    && ctx.FleaState.ContainsItemsWithFleaValue();
                if (data.HasContents)
                {
                    data.ContentsPrice = ctx.FleaState.PriceSumOfContainedItems;
                    data.ContentsPerSlot = data.ContentsPrice / Math.Max(ctx.ItemState.Slots, 1);
                }

                data.Show = true;
            }

            return data;
        }

        internal static string FormatCompact(int value)
        {
            var culture = CultureInfo.InvariantCulture;
            if (value < 1000)
                return value.ToString(culture);
            if (value < 999500)
                return (value / 1000.0).ToString("0.#", culture) + "k";
            return (value / 1000000.0).ToString("0.#", culture) + "M";
        }

        private static void Hide(GridItemView view)
        {
            var chip = view.transform.Find(ChipName);
            if (chip != null && chip.gameObject.activeSelf)
            {
                chip.gameObject.SetActive(false);
            }
        }

        private static void Draw(GridItemView view, string text, Color color)
        {
            var existing = view.transform.Find(ChipName);
            var rect = existing != null ? (RectTransform)existing : Create(view);
            rect.gameObject.SetActive(true);
            EverDrawn = true;

            var tmp = rect.GetComponentInChildren<TextMeshProUGUI>(true);
            tmp.fontSize = LootValueMod.ChipFontSize.Value;
            tmp.text = text;
            var opacity = LootValueMod.ChipOpacity.Value;
            color.a *= opacity;
            tmp.color = color;

            var background = rect.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, LootValueMod.ChipBackgroundOpacity.Value * opacity);

            var preferred = tmp.GetPreferredValues(text);
            rect.sizeDelta = new Vector2(preferred.x + 6f, preferred.y + 2f);

            var extra = new Vector2(LootValueMod.ChipOffsetX.Value, LootValueMod.ChipOffsetY.Value);

            Vector2 anchor;
            Vector2 offset;
            switch (LootValueMod.ChipCornerPosition.Value)
            {
                case ChipCorner.BottomRight:
                    anchor = new Vector2(1f, 0f);
                    offset = new Vector2(-1f, 1f);
                    break;
                case ChipCorner.TopLeft:
                    anchor = new Vector2(0f, 1f);
                    offset = new Vector2(1f, -1f);
                    break;
                case ChipCorner.TopRight:
                    anchor = new Vector2(1f, 1f);
                    offset = new Vector2(-1f, -1f);
                    break;
                default:
                    anchor = new Vector2(0f, 0f);
                    offset = new Vector2(1f, 1f);
                    break;
            }
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            // positive offsets move the chip towards the middle of the icon, from whichever corner it sits in
            rect.anchoredPosition = offset + new Vector2(extra.x * Mathf.Sign(offset.x), extra.y * Mathf.Sign(offset.y));
            rect.SetAsLastSibling();
        }

        // Views are pooled, so this runs once per view; later refreshes find the child again by name.
        private static RectTransform Create(GridItemView view)
        {
            var chip = new GameObject(ChipName, typeof(RectTransform), typeof(Image));
            chip.layer = view.gameObject.layer;
            var rect = (RectTransform)chip.transform;
            rect.SetParent(view.transform, false);
            chip.GetComponent<Image>().raycastTarget = false;

            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.layer = chip.layer;
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.richText = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = TextAlignmentOptions.Center;
            if (view.Caption != null)
            {
                // the game's own font, so every digit has a glyph
                tmp.font = view.Caption.font;
                tmp.fontSharedMaterial = view.Caption.fontSharedMaterial;
            }

            return rect;
        }

        internal class GridItemUpdateInfoPatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod() =>
                typeof(GridItemView).GetMethod("UpdateInfo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            [PatchPostfix]
            private static void PatchPostfix(GridItemView __instance)
            {
                Apply(__instance);
            }
        }
    }

    // Redraws the visible chips when something they depend on changed. Never walks the views per frame: it compares a few
    // numbers, and only on a change does it go through the views (and Alt only touches the item under the pointer).
    public class PriceChipManager : MonoBehaviour
    {
        private DateTime lastRefresh;
        private int lastSettingsVersion;
        private bool lastAlt;

        void Update()
        {
            if (LootValueMod.ChipToggleKey.Value.IsDown())
            {
                PriceChipController.ToggleHidden();
            }

            var refresh = FleaPriceCache.LastGlobalRefresh;
            if (refresh != lastRefresh || PriceChipController.SettingsVersion != lastSettingsVersion)
            {
                lastRefresh = refresh;
                lastSettingsVersion = PriceChipController.SettingsVersion;
                PriceChipController.RedrawAll();
            }

            if (!LootValueMod.ShowPriceChip.Value)
                return;

            var alt = Input.GetKey(KeyCode.LeftAlt);
            if (alt == lastAlt)
                return;

            lastAlt = alt;
            var hovered = PriceChipController.HoveredView;
            if (hovered != null && LootValueMod.ChipAltShowsContents.Value)
            {
                PriceChipController.Apply(hovered);
            }
        }
    }
}
