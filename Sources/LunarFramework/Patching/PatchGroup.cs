using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LunarFramework.Internal;
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
}
