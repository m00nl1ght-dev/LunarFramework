using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Verse;
using Debug = UnityEngine.Debug;

namespace LunarLoader;

public class LunarLoader : Mod
{
    private const string FrameworkDirName = "Lunar";
    private const string FrameworkAssemblyName = "LunarFramework";
    private const string FrameworkEntrypointClass = "LunarFramework.Bootstrap.Entrypoint";
    
    private static string VersionFileIn(ModContentPack mcp) => Path.Combine(mcp.RootDir, "About", "Version.txt");
    private static string FrameworkAssemblyFileIn(string dir) => Path.Combine(dir, FrameworkAssemblyName + ".dll");
    private static string CheckFileFor(string file) { int l; return ((l = file.LastIndexOf('.')) < 0 ? file : file.Substring(0, l)) + ".lfc"; }

    private static readonly Version RequiredHarmonyVersion = new("2.2.2.0");
    private static readonly Version InvalidVersion = new("0.0.0.0");

    private static Version LoaderVersion => typeof(LunarLoader).Assembly.GetName().Version;
    
    private static string LogPrefix => "[Lunar v" + LoaderVersion + "] ";
    
    public LunarLoader(ModContentPack content) : base(content)
    {
        try
        {
            LoadFramework();
        }
        catch (Exception e)
        {
            Log.Error(LogPrefix + "Failed to load mod, an error occured while loading framework for: " + content?.PackageId);
            Debug.LogException(e);
        }
    }

    private static void LoadFramework()
    {
        Log.ResetMessageCount();
        
        var existing = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == FrameworkAssemblyName);
        
        if (existing != null)
        {
            Debug.LogWarning(LogPrefix + "Framework is already loaded.");
            return;
        }
        
        var harmonyType = Type.GetType("HarmonyLib.Harmony", false);
        if (harmonyType == null || harmonyType.Assembly.GetName().Version < RequiredHarmonyVersion)
        {
            Log.Error(LogPrefix + "Failed to load, Harmony (version " + RequiredHarmonyVersion + " or later) is required!");
            return;
        }

        var lunarMods = (
            from mcp in LoadedModManager.RunningMods 
            let dir = mcp.foldersToLoadDescendingOrder.FirstOrDefault(dir => Directory.Exists(Path.Combine(dir, FrameworkDirName))) 
            let assemblyFile = FrameworkAssemblyFileIn(dir) 
            where File.Exists(assemblyFile) 
            let frameworkVersion = GetAssemblyVersion(assemblyFile) 
            select new ModInfo(mcp, dir, frameworkVersion))
            .ToList();

        if (lunarMods.Count == 0) return; // no need to load framework if no mods use it

        var frameworkProvider = lunarMods.OrderByDescending(m => m.FrameworkVersion).First();
        var frameworkAssemblyFile = FrameworkAssemblyFileIn(frameworkProvider.FrameworkDir);
        var frameworkAssemblyCheckFile = CheckFileFor(frameworkAssemblyFile);
        var frameworkProviderModVersion = GetModVersion(frameworkProvider.ModContentPack);
        var frameworkProviderPackageId = frameworkProvider.ModContentPack.PackageId;
        
        if (frameworkProvider.FrameworkVersion == InvalidVersion)
        {
            Log.Error(LogPrefix + "Failed to load mod, no valid framework found in: " + frameworkProviderPackageId);
            return;
        }
        
        if (frameworkProviderModVersion == InvalidVersion)
        {
            Log.Error(LogPrefix + "Failed to load mod, version info is invalid: " + frameworkProviderPackageId);
            return;
        }

        if (!File.Exists(frameworkAssemblyCheckFile))
        {
            Log.Error(LogPrefix + "Failed to load mod, file is missing: " + frameworkAssemblyCheckFile);
            return;
        }
        
        bool check = false;
        try
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(frameworkAssemblyFile);
            byte[] aBytes = md5.ComputeHash(stream);
            byte[] vBytes = md5.ComputeHash(Encoding.ASCII.GetBytes(frameworkProviderModVersion.ToString()));
            byte[] fBytes = File.ReadAllBytes(frameworkAssemblyCheckFile);
            if (aBytes.Length == fBytes.Length) check = true;
            if (check) for (var i = 0; i < aBytes.Length; i++)
            {
                var b = aBytes[i];
                b ^= vBytes[i];
                if (b != fBytes[i]) check = false;
            }
        }
        catch (Exception)
        {
            check = false;
        }

        if (!check)
        {
            Debug.LogError(LogPrefix + "Failed to load mod, file is damaged or incomplete: " + frameworkAssemblyFile);
            return;
        }
        
        byte[] rawAssembly = File.ReadAllBytes(frameworkAssemblyFile);
        var loadedAssembly = AppDomain.CurrentDomain.Load(rawAssembly);
        
        frameworkProvider.ModContentPack.assemblies.loadedAssemblies.Add(loadedAssembly);
        GenTypes.ClearCache();

        var entrypoint = loadedAssembly.GetType(FrameworkEntrypointClass);
        if (entrypoint == null)
        {
            Debug.LogError(LogPrefix + $"Failed to load mod, framework v{frameworkProvider.FrameworkVersion} entrypoint is missing in: " + frameworkProviderPackageId);
            return;
        }
        
        RuntimeHelpers.RunClassConstructor(entrypoint.TypeHandle);
    }

    private static Version GetModVersion(ModContentPack mcp)
    {
        try
        {
            var versionText = File.ReadAllText(VersionFileIn(mcp));
            return new Version(versionText);
        }
        catch (Exception)
        {
            return InvalidVersion;
        }
    }
    
    private static Version GetAssemblyVersion(string file)
    {
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(file);
            if (versionInfo.FileVersion == null) return InvalidVersion;
            return new Version(versionInfo.FileVersion);
        }
        catch (Exception)
        {
            return InvalidVersion;
        }
    }

    private readonly struct ModInfo
    {
        public readonly ModContentPack ModContentPack;
        public readonly string FrameworkDir;
        public readonly Version FrameworkVersion;

        public ModInfo(ModContentPack modContentPack, string frameworkDir, Version frameworkVersion)
        {
            ModContentPack = modContentPack;
            FrameworkDir = frameworkDir;
            FrameworkVersion = frameworkVersion;
        }
    }
}