using SPT.Common.Http;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SPT.Reflection.Utils;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using static EFT.ConnectionClient;
using EFT;

namespace LootValuePlus
{
	internal static class FleaPriceCache
	{
		public static Dictionary<string, CachePrice> cache = new Dictionary<string, CachePrice>();
		public static DateTime LastGlobalRefresh { get; private set; } = DateTime.MinValue;

		// From the same bulk request as the averages. What SPT prices a single weapon by: its own handbook price plus the flea base price of each part
		private static Dictionary<string, int> basePrices = new Dictionary<string, int>();
		private static Dictionary<string, int> staticPrices = new Dictionary<string, int>();

		public static int GetBasePrice(string templateId) => basePrices.TryGetValue(templateId, out var price) ? price : 0;
		public static int GetStaticPrice(string templateId) => staticPrices.TryGetValue(templateId, out var price) ? price : 0;
		public static IEftSession Session => ClientAppUtils.GetClientApp().GetClientBackEndSession();

		public static async Task<int?> FetchPrice(string templateId)
		{
			bool fleaAvailable = Session.RagFair.Available || LootValueMod.ShowFleaPriceBeforeAccess.Value;
			if (!fleaAvailable)
			{
				Globals.LogDebug($"Flea market is not available, returning null value.");
				return null;
			}

			if (cache.ContainsKey(templateId))
			{
				Globals.LogDebug($"Template id {templateId} is in cache.");
				var cachedPrice = cache[templateId];
				if (cachedPrice.ShouldUpdate())
				{
					Globals.LogDebug($"Template id {templateId} has expired.");
					if (LootValueMod.UpdateGlobalCacheIfAnyCacheOutOfDate.Value && LootValueMod.EnableGlobalCache.Value)
					{
						// refresh global cache instead
						Globals.LogDebug($"Refreshing global cache.");
						await FetchPricesAndUpdateCache();
						var price = cache[templateId]?.price ?? 0;
						Globals.LogDebug($"Global cache refreshed, returning price of {price}.");
						return price;
					}
					else
					{
						Globals.LogDebug($"Fetching individual price for {templateId}.");
						// fetch individual price
						var price = await QueryTemplateIdSellingPrice(templateId);
						Globals.LogDebug($"Updating cache price for {templateId} on cache: {price}");
						cachedPrice.Update(price);
						return price;
					}
				}

				Globals.LogDebug($"Up to date cached price for {templateId}: {cachedPrice.price}");
				return cachedPrice.price;
			}
			else
			{
				Globals.LogDebug($"Template id {templateId} is not in cache.");
				if (LootValueMod.UpdateGlobalCacheIfAnyCacheOutOfDate.Value && LootValueMod.EnableGlobalCache.Value)
				{
					// refresh global cache instead
					Globals.LogDebug($"Refreshing global cache.");
					await FetchPricesAndUpdateCache();
					var price = cache[templateId]?.price ?? 0;
					Globals.LogDebug($"Global cache refreshed, returning price of {price}.");
					return price;
				}
				else
				{
					Globals.LogDebug($"Fetching individual price for {templateId}.");
					var price = await QueryTemplateIdSellingPrice(templateId);
					Globals.LogDebug($"Updating cache price for {templateId} on cache: {price}");
					cache[templateId] = new CachePrice(price);
					return price;
				}


			}
		}

		public static async Task<int?> FetchPrice(IEnumerable<string> templateIds)
		{
			bool fleaAvailable = Session.RagFair.Available || LootValueMod.ShowFleaPriceBeforeAccess.Value;

			if (!fleaAvailable)
			{
				return null;
			}

			// everything that is not cached should be fetched
			var templateIdsNotInCache = templateIds.Where(id => !cache.ContainsKey(id));
			// Globals.logger.LogInfo($"Templates not in cache: {templateIdsNotInCache.ToJson()}");

			// everything that is cached, but ttl is expired, should be refetched
			var templateIdsInCache = templateIds.Where(cache.ContainsKey);
			// Globals.logger.LogInfo($"Templates in cache: {templateIdsInCache.ToJson()}");
			var templateIdsThatMustBeUpdated = templateIdsInCache.Where(id =>
			{
				var cachedPrice = cache[id];
				return cachedPrice.ShouldUpdate();
			});

			// fetch all expired cache keys and non cached keys
			var templateIdsToFetch = templateIdsThatMustBeUpdated.Concat(templateIdsNotInCache);
			// Globals.logger.LogInfo($"Template ids to fetch: {templateIdsToFetch.ToJson()}");

			// if we have to fetch anything, and global cache is enabled, and flag to update all is enabled, we do that instead
			if (templateIdsToFetch.Any() && LootValueMod.UpdateGlobalCacheIfAnyCacheOutOfDate.Value && LootValueMod.EnableGlobalCache.Value)
			{
				// refresh global cache instead
				await FetchPricesAndUpdateCache();
			}
			else
			{
				// fetch all ids & update cache
				var prices = await QueryTemplateIdSellingPrice(templateIdsToFetch);
				prices.ExecuteForEach(price =>
				{
					var templateId = price.templateId;
					if (cache.ContainsKey(templateId))
					{
						cache[templateId].Update(price.price);
					}
					else
					{
						cache[templateId] = new CachePrice(price.price);
					}
				});
			}
			
			return templateIds.Select(id => cache[id]?.price ?? 0).Sum();
		}

