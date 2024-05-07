using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Verse;

namespace LunarDevTools;

public class LunarDevTools : Mod
{
    private const string FrameworkDirName = "Lunar";
    private const string FrameworkAssemblyName = "LunarFramework";
    private const string HarmonyAssemblyFileName = "HarmonyLib";

    private static string VersionFileIn(ModContentPack mcp) => Path.Combine(mcp.RootDir, "About", "Version.txt");
    private static string ComponentsDirIn(string frameworkDir) => Path.Combine(frameworkDir, "Components");
    private static string ManifestFileIn(string frameworkDir) => Path.Combine(frameworkDir, "Manifest.xml");
    private static string FrameworkAssemblyFileIn(string frameworkDir) => Path.Combine(ComponentsDirIn(frameworkDir), FrameworkAssemblyName + ".dll");
    private static string HarmonyAssemblyFileIn(string frameworkDir) => Path.Combine(ComponentsDirIn(frameworkDir), HarmonyAssemblyFileName + ".dll");

    private static readonly Version InvalidVersion = new("0.0.0.0");

    private static Version DevToolsVersion => Assembly.GetExecutingAssembly().GetName().Version;
    private static string LogPrefix => "[LunarDevTools v" + DevToolsVersion + "] ";

    private static readonly HashSet<string> CheckFilesCreated = new();

    public LunarDevTools(ModContentPack content) : base(content)
    {
        try
        {
            CreateCheckFiles();
        }
        catch (Exception e)
        {
            Log.Error(LogPrefix + "Failed to create check files\n" + e);
        }
    }

    private static void CreateCheckFiles()
    {
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

        if (lunarMods.Count == 0) return;

        foreach (var lunarMod in lunarMods)
        {
            var modVersion = GetModVersion(lunarMod.ModContentPack);

            if (modVersion == InvalidVersion)
            {
                Log.Error(LogPrefix + "Mod is missing version info: " + lunarMod.ModContentPack.PackageId);
                continue;
            }

            CreateCheckFile(modVersion, ManifestFileIn(lunarMod.FrameworkDir));

            var componentDir = new DirectoryInfo(ComponentsDirIn(lunarMod.FrameworkDir));
            foreach (var file in componentDir.GetFiles("*.dll"))
            {
                CreateCheckFile(modVersion, file.FullName);
            }
        }
    }

    private static void CreateCheckFile(Version modVersion, string file)
    {
        using var md5 = MD5.Create();
        var checkFile = CheckFileFor(file);
        if (CheckFilesCreated.Contains(checkFile)) throw new Exception("already created: " + checkFile);
        using var stream = File.OpenRead(file);
        byte[] aBytes = md5.ComputeHash(stream);
        byte[] vBytes = md5.ComputeHash(Encoding.ASCII.GetBytes(modVersion.ToString()));
        for (var i = 0; i < aBytes.Length; i++) aBytes[i] ^= vBytes[i];
        File.WriteAllBytes(checkFile, aBytes);
        CheckFilesCreated.Add(checkFile);
    }

    private static string CheckFileFor(string file)
    {
        int index = file.LastIndexOf('.');
        return (index < 0 ? file : file.Substring(0, index)) + ".lfc";
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
