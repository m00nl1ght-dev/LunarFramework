using System;
using System.IO;
using Verse;

namespace LunarFramework.Bootstrap;

public class LunarMod
{
    public const string LoaderAssemblyFileName = "LunarLoader.dll";
    public const string FrameworkAssemblyFileName = "LunarFramework.dll";
    
    internal static string FrameworkDirIn(string loadDir) => Path.Combine(loadDir, "Lunar");
    internal static string AssembliesDirIn(string loadDir) => Path.Combine(loadDir, "Assemblies");
    internal static string ComponentsDirIn(string frameworkDir) => Path.Combine(frameworkDir, "Components");
    internal static string ManifestFileIn(string frameworkDir) => Path.Combine(frameworkDir, "Manifest.xml");
    internal static string FrameworkAssemblyFileIn(string frameworkDir) => Path.Combine(ComponentsDirIn(frameworkDir), FrameworkAssemblyFileName);
    internal static string VersionFileIn(ModContentPack mcp) => Path.Combine(mcp.RootDir, "About", "Version.txt");

    internal static readonly Version InvalidVersion = new("0.0.0.0");
    
    public readonly ModContentPack ModContentPack;
    public readonly string FrameworkDir;
    public readonly Version Version;
    public readonly int SortOrderIdx;

    public string Name => ModContentPack.Name;
    public string PackageId => ModContentPack.ModMetaData.PackageIdNonUnique;
    
    public string VersionFile => VersionFileIn(ModContentPack);
    public string ManifestFile => ManifestFileIn(FrameworkDir);
    public string ComponentsDir => ComponentsDirIn(FrameworkDir);

    internal Manifest Manifest { get; set; }

    public LoadingState LoadingState { get; internal set; } = LoadingState.Pending;

    internal LunarMod(ModContentPack modContentPack, string frameworkDir, int sortOrderIdx)
    {
        ModContentPack = modContentPack;
        FrameworkDir = frameworkDir;
        SortOrderIdx = sortOrderIdx;
        Version = ReadModVersion();
    }

    internal bool IsModContentPackValid()
    {
        if (ModContentPack == null) return false;
        if (Manifest.PackageId != null && !ModContentPack.PackageId.EqualsIgnoreCase(Manifest.PackageId)) return false;
        return true;
    }
    
    private Version ReadModVersion()
    {
        try
        {
            return new Version(File.ReadAllText(VersionFile));
        }
        catch (Exception)
        {
            return InvalidVersion;
        }
    }
}