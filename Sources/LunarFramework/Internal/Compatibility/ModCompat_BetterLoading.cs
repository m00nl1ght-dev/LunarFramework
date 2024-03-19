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
                                  "This may cause issues. For best compatibility, use the official version by Samboy063 instead.");
        }

        var lateInitMethod = Require(AccessTools.Method("BetterLoading.Stage.InitialLoad.StageRunPostFinalizeCallbacks:BecomeActive"));
        Patch_Verse_StaticConstructorOnStartupUtility.TargetMethodOverride = lateInitMethod;

        var menuReadyMethod = Require(AccessTools.Method("BetterLoading.BetterLoadingApi:DispatchLoadComplete"));
        Patch_RimWorld_MainMenuDrawer.TargetMethodOverride = menuReadyMethod;

        return true;
    }
}
