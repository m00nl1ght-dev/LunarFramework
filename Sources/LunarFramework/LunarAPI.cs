using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LunarFramework.Bootstrap;
using LunarFramework.Internal;
using LunarFramework.Logging;
using LunarFramework.Patching;
using LunarFramework.Utility;

namespace LunarFramework;

public class LunarAPI
{
    public static Version FrameworkVersion => typeof(LunarAPI).Assembly.GetName().Version;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static LunarAPI Create(string displayName, Action initAction = null, Action cleanupAction = null)
    {
        var assembly = new StackTrace().GetFrame(1)?.GetMethod()?.ReflectedType?.Assembly;

        if (assembly == null)
        {
            LunarRoot.Logger.Error("Failed to load '" + displayName + "' because its origin component could not be determined.");
            return null;
        }

        if (!Entrypoint.LunarComponents.TryGetValue(assembly.GetName().Name, out var component))
        {
            LunarRoot.Logger.Error("Failed to load '" + displayName + "' because component '" + assembly.GetName().Name + "' is not present.");
            return null;
        }

        if (component.LunarAPI != null)
        {
            LunarRoot.Logger.Error("LunarAPI for '" + displayName + "' has already been created.");
            return null;
        }

        if (component.LoadingState > LoadingState.Loaded)
        {
            LunarRoot.Logger.Error("Failed to load '" + displayName + "' because component is in state '" + component.LoadingState + "'.");
            return null;
        }

        component.InitAction = initAction;
        component.CleanupAction = cleanupAction;
        return new LunarAPI(component, displayName);
    }

    public readonly LunarComponent Component;
    public readonly string DisplayName;

    public readonly LogContext LogContext;
    public readonly PatchGroup RootPatchGroup;
    public readonly LifecycleHooks LifecycleHooks;

    private LunarAPI(LunarComponent component, string displayName)
    {
        Component = component;
        DisplayName = displayName;

        LogContext = new IngameLogContext(typeof(LunarAPI), displayName, component.LatestVersion);
        RootPatchGroup = new PatchGroup(component.AssemblyName);
        LifecycleHooks = new LifecycleHooks();
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class LunarComponentEntrypoint : Attribute { }
