using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LunarFramework.Bootstrap;

public class LunarComponent : IComparable<LunarComponent>
{
    public readonly string AssemblyName;
    public readonly int SortOrderIdx;

    public Version LatestVersion => ProvidedVersions.Values.Max();
    public LunarMod LatestVersionProvidedBy => ProvidedVersions.OrderByDescending(p => p.Value).Select(p => p.Key).First();

    public IReadOnlyDictionary<LunarMod, Version> ProvidedVersions => ProvidedVersionsInternal;
    internal readonly Dictionary<LunarMod, Version> ProvidedVersionsInternal = new();

    public IEnumerable<LunarMod> ProvidingMods => ProvidedVersions.Keys;

    public IReadOnlyCollection<string> Aliases => AliasesInternal;
    internal readonly HashSet<string> AliasesInternal = new();

    public IReadOnlyCollection<string> DependsOn => DependsOnInternal;
    internal readonly HashSet<string> DependsOnInternal = new();

    public IReadOnlyCollection<LunarComponent> Dependents => DependentsInternal;
    internal readonly HashSet<LunarComponent> DependentsInternal = new();

    public bool AllowNonLunarSource { get; internal set; } = true;

    public LoadingState LoadingState { get; internal set; } = LoadingState.Pending;

    public Assembly LoadedAssembly { get; internal set; }

    internal LunarAPI LunarAPI { get; set; }

    internal Action InitAction { get; set; }
    internal Action CleanupAction { get; set; }

    internal LunarComponent(string assemblyName, int sortOrderIdx)
    {
        AssemblyName = assemblyName;
        SortOrderIdx = sortOrderIdx;
    }

    public int CompareTo(LunarComponent other)
    {
        return SortOrderIdx.CompareTo(other.SortOrderIdx);
    }
}
