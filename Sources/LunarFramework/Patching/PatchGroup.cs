using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LunarFramework.Internal;
using LunarFramework.Logging;
using LunarFramework.Utility;

namespace LunarFramework.Patching;

public class PatchGroup : IPatchGroup
{
    public string Name { get; }

    public bool Active => _subscribers.Count > 0;

    public IEnumerable<Type> OwnPatchClasses => _ownPatchClasses;
    public IEnumerable<IPatchGroup> OwnSubGroups => _ownSubGroups;

    private readonly HashSet<Type> _ownPatchClasses = new();
    private readonly HashSet<IPatchGroup> _ownSubGroups = new();

    private readonly HashSet<PatchGroupSubscriber> _subscribers = new();

    private readonly float _unpatchDelay;

    private readonly Harmony _harmony;
    private bool _patchesApplied;

    public PatchGroup(string name, float unpatchDelay = 0f)
    {
        Name = name;
        _harmony = new Harmony(name);
        _unpatchDelay = unpatchDelay;
    }

    public PatchGroup(string name, float unpatchDelay, params Type[] patchClasses) : this(name, unpatchDelay)
    {
        foreach (var patchClass in patchClasses)
        {
            AddPatch(patchClass);
        }
    }

    public void AddPatch(Type patchClass)
    {
        if (_ownPatchClasses.Add(patchClass))
        {
            if (_patchesApplied)
            {
                TryPatch(patchClass);
            }
        }
    }

    public void AddPatches(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name;
        foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
        {
            var attribute = type.GetCustomAttribute<PatchGroupAttribute>();
            if (attribute != null && assemblyName + "." + attribute.Name == Name)
            {
                AddPatch(type);
            }
        }
    }

    public PatchGroup NewSubGroup(string subname, float unpatchDelay = 0f)
    {
        var group = new PatchGroup(Name + "." + subname, unpatchDelay);
        AddSubGroup(group);
        return group;
    }

    public void AddSubGroup(PatchGroup group)
    {
        if (_ownSubGroups.Add(group))
        {
            foreach (var subscriber in _subscribers)
            {
                group.Subscribe(subscriber);
            }
        }
    }

    public void Subscribe(PatchGroupSubscriber subscriber = null, bool selfOnly = false)
    {
        subscriber ??= PatchGroupSubscriber.Generic;

        if (_subscribers.Add(subscriber))
        {
            TryPatch();
        }

        if (!selfOnly)
        {
            foreach (var group in _ownSubGroups)
            {
                group.Subscribe(subscriber);
            }
        }
    }

    public void Unsubscribe(PatchGroupSubscriber subscriber = null, bool selfOnly = false)
    {
        subscriber ??= PatchGroupSubscriber.Generic;

        if (_subscribers.Remove(subscriber))
        {
            if (_subscribers.Count == 0)
            {
                TryUnpatch();
            }
        }

        if (!selfOnly)
        {
            foreach (var group in _ownSubGroups)
            {
                group.Unsubscribe(subscriber);
            }
        }
    }

    public void UnsubscribeAll(bool selfOnly = false)
    {
        _subscribers.Clear();
        TryUnpatch();

        if (!selfOnly)
        {
            foreach (var group in _ownSubGroups)
            {
                group.UnsubscribeAll();
            }
        }
    }

    private void TryPatch()
    {
        if (Active && !_patchesApplied)
        {
            _patchesApplied = true;
            foreach (var patchClass in _ownPatchClasses)
            {
                TryPatch(patchClass);
            }
        }
    }

    private void TryPatch(Type patchClass)
    {
        try
        {
            _harmony.CreateClassProcessor(patchClass).Patch();
        }
        catch (HarmonyException)
        {
            UnsubscribeAll();
            throw;
        }
    }

    private void TryUnpatch()
    {
        void DoUnpatch()
        {
            if (!Active && _patchesApplied)
            {
                _patchesApplied = false;
                _harmony.UnpatchAll(_harmony.Id);
            }
        }

        if (!Active && _patchesApplied)
        {
            if (LunarRoot.IsReady)
            {
                LifecycleHooks.InternalInstance.DoOnce(DoUnpatch, _unpatchDelay);
            }
            else
            {
                DoUnpatch();
            }
        }
    }

    public void CheckForConflicts(Action<MethodBase, Patch> onConflict) => CheckForConflicts(_harmony.Id, onConflict);

    public void CheckForConflicts(LogContext logger)
    {
        CheckForConflicts((method, patch) =>
        {
            var source = patch.PatchMethod.DeclaringType?.FindSourceMod()?.Name ?? patch.owner;
            logger.Warn($"Detected potential conflict: The mod \"{source}\" ({patch.owner}) adds a destructive patch " +
                        $"that will likely override or break some functionality of {logger.Name}.\n" +
                        $"Patch method: {patch.PatchMethod?.FullDescription()}\n" +
                        $"Target method: {method.FullDescription()}");
        });
    }

    public static void CheckForConflicts(string id, Action<MethodBase, Patch> onConflict)
    {
        bool ShouldCheckForConflicts(Patch patch)
        {
            return patch.owner == id && patch.PatchMethod?.GetCustomAttribute<PatchExcludedFromConflictCheckAttribute>() == null;
        }
        
        foreach (var method in Harmony.GetAllPatchedMethods())
        {
            try
            {
                var patchInfo = Harmony.GetPatchInfo(method);
                var prefix = patchInfo.Prefixes.FirstOrDefault(ShouldCheckForConflicts);
                var transpiler = patchInfo.Transpilers.FirstOrDefault(ShouldCheckForConflicts);

                if (prefix != null || transpiler != null)
                {
                    foreach (var other in patchInfo.Prefixes)
                    {
                        if (other.owner == id) continue;
                        if (other.PatchMethod.ReturnType != typeof(bool)) continue;
                        if (transpiler == null && other.priority < prefix.priority) continue;
                        if (prefix != null && (prefix.after.Contains(other.owner) || prefix.before.Contains(other.owner))) continue;
                        if (transpiler != null && (transpiler.after.Contains(other.owner) || transpiler.before.Contains(other.owner))) continue;
                        onConflict(method, other);
                    }
                }
            }
            catch (Exception e)
            {
                LunarRoot.Logger.Warn($"Exception occured while checking for conflicts on {method.FullDescription()}", e);
            }
        }
    }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class PatchExcludedFromConflictCheckAttribute : Attribute { }
