using System;
using HarmonyLib;

namespace LunarFramework.Logging;

public static class LogPublisher
{
    private static readonly Action ShowPublishPromptAction;

    static LogPublisher()
    {
        ShowPublishPromptAction = TryReflectHugsLib();
        ShowPublishPromptAction ??= TryReflectStandalone();
    }

    public static bool TryShowPublishPrompt()
    {
        if (ShowPublishPromptAction == null) return false;
        ShowPublishPromptAction.Invoke();
        return true;
    }

    private static Action TryReflectHugsLib()
    {
        try
        {
            var type = AccessTools.TypeByName("HugsLib.Logs.LogPublisher");
            if (type != null)
            {
                var cType = AccessTools.TypeByName("HugsLib.HugsLibController");
                var cField = AccessTools.Field(cType, "instance");
                var cInstance = cField.GetValue(null);
                var prop = AccessTools.Property(cType, "LogUploader");
                var method = AccessTools.Method(type, "ShowPublishPrompt");
                var instance = prop.GetValue(cInstance);

                if (method != null && instance != null)
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

    private static Action TryReflectStandalone()
    {
        try
        {
            var type = AccessTools.TypeByName("HugsLogPublisher.LogPublisher");
            if (type != null)
            {
                var field = AccessTools.Field(type, "Instance");
                var method = AccessTools.Method(type, "ShowPublishPrompt");
                var instance = field.GetValue(null);

                if (method != null && instance != null)
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
