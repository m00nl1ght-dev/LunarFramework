using HarmonyLib;
using LunarFramework.Patching;
using RimWorld;
using Verse;

namespace LunarFramework.Internal.Patches;

[PatchGroup("Main")]
[HarmonyPatch(typeof(Page))]
internal static class Patch_RimWorld_Page
{
    [HarmonyPostfix]
    [HarmonyPatch("DoBack")]
    private static void DoBack_Postfix(Page __instance)
    {
        // Normally this happens in UIRoot_Entry.DoMainMenu but the exact sequence is dependent on
        // Unity event listener order which is apparently different with LunarRoot.OnGUI present compared to vanilla.
        // There are some mod-added GameComponents which when updated during entry can throw errors in that case.
        // (e.g. Research Reinvented trying to use TickManager.Paused which depends on Find.World in some code branches)
        // Therefore, clear Current.Game here so that no residual game components
        // attempt to Update() during the first frame on the main menu.
        if (__instance.prev == null && __instance is Page_SelectScenario or Page_CreateWorldParams)
        {
            Current.Game = null;
        }
    }
}
