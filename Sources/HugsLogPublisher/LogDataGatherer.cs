using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace HugsLogPublisher;

internal static class LogDataGatherer
{
    private const int MaxLogLineCount = 20000;
    private const int MaxLogCharCount = 1500000; // 1.5 MB

    internal static string PrepareLogData(LogPublisherOptions options)
    {
        try
        {
            var logfile = GetLogFileContents();
            logfile = NormalizeLineEndings(logfile);
            // redact logs for privacy
            logfile = RedactRimworldPaths(logfile);
            logfile = RedactPlayerConnectInformation(logfile);
            if (!options.IncludePlatformInfo)
                logfile = RedactRendererInformation(logfile);
            logfile = RedactHomeDirectoryPaths(logfile);
            logfile = RedactSteamId(logfile);
            logfile = RedactUselessLines(logfile);
            logfile = ConsolidateRepeatedLines(logfile);
            var combined = string.Concat(MakeLogTimestamp(),
                ListActiveMods(), "\n",
                ListHarmonyPatches(), "\n",
                ListPlatformInfo(options.IncludePlatformInfo), "\n",
                logfile);
            combined = TrimExcessLines(combined);
            return combined;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        return null;
    }

    private static string NormalizeLineEndings(string log)
    {
        return log.Replace("\r\n", "\n");
    }

    internal static string ConsolidateRepeatedLines(string log)
    {
        const int searchRange = 40;
        const int minRepetitions = 2;
        const int minLength = 10;

        var lines = log.Split('\n');

        var result = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            result.Append(line).Append('\n');

            for (int o = 1; o < searchRange && i + 2 * o <= lines.Length; o++)
            {
                bool match;

                int r = 0;
                int j = i;

                do
                {
                    match = lines[j] == lines[j + o];

                    if (match)
                    {
                        for (int k = 1; k < o; k++)
                        {
                            if (lines[j + k] != lines[j + o + k])
                            {
                                match = false;
                                break;
                            }
                        }

                        if (match)
                        {
                            j += o;
                            r++;
                        }
                    }
                }
                while (match && j + 2 * o <= lines.Length);

                if (r >= minRepetitions && (r + 1) * o >= minLength)
                {
                    for (int k = 1; k < o - 1; k++)
                    {
                        result.Append(lines[i + k]).Append('\n');
                    }

                    var n = lines[i].Length == 0 ? o - 1 : o;

                    if (lines[i + o - 1].Length != 0)
                    {
                        result.Append(lines[i + o - 1]).Append('\n');
                    }
                    else
                    {
                        n--;
                    }

                    if (n == 1)
                        result.Append($"########## The preceding line was repeated {r} times ##########").Append('\n');
                    else if (n > 1)
                        result.Append($"########## The preceding {n} lines were repeated {r} times ##########").Append('\n');

                    i += o * r + (o - 1);

                    if (n >= 1 && i + 1 < lines.Length && lines[i + 1].Length != 0)
                    {
                        result.Append('\n');
                    }

                    break;
                }
            }
        }

        return result.ToString();
    }

    private static string TrimExcessLines(string log)
    {
        var indexOfLastNewline = IndexOfOccurence(log, '\n', MaxLogLineCount);
        if (indexOfLastNewline >= 0)
        {
            log = $"{log.Substring(0, indexOfLastNewline + 1)}\n(log trimmed to {MaxLogLineCount:N0} lines)";
        }

        if (log.Length > MaxLogCharCount)
        {
            log = $"{log.Substring(0, MaxLogLineCount)}\n(log trimmed to {MaxLogCharCount:N0} characters)";
        }

        return log;
    }

    private static int IndexOfOccurence(string s, char match, int occurence)
    {
        int currentOccurence = 1;
        int currentIndex = 0;
        while (currentOccurence <= occurence && (currentIndex = s.IndexOf(match, currentIndex + 1)) != -1)
        {
            if (currentOccurence == occurence) return currentIndex;
            currentOccurence++;
        }

        return -1;
    }

    internal static string RedactUselessLines(string log)
    {
        log = Regex.Replace(log, "Non platform assembly:.+\n", "");
        log = Regex.Replace(log, "Platform assembly: .+\n", "");
        log = Regex.Replace(log, "Fallback handler could not load library.+\n", "");
        log = Regex.Replace(log, "- Completed reload, in [\\d\\. ]+ seconds\n", "");
        log = Regex.Replace(log, "UnloadTime: [\\d\\. ]+ ms\n", "");
        log = Regex.Replace(log, "<RI> Initializing input\\.\r\n", "");
        log = Regex.Replace(log, "<RI> Input initialized\\.\r\n", "");
        log = Regex.Replace(log, "<RI> Initialized touch support\\.\r\n", "");
        log = Regex.Replace(log, "\\(Filename: .+Line: .+\\)\n", "");
        log = Regex.Replace(log, "Curl error .+: Failed to connect to .+\n", "");
        log = Regex.Replace(log, "\n \n", "\n");
        return log;
    }

