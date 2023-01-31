using System;

namespace LunarFramework.Patching;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class PatchGroupAttribute : Attribute
{
    public readonly string Name;

    public PatchGroupAttribute(string name)
    {
        Name = name;
    }
}
