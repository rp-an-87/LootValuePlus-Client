using System.Linq;
using System.Reflection;
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

        public static bool IsOnInsuranceScreen() {
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
                CurrentScreen = eftScreenType;
                // Globals.logger.LogInfo($"Screen change: {eftScreenType}");
            }
        }

    }

}