    // only relevant on linux
    private static string RedactSteamId(string log)
    {
        const string idReplacement = "[Steam Id redacted]";
        return Regex.Replace(log, "Steam_SetMinidumpSteamID.+", idReplacement);
    }

    private static string RedactHomeDirectoryPaths(string log)
    {
        const string pathReplacement = "[Home_dir]";
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Regex.Replace(log, Regex.Escape(homePath), pathReplacement, RegexOptions.IgnoreCase);
    }


    private static string RedactRimworldPaths(string log)
    {
        const string pathReplacement = "[Rimworld_dir]";
        // easiest way to get the game folder is one level up from dataPath
        var appPath = Path.GetFullPath(Application.dataPath);
        var pathParts = appPath.Split(Path.DirectorySeparatorChar).ToList();
        pathParts.RemoveAt(pathParts.Count - 1);
        appPath = pathParts.Join(Path.DirectorySeparatorChar.ToString());
        log = log.Replace(appPath, pathReplacement);
        if (Path.DirectorySeparatorChar != '/')
        {
            // log will contain mixed windows and unix style paths
            appPath = appPath.Replace(Path.DirectorySeparatorChar, '/');
            log = log.Replace(appPath, pathReplacement);
        }

        return log;
    }

    private static string RedactRendererInformation(string log)
    {
        // apparently renderer information can appear multiple times in the log
        for (int i = 0; i < 5; i++)
        {
            var redacted = RedactString(log, "GfxDevice: ", "\nBegin MonoManager", "[Renderer information redacted]");
            if (log.Length == redacted.Length) break;
            log = redacted;
        }

        return log;
    }

    private static string RedactPlayerConnectInformation(string log)
    {
        return RedactString(log, "PlayerConnection ", "Initialize engine", "[PlayerConnect information redacted]\n");
    }

    private static string GetLogFileContents()
    {
        var filePath = HugsLibUtility.TryGetLogFilePath();
        if (filePath.NullOrEmpty() || !File.Exists(filePath))
        {
            throw new FileNotFoundException("Log file not found:" + filePath);
        }

        var tempPath = Path.GetTempFileName();
        File.Delete(tempPath);
        // we need to copy the log file since the original is already opened for writing by Unity
        File.Copy(filePath, tempPath);
        var fileContents = File.ReadAllText(tempPath);
        File.Delete(tempPath);
        return "Log file contents:\n" + fileContents;
    }

    private static string MakeLogTimestamp()
    {
        var utc = DateTime.Now.ToUniversalTime();
        return $"Log uploaded on {utc.ToLongDateString()}, {utc.ToLongTimeString()} UTC\n";
    }

    private static string RedactString(string original, string redactStart, string redactEnd, string replacement)
    {
        var startIndex = original.IndexOf(redactStart, StringComparison.Ordinal);
        var endIndex = original.IndexOf(redactEnd, StringComparison.Ordinal);

        string result = original;

        if (startIndex >= 0 && endIndex >= 0)
        {
            var logTail = original.Substring(endIndex);
            result = original.Substring(0, startIndex + redactStart.Length);
            result += replacement;
            result += logTail;
        }

        return result;
    }

    private static string ListHarmonyPatches()
    {
        var patchListing = HarmonyUtility.DescribeAllPatchedMethods();

        return string.Concat("Active Harmony patches:\n",
            patchListing,
            patchListing.EndsWith("\n") ? "" : "\n",
            HarmonyUtility.DescribeHarmonyVersions(), "\n");
    }

    private static string ListPlatformInfo(bool include)
    {
        const string sectionTitle = "Platform information: ";

        if (include)
        {
            return string.Concat(sectionTitle, "\nCPU: ",
                SystemInfo.processorType,
                "\nOS: ",
                SystemInfo.operatingSystem,
                "\nMemory: ",
                SystemInfo.systemMemorySize,
                " MB",
                "\n");
        }

        return sectionTitle + "(hidden, use publishing options to include)\n";
    }

    private static string ListActiveMods()
    {
        var builder = new StringBuilder();
        builder.Append("Loaded mods:\n");
        foreach (var modContentPack in LoadedModManager.RunningMods)
        {
            builder.AppendFormat("{0}({1})", modContentPack.Name, modContentPack.PackageIdPlayerFacing);
            builder.Append(": ");
            var firstAssembly = true;
            var anyAssemblies = false;
            foreach (var loadedAssembly in modContentPack.assemblies.loadedAssemblies)
            {
                if (!firstAssembly)
                {
                    builder.Append(", ");
                }

                firstAssembly = false;
                builder.Append(loadedAssembly.GetName().Name);
                builder.AppendFormat("({0})", AssemblyVersionInfo.ReadModAssembly(loadedAssembly, modContentPack));
                anyAssemblies = true;
            }

            if (!anyAssemblies)
            {
                builder.Append("(no assemblies)");
            }

            builder.Append("\n");
        }

        return builder.ToString();
    }
}
