using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using LunarFramework.Internal;
using LunarFramework.Patching;
using LunarFramework.Utility;
using RimWorld;
using UnityEngine;
using Verse;

namespace LunarFramework.Bootstrap;

internal static class Entrypoint
{
    internal static readonly Dictionary<string, LunarMod> LunarMods = new();
    internal static readonly Dictionary<string, LunarComponent> LunarComponents = new();

    internal static readonly Dictionary<string, Assembly> AllModAssemblies = [];

    internal static event Action OnComponentAssembliesLoaded;

    internal static void RunBootstrap()
    {
        CacheAssemblyList();

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
            }
        }

        LoadComponents();

        foreach (var mod in LunarMods.Values.Where(m => m.LoadingState == LoadingState.Pending))
        {
            mod.LoadingState = LoadingState.Loaded;
        }
    }

    private static void CacheAssemblyList()
    {
        AllModAssemblies.Clear();

        foreach (var loadedAssembly in LoadedModManager.RunningModsListForReading.SelectMany(m => m.assemblies.loadedAssemblies))
        {
            AllModAssemblies.AddDistinct(loadedAssembly.GetName().Name, loadedAssembly);
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
            LunarMods[mod.ModContentPack.ModMetaData.PackageIdNonUnique] = mod;
        }
    }

    private static void PrepareMod(LunarMod mod)
    {
        if (!File.Exists(CheckFileFor(mod.ManifestFile)))
        {
            OnError(mod, "file is missing: " + CheckFileFor(mod.ManifestFile));
            return;
        }

        if (!CheckFile(mod.Version, mod.ManifestFile))
        {
            OnError(mod, "file is damaged or incomplete: " + mod.ManifestFile);
            return;
        }

        try
        {
            mod.Manifest = Manifest.ReadFromFile(mod.ManifestFile);
        }
        catch (Exception e)
        {
            OnError(mod, "an error occured while reading its manifest file.", true, e);
            return;
        }

        if (mod.Manifest.MinGameVersion != null)
        {
            var minVersion = ParseVersion(mod.Manifest.MinGameVersion);
            if (VersionControl.CurrentVersion < minVersion)
            {
                OnGameOutdated(mod, minVersion);
                return;
            }
        }

        if (mod.Manifest.Compatibility.Lunar != null)
        {
            foreach (var entry in mod.Manifest.Compatibility.Lunar)
            {
                var packageId = entry.PackageId.ToLower();
                var mcp = LoadedModManager.RunningMods.FirstOrDefault(m => m.ModMetaData.PackageIdNonUnique == packageId);
                if (mcp != null)
                {
                    var minVersion = ParseVersion(entry.MinVersion);
                    if (LunarMods.TryGetValue(packageId, out var otherMod))
                    {
                        if (otherMod.Version < minVersion)
                        {
                            OnConflict(mod, mcp, minVersion);
                            return;
                        }
                    }
                    else
                    {
                        OnConflict(mod, mcp, minVersion);
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
                var mcp = LoadedModManager.RunningMods.FirstOrDefault(m => m.ModMetaData.PackageIdNonUnique == packageId);
                if (mcp != null)
                {
                    OnConflict(mod, mcp, null);
                    return;
                }
            }
        }

        foreach (var componentDef in mod.Manifest.Components)
        {
            var assemblyFile = Path.Combine(mod.ComponentsDir, componentDef.AssemblyName + ".dll");
            if (!File.Exists(assemblyFile))
            {
                OnError(mod, "file is missing: " + assemblyFile);
                return;
            }

            if (!File.Exists(CheckFileFor(assemblyFile)))
            {
                OnError(mod, "file is missing: " + CheckFileFor(assemblyFile));
                return;
            }

            if (!CheckFile(mod.Version, assemblyFile))
            {
                OnError(mod, "file is damaged or incomplete: " + assemblyFile);
                return;
            }

            var assemblyVersion = GetAssemblyVersion(assemblyFile);
            if (assemblyVersion == LunarMod.InvalidVersion)
            {
                OnError(mod, "file has invalid version info: " + assemblyFile);
                return;
            }

            var aliases = componentDef.Aliases ?? [];
            var dependsOn = componentDef.DependsOn ?? [];

            var component = LunarComponents.TryGetValue(componentDef.AssemblyName);

            if (component == null)
            {
                component = new LunarComponent(componentDef.AssemblyName, LunarComponents.Count);
                component.AliasesInternal.AddRange(aliases);
                component.AllowNonLunarSource = componentDef.AllowNonLunarSource;
                LunarComponents[componentDef.AssemblyName] = component;
            }
            else
            {
                if (component.AllowNonLunarSource && !componentDef.AllowNonLunarSource)
                {
                    component.AllowNonLunarSource = false;
                    component.AliasesInternal.Clear();
                }

                if (component.AllowNonLunarSource)
                {
                    component.AliasesInternal.RemoveWhere(a => !aliases.Contains(a));
                }
                else if (!componentDef.AllowNonLunarSource)
                {
                    component.AliasesInternal.AddRange(aliases);
                }
            }

            component.DependsOnInternal.AddRange(dependsOn);
            component.ProvidedVersionsInternal[mod] = assemblyVersion;
        }
    }

    private static void LoadComponents()
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var component in LunarComponents.Values)
        {
            foreach (var dependsOn in component.DependsOn)
            {
                if (LunarComponents.TryGetValue(dependsOn, out var depComponent))
                {
                    depComponent.DependentsInternal.Add(component);
                }
            }
        }

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
                        OnError(component, "assembly '" + alias + "' is already loaded.");
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
                assembly = Assembly.LoadFrom(assemblyFile);
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

                if (e.LoaderExceptions != null)
                    foreach (var le in e.LoaderExceptions)
                        stringBuilder.AppendLine("   => " + le);

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

            OnComponentAssembliesLoaded?.Invoke();

            foreach (var modType in types)
            {
                if (modType.HasAttribute<LunarComponentEntrypoint>())
                {
                    try
                    {
                        RuntimeHelpers.RunClassConstructor(modType.TypeHandle);
                    }
                    catch (Exception e)
                    {
                        OnError(component, "error in component entrypoint '" + modType.FullName + "'", e);
                        break;
                    }
                }

                if (modType.IsSubclassOf(typeof(Mod)) && !modType.IsAbstract)
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
            }

            if (component.LoadingState == LoadingState.Pending)
            {
                component.LoadingState = LoadingState.Loaded;
            }
        }
    }

    /// <summary>
    /// Called when game startup is nearly finished. May be called multiple times:
    /// - Once directly after static constructors (only if BetterLoading is not present)
    /// - Once when PlayDataLoader is fully finished (for redundancy and BetterLoading support)
    /// - Possibly again when game data is reloaded (e.g. language change)
    /// </summary>
    internal static void OnPlayDataLoadFinished()
    {
        if (LunarRoot.Instance != null) return;

        try
        {
            CacheAssemblyList();

            LunarRoot.CreateInstance();
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

            #if RW_1_6_OR_GREATER

            try
            {
                HarmonyInliningFixer.Apply();
            }
            catch (Exception e)
            {
                LunarRoot.Logger.Fatal("Failed to refresh Harmony patches", e);
            }

            #endif
        }
        catch (Exception e)
        {
            LunarRoot.Logger.Fatal("Exception during framework initialization", e);
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
                LunarRoot.Logger.Error("Exception occured in cleanup action", ex);
            }
        }
    }

    private static void OnError(LunarMod mod, string error, bool askForRedownload = true, Exception exception = null)
    {
        mod.LoadingState = LoadingState.Errored;

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
        }

        foreach (var dependent in component.Dependents.Where(c => c.LoadingState != LoadingState.Errored))
        {
            OnError(dependent, error, exception);
        }
    }

    private static void OnConflict(LunarMod mod, ModContentPack other, Version minVersion)
    {
        mod.LoadingState = LoadingState.Errored;

        string message = minVersion != null
            ? "Failed to load mod '" + mod.ModContentPack.Name + "' " +
              "because it is incompatible with old versions of '" + other.Name + "'. " +
              "Update '" + other.Name + "' to version " + minVersion + " or newer to fix this problem."
            : "Failed to load mod '" + mod.ModContentPack.Name + "' " +
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

    private static void OnGameOutdated(LunarMod mod, Version minVersion)
    {
        OnError(mod, "it requires RimWorld version " + minVersion + " or later.", false);

        string popupMessage = $"Failed to load mod '{mod.ModContentPack?.Name}' because it " +
                              $"requires RimWorld version {minVersion} or newer. You currently have RimWorld " +
                              $"version {VersionControl.CurrentVersionString}, please update the game. \n\n" +
                              "If you are using Steam, make sure that there is no old beta channel selected. \n" +
                              "[ RimWorld -> Properties -> Betas -> Beta Participation -> set to None ]";

        LifecycleHooks.InternalInstance.DoOnceOnMainMenu(() =>
        {
            Find.WindowStack.Add(new Dialog_MessageBox(popupMessage));
        });
    }

    private static void ShowMessageAfterStartup(ModContentPack linkedMod, string message)
    {
        bool isSteamContent = IsSteamContent(linkedMod);
        string url = isSteamContent ? GetSteamUrl(linkedMod) : GetGitHubUrl(linkedMod);

        if (url == null) return;

        message += "\n\n";
        message += isSteamContent
            ? "If you are using Steam, simply unsubscribe from '" + linkedMod.Name + "', then restart Steam and resubscribe. " +
              "This will force Steam to redownload the mod files and update them to the latest version."
            : "You can download the latest version from the project's GitHub Releases page.";

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
        int index = file.LastIndexOf('.');
        return (index < 0 ? file : file.Substring(0, index)) + ".lfc";
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
