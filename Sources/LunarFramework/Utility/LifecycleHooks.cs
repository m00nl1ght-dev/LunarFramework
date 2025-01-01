using System;
using System.Collections;
using LunarFramework.Internal;
using UnityEngine;

namespace LunarFramework.Utility;

public class LifecycleHooks
{
    internal static readonly LifecycleHooks InternalInstance = new();

    internal LifecycleHooks() { }

    public static float FrameStartTime => LunarRoot.FrameStartTime;

    public void DoOnce(Action action, float delay = 0f)
    {
        if (delay > 0f)
        {
            LunarRoot.RunCoroutine(DoOnceEnumerator(action, delay));
        }
        else
        {
            LunarRoot.DoOnceOnUpdate += action;
        }
    }

    private IEnumerator DoOnceEnumerator(Action action, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
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
        LunarRoot.DoOnceOnMainMenu += action;
    }

    public void DoOnceOnShutdown(Action action)
    {
        LunarRoot.DoOnQuit += action;
    }

    public void DoOnGUI(Action action)
    {
        LunarRoot.DoOnGUI += action;
    }
}
