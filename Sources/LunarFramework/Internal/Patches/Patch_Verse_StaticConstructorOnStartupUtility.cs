using System;
using System.Reflection;
using HarmonyLib;
using LunarFramework.Bootstrap;
using LunarFramework.Patching;
using Verse;

namespace LunarFramework.Internal.Patches;

[PatchGroup("Bootstrap")]
[HarmonyPatch]
internal static class Patch_Verse_StaticConstructorOnStartupUtility
{
    internal static MethodBase TargetMethodOverride;

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return TargetMethodOverride ?? AccessTools.Method(typeof(StaticConstructorOnStartupUtility), "CallAll");
    }

    [HarmonyPostfix]
    private static void LateInit()
    {
        try
        {
            Entrypoint.OnPlayDataLoadFinished();
        }
        catch (Exception e)
        {
            LunarRoot.Logger.Fatal("Exception in OnPlayDataLoadFinished", e);
        }
    }
}
