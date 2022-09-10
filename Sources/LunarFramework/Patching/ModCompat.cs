using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace LunarFramework.Patching;

public abstract class ModCompat
{
    public abstract string TargetAssembly { get; }
    public abstract string DisplayName { get; }

    public static void ApplyAll(LunarAPI lunarAPI, PatchGroup patchGroup)
    {
        var assemblies = LoadedModManager.RunningModsListForReading
            .SelectMany(m => m.assemblies.loadedAssemblies).Distinct()
            .ToDictionary(a => a.GetName().Name);

        foreach (var type in AccessTools.GetTypesFromAssembly(lunarAPI.Component.LoadedAssembly))
        {
            if (type.IsSubclassOf(typeof(ModCompat)) && !type.IsAbstract)
            {
                try
                {
                    var instance = (ModCompat) Activator.CreateInstance(type);
                    if (assemblies.TryGetValue(instance.TargetAssembly, out var target))
                    {
                        try
                        {
                            instance.TryApply(patchGroup, target);
                        }
                        catch (Exception e)
                        {
                            lunarAPI.LogContext.Error("Failed to apply compatibility patches for " + instance.DisplayName, e);
                        }
                    }
                }
                catch (Exception e)
                {
                    lunarAPI.LogContext.Error("Failed to create compatibility patches from " + type.Name, e);
                }
            }
        }
    }

    protected void TryApply(PatchGroup patchGroup, Assembly assembly)
    {
        if (OnApply(assembly))
        {
            if (GetType().GetCustomAttribute<HarmonyPatch>() != null)
            {
                var subGroup = patchGroup.NewSubGroup(TargetAssembly);
                subGroup.AddPatch(GetType());
            }
        }
    }

    protected virtual bool OnApply(Assembly assembly) => true;
}