using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using LunarFramework.Internal;
using LunarFramework.Utility;
using RimWorld;
using UnityEngine;
using Verse;

namespace LunarFramework.Bootstrap;

internal static class Entrypoint
{
    internal static readonly Dictionary<string, LunarMod> LunarMods = new();
    internal static readonly Dictionary<string, LunarComponent> LunarComponents = new();

    internal static readonly List<string> CleanedUpOldAssemblies = new();

    internal static void RunBootstrap()
    {
        LunarRoot.Initialize();
        
        FindLunarMods();
        
        foreach (var mod in LunarMods.Values.OrderBy(m => m.SortOrderIdx))
        {
            try
            {
                PrepareMod(mod);
            }
            catch (Exception e)
            {
                OnError(mod, "an unknown error occured", false, e);
                mod.LoadingState = LoadingState.Errored;
            }
        }

        if (CleanedUpOldAssemblies.Count > 0)
        {
            Log.Warning("Successfully cleaned up old/obsolete mod files. The game will now restart for this to take effect.");
            GenCommandLine.Restart();
            return;
        }
        
        LoadComponents();

        foreach (var mod in LunarMods.Values.Where(m => m.LoadingState == LoadingState.Pending))
        {
            mod.LoadingState = LoadingState.Loaded;
        }
    }

    private static void FindLunarMods()
    {
        foreach
        (
            var mod in from mcp in LoadedModManager.RunningMods 
            let dir = mcp.foldersToLoadDescendingOrder
                .Select(LunarMod.FrameworkDirIn)
                .FirstOrDefault(Directory.Exists)
            where dir != null
            let assemblyFile = LunarMod.FrameworkAssemblyFileIn(dir) 
            where File.Exists(assemblyFile) 
            select new LunarMod(mcp, dir, LunarMods.Count)
        )
        {
            LunarMods[mod.ModContentPack.PackageId] = mod;
        }
    }

