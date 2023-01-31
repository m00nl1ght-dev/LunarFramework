using System;

namespace LunarFramework.Patching;

public sealed class PatchGroupSubscriber
{
    public static readonly PatchGroupSubscriber Generic = new(null);

    public readonly Type Source;

    public PatchGroupSubscriber(Type source)
    {
        Source = source;
    }
}
