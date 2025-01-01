using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LunarFramework.Bootstrap;
using LunarFramework.Logging;
using Verse;

namespace LunarFramework.Patching;

public abstract class ModCompat
{
    public abstract string TargetAssemblyName { get; }
    public virtual string DisplayName => TargetAssemblyName;

    public Assembly TargetAssembly { get; private set; }

    public ModContentPack ModContentPack => LoadedModManager.RunningMods
        .FirstOrDefault(mcp => mcp.assemblies.loadedAssemblies.Contains(TargetAssembly));

    private int _reflectiveAccessOperationIdx;

    public static bool IsModAssemblyPresent(string name)
    {
        return Entrypoint.AllModAssemblies.ContainsKey(name);
    }

    public static void ApplyAll(LunarAPI lunarAPI, PatchGroup patchGroup)
    {
        ApplyAll(lunarAPI.Component.LoadedAssembly, lunarAPI.LogContext, patchGroup);
    }

    public static void ApplyAll(Assembly assembly, LogContext logContext, PatchGroup patchGroup)
    {
        foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
        {
            if (type.IsSubclassOf(typeof(ModCompat)) && !type.IsAbstract)
            {
                try
                {
                    var instance = (ModCompat) Activator.CreateInstance(type);
                    if (Entrypoint.AllModAssemblies.TryGetValue(instance.TargetAssemblyName, out var target))
                    {
                        try
                        {
                            instance.TargetAssembly = target;
                            instance.TryApply(patchGroup);
                        }
                        catch (Exception e)
                        {
                            logContext.Error("Failed to apply compatibility patches for " + instance.DisplayName, e);
                        }
                    }
                }
                catch (Exception e)
                {
                    logContext.Error("Failed to create compatibility patches from " + type.Name, e);
                }
            }
        }
    }

    protected void TryApply(PatchGroup patchGroup)
    {
        _reflectiveAccessOperationIdx = 0;

        if (OnApply())
        {
            if (GetType().GetCustomAttribute<HarmonyPatch>() != null)
            {
                var subGroup = patchGroup.NewSubGroup(TargetAssemblyName);
                subGroup.AddPatch(GetType());
            }
        }
    }

    protected virtual bool OnApply() => true;

    public Type FindType(string name)
    {
        return Require(TargetAssembly.GetType(name));
    }

    public T Require<T>(T value)
    {
        if (value == null) throw new Exception("Reflection target with index " + _reflectiveAccessOperationIdx + " not found");
        _reflectiveAccessOperationIdx++;
        return value;
    }
}
