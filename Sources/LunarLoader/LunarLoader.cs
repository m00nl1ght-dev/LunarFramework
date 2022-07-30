using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Verse;
using Debug = UnityEngine.Debug;

namespace LunarLoader;

public class LunarLoader : Mod
{
    private const string FrameworkDir = "Lunar";
    private const string FrameworkAssemblyName = "LunarFramework";
    private const string FrameworkEntrypointClass = "LunarFramework.LunarEntrypoint";
    
    private static string FrameworkDirIn(ModContentPack mcp) => Path.Combine(mcp.RootDir, FrameworkDir);
    private static string FrameworkAssemblyFileIn(ModContentPack mcp) => Path.Combine(FrameworkDirIn(mcp), FrameworkAssemblyName + ".dll");
    private static string CheckFileFor(string file) { int l; return ((l = file.LastIndexOf('.')) == -1 ? file : file.Substring(0, l)) + ".lfc"; }

    private static readonly Version InvalidVersion = new("0.0.0.0");

    private static Version LoaderVersion => Assembly.GetExecutingAssembly().GetName().Version;
    private static string LogPrefix => "[LunarLoader v" + LoaderVersion + "] ";
    
    public LunarLoader(ModContentPack content) : base(content)
    {
        try
        {
            LoadFramework();
        }
        catch (Exception e)
        {
            Debug.LogError(LogPrefix + "Failed to load LunarFramework.");
            Debug.LogException(e);
        }
    }

    private static void LoadFramework()
    {
        var existing = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == FrameworkAssemblyName);
        
        if (existing != null)
        {
            Debug.LogWarning(LogPrefix + "Framework is already loaded.");
            return;
        }

        var lunarMods = LoadedModManager.RunningMods.Where(IsLunarMod).ToList();
        if (lunarMods.Count == 0) return; // no need to load framework if no mods use it

        var hasLatestFrameworkAssembly = lunarMods.OrderByDescending(GetFrameworkVersion).First();
        var latestFrameworkAssemblyFile = FrameworkAssemblyFileIn(hasLatestFrameworkAssembly);
        var latestFrameworkAssemblyCheckFile = CheckFileFor(latestFrameworkAssemblyFile);
        var latestFrameworkVersion = GetFrameworkVersion(hasLatestFrameworkAssembly);
        
        if (latestFrameworkVersion == InvalidVersion)
        {
            Debug.LogWarning(LogPrefix + "No valid framework assembly found.");
            return;
        }

        if (!File.Exists(latestFrameworkAssemblyCheckFile))
        {
            Debug.LogError(LogPrefix + "File is missing: " + latestFrameworkAssemblyCheckFile);
            return;
        }
        
        bool check = false;
        using (var stream = File.OpenRead(latestFrameworkAssemblyFile))
        {
            byte[] aBytes = MD5.Create().ComputeHash(stream);
            byte[] fBytes = File.ReadAllBytes(latestFrameworkAssemblyCheckFile);
            if (aBytes.Length == fBytes.Length) check = true;
            if (check) for (var i = 0; i < aBytes.Length; i++)
            {
                var aByte = aBytes[i];
                aByte ^= 0b01010011;
                if (aByte != fBytes[i]) check = false;
            }
        }

        if (!check)
        {
            Debug.LogError(LogPrefix + "File is currupted: " + latestFrameworkAssemblyFile);
            return;
        }
        
        byte[] rawAssembly = File.ReadAllBytes(latestFrameworkAssemblyFile);
        var loadedAssembly = AppDomain.CurrentDomain.Load(rawAssembly);
        
        hasLatestFrameworkAssembly.assemblies.loadedAssemblies.Add(loadedAssembly);
        GenTypes.ClearCache();

        var entrypoint = loadedAssembly.GetType(FrameworkEntrypointClass);
        if (entrypoint == null)
        {
            Debug.LogError(LogPrefix + "Framework entrypoint is missing.");
            return;
        }
        
        RuntimeHelpers.RunClassConstructor(entrypoint.TypeHandle);
    }

    private static bool IsLunarMod(ModContentPack mcp)
    {
        return File.Exists(FrameworkAssemblyFileIn(mcp));
    }
    
    private static Version GetFrameworkVersion(ModContentPack mcp)
    {
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(FrameworkAssemblyFileIn(mcp));
            if (versionInfo.FileVersion == null) return InvalidVersion;
            return new Version(versionInfo.FileVersion);
        }
        catch (Exception)
        {
            return InvalidVersion;
        }
    }
}