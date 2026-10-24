using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using EFT;
using SPT.Reflection.Patching;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using Comfort.Common;
using static LootValuePlus.Globals;
using UnityEngine;
using System.Threading.Tasks;
using EFT.Trading;

namespace LootValuePlus
{
	[BepInPlugin(pluginGuid, pluginName, pluginVersion)]
	public class LootValueMod : BaseUnityPlugin
	{
		// BepinEx
		public const string pluginGuid = "RPAN87.LootValuePlus";
		public const string pluginName = "LootValuePlus";
		public const string pluginVersion = "2.0.0";

		private void Awake()
		{
			Config.SaveOnConfigSet = true;

			logger = Logger;

			SetupConfig();

			new TraderPatch().Enable();
			new TooltipController.ShowTooltipPatch().Enable();
			new HoverItemController.GridItemOnPointerEnterPatch().Enable();
			new HoverItemController.GridItemOnPointerExitPatch().Enable();
			new ClickItemController.ItemViewOnClickPatch().Enable();
			new ScreenChangeController.ScreenTypePatch().Enable();
			new PriceController.OnGameSessionEndPatch().Enable();
			new PriceController.ProfileSelectedPatch().Enable();
			// new FixesController.WeaponManagerClass_ValidateScopeSmoothZoomUpdate_Patch().Enable();

			SlotColoring.UseDefaultColors();
			gameObject.AddComponent<TooltipRedrawManager>();
		}

		internal static ConfigEntry<bool> EnableQuickSell;
		internal static ConfigEntry<bool> SellSimilarItems;
		internal static ConfigEntry<bool> SellOnlySimilarItemsFiR;
		internal static ConfigEntry<bool> AllowQuickSellPinned;
		internal static ConfigEntry<bool> AllowQuickSellLocked;
		internal static ConfigEntry<bool> OneButtonQuickSell;
		internal static ConfigEntry<bool> SellToTraderIfWeaponIsNonOperational;
		internal static ConfigEntry<int> SellToFleaIfWeaponIsNonOperationalAboveThreshold;
		internal static ConfigEntry<bool> SellToTraderBelowProfitThresholdEnabled;
		internal static ConfigEntry<int> SellToTraderProfitThreshold;

		internal static ConfigEntry<bool> SellToTraderBelowDurabilityThresholdEnabled;
		internal static ConfigEntry<int> SellToTraderDurabilityThreshold;

		internal static ConfigEntry<bool> ShowFleaPricesInRaid;
		internal static ConfigEntry<bool> ShowPrices;

		internal static ConfigEntry<bool> IgnoreFleaMaxOfferCount;
		internal static ConfigEntry<bool> ShowFleaPriceBeforeAccess;
		internal static ConfigEntry<bool> ShowPricePerKgAndPerSlotInRaid;
		internal static ConfigEntry<bool> ShowPricePerKgAndPerSlotOutOfRaid;


		internal static ConfigEntry<bool> ShowTotalFleaValueOfContainedItems;
		internal static ConfigEntry<bool> TotalValueOfContainedItemsOverridesKgAndSlotPrice;
		internal static ConfigEntry<bool> ShowMaximumSingleItemPriceWithContainedItems;
		internal static ConfigEntry<bool> ShowBestInSlotInsteadOfBestRawPrice;

		internal static ConfigEntry<bool> HideLowerPrice;
		internal static ConfigEntry<bool> HideLowerPriceInRaid;

		internal static ConfigEntry<bool> ReducePriceInFleaForBrokenItem;
		internal static ConfigEntry<int> FleaSellRandomVariancePercent;
		internal static ConfigEntry<bool> UseWeaponPartsPrice;
		internal static ConfigEntry<bool> AllowForeignCurrencyTraders;
		internal static ConfigEntry<bool> ShowFleaMarketEligibility;
		internal static ConfigEntry<bool> ShowNonVitalWeaponPartsFleaPrice;

		internal static ConfigEntry<bool> EnableGlobalCache;
		internal static ConfigEntry<bool> UpdateGlobalCacheOnFleaMarketOpen;
		internal static ConfigEntry<int> CacheTtl;
		internal static ConfigEntry<bool> UpdateGlobalCacheIfAnyCacheOutOfDate;
		internal static ConfigEntry<bool> DebugLogs;
		internal static ConfigEntry<float> SellChanceBase;
		internal static ConfigEntry<float> SellChanceMultiplier;

