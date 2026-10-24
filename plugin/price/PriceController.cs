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
                // raid ended (extract, death or leaving): the offers on the flea move on while we play, so take the averages again.
                // Forced, so it doesn't depend on the flea market state during the raid end; the old prices are kept if it fails.
                if (LootValueMod.EnableGlobalCache.Value)
                {
                    Globals.LogDebug("Raid ended - updating flea price cache...");
                    Task.Run(async () =>
                    {
                        try
                        {
                            await FleaPriceCache.FetchPricesAndUpdateCache(forceUpdate: true);
                        }
                        catch (Exception ex)
                        {
                            Globals.logger.LogError($"Failed to update flea price cache after the raid: {ex}");
                        }
                    });
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