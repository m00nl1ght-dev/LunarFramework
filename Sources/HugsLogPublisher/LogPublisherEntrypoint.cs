using HugsLogPublisher.Compatibility;
using LunarFramework;
using LunarFramework.Logging;
using LunarFramework.Patching;
using UnityEngine;

namespace HugsLogPublisher;

[LunarComponentEntrypoint]
public static class LogPublisherEntrypoint
{
    internal static readonly LunarAPI LunarAPI = LunarAPI.Create("HugsLogPublisher", Init, Cleanup);

    internal static LogContext Logger => LunarAPI.LogContext;

    internal static PatchGroup CompatPatchGroup;

    private static void Init()
    {
        CompatPatchGroup ??= LunarAPI.RootPatchGroup.NewSubGroup("Compat");
        CompatPatchGroup.Subscribe();

        ModCompat.ApplyAll(LunarAPI, CompatPatchGroup);

        if (!ModCompat_HugsLib.IsPresent)
        {
            LunarAPI.LifecycleHooks.DoOnGUI(OnGUI);
        }
    }

    private static void Cleanup()
    {
        CompatPatchGroup?.UnsubscribeAll();
    }

    private static void OnGUI()
    {
        if (Event.current.type != EventType.KeyDown) return;

        if (Input.GetKey(KeyCode.F12) && HugsLibUtility.ControlIsHeld)
        {
            if (HugsLibUtility.AltIsHeld)
            {
                LogPublisher.Instance.CopyToClipboard();
            }
            else
            {
                LogPublisher.Instance.ShowPublishPrompt();
            }

            Event.current.Use();
        }
    }
}
