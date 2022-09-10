using System;
using System.Collections;
using LunarFramework.Logging;
using LunarFramework.Patching;
using UnityEngine;

namespace LunarFramework.Internal;

internal class LunarRoot : MonoBehaviour
{
    internal static LogContext Logger => new IngameLogContext(typeof(LunarRoot), "LunarFramework", LunarAPI.FrameworkVersion);
    
    internal static LunarRoot Instance { get; private set; }

    internal static readonly PatchGroup MainPatchGroup = new("LunarFramework.Main");
    internal static readonly PatchGroup BootstrapPatchGroup = new("LunarFramework.Bootstrap");

    internal static event Action OnQuit;

    internal static void Initialize()
    {
        if (Instance != null) return;
        
        var gameObject = new GameObject("LunarRoot");
        Instance = gameObject.AddComponent<LunarRoot>();
        DontDestroyOnLoad(gameObject);
        
        MainPatchGroup.AddPatches(typeof(LunarRoot).Assembly);
        MainPatchGroup.Subscribe();
        
        BootstrapPatchGroup.AddPatches(typeof(LunarRoot).Assembly);
        BootstrapPatchGroup.Subscribe();
    }

    internal static void RunCoroutine(IEnumerator coroutine)
    {
        Instance.StartCoroutine(coroutine);
    }

    private void OnApplicationQuit()
    {
        OnQuit?.Invoke();
    }
}