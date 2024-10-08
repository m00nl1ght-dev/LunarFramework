using System;
using LunarFramework.Utility;
using Verse;

namespace LunarFramework.Logging;

public class IngameLogContext : LogContext
{
    public LogLevel IgnoreLogLimitLevel { get; set; } = LogLevel.Fatal;

    public IngameLogContext(Type owner, string name = null, Version version = null) : base(owner, name, version) { }

    public override void Log(LogLevel level, string message, Exception exception = null)
    {
        if (level < Level && !Prefs.LogVerbose) return;

        message = $"[{Name} v{Version.ToStringPretty()}] {message}";

        if (level >= IgnoreLogLimitLevel)
        {
            Verse.Log.ResetMessageCount();
        }

        if (exception != null)
        {
            message += "\n" + exception;
        }

        switch (level)
        {
            case LogLevel.Debug or LogLevel.Info:
                Verse.Log.Message(message);
                break;
            case LogLevel.Warn:
                Verse.Log.Warning(message);
                break;
            case LogLevel.Error or LogLevel.Fatal:
                Verse.Log.Error(message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }
}