    private static void PrepareMod(LunarMod mod)
    {
        if (!File.Exists(CheckFileFor(mod.ManifestFile)))
        {
            OnError(mod, "file is missing: " + CheckFileFor(mod.ManifestFile));
            mod.LoadingState = LoadingState.Errored;
            return;
        }
        
        if (!CheckFile(mod.Version, mod.ManifestFile))
        {
            OnError(mod, "file is damaged or incomplete: " + mod.ManifestFile);
            mod.LoadingState = LoadingState.Errored;
            return;
        }
        
        try
        {
            mod.Manifest = Manifest.ReadFromFile(mod.ManifestFile);
        }
        catch (Exception e)
        {
            OnError(mod, "an error occured while reading its manifest file.", true, e);
            mod.LoadingState = LoadingState.Errored;
            return;
        }

        if (!mod.IsModContentPackValid())
        {
            OnError(mod, "its metadata is damaged or incomplete.");
            mod.LoadingState = LoadingState.Errored;
            return;
        }

        if (mod.Manifest.MinGameVersion != null)
        {
            var minVersion = ParseVersion(mod.Manifest.MinGameVersion);
            if (VersionControl.CurrentVersion < minVersion)
            {
                OnError(mod, "it requires RimWorld version " + minVersion + " or later.");
                mod.LoadingState = LoadingState.Errored;
                return;
            }
        }
        
        if (mod.Manifest.Compatibility.Lunar != null)
        {
            foreach (var entry in mod.Manifest.Compatibility.Lunar)
            {
                var packageId = entry.PackageId.ToLower();
                var mcp = LoadedModManager.RunningMods.FirstOrDefault(m => m.PackageId == packageId);
                if (mcp != null)
                {
                    var minVersion = ParseVersion(entry.MinVersion);
                    if (LunarMods.TryGetValue(packageId, out var otherMod))
                    {
                        if (otherMod.Version < minVersion)
                        {
                            OnConflict(mod, mcp, minVersion);
                            mod.LoadingState = LoadingState.Errored;
                            return;
                        }
                    }
                    else
                    {
                        OnConflict(mod, mcp, minVersion);
                        mod.LoadingState = LoadingState.Errored;
                        return;
                    }
                }
            }
        }
        
        if (mod.Manifest.Compatibility.Refuse != null)
        {
            foreach (var entry in mod.Manifest.Compatibility.Refuse)
            {
                var packageId = entry.PackageId.ToLower();
                var mcp = LoadedModManager.RunningMods.FirstOrDefault(m => m.PackageId == packageId);
                if (mcp != null)
                {
                    OnConflict(mod, mcp, null);
                    mod.LoadingState = LoadingState.Errored;
                    return;
                }
            }
        }

        if (!CleanUpOldAssemblies(mod))
        {
            OnError(mod, "its files are damaged or incomplete.");
            mod.LoadingState = LoadingState.Errored;
            return;
        }
        
        foreach (var componentDef in mod.Manifest.Components)
        {
            var assemblyFile = Path.Combine(mod.ComponentsDir, componentDef.AssemblyName + ".dll");
            if (!File.Exists(assemblyFile))
            {
                OnError(mod, "file is missing: " + assemblyFile);
                mod.LoadingState = LoadingState.Errored;
                return;
            }
            
            if (!File.Exists(CheckFileFor(assemblyFile)))
            {
                OnError(mod, "file is missing: " + CheckFileFor(assemblyFile));
                mod.LoadingState = LoadingState.Errored;
                return;
            }
        
            if (!CheckFile(mod.Version, assemblyFile))
            {
                OnError(mod, "file is damaged or incomplete: " + assemblyFile);
                mod.LoadingState = LoadingState.Errored;
                return;
            }
            
            var assemblyVersion = GetAssemblyVersion(assemblyFile);
            if (assemblyVersion == LunarMod.InvalidVersion)
            {
                OnError(mod, "file has invalid version info: " + assemblyFile);
                mod.LoadingState = LoadingState.Errored;
                return;
            }
            
            var component = LunarComponents.TryGetValue(componentDef.AssemblyName);
            component ??= new LunarComponent(componentDef.AssemblyName, LunarComponents.Count);
            component.ProvidedVersionsInternal[mod] = assemblyVersion;
            if (componentDef.Aliases != null) component.AliasesInternal.AddRange(componentDef.Aliases);
            if (!componentDef.AllowNonLunarSource) component.AllowNonLunarSource = false;
            LunarComponents[componentDef.AssemblyName] = component;
        }
    }

    private static bool CleanUpOldAssemblies(LunarMod mod)
    {
        const string loaderAssemblyFileName = "LunarLoader.dll";
        
        var oldAssmeblies = ModContentPack.GetAllFilesForModPreserveOrder(mod.ModContentPack, "Assemblies/", e => e.ToLower() == ".dll")
            .Select(f => f.Item2)
            .Where(f => !f.Name.Equals(loaderAssemblyFileName))
            .ToList();

        if (oldAssmeblies.Count == 0) return true;

        var backupDir = Path.Combine(mod.FrameworkDir, "Backup");
        
        if (Directory.Exists(backupDir)) return false;
        Directory.CreateDirectory(backupDir);
        
        Log.Warning("Found leftover files from an old version of mod: " + mod.PackageId);

        foreach (var oldAssmebly in oldAssmeblies)
        {
            var destFileName = Path.Combine(backupDir, oldAssmebly.Name);
            File.Move(oldAssmebly.FullName, destFileName);
            CleanedUpOldAssemblies.Add(oldAssmebly.FullName);
        }

        return true;
    }
    
