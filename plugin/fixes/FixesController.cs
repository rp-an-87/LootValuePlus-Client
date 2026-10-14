namespace LootValuePlus
{

    // internal class FixesController
    // {
    //     // https://github.com/project-fika/Fika-Plugin/blob/e75584397be5bda1d30e16900fa6f0839bc8e55a/Fika.Core/Main/Patches/Camera/WeaponManagerClass_ValidateScopeSmoothZoomUpdate_Patch.cs#L6
    //     internal class WeaponManagerClass_ValidateScopeSmoothZoomUpdate_Patch : ModulePatch
    //     {
    //         protected override MethodBase GetTargetMethod()
    //         {
    //             return typeof(WeaponManagerClass).GetMethod(nameof(WeaponManagerClass.ValidateScopeSmoothZoomUpdate));
    //         }

    //         [PatchPrefix]
    //         public static bool Prefix(WeaponManagerClass __instance)
    //         {
    //             if (__instance.Player != null && !__instance.Player.IsYourPlayer)
    //             {
    //                 return false;
    //             }
    //             return true;
    //         }
    //     }

    // }


}