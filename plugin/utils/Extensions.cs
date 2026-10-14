using SPT.Reflection.Utils;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using System.Reflection;
using EFT.Trading;

namespace LootValuePlus
{
	internal static class NumberExtensions
	{
		public static string FormatNumber(this int s)
		{
			if (s > 1000)
				return $"{string.Format("{0:0.0}", (double)s / 1000)}k";
			return s.ToString();
		}

		public static string FormatNumber(this double s)
		{
			if (s > 1000)
				return $"{string.Format("{0:0.0}", s / 1000)}k";
			return s.ToString();
		}
	}

	internal static class TraderClassExtensions
	{
		private static IEftSession Session => ClientAppUtils.GetClientApp().GetClientBackEndSession();

		private static readonly FieldInfo SupplyDataField =
			typeof(Trader).GetField("_supplyData", BindingFlags.Public | BindingFlags.Instance);

		public static SupplyData GetSupplyData(this Trader trader) =>
			SupplyDataField.GetValue(trader) as SupplyData;

		public static void SetSupplyData(this Trader trader, SupplyData supplyData) =>
			SupplyDataField.SetValue(trader, supplyData);

		public static async void UpdateSupplyData(this Trader trader)
		{
			Result<SupplyData> result = await Session.GetSupplyData(trader.Id);

			if (result.Failed)
				return;

			trader.SetSupplyData(result.Value);
		}
	}
}
