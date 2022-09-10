using System;
using System.Collections;
using LunarFramework.Internal;
using LunarFramework.Internal.Patches;
using LunarFramework.Patching;
using UnityEngine;

namespace LunarFramework.Utility;

public class LifecycleHooks
{
    private static readonly PatchGroupSubscriber PatchGroupSubscriber = new(typeof(LifecycleHooks));

    internal static readonly LifecycleHooks InternalInstance = new();
    
    internal LifecycleHooks() {}

    public void DoOnce(Action action, float delay = 0f)
    {
        LunarRoot.RunCoroutine(DoOnceEnumerator(action, delay));
    }

    private IEnumerator DoOnceEnumerator(Action action, float delay)
    {
        yield return delay <= 0f ? null : new WaitForSecondsRealtime(delay);
        action.Invoke();
    }
    
    public void DoWhile(Func<bool> action, float delay = 0f)
    {
        LunarRoot.RunCoroutine(DoWhileEnumerator(action, delay));
    }

    private IEnumerator DoWhileEnumerator(Func<bool> action, float delay)
    {
        yield return delay <= 0f ? null : new WaitForSecondsRealtime(delay);

        while (action.Invoke())
        {
            yield return null;
        }
    }

    public void DoEnumerator(IEnumerator enumerator)
    {
        LunarRoot.RunCoroutine(enumerator);
    }
    
    public void DoOnceOnMainMenu(Action action)
    {
        Patch_RimWorld_MainMenuDrawer.OnMainMenuReady += action;
    }
    
    public void DoOnceOnShutdown(Action action)
    {
        LunarRoot.OnQuit += action;
    }
}