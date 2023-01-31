using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LunarFramework.Bootstrap;
using LunarFramework.Patching;
using LunarFramework.Utility;

namespace LunarFramework.Internal.Compatibility;

[HarmonyPatch]
internal class ModCompat_HotSwap : ModCompat
{
    public override string TargetAssemblyName => "HotSwap";

    protected override bool OnApply()
    {
        LifecycleHooks.InternalInstance.DoOnceOnMainMenu(AddLunarComponentAssemblies);
        return true;
    }

    private void AddLunarComponentAssemblies()
    {
        var type = FindType("HotSwap.HotSwapMain");
        var field = Require(AccessTools.Field(type, "AssemblyFiles"));
        var assemblyFiles = (Dictionary<Assembly, FileInfo>) Require(field.GetValue(null));

        var frameworkProvider = Entrypoint.LunarMods.Values
            .OrderByDescending(m => Entrypoint.GetAssemblyVersion(LunarMod.FrameworkAssemblyFileIn(m.FrameworkDir)))
            .First();

        var frameworkAssemblyFile = LunarMod.FrameworkAssemblyFileIn(frameworkProvider.FrameworkDir);
        assemblyFiles.Add(typeof(LunarAPI).Assembly, new FileInfo(frameworkAssemblyFile));
        LunarRoot.Logger.Log($"HotSwap mapped {frameworkAssemblyFile} to {typeof(LunarAPI).Assembly.GetName()}");

        foreach (var component in Entrypoint.LunarComponents.Values)
        {
            if (component.LoadedAssembly == null || assemblyFiles.ContainsKey(component.LoadedAssembly)) continue;
            var assemblyFile = Path.Combine(component.LatestVersionProvidedBy.ComponentsDir, component.AssemblyName + ".dll");
            assemblyFiles.Add(component.LoadedAssembly, new FileInfo(assemblyFile));
            LunarRoot.Logger.Log($"HotSwap mapped {assemblyFile} to {component.LoadedAssembly.GetName()}");
        }
    }
}