		public static async Task FetchPricesAndUpdateCache(bool forceUpdate = false)
		{
			bool fleaAvailable = Session.RagFair.Available || LootValueMod.ShowFleaPriceBeforeAccess.Value;
			Globals.LogDebug($"Flea market available: {fleaAvailable}");
			if (!fleaAvailable && !forceUpdate)
			{
				return;
			}

			// fetch all ids first: if the request fails or comes back empty (server busy right after a raid) the old prices stay
			var prices = (await GetAllTemplateIdSellingPrice()).ToList();
			if (prices.Count == 0)
			{
				Globals.logger.LogWarning($"Flea price refresh returned no prices, keeping the {cache.Count} cached ones.");
				return;
			}

			var fresh = new Dictionary<string, CachePrice>();
			var freshBase = new Dictionary<string, int>();
			var freshStatic = new Dictionary<string, int>();
			prices.ExecuteForEach(price =>
			{
				fresh[price.templateId] = new CachePrice(price.price);
				freshBase[price.templateId] = price.basePrice;
				freshStatic[price.templateId] = price.staticPrice;
			});

			// replace the cache in one step, so a lookup never sees a half filled one
			basePrices = freshBase;
			staticPrices = freshStatic;
			cache = fresh;
			LastGlobalRefresh = DateTime.Now;
			Globals.logger.LogInfo($"Flea price cache refreshed: {fresh.Count} prices.");

			return;
		}



		// The server's average for one item right now, bypassing the cache (diagnostics only)
		public static int QueryLivePrice(string templateId)
		{
			return Task.Run(() => QueryTemplateIdSellingPrice(templateId)).Result;
		}

		private static async Task<int> QueryTemplateIdSellingPrice(string templateId)
		{
			string response = await QueryPrice(templateId);
			if (string.IsNullOrEmpty(response) || response == "null")
			{
				return 0;
			}

			return int.Parse(response);
		}

		private static async Task<IEnumerable<FleaPrice>> QueryTemplateIdSellingPrice(IEnumerable<string> templateIds)
		{
			if (templateIds.IsNullOrEmpty())
			{
				return [];
			}

			string response = await QueryPrice(templateIds);
			if (string.IsNullOrEmpty(response) || response == "null")
			{
				return templateIds.Select(templateId => new FleaPrice() { templateId = templateId, price = 0 });
			}

			// Globals.logger.LogInfo($"RESPONSE: {response}");
			return JsonConvert.DeserializeObject<FleaPricesResponse>(response).prices;
		}

		private static async Task<IEnumerable<FleaPrice>> GetAllTemplateIdSellingPrice()
		{
			string response = await QueryPrices();
			if (string.IsNullOrEmpty(response) || response == "null")
			{
				return [];
			}

			// Globals.logger.LogInfo($"RESPONSE: {response}");
			return JsonConvert.DeserializeObject<FleaPricesResponse>(response).prices;
		}

		private static async Task<string> QueryPrice(string templateId)
		{
			return await CustomRequestHandler.PostJsonAsync("/LootValue/GetItemLowestFleaPrice", JsonConvert.SerializeObject(new FleaPriceRequest(templateId)));
		}

		private static async Task<string> QueryPrice(IEnumerable<string> templateIds)
		{
			return await CustomRequestHandler.PostJsonAsync("/LootValue/GetMultipleItemsSellingFleaPrice", JsonConvert.SerializeObject(new FleaPricesRequest(templateIds)));
		}

		private static async Task<string> QueryPrices()
		{
			return await CustomRequestHandler.GetAsync("/LootValue/GetAllItemSellingFleaPrice");
		}

	}

	public class FleaPriceRequest
	{
		public string templateId;
		public FleaPriceRequest(string templateId) => this.templateId = templateId;
	}

	public class FleaPricesRequest
	{
		public IEnumerable<string> templateIds;
		public FleaPricesRequest(IEnumerable<string> templateIds) => this.templateIds = templateIds;
	}

	public class FleaPrice
	{
		public string templateId { get; set; }
		public int price { get; set; }
		public int basePrice { get; set; }
		public int staticPrice { get; set; }
	}

	public class FleaPricesResponse
	{
		public Collection<FleaPrice> prices { get; set; }
	}

	internal class CachePrice
	{
		public int price { get; private set; }
		public DateTime lastUpdate { get; private set; }

		public CachePrice(int price)
		{
			this.price = price;
			lastUpdate = DateTime.Now;
		}

		public void Update(int price)
		{
			if (price == 0 && this.price > 0)
			{
				Globals.logger.LogInfo($"Tried to update price with a '0' when current price is {price}, ignoring price update order.");
				return;
			}

			this.price = price;
			lastUpdate = DateTime.Now;
		}

		public bool ShouldUpdate()
		{
			return (DateTime.Now - lastUpdate).TotalSeconds >= LootValueMod.CacheTtl.Value;
		}
	}
}
