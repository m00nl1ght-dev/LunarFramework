#if RW_1_7_OR_GREATER

using System;
using HarmonyLib;
using LunarFramework.Patching;
using Verse;

namespace LunarFramework.Internal.Patches;

/// <summary>
/// In 1.7 the engine no longer calls OnApplicationQuit on MonoBehaviours, so now the OnQuit hooks are called by this patch instead.
/// </summary>
[PatchGroup("Main")]
[HarmonyPatch(typeof(Root))]
internal static class Patch_Verse_Root
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Root.Shutdown))]
    private static void Shutdown_Prefix()
    {
        try
        {
            LunarRoot.OnQuit();
        }
        catch (Exception e)
        {
            try
            {
                LunarRoot.Logger.Error("Error in OnQuit", e);
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}

#endif
