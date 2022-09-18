using System;
using HarmonyLib;
using LunarFramework.Patching;
using RimWorld;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace LunarFramework.Internal.Patches;

[PatchGroup("Main")]
[HarmonyPatch(typeof(MainMenuDrawer))]
internal static class Patch_RimWorld_MainMenuDrawer
{
    internal static event Action OnMainMenuReady;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(MainMenuDrawer.Init))]
    private static void Init() // TODO ensure that this is called with BetterLoading
    {
        OnMainMenuReady?.Invoke();
        OnMainMenuReady = null;
    }
}