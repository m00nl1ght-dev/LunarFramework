#if RW_1_7_OR_GREATER

using System;
using HarmonyLib;
using LunarFramework.Patching;
using Verse;

namespace LunarFramework.Internal.Patches;

/// <summary>
/// In 1.7, adding an additional MonoBehaviour with an OnGUI hook breaks GUI.depth and causes sliders in other mods' UIs to no longer work.
/// So instead, this conditional patch hooks into the existing OnGUI hook of the Verse.Root MonoBehaviour.
/// </summary>
[PatchGroup("OnGUI")]
[HarmonyPatch(typeof(Root))]
internal static class Patch_Verse_Root_OnGUI
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(Root.OnGUI))]
    private static void OnGUI_Postfix()
    {
        try
        {
            LunarRoot.OnGUI();
        }
        catch (Exception e)
        {
            LunarRoot.Logger.Error("Error in OnGUI", e);
        }
    }
}

#endif
