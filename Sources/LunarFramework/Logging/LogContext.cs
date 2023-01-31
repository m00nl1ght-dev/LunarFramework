using System;

namespace LunarFramework.Logging;

public abstract class LogContext
{
    public Type Owner { get; }
    public string Name { get; }
    public Version Version { get; }

    public LogLevel Level { get; set; } = LogLevel.Info;

    protected LogContext(Type owner, string name = null, Version version = null)
    {
        Owner = owner;
        Name = name ?? owner.Name;
        Version = version ?? owner.Assembly.GetName().Version;
    }

    public abstract void Log(LogLevel level, string message, Exception exception = null);

    public void Debug(string message, Exception exception = null) => Log(LogLevel.Debug, message, exception);

    public void Log(string message, Exception exception = null) => Log(LogLevel.Info, message, exception);

    public void Warn(string message, Exception exception = null) => Log(LogLevel.Warn, message, exception);

    public void Error(string message, Exception exception = null) => Log(LogLevel.Error, message, exception);

    public void Fatal(string message, Exception exception = null) => Log(LogLevel.Fatal, message, exception);

    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }
}
