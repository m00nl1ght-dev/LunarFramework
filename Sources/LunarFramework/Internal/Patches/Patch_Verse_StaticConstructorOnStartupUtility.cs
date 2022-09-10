using System;
using HarmonyLib;
using LunarFramework.Bootstrap;
using LunarFramework.Patching;
using Verse;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace LunarFramework.Internal.Patches;

[PatchGroup("Bootstrap")]
[HarmonyPatch(typeof(StaticConstructorOnStartupUtility))]
internal static class Patch_Verse_StaticConstructorOnStartupUtility
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(StaticConstructorOnStartupUtility.CallAll))]
    private static void CallAll()
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