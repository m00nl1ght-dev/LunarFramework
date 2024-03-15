using HarmonyLib;
using LunarFramework.Patching;

namespace HugsLogPublisher.Compatibility;

[HarmonyPatch]
internal class ModCompat_HugsLib : ModCompat
{
    public override string TargetAssemblyName => "HugsLib";
    public override string DisplayName => "HugsLib";

    public static bool IsPresent { get; private set; }

    protected override bool OnApply()
    {
        IsPresent = true;
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("HugsLib.Logs.LogPublisher", "ShowPublishPrompt")]
    private static bool ShowPublishPrompt_Prefix()
    {
        LogPublisher.Instance.ShowPublishPrompt();
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("HugsLib.Logs.LogPublisher", "CopyToClipboard")]
    private static bool CopyToClipboard_Prefix()
    {
        LogPublisher.Instance.CopyToClipboard();
        return false;
    }
}
