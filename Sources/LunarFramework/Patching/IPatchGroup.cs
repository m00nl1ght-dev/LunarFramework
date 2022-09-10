using System;
using System.Collections.Generic;
using System.Linq;

namespace LunarFramework.Patching;

public interface IPatchGroup
{
    public string Name { get; }
    
    public bool Active { get; }

    public IEnumerable<Type> OwnPatchClasses { get; }
    public IEnumerable<IPatchGroup> OwnSubGroups { get; }

    public void Subscribe(PatchGroupSubscriber subscriber, bool selfOnly = false);
    public void Unsubscribe(PatchGroupSubscriber subscriber, bool selfOnly = false);
    public void UnsubscribeAll(bool selfOnly = false);
}

public static class PatchGroupExtensions
{
    public static IEnumerable<Type> PatchClasses(this IPatchGroup group)
    {
        return group.OwnPatchClasses.Concat(group.OwnSubGroups.SelectMany(g => g.PatchClasses()));
    }

    public static IEnumerable<IPatchGroup> SubGroups(this IPatchGroup group)
    {
        return group.OwnSubGroups.Concat(group.OwnSubGroups.SelectMany(g => g.OwnSubGroups));
    }
}