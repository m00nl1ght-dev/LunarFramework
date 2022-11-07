using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using LunarFramework.Patching;
using RimWorld;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace LunarFramework.Internal.Patches;

[PatchGroup("Main")]
[HarmonyPatch]
internal static class Patch_RimWorld_MainMenuDrawer
{
    internal static event Action OnMainMenuReady;
    
    internal static MethodBase TargetMethodOverride;
    
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return TargetMethodOverride ?? AccessTools.Method(typeof(MainMenuDrawer), "Init");
    }

    [HarmonyPostfix]
    private static void MenuReady()
    {
        Interlocked.Exchange(ref OnMainMenuReady, null)?.Invoke();
    }
}