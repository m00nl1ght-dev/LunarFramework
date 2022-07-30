using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Verse;
using Debug = UnityEngine.Debug;

namespace LunarDevTools;

public class LunarDevTools : Mod
{
    private const string FrameworkDir = "Lunar";
    private const string FrameworkAssemblyName = "LunarFramework";
    
    private static string FrameworkDirIn(ModContentPack mcp) => Path.Combine(mcp.RootDir, FrameworkDir);
    private static string ComponentDirIn(ModContentPack mcp) => Path.Combine(FrameworkDirIn(mcp), "Components");
    private static string FrameworkAssemblyFileIn(ModContentPack mcp) => Path.Combine(FrameworkDirIn(mcp), FrameworkAssemblyName + ".dll");
    private static string CheckFileFor(string file) { int l; return ((l = file.LastIndexOf('.')) == -1 ? file : file.Substring(0, l)) + ".lfc"; }

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
            Debug.LogError(LogPrefix + "Failed to create check files.");
            Debug.LogException(e);
        }
    }

    private static void CreateCheckFiles()
    {
        var lunarMods = LoadedModManager.RunningMods.Where(IsLunarMod).ToList();
        if (lunarMods.Count == 0) return;
        
        foreach (var lunarMod in lunarMods)
        {
            var frameworkAssemblyFile = FrameworkAssemblyFileIn(lunarMod);
            CreateCheckFile(frameworkAssemblyFile);

            var componentDir = new DirectoryInfo(ComponentDirIn(lunarMod));
            foreach (var file in componentDir.GetFiles("*.dll"))
            {
                CreateCheckFile(file.FullName);
            }
        }
    }

    private static void CreateCheckFile(string file)
    {
        var checkFile = CheckFileFor(file);
        if (CheckFilesCreated.Contains(checkFile)) throw new Exception("already created: " + checkFile);
        using var stream = File.OpenRead(file);
        byte[] aBytes = MD5.Create().ComputeHash(stream);
        for (var i = 0; i < aBytes.Length; i++) aBytes[i] ^= 0b01010011;
        File.WriteAllBytes(checkFile, aBytes);
        CheckFilesCreated.Add(checkFile);
    }

    private static bool IsLunarMod(ModContentPack mcp)
    {
        return File.Exists(FrameworkAssemblyFileIn(mcp));
    }
}