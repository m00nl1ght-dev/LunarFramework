using System;
using System.Collections.Generic;
using System.Linq;

namespace LunarFramework.Bootstrap;

public class LunarComponent
{
    public readonly string AssemblyName;
    
    public Version LatestVersion => ProvidedVersions.Values.Max();
    public LunarMod LatestVersionProvidedBy => ProvidedVersions.OrderByDescending(p => p.Value).Select(p => p.Key).First();

    public IReadOnlyDictionary<LunarMod, Version> ProvidedVersions => ProvidedVersionsInternal;
    internal readonly Dictionary<LunarMod, Version> ProvidedVersionsInternal = new();
    
    public IEnumerable<LunarMod> ProvidingMods => ProvidedVersions.Keys;

    public IReadOnlyCollection<string> Aliases => AliasesInternal;
    internal readonly HashSet<string> AliasesInternal = new();

    public bool AllowNonLunarSource { get; internal set; } = true;
    
    public LoadingState LoadingState { get; internal set; } = LoadingState.Pending;

    internal LunarComponent(string assemblyName)
    {
        AssemblyName = assemblyName;
    }
}