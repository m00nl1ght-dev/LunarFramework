using System;
using System.Collections;
using System.Threading;
using LunarFramework.Logging;
using LunarFramework.Patching;
using UnityEngine;

namespace LunarFramework.Internal;

internal class LunarRoot : MonoBehaviour
{
    internal static readonly LogContext Logger = new IngameLogContext(typeof(LunarRoot), "LunarFramework", LunarAPI.FrameworkVersion);

    internal static LunarRoot Instance { get; private set; }
    internal static bool IsReady => Instance != null;

    internal static readonly PatchGroup MainPatchGroup = new("LunarFramework.Main");
    internal static readonly PatchGroup CompatPatchGroup = new("LunarFramework.Compat");
    internal static readonly PatchGroup BootstrapPatchGroup = new("LunarFramework.Bootstrap");

    internal static event Action DoOnceOnUpdate;
    internal static event Action DoOnceOnMainMenu;

    internal static event Action DoOnGUI;
    internal static event Action DoOnQuit;

    internal static float FrameStartTime;
    internal static int LastFrame = -1;

    internal static void Initialize()
    {
        try
        {
            ModCompat.ApplyAll(typeof(LunarRoot).Assembly, Logger, CompatPatchGroup);

            MainPatchGroup.AddPatches(typeof(LunarRoot).Assembly);
            MainPatchGroup.Subscribe();

            CompatPatchGroup.Subscribe();

            BootstrapPatchGroup.AddPatches(typeof(LunarRoot).Assembly);
            BootstrapPatchGroup.Subscribe();
        }
        catch
        {
            MainPatchGroup?.UnsubscribeAll();
            CompatPatchGroup?.UnsubscribeAll();
            BootstrapPatchGroup?.UnsubscribeAll();

            throw;
        }
    }

    internal static void CreateInstance()
    {
        if (Instance != null) return;
        var gameObject = new GameObject("LunarRoot");
        Instance = gameObject.AddComponent<LunarRoot>();
        DontDestroyOnLoad(gameObject);
    }

    internal static void RunCoroutine(IEnumerator coroutine)
    {
        DoOnceOnUpdate += () => Instance.StartCoroutine(coroutine);
    }

    internal static void OnMainMenuReady()
    {
        Interlocked.Exchange(ref DoOnceOnMainMenu, null)?.Invoke();
    }

    private void FixedUpdate()
    {
        if (Time.frameCount > LastFrame)
        {
            LastFrame = Time.frameCount;
            FrameStartTime = Time.realtimeSinceStartup;
        }
    }

    private void Update()
    {
        Interlocked.Exchange(ref DoOnceOnUpdate, null)?.Invoke();
    }

    private void OnGUI()
    {
        DoOnGUI?.Invoke();
    }

    private void OnApplicationQuit()
    {
        DoOnQuit?.Invoke();
    }
}
