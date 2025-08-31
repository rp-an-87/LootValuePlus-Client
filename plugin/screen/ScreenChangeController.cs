using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EFT.UI;
using EFT.UI.Screens;
using SPT.Reflection.Patching;

namespace LootValuePlus
{

    internal class ScreenChangeController
    {

        internal static EEftScreenType CurrentScreen;

        public static bool CanQuickSellOnCurrentScreen()
        {
            if (CurrentScreen == EEftScreenType.Inventory)
            {
                return true;
            }

            return false;
        }

        public static bool CanShowItemPriceTooltipsOnCurrentScreen()
        {
            if (CurrentScreen == EEftScreenType.EditBuild
                || CurrentScreen == EEftScreenType.WeaponModding
                || CurrentScreen == EEftScreenType.HealthTreatment
                || CurrentScreen == EEftScreenType.BattleUI)
            {
                return false;
            }

            return true;
        }

        public static bool IsOnInsuranceScreen()
        {
            return CurrentScreen == EEftScreenType.Insurance;
        }


        internal class ScreenTypePatch : ModulePatch
        {

            protected override MethodBase GetTargetMethod()
            {
                return typeof(MenuTaskBar)
                        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .Where(x => x.Name == "OnScreenChanged")
                        .ToList()[0];
            }

            [PatchPrefix]
            static void Prefix(EEftScreenType eftScreenType)
            {

                var globalCacheFleaMarketRefresh = LootValueMod.EnableGlobalCache.Value && LootValueMod.UpdateGlobalCacheOnFleaMarketOpen.Value;
                var newScreenIsFleaMarket = eftScreenType == EEftScreenType.FleaMarket && CurrentScreen != EEftScreenType.FleaMarket;

                if (globalCacheFleaMarketRefresh && newScreenIsFleaMarket)
                {
                    Task.Run(() => FleaPriceCache.FetchPricesAndUpdateCache());
                }

                CurrentScreen = eftScreenType;

                HoverItemController.ClearHoverItem();
                TooltipController.GameTooltipContext.ClearTooltip();
                // Globals.logger.LogInfo($"Screen change: {eftScreenType}");

            }
        }

    }

}