using System;

namespace LunarFramework.Utility;

public static class CommonExtensions
{
    public static string ToStringPretty(this Version version)
    {
        if (version.Major < 0 || version.Minor < 0) return version.ToString();
        if (version.Build <= 0) return version.ToString(2);
        if (version.Revision <= 0) return version.ToString(3);
        return version.ToString();
    }
}