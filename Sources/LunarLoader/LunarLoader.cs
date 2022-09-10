using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private const string FrameworkEntrypointMethodName = "RunBootstrap";
    
    private const string HarmonyAssemblyName = "0Harmony";
    private const string HarmonyAssemblyFileName = "HarmonyLib";
    
    private static string VersionFileIn(ModContentPack mcp) => Path.Combine(mcp.RootDir, "About", "Version.txt");
    private static string HarmonyAssemblyFileIn(string dir) => Path.Combine(dir, "Components", HarmonyAssemblyFileName + ".dll");
    private static string FrameworkAssemblyFileIn(string dir) => Path.Combine(dir, "Components", FrameworkAssemblyName + ".dll");
    private static string CheckFileFor(string file) { int l; return ((l = file.LastIndexOf('.')) < 0 ? file : file.Substring(0, l)) + ".lfc"; }

    private static readonly Version RequiredHarmonyVersion = new("2.2.2.0");
    private static readonly Version InvalidVersion = new("0.0.0.0");

    private static Version LoaderVersion => typeof(LunarLoader).Assembly.GetName().Version;
    
    private static string LogPrefix => "[Lunar v" + LoaderVersion + "] ";
    
    public LunarLoader(ModContentPack mcp) : base(mcp)
    {
        try
        {
            LoadFramework();
        }
        catch (Exception e)
        {
            LogError(mcp, "an error occured while loading the framework.");
            Debug.LogException(e);
        }
    }

    private static void LoadFramework()
    {
        Log.ResetMessageCount();

        var loadedFrameworkVersion = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == FrameworkAssemblyName);
        
        var loadedHarmonyAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == HarmonyAssemblyName);
        
        if (loadedFrameworkVersion != null)
        {
            Log.Warning(LogPrefix + "Framework v" + loadedFrameworkVersion + " is already loaded.");
            return;
        }

        var lunarMods = (
            from mcp in LoadedModManager.RunningMods
            let dir = mcp.foldersToLoadDescendingOrder
                .Select(lf => Path.Combine(lf, FrameworkDirName))
                .FirstOrDefault(Directory.Exists)
            where dir != null
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
        
        if (frameworkProvider.FrameworkVersion == InvalidVersion)
        {
            LogError(frameworkProvider.ModContentPack, "no valid framework found.");
            return;
        }
        
        if (frameworkProviderModVersion == InvalidVersion)
        {
            LogError(frameworkProvider.ModContentPack, "version info is invalid or missing.");
            return;
        }

        if (!File.Exists(frameworkAssemblyCheckFile))
        {
            LogError(frameworkProvider.ModContentPack, "file is missing: " + frameworkAssemblyCheckFile);
            return;
        }
        
        if (!CheckFile(frameworkProviderModVersion, frameworkAssemblyFile))
        {
            LogError(frameworkProvider.ModContentPack, "file is damaged or incomplete: " + frameworkAssemblyFile);
            return;
        }

        if (loadedHarmonyAssembly != null)
        {
            var harmonyType = loadedHarmonyAssembly.GetType("HarmonyLib.Harmony");
            if (harmonyType == null || harmonyType.Assembly.GetName().Version < RequiredHarmonyVersion)
            {
                LogError(frameworkProvider.ModContentPack, "Harmony (version " + RequiredHarmonyVersion + " or later) is required!");
                return;
            }
        }
        else
        {
            var harmonyAssemblyFile = HarmonyAssemblyFileIn(frameworkProvider.FrameworkDir);
            var harmonyAssemblyCheckFile = CheckFileFor(harmonyAssemblyFile);
            
            if (!File.Exists(harmonyAssemblyFile))
            {
                LogError(frameworkProvider.ModContentPack, "file is missing: " + harmonyAssemblyFile);
                return;
            }
            
            if (!File.Exists(harmonyAssemblyCheckFile))
            {
                LogError(frameworkProvider.ModContentPack, "file is missing: " + harmonyAssemblyCheckFile);
                return;
            }
        
            if (!CheckFile(frameworkProviderModVersion, harmonyAssemblyFile))
            {
                LogError(frameworkProvider.ModContentPack, "file is damaged or incomplete: " + harmonyAssemblyFile);
                return;
            }
            
            byte[] rawHarmonyAssembly = File.ReadAllBytes(harmonyAssemblyFile);
            AppDomain.CurrentDomain.Load(rawHarmonyAssembly);
        }

        byte[] rawAssembly = File.ReadAllBytes(frameworkAssemblyFile);
        var loadedAssembly = AppDomain.CurrentDomain.Load(rawAssembly);
        
        frameworkProvider.ModContentPack.assemblies.loadedAssemblies.Add(loadedAssembly);
        GenTypes.ClearCache();

        var entrypoint = loadedAssembly.GetType(FrameworkEntrypointClass);
        var bootstrapMethod = entrypoint?.GetMethod(FrameworkEntrypointMethodName, BindingFlags.NonPublic | BindingFlags.Static);
        
        if (bootstrapMethod == null)
        {
            LogError(frameworkProvider.ModContentPack, $"framework v{frameworkProvider.FrameworkVersion} entrypoint is missing.");
            return;
        }
        
        bootstrapMethod.Invoke(null, null);
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
    
    [SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
    private static bool CheckFile(Version version, string file)
    {
        try
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(file);
            
            byte[] aBytes = md5.ComputeHash(stream);
            byte[] vBytes = md5.ComputeHash(Encoding.ASCII.GetBytes(version.ToString()));
            byte[] fBytes = File.ReadAllBytes(CheckFileFor(file));
            
            if (aBytes.Length != fBytes.Length) return false;
            for (var i = 0; i < aBytes.Length; i++)
            {
                var b = aBytes[i];
                b ^= vBytes[i];
                if (b != fBytes[i]) return false;
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void LogError(ModContentPack frameworkProvider, string error)
    {
        Log.Error(LogPrefix + "Failed to load mod '" + frameworkProvider.Name + "', " + error);
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