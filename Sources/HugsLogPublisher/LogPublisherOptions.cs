using System;

namespace HugsLogPublisher;

internal interface ILogPublisherOptions
{
    bool UseCustomOptions { get; set; }
    bool IncludePlatformInfo { get; set; }
}

[Serializable]
internal class LogPublisherOptions : IEquatable<LogPublisherOptions>, ILogPublisherOptions
{
    public bool UseCustomOptions { get; set; }

    public bool IncludePlatformInfo { get; set; }

    public bool Equals(LogPublisherOptions other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return UseCustomOptions == other.UseCustomOptions &&
               IncludePlatformInfo == other.IncludePlatformInfo;
    }
}
