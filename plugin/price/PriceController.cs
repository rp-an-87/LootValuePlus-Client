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
                return typeof(Class308).GetMethod(nameof(Class308.SetMainProfile), BindingFlags.Instance | BindingFlags.Public);
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

    }

}