using System.Linq;
using System.Reflection;
using EFT.CameraControl;
using SPT.Reflection.Patching;
using UnityEngine;

namespace LootValuePlus
{

    internal class FixesController
    {
        // https://github.com/project-fika/Fika-Plugin/blob/main/Fika.Core/Coop/Patches/Camera/WeaponManagerClass_ValidateScopeSmoothZoomUpdate_Patch.cs
        internal class WeaponManagerClass_ValidateScopeSmoothZoomUpdate_Patch : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(WeaponManagerClass).GetMethod(nameof(WeaponManagerClass.ValidateScopeSmoothZoomUpdate));
            }

            [PatchPrefix]
            public static bool Prefix(WeaponManagerClass __instance)
            {
                if (__instance.Player != null && !__instance.Player.IsYourPlayer)
                {
                    return false;
                }
                return true;
            }
        }

        // https://github.com/project-fika/Fika-Plugin/blob/main/Fika.Core/Coop/Patches/Camera/WeaponManagerClass_method_13_Patch.cs
        internal class WeaponManagerClass_method_13_Patch : ModulePatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(WeaponManagerClass).GetMethod(nameof(WeaponManagerClass.method_13));
            }

            [PatchPrefix]
            public static bool Prefix(WeaponManagerClass __instance)
            {
                if (__instance.Player != null && !__instance.Player.IsYourPlayer)
                {
                    __instance.tacticalComboVisualController_0 = [.. __instance.transform_1.GetComponentsInChildrenActiveIgnoreFirstLevel<TacticalComboVisualController>()];
                    __instance.sightModVisualControllers_0 = [.. __instance.transform_1.GetComponentsInChildrenActiveIgnoreFirstLevel<SightModVisualControllers>()];
                    __instance.launcherViauslController_0 = [.. __instance.transform_1.GetComponentsInChildrenActiveIgnoreFirstLevel<LauncherViauslController>()];
                    __instance.bipodViewController_0 = __instance.transform_1.GetComponentsInChildrenActiveIgnoreFirstLevel<BipodViewController>().FirstOrDefault();

                    return false;
                }

                return true;
            }
        }

    }


}