		private void SetupConfig()
		{

			// General: Show Tooltip
			ShowPrices = Config.Bind("0. Item Prices", "0. Show prices when hovering item", true);
			ShowFleaPricesInRaid = Config.Bind("0. Item Prices", "1. Show prices while in raid", true);
			ShowFleaPriceBeforeAccess = Config.Bind("0. Item Prices", "2. Show flea prices before access to it", false);
			ShowPricePerKgAndPerSlotInRaid = Config.Bind("0. Item Prices", "3. Show price p/KG & p/slot in raid", false);
			ShowPricePerKgAndPerSlotOutOfRaid = Config.Bind("0. Item Prices", "3.1. Show price p/KG & p/slot out of raid", false);
			HideLowerPrice = Config.Bind("0. Item Prices", "4. Hide lower price", false);
			HideLowerPriceInRaid = Config.Bind("0. Item Prices", "5. Hide lower price in raid", false);
			ShowFleaMarketEligibility = Config.Bind("0. Item Prices", "6. Show if item is banned from flea market", true);
			ShowNonVitalWeaponPartsFleaPrice = Config.Bind("0. Item Prices", "7. Show flea market price of non vital parts on weapons", false,
				"This will make the flea market price always appear if the mods prices are higher than the trader price");
			ShowTotalFleaValueOfContainedItems = Config.Bind("0. Item Prices", "8. Show total value of contained items while pressing LEFT-ALT", false,
				"If the item contains items, flea market price of all contained items will be shown when pressing alt.");
			TotalValueOfContainedItemsOverridesKgAndSlotPrice = Config.Bind("0. Item Prices", "8.1. Override p/kg & p/slot with contained items", false,
				"This will also make it so if viewing contained item prices, it will reflect the price on the kg/slot calculation.");
			ShowMaximumSingleItemPriceWithContainedItems = Config.Bind("0. Item Prices", "8.2 Show also value of most expensive contained item", false,
				"When seeing contained items, it will also show the price of the most expensive item, painted in it's price per slot color.");
			ShowBestInSlotInsteadOfBestRawPrice = Config.Bind("0. Item Prices", "8.2.1 Consider best price per slot as most expensive", false,
				"Most expensive contained item will be calculated based on Price per slot instead of net price.");

			// General: Quick Sell
			EnableQuickSell = Config.Bind("1. Quick Sell", "0. Enable quick sell", true, "Sell any item(s) instantly using the key combination described in 'One button quick sell'.");

			// General -> Quick Sell:
			OneButtonQuickSell = Config.Bind("1. Quick Sell", "1. One button quick sell", true,
@"If disabled: 
[Alt + Shift + Left Click] sells to trader, 
[Alt + Shift + Right Click] sells to flea market

If enabled:
[Alt + Shift + Left Click] sells to either depending on who pays more");

			SellSimilarItems = Config.Bind("1. Quick Sell", "2. Sell all similar items in one go", false, "If you sell one item and have multiple of the same, they will all be sold simultaneously.");
			SellOnlySimilarItemsFiR = Config.Bind("1. Quick Sell", "2.1. Consider only FiR items", false, "If this is enabled, sell multiple will only select FiR items.");
			AllowQuickSellPinned = Config.Bind("1. Quick Sell", "3. Allow selling Pinned items", false, "Selling all similar items will also consider pinned items if enabled.");
			AllowQuickSellLocked = Config.Bind("1. Quick Sell", "4. Allow selling Locked items", false, "Selling all similar items will also consider locked items if enabled.");
			IgnoreFleaMaxOfferCount = Config.Bind("1. Quick Sell", "5. Ignore flea max offer count", false);
			SellToTraderIfWeaponIsNonOperational = Config.Bind("1. Quick Sell", "6. Sell unusable weapons to trader", false, "Will sell all non operational weapons to the trader.");
			SellToFleaIfWeaponIsNonOperationalAboveThreshold = Config.Bind("1. Quick Sell", "6.1 Unless profit in flea above threshold", 0, "Unless this value is higher than 0, then it will do it based on the price.");
			SellToTraderBelowProfitThresholdEnabled = Config.Bind("1. Quick Sell", "7. Sell to trader if flea price below threshold", false);
			SellToTraderProfitThreshold = Config.Bind("1. Quick Sell", "7.1. Flea market profit threshold", 0,
				"This means that if the flea market profit for this item is below this number, it will be sold to the traders instead. Useful to not clutter offers with minimal impact. If sell multiple at once is enabled, the total profit of the entire offer will be used instead.");
			SellToTraderBelowDurabilityThresholdEnabled = Config.Bind("1. Quick Sell", "8. Sell to trader below durability threshold", false);
			SellToTraderDurabilityThreshold = Config.Bind("1. Quick Sell", "8.1. Durability threshold %", 0,
				"This means that any item that is below this % of durability, will be sold to trader regardless of flea market price. This means if this value is configured at 50%, an ifak with 150/300 would be sold to the traders instead the flea market.");
			ReducePriceInFleaForBrokenItem = Config.Bind("1. Quick Sell", "9. Reduce flea market offer relative to missing durability of item", true,
				"This means if any item that has durability, i.e: IFAK, has 200/300 (66% durability remaining), it's flea market price will be reduced by 33%. This applies to most of things that have a durability bar.");
			AllowForeignCurrencyTraders = Config.Bind("1. Quick Sell", "10. Allow selling to traders paying in USD/EUR", false,
				"If enabled, traders that pay in foreign currency (i.e: Peacekeeper) are considered. Their price is converted to roubles to compare against other traders and the flea market, but you will be paid in their currency.");

			FleaSellRandomVariancePercent = Config.Bind("1. Quick Sell", "11. Flea listing price random variance (%)", 4,
				new ConfigDescription(@"The price shown (Flea Avg) is the average of what the server compares your offer against to decide if it sells. When listing to the flea, the price is randomized by +/- this percent so your offers are not all priced identically.
At 0 offers are listed at exactly the average.
The default server sell formula gives a 100% sell chance as long as you list up to ~4% above the average, and the chance drops quickly above that (about 50% at +24%). That is why the default is 4: it never goes above that and still varies the price.
Increase it for more variance, but values above 4 can list above the 100% sale zone. The average can also shift between when the price is cached and when you list, so keep a margin if you want sales to be near-certain.",
					new AcceptableValueRange<int>(0, 50)));
			UseWeaponPartsPrice = Config.Bind("1. Quick Sell", "12. Price single weapons by their parts", true,
				@"When you list one weapon, the server does not compare your price with the flea average of that weapon: it compares it with the weapon's handbook price plus the flea price of each part. That is often far below the average, so a gun listed at the average rarely sells.
With this on, weapons are priced (and shown in the tooltip as 'Flea (parts)') the way the server judges them, so they sell. Needs the global item cache. Other items always use the flea average.
Turn it off to go back to listing weapons at the flea average of their model.");

			CreateAdvancedConfig();
			ApplyNaturalSettingsOrder();
		}

		// The settings window sorts the keys as text, so "10." lands before "2.". Give every setting an explicit order that follows the
		// numbers in its name instead (0, 0.1, 1, 2, 2.1, ..., 10, 11). Keys are not renamed, so saved values stay.
		private static readonly Regex NumberPrefix = new Regex(@"^\s*(\d+(?:\.\d+)*)", RegexOptions.Compiled);
		private static readonly FieldInfo DescriptionField = typeof(ConfigEntryBase).GetField("<Description>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

		private static int[] NumberPrefixOf(string key)
		{
			var match = NumberPrefix.Match(key);
			return match.Success ? match.Groups[1].Value.Split('.').Select(int.Parse).ToArray() : null;
		}

		private static int CompareKeys(string a, string b)
		{
			var pa = NumberPrefixOf(a);
			var pb = NumberPrefixOf(b);
			if (pa == null || pb == null)
			{
				return pa == pb ? string.CompareOrdinal(a, b) : (pa == null ? 1 : -1);  // unnumbered keys go last
			}

			for (var i = 0; i < Math.Min(pa.Length, pb.Length); i++)
			{
				if (pa[i] != pb[i])
				{
					return pa[i].CompareTo(pb[i]);
				}
			}

			return pa.Length != pb.Length ? pa.Length.CompareTo(pb.Length) : string.CompareOrdinal(a, b);
		}

		private void ApplyNaturalSettingsOrder()
		{
			try
			{
				foreach (var section in Config.Keys.GroupBy(definition => definition.Section))
				{
					var ordered = section.ToList();
					ordered.Sort((a, b) => CompareKeys(a.Key, b.Key));

					for (var i = 0; i < ordered.Count; i++)
					{
						var entry = Config[ordered[i]];
						var order = ordered.Count - i;  // the higher the order, the higher in the list

						var attributes = entry.Description.Tags.OfType<ConfigurationManagerAttributes>().FirstOrDefault();
						if (attributes != null)
						{
							attributes.Order = order;
							continue;
						}

						var description = new ConfigDescription(entry.Description.Description, entry.Description.AcceptableValues,
							entry.Description.Tags.Concat(new object[] { new ConfigurationManagerAttributes { Order = order } }).ToArray());
						DescriptionField?.SetValue(entry, description);
					}
				}
			}
			catch (Exception e)
			{
				logger.LogWarning($"Could not order the settings: {e.Message}");
			}
		}

		private void CreateAdvancedConfig()
		{

			EnableGlobalCache = Config.Bind("2. Advanced", "0. Enable Global Item Cache", true, getAdvancedConfigDescription("All items are fetched on profile load and when a match ends."));
			UpdateGlobalCacheOnFleaMarketOpen = Config.Bind("2. Advanced", "0.1. Fetch all prices when opening flea market", false, getAdvancedConfigDescription("Also update all prices when opening flea market."));
			CacheTtl = Config.Bind("2. Advanced", "1. Individual Item Cache TTL", 3600, getAdvancedConfigDescription("Time to live for each individual item in the cache."));
			UpdateGlobalCacheIfAnyCacheOutOfDate = Config.Bind("2. Advanced", "1.1. Update global cache if one expires", true, getAdvancedConfigDescription("If one item expires, it will update the whole cache. Requires global cache to be enabled."));
			DebugLogs = Config.Bind("2. Advanced", "2. Debug logs", true, getAdvancedConfigDescription("Special logs as INFO messages on console."));

			CreateSimpleButton(
				"2. Advanced",
				"2. Reset Cache",
				"Click to Reset",
				"This will reset the cache of all prices",
				true,
				() =>
				{
					FleaPriceCache.cache.Clear();
					return "";
				}
			);

			CreateSimpleButton(
				"2. Advanced",
				"3. Update Global Cache",
				"Click to get all prices",
				"This will get all prices",
				true,
				() =>
				{
					Task.Run(() => FleaPriceCache.FetchPricesAndUpdateCache(forceUpdate:true));
					return "";
				}
			);

			SellChanceBase = Config.Bind("2. Advanced", "5. Server flea sell chance: base", 50f, getAdvancedConfigDescription("The server's sell.chance.base from ragfair.json (50 by default). Only change it if you changed it on the server. Used to keep listings of damaged items inside the 100% sale zone."));
			SellChanceMultiplier = Config.Bind("2. Advanced", "6. Server flea sell chance: multiplier", 1.24f, getAdvancedConfigDescription("The server's sell.chance.sellMultiplier from ragfair.json (1.24 by default). Only change it if you changed it on the server."));


		}

		private void CreateSimpleButton(
			string configSection,
			string configEntryName,
			string buttonName,
			string description,
			bool isAdvanced,
			System.Func<string> doThings
		)
		{
			System.Action<ConfigEntryBase> drawer = (ConfigEntryBase entry) =>
			{
				if (GUILayout.Button(buttonName, GUILayout.ExpandWidth(true)))
				{
					doThings();
				}
			};

			ConfigDescription configDescription =
				new(
					description,
					null,
					new ConfigurationManagerAttributes { CustomDrawer = drawer, IsAdvanced = isAdvanced }
				);

			Config.Bind(configSection, configEntryName, "", configDescription);
		}

		private ConfigDescription getAdvancedConfigDescription(string description)
		{
			return new ConfigDescription(
					description,
					null,
					new ConfigurationManagerAttributes { IsAdvanced = true }
				);
		}

	}

	internal class TraderPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod() => typeof(Trader).GetConstructors()[0];

		[PatchPostfix]
		private static void PatchPostfix(ref Trader __instance)
		{
			__instance.UpdateSupplyData();
		}
	}

	/* internal static class PlayerExtensions
	{
		private static readonly FieldInfo InventoryControllerField =
			typeof(Player).GetField("_inventoryController", BindingFlags.NonPublic | BindingFlags.Instance);

		public static InventoryController GetInventoryController(this Player player) =>
			InventoryControllerField.GetValue(player) as InventoryController;
	} */

	internal static class Globals
	{
		public static ManualLogSource logger { get; set; }

		public static bool HasRaidStarted()
		{
			bool? inRaid = Singleton<AbstractGame>.Instance?.InRaid;
			return inRaid.HasValue && inRaid.Value;
		}

		public static void LogDebug(string msg)
		{
			if (LootValueMod.DebugLogs.Value)
			{
				logger.LogInfo(msg);
			}
		}

	}


}
