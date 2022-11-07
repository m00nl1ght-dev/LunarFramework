using HarmonyLib;
using LunarFramework.Internal.Patches;
using LunarFramework.Patching;

// ReSharper disable RedundantAssignment
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace LunarFramework.Internal.Compatibility;

internal class ModCompat_BetterLoading : ModCompat
{
    public override string TargetAssemblyName => "BetterLoading";
    public override string DisplayName => "Better Loading";

    protected override bool OnApply()
    {
        var lateInitMethod = Require(AccessTools.Method("BetterLoading.Stage.InitialLoad.StageRunStaticCctors:Finish"));
        Patch_Verse_StaticConstructorOnStartupUtility.TargetMethodOverride = lateInitMethod;
        
        var menuReadyMethod = Require(AccessTools.Method("BetterLoading.BetterLoadingApi:DispatchLoadComplete"));
        Patch_RimWorld_MainMenuDrawer.TargetMethodOverride = menuReadyMethod;
        
        return true;
    }
}