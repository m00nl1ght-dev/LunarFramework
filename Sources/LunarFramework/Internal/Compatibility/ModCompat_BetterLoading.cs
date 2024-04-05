using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LunarFramework.Bootstrap;
using LunarFramework.Patching;

namespace LunarFramework.Internal.Compatibility;

[HarmonyPatch]
internal class ModCompat_BetterLoading : ModCompat
{
    public override string TargetAssemblyName => "BetterLoading";
    public override string DisplayName => "Better Loading";

    internal static bool IsPresent;

    private static FieldInfo _taskQueue;

    protected override bool OnApply()
    {
        var mcp = ModContentPack;

        if (mcp != null && mcp.ModMetaData.AuthorsString != "Samboy063")
        {
            LunarRoot.Logger.Warn("An unofficial version of the BetterLoading mod is installed. " +
                                  "This may cause issues. For best compatibility, use the official version by Samboy063 instead.");
        }

        var type = FindType("BetterLoading.Stage.InitialLoad.StageRunStaticCctors");
        _taskQueue = Require(type.GetField("_queue", BindingFlags.NonPublic | BindingFlags.Static));

        IsPresent = true;
        return true;
    }

    /// <summary>
    /// BetterLoading sidesteps the vanilla static constructor invocation.
    /// Therefore it is necessary to inject OnPlayDataLoadFinished into the task queue of BetterLoading.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch("BetterLoading.Stage.InitialLoad.StageRunStaticCctors", "PreCallAll")]
    private static void PreCallAll_Postfix()
    {
        var queue = (List<Action>) _taskQueue.GetValue(null);
        queue?.Add(Entrypoint.OnPlayDataLoadFinished);
    }

    /// <summary>
    /// In vanilla, the UI root is initialized once the PlayDataLoader is fully finished,
    /// but BetterLoading changes the order and causes it to be initialized way earlier,
    /// even before static constructors are called. Therefore it is necessary to hook into
    /// the completion event from BetterLoading additionally.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch("BetterLoading.BetterLoadingApi", "DispatchLoadComplete")]
    private static void DispatchLoadComplete_Postfix()
    {
        if (LunarRoot.Instance != null)
        {
            LunarRoot.OnMainMenuReady();
        }
        else
        {
            var modNames = Entrypoint.LunarMods.Values.Select(m => m.Name).Join();
            LunarRoot.Logger.Error("Initialization is incomplete due to preceding errors.");
            LunarRoot.Logger.Error("The following mods will not work: " + modNames);
        }
    }
}