    private static void LoadComponents()
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var component in LunarComponents.Values)
        {
            foreach (var alias in component.Aliases.Prepend(component.AssemblyName))
            {
                if (loadedAssemblies.Any(a => a.GetName().Name.EqualsIgnoreCase(alias)))
                {
                    if (component.AllowNonLunarSource)
                    {
                        component.LoadingState = LoadingState.Loaded;
                    }
                    else
                    {
                        OnError(component, "assembly '" + alias + "' is already loaded");
                        break;
                    }
                }
            }
        }

        var runningModClassesField = AccessTools.Field(typeof(LoadedModManager), "runningModClasses");
        if (runningModClassesField == null) throw new Exception("failed to find LoadedModManager#runningModClasses");

        var runningModClasses = (Dictionary<Type, Mod>) runningModClassesField.GetValue(null);
        if (runningModClasses == null) throw new Exception("failed to get runningModClasses");

        foreach (var component in LunarComponents.Values.OrderBy(m => m.SortOrderIdx))
        {
            if (component.LoadingState != LoadingState.Pending) continue;
            
            var provider = component.LatestVersionProvidedBy;
            if (provider == null) continue;
            
            var assemblyFile = Path.Combine(provider.ComponentsDir, component.AssemblyName + ".dll");
            
            Assembly assembly;
            Type[] types;

            try
            {
                byte[] rawAssembly = File.ReadAllBytes(assemblyFile);
                assembly = AppDomain.CurrentDomain.Load(rawAssembly);
            }
            catch (Exception e)
            {
                OnError(component, "failed to load assembly '" + component.AssemblyName + "'", e);
                continue;
            }
            
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                OnError(component, "failed to get types in assembly '" + component.AssemblyName + "'");
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("ReflectionTypeLoadException getting types in assembly " + assembly.GetName().Name + ": " + e);
                stringBuilder.AppendLine().AppendLine("Loader exceptions:");
                if (e.LoaderExceptions != null) foreach (var le in e.LoaderExceptions) stringBuilder.AppendLine("   => " + le);
                Log.Error(stringBuilder.ToString());
                continue;
            }
            catch (Exception e)
            {
                OnError(component, "failed to get types in assembly '" + component.AssemblyName + "'", e);
                continue;
            }

            component.LoadedAssembly = assembly;
            provider.ModContentPack.assemblies.loadedAssemblies.Add(assembly);
            GenTypes.ClearCache();
            
            foreach (var modType in types.Where(t => t.IsSubclassOf(typeof(Mod)) && !t.IsAbstract))
            {
                try
                {
                    if (!runningModClasses.ContainsKey(modType))
                    {
                        runningModClasses[modType] = (Mod) Activator.CreateInstance(modType, provider.ModContentPack);
                    }
                }
                catch (Exception e)
                {
                    OnError(component, "error in '" + modType.FullName + "'", e);
                    break;
                }
            }

            if (component.LoadingState == LoadingState.Pending)
            {
                component.LoadingState = LoadingState.Loaded;
            }
        }
    }
    
    internal static void OnPlayDataLoadFinished()
    {
        LunarRoot.BootstrapPatchGroup.UnsubscribeAll();

        foreach (var component in LunarComponents.Values.OrderBy(m => m.SortOrderIdx))
        {
            if (component.LoadingState != LoadingState.Loaded) continue;
            InitComponent(component);
        }
        
        foreach (var mod in LunarMods.Values.Where(m => m.LoadingState == LoadingState.Loaded))
        {
            mod.LoadingState = LoadingState.Initialized;
        }
    }

    private static void InitComponent(LunarComponent component)
    {
        try
        {
            component.InitAction?.Invoke();
            component.LoadingState = LoadingState.Initialized;
        }
        catch (Exception e)
        {
            OnError(component, "an unknown error occured during its initialization.", e);

            try
            {
                component.CleanupAction?.Invoke();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }
    }

    private static void OnError(LunarMod mod, string error, bool askForRedownload = true, Exception exception = null)
    {
        string logMessage = "Failed to load mod '" + mod.ModContentPack?.Name + "', " + error;
        
        string popupMessage = "Failed to load mod '" + mod.ModContentPack?.Name + "' " +
                              "because its files are damaged or incomplete. " +
                              "Redownload the mod to fix this problem.";

        if (askForRedownload)
        {
            LunarRoot.Logger.Warn(logMessage, exception);
            ShowMessageAfterStartup(mod.ModContentPack, popupMessage);
        }
        else
        {
            LunarRoot.Logger.Error(logMessage, exception);
        }
    }

    private static void OnError(LunarComponent component, string error, Exception exception = null)
    {
        component.LoadingState = LoadingState.Errored;
        foreach (var mod in component.ProvidingMods.Where(m => m.LoadingState != LoadingState.Errored))
        {
            OnError(mod, error, false, exception);
            mod.LoadingState = LoadingState.Errored;
        }
    }
    
    private static void OnConflict(LunarMod mod, ModContentPack other, Version minVersion)
    {
        string message = minVersion != null
            ? 
            "Failed to load mod '" + mod.ModContentPack.Name + "' " +
            "because it is incompatible with old versions of '" + other.Name + "'. " +
            "Update '" + other.Name + "' to version " + minVersion + " or newer to fix this problem."
            : 
            "Failed to load mod '" + mod.ModContentPack.Name + "' " +
            "because it is incompatible with '" + other.Name + "'.";

        if (minVersion != null)
        {
            LunarRoot.Logger.Warn(message);
            ShowMessageAfterStartup(other, message);
        }
        else
        {
            LunarRoot.Logger.Error(message);
        }
    }
    
    private static void ShowMessageAfterStartup(ModContentPack linkedMod, string message)
    {
        bool isSteamContent = IsSteamContent(linkedMod);
        string url = isSteamContent ? GetSteamUrl(linkedMod) : GetGitHubUrl(linkedMod);
        
        if (url == null) return;

        message += "\n\n";
        message += isSteamContent
            ? 
            "If you are using Steam, simply unsubscribe from '" + linkedMod.Name + "', then restart Steam and resubscribe. " + 
            "This will force Steam to redownload the mod files and update them to the latest version."
            : 
            "You can download the latest version of from the project's GitHub Releases page. " +
            "GitHub is the only official source for direct downloads, do not download it from any third-party websites.";

        void OpenModPageAction()
        {
            Application.OpenURL(url);
        }

        string openButtonText = isSteamContent ? "Open Workshop page" : "Open GitHub page";
        
        LifecycleHooks.InternalInstance.DoOnceOnMainMenu(() =>
        {
            Find.WindowStack.Add(new Dialog_MessageBox(message, openButtonText, OpenModPageAction, "Close"));
        });
    }

    internal static string CheckFileFor(string file)
    {
        int l; return ((l = file.LastIndexOf('.')) < 0 ? file : file.Substring(0, l)) + ".lfc";
    }
    
    internal static Version GetAssemblyVersion(string file)
    {
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(file);
            if (versionInfo.FileVersion == null) return LunarMod.InvalidVersion;
            return new Version(versionInfo.FileVersion);
        }
        catch (Exception)
        {
            return LunarMod.InvalidVersion;
        }
    }
    
    internal static Version ParseVersion(string versionString)
    {
        try
        {
            return new Version(versionString);
        }
        catch (Exception)
        {
            return LunarMod.InvalidVersion;
        }
    }
    
    internal static bool IsSteamContent(ModContentPack mcp)
    {
        return mcp.RootDir.Contains("workshop" + Path.PathSeparator + "content");
    }
    
    internal static string GetGitHubUrl(ModContentPack mcp)
    {
        var url = mcp.ModMetaData.Url;
        if (url == null || !url.StartsWith("https://github.com/")) return null;
        if (url.Contains("/releases")) return url;
        return url + "/releases";
    }
    
    internal static string GetSteamUrl(ModContentPack mcp)
    {
        return int.TryParse(mcp.FolderName, out var workshopId) ? "steam://url/CommunityFilePage/" + workshopId : null;
    }

    [SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
    internal static bool CheckFile(Version version, string file)
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
}