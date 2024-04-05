using HarmonyLib;
using LunarFramework.Bootstrap;
using LunarFramework.Internal.Compatibility;
using LunarFramework.Patching;
using Verse;

namespace LunarFramework.Internal.Patches;

[PatchGroup("Bootstrap")]
[HarmonyPatch(typeof(StaticConstructorOnStartupUtility))]
internal static class Patch_Verse_StaticConstructorOnStartupUtility
{
    [HarmonyPrepare]
    private static bool PatchCondition() => !ModCompat_BetterLoading.IsPresent;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(StaticConstructorOnStartupUtility.CallAll))]
    private static void CallAll_Postfix()
    {
        Entrypoint.OnPlayDataLoadFinished();
    }
}
