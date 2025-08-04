using SPT.Common.Http;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SPT.Reflection.Utils;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;

namespace LootValuePlus
{
	internal static class FleaPriceCache
	{
		public static Dictionary<string, CachePrice> cache = new Dictionary<string, CachePrice>();
		public static ISession Session => ClientAppUtils.GetMainApp().GetClientBackEndSession();

		public static async Task<int?> FetchPrice(string templateId)
		{
			bool fleaAvailable = Session.RagFair.Available || LootValueMod.ShowFleaPriceBeforeAccess.Value;
			if (!fleaAvailable)
			{
				return null;
			}

			if (cache.ContainsKey(templateId))
			{
				var cachedPrice = cache[templateId];
				if (cachedPrice.ShouldUpdate())
				{
					
					if (LootValueMod.UpdateGlobalCacheIfAnyCacheOutOfDate.Value && LootValueMod.EnableGlobalCache.Value)
					{
						// refresh global cache instead
						await FetchPricesAndUpdateCache();
					}
					else
					{
						// fetch individual price
						var price = await QueryTemplateIdSellingPrice(templateId);
						cachedPrice.Update(price);
					}
				}
				return cachedPrice.price;
			}
			else
			{
				var price = await QueryTemplateIdSellingPrice(templateId);
				cache[templateId] = new CachePrice(price);
				return price;
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
			
			return templateIds.Select(id => cache[id].price).Sum();
		}

		public static async Task FetchPricesAndUpdateCache()
		{
			bool fleaAvailable = Session.RagFair.Available || LootValueMod.ShowFleaPriceBeforeAccess.Value;
			if (!fleaAvailable)
			{
				return;
			}

			// clear cache for previously saved stuff
			cache.Clear();

			// fetch all ids & update cache
			// Globals.logger.LogInfo($"Getting prices");
			var prices = await GetAllTemplateIdSellingPrice();
			// Globals.logger.LogInfo($"Get prices: {prices}");
			prices.ExecuteForEach(price =>
			{
				var templateId = price.templateId;
				if (cache.ContainsKey(templateId))
				{
					// Globals.logger.LogInfo($"Update cache [{templateId}]: {price.price}");
					cache[templateId].Update(price.price);
				}
				else
				{
					// Globals.logger.LogInfo($"Create cache [{templateId}]: {price.price}");
					cache[templateId] = new CachePrice(price.price);
				}
			});

			return;
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
			this.price = price;
			lastUpdate = DateTime.Now;
		}

		public bool ShouldUpdate()
		{
			return (DateTime.Now - lastUpdate).TotalSeconds >= LootValueMod.CacheTtl.Value;
		}
	}
}
