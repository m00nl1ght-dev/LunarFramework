using HarmonyLib;
using LunarFramework.Patching;
using RimWorld;

namespace LunarFramework.Internal.Patches;

[PatchGroup("Main")]
[HarmonyPatch(typeof(MainMenuDrawer))]
internal static class Patch_RimWorld_MainMenuDrawer
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(MainMenuDrawer.Init))]
    private static void Init_Postfix()
    {
        LunarRoot.OnMainMenuReady();
    }
}
