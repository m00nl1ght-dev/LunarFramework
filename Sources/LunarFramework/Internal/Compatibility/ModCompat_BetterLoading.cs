using HarmonyLib;
using LunarFramework.Internal.Patches;
using LunarFramework.Patching;

namespace LunarFramework.Internal.Compatibility;

internal class ModCompat_BetterLoading : ModCompat
{
    public override string TargetAssemblyName => "BetterLoading";
    public override string DisplayName => "Better Loading";

    protected override bool OnApply()
    {
        var mcp = ModContentPack;
        if (mcp != null && mcp.ModMetaData.AuthorsString != "Samboy063")
        {
            LunarRoot.Logger.Warn("An unofficial version of the BetterLoading mod is installed. " +
                                  "Compatibility with BetterLoading is experimental and likely unstable. " +
                                  "If you encounter any problem or error, before reporting it as a bug, " +
                                  "try removing BetterLoading first and check if that fixes it!");
        }

        var lateInitMethod = Require(AccessTools.Method("BetterLoading.Stage.InitialLoad.StageRunStaticCctors:Finish"));
        Patch_Verse_StaticConstructorOnStartupUtility.TargetMethodOverride = lateInitMethod;

        var menuReadyMethod = Require(AccessTools.Method("BetterLoading.BetterLoadingApi:DispatchLoadComplete"));
        Patch_RimWorld_MainMenuDrawer.TargetMethodOverride = menuReadyMethod;

        return true;
    }
}
