using System;
using HarmonyLib;
using Verse;

namespace LunarFramework.Logging;

public static class LogPublisher
{
    private static readonly Action ShowPublishPromptAction;

    static LogPublisher()
    {
        ShowPublishPromptAction = TryReflect("HugsLib.Logs.LogPublisher", "ShowPublishPrompt");
        ShowPublishPromptAction ??= TryReflect("HugsLogPublisher.LogPublisher", "ShowPublishPrompt");
    }
    
    public static bool TryShowPublishPrompt()
    {
        if (ShowPublishPromptAction == null) return false;
        ShowPublishPromptAction.Invoke();
        return true;
    }

    private static Action TryReflect(string publisherType, string methodName)
    {
        try
        {
            var type = GenTypes.GetTypeInAnyAssembly(publisherType);
            if (type != null)
            {
                var instance = Activator.CreateInstance(type);
                
                var method = AccessTools.Method(type, methodName);
                if (method != null)
                {
                    return () => method.Invoke(instance, Array.Empty<object>());
                }
            }
        }
        catch (Exception)
        {
            // ignored
        }

        return null;
    }
}