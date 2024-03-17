using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;

namespace HugsLogPublisher;

internal static class HugsLibUtility
{
    /// <summary>
    /// Returns true if the left or right Alt keys are currently pressed.
    /// </summary>
    public static bool AltIsHeld =>
        Input.GetKey(KeyCode.LeftAlt) ||
        Input.GetKey(KeyCode.RightAlt);

    /// <summary>
    /// Returns true if the left or right Control keys are currently pressed.
    /// Mac command keys are supported, as well.
    /// </summary>
    public static bool ControlIsHeld =>
        Input.GetKey(KeyCode.LeftControl) ||
        Input.GetKey(KeyCode.RightControl) ||
        Input.GetKey(KeyCode.LeftCommand) ||
        Input.GetKey(KeyCode.RightCommand);

    /// <summary>
    /// Copies a string to the system copy buffer and displays a confirmation message.
    /// </summary>
    public static void CopyToClipboard(string data)
    {
        GUIUtility.systemCopyBuffer = data;
        Messages.Message("HugsLogPublisher.copiedToClipboard".Translate(), MessageTypeDefOf.TaskCompletion);
    }

    /// <summary>
    /// Returns an enumerable as a string, joined by a separator string. By default null values appear as an empty string.
    /// </summary>
    /// <param name="list">A list of elements to string together</param>
    /// <param name="separator">A string to inset between elements</param>
    /// <param name="explicitNullValues">If true, null elements will appear as "[null]"</param>
    public static string Join(this IEnumerable list, string separator, bool explicitNullValues = false)
    {
        if (list == null) return "";
        var builder = new StringBuilder();
        var useSeparator = false;
        foreach (var elem in list)
        {
            if (useSeparator) builder.Append(separator);
            useSeparator = true;
            if (elem != null || explicitNullValues)
            {
                builder.Append(elem != null ? elem.ToString() : "[null]");
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Attempts to return the path of the log file Unity is writing to.
    /// </summary>
    /// <returns></returns>
    public static string TryGetLogFilePath()
    {
        if (TryGetCommandLineOptionValue("logfile") is { } cmdLog)
        {
            return cmdLog;
        }

        var platform = GetCurrentPlatform();
        return platform switch
        {
            PlatformType.Linux => @"/tmp/rimworld_log",
            PlatformType.MacOSX => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                $"Library/Logs/{Application.companyName}/{Application.productName}/Player.log"),
            PlatformType.Windows => Path.Combine(Application.persistentDataPath, "Player.log"),
            _ => null
        };
    }

    private static string TryGetCommandLineOptionValue(string key)
    {
        const string keyPrefix = "-";
        var prefixedKey = keyPrefix + key;
        var valueExpectedNext = false;
        foreach (var arg in Environment.GetCommandLineArgs())
        {
            if (arg.EqualsIgnoreCase(prefixedKey))
            {
                valueExpectedNext = true;
            }
            else if (valueExpectedNext && !arg.StartsWith(keyPrefix) && !arg.NullOrEmpty())
            {
                return arg;
            }
        }

        return null;
    }

    internal static string FullName(this MethodBase methodInfo)
    {
        if (methodInfo == null) return "[null reference]";
        if (methodInfo.DeclaringType == null) return methodInfo.Name;
        return methodInfo.DeclaringType.FullName + "." + methodInfo.Name;
    }

    internal static string ToSemanticString(this Version v, string nullFallback = "unknown")
    {
        if (v == null) return nullFallback;
        // System.Version parts: Major.Minor.Build.Revision
        return v.Build < 0
            ? $"{v.ToString(2)}.0"
            : v.ToString(v.Revision <= 0 ? 3 : 4);
    }

    /// <summary>
    /// Tries to find the file handle for a given mod assembly name.
    /// </summary>
    /// <remarks>This is a replacement for <see cref="Assembly.Location"/> mod assemblies are loaded from byte arrays.</remarks>
    /// <param name="assemblyName">The <see cref="AssemblyName.Name"/> of the assembly</param>
    /// <param name="contentPack">The content pack the assembly was presumably loaded from</param>
    /// <returns>Returns null if the file is not found</returns>
    public static FileInfo GetModAssemblyFileInfo(string assemblyName, [NotNull] ModContentPack contentPack)
    {
        if (contentPack == null) throw new ArgumentNullException(nameof(contentPack));
        const string assembliesFolderName = "Assemblies";
        const string lunarComponentsFolderName = "Lunar/Components";
        var expectedAssemblyFileName = $"{assemblyName}.dll";
        var modAssemblyFolderFiles = ModContentPack.GetAllFilesForMod(contentPack, assembliesFolderName);
        var fromAssemblyFolder = modAssemblyFolderFiles.Values.FirstOrDefault(f => f.Name == expectedAssemblyFileName);
        if (fromAssemblyFolder != null) return fromAssemblyFolder;
        var lunarComponentFolderFiles = ModContentPack.GetAllFilesForMod(contentPack, lunarComponentsFolderName);
        return lunarComponentFolderFiles.Values.FirstOrDefault(f => f.Name == expectedAssemblyFileName);
    }

    /// <summary>
    /// Same as <see cref="GetModAssemblyFileInfo"/> but suppresses all exceptions.
    /// </summary>
    public static FileInfo TryGetModAssemblyFileInfo(string assemblyName, ModContentPack modPack)
    {
        try
        {
            return GetModAssemblyFileInfo(assemblyName, modPack);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static PlatformType GetCurrentPlatform()
    {
        // Will need changing if another platform is supported by RimWorld in the future
        if (UnityData.platform is RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor)
            return PlatformType.MacOSX;
        if (UnityData.platform is RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor)
            return PlatformType.Windows;
        if (UnityData.platform == RuntimePlatform.LinuxPlayer)
            return PlatformType.Linux;
        return PlatformType.Unknown;
    }
}

public enum PlatformType
{
    Linux,
    MacOSX,
    Windows,
    Unknown
}
