using System.Linq;
using System.Reflection;
using SPT.Reflection.Patching;
using EFT.UI;
using UnityEngine;

namespace LootValuePlus
{

	internal static class TooltipController
	{
		internal static class GameTooltipContext
		{
			public static string Text;
			public static float Delay;
			public static SimpleTooltip Tooltip;
			public static ItemTooltipHandler ItemTooltipHandler;

			public static void SetupTooltip(SimpleTooltip _tooltip, ref float _delay, ref string _text)
			{
				Text = _text;
				Delay = _delay;
				Tooltip = _tooltip;

				_delay = 0;
			}

			public static void ClearTooltip()
			{
				Tooltip?.Close();
				Tooltip = null;
				Text = null;
				ClearHandler();
			}

			public static void SetupHandler(ItemTooltipHandler handler)
			{
				ItemTooltipHandler = handler;
			}

			public static void ClearHandler()
			{
				ItemTooltipHandler?.Dispose();
				ItemTooltipHandler = null;
			}
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
				GameTooltipContext.SetupTooltip(__instance, ref delay, ref text);

				if (!ItemTooltipHandler.ShouldModifyTooltipForItem(HoverItemController.hoveredItem))
					return;

				var handler = new ItemTooltipHandler(HoverItemController.hoveredItem);
				GameTooltipContext.SetupHandler(handler);
				handler.Handle(ref text);
			}



		}
	}




}