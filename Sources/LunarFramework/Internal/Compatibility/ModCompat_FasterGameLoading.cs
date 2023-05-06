using System.Reflection;
using HarmonyLib;
using LunarFramework.Bootstrap;
using LunarFramework.Patching;

namespace LunarFramework.Internal.Compatibility;

[HarmonyPatch]
internal class ModCompat_FasterGameLoading : ModCompat
{
    public override string TargetAssemblyName => "FasterGameLoading";
    public override string DisplayName => "FasterGameLoading";

    private static FieldInfo _cacheField;

    protected override bool OnApply()
    {
        _cacheField = Require(AccessTools.Field(FindType("FasterGameLoading.AccessTools_AllTypes_Patch"), "allTypesCached"));
        Entrypoint.OnComponentAssembliesLoaded += OnComponentAssembliesLoaded;
        return true;
    }

    private void OnComponentAssembliesLoaded()
    {
        _cacheField.SetValue(null, null);
    }
}
