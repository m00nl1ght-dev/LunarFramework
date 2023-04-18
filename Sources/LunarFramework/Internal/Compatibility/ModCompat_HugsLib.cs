using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using LunarFramework.Patching;
using Verse;

namespace LunarFramework.Internal.Compatibility;

[HarmonyPatch]
internal class ModCompat_HugsLib : ModCompat
{
    public override string TargetAssemblyName => "HugsLib";
    public override string DisplayName => "HugsLib";

    protected override bool OnApply()
    {
        try
        {
            var methodObs = AccessTools.Method(typeof(Game), "DeinitAndRemoveMap", new[] { typeof(Map) });
            var methodNew = AccessTools.Method(typeof(Game), "DeinitAndRemoveMap_NewTemp", new[] { typeof(Map), typeof(bool) });

            if (methodObs != null && methodNew != null)
            {
                var patches = Harmony.GetPatchInfo(methodObs);
                var postfix = patches.Postfixes.FirstOrDefault(p => p.owner == "UnlimitedHugs.HugsLib");

                if (postfix != null)
                {
                    var harmony = new Harmony(postfix.owner);
                    harmony.Patch(methodNew, null, new HarmonyMethod(postfix.PatchMethod));
                    harmony.Unpatch(methodObs, postfix.PatchMethod);
                }
            }
        }
        catch (Exception e)
        {
            LunarRoot.Logger.Debug("Failed to fix HugsLib obsolete method patch.", e);
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch("HugsLib.Utils.HugsLibUtility", "GetModAssemblyFileInfo")]
    private static void HugsLibUtility_GetModAssemblyFileInfo(string assemblyName, ModContentPack contentPack, ref FileInfo __result)
    {
        if (__result != null) return;
        const string lunarComponentsFolderName = "Lunar/Components";
        var expectedAssemblyFileName = $"{assemblyName}.dll";
        var lunarComponentFolderFiles = ModContentPack.GetAllFilesForMod(contentPack, lunarComponentsFolderName);
        __result = lunarComponentFolderFiles.Values.FirstOrDefault(f => f.Name == expectedAssemblyFileName);
    }

    private static readonly Regex _uploadResponseUrlMatch = new("\"html_url\":\"(https://gist\\.github\\.com/[\\w/]+)\"");

    [HarmonyPrefix]
    [HarmonyPatch("HugsLib.Logs.LogPublisher", "TryExtractGistUrlFromUploadResponse")]
    private static bool TryExtractGistUrlFromUploadResponse(string response, ref string __result)
    {
        var match = _uploadResponseUrlMatch.Match(response);
        if (!match.Success) return true;
        __result = match.Groups[1].ToString();
        return false;
    }
}
