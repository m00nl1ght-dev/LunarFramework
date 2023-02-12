using System;
using HarmonyLib;
using LunarFramework.Utility;

namespace LunarFramework.Logging;

public class HarmonyLogContext : LogContext
{
    public HarmonyLogContext(Type owner, string name = null, Version version = null) : base(owner, name, version) { }

    public override void Log(LogLevel level, string message, Exception exception = null)
    {
        if (level < Level) return;

        message = $"[{level.ToString()}] [{Name} v{Version.ToStringPretty()}] {message}";

        FileLog.Debug(message);

        if (exception != null)
        {
            FileLog.Debug(exception.ToString());
        }
    }
}
