using System.IO;
using System.Linq;
using HarmonyLib;
using LunarFramework.Patching;
using Verse;

namespace LunarFramework.Internal.Compatibility;

[HarmonyPatch]
internal class ModCompat_HugsLib : ModCompat
{
    public override string TargetAssemblyName => "HugsLib";
    public override string DisplayName => "HugsLib";

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
}
