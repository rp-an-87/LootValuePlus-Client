using System.Reflection;
using System.Threading.Tasks;
using EFT;
using EFT.UI.Ragfair;
using SPT.Reflection.Patching;

namespace LootValuePlus
{

    internal class PriceController
    {

        internal class OnGameSessionEndPatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(Player).GetMethod(nameof(Player.OnGameSessionEnd), BindingFlags.Instance | BindingFlags.Public);
            }

            [PatchPrefix]
            protected static void Prefix()
            {
                // game ended, do something here
                if (LootValueMod.EnableGlobalCache.Value)
                {

                    Task.Run(() => FleaPriceCache.FetchPricesAndUpdateCache());

                }

            }
        }

        internal class ProfileSelectedPatch : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(Class303.Class1470).GetMethod(nameof(Class303.Class1470.method_0), BindingFlags.Instance | BindingFlags.Public);
            }

            [PatchPrefix]
            protected static void Prefix()
            {
                // game ended, do something here
                if (LootValueMod.EnableGlobalCache.Value)
                {

                    Task.Run(() => FleaPriceCache.FetchPricesAndUpdateCache());

                }
            }

        }

        /* internal class FleaMarketOpenPatch : ModulePatch
        {

            protected override MethodBase GetTargetMethod()
            {
                return typeof(OfferViewList).GetMethod(nameof(OfferViewList.Show), BindingFlags.Instance | BindingFlags.Public);
            }


            [PatchPrefix]
            protected static void Prefix()
            {
                // game ended, do something here
                if (LootValueMod.EnableGlobalCache.Value && LootValueMod.UpdateGlobalCacheOnFleaMarketOpen.Value)
                {

                    Task.Run(() => FleaPriceCache.FetchPricesAndUpdateCache());

                }
            }

        } */

    }

}