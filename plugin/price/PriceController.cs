using System;
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
                return typeof(EftClientBackendSession).GetMethod(nameof(EftClientBackendSession.SetMainProfile), BindingFlags.Instance | BindingFlags.Public);
            }

            [PatchPostfix]
            protected static void PatchPostfix()
            {
                Globals.LogDebug("Game started - updating flea price cache...");

                if (LootValueMod.EnableGlobalCache.Value)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await FleaPriceCache.FetchPricesAndUpdateCache(forceUpdate: true);
                            Globals.LogDebug("Flea price cache updated successfully.");
                        }
                        catch (Exception ex)
                        {
                            Globals.LogDebug($"Failed to update flea price cache: {ex}");
                        }
                    });
                }
            }

        }

    }

}