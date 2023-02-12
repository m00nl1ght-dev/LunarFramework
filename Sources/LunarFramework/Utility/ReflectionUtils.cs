using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;

namespace LunarFramework.Utility;

public static class ReflectionUtils
{
    private static MethodInfo _cloneMethod;

    public static T MakeShallowCopy<T>(this T obj)
    {
        if (_cloneMethod is null)
        {
            _cloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            if (_cloneMethod == null) throw new Exception("Failed to reflect object clone method");
        }

        return (T) _cloneMethod.Invoke(obj, null);
    }

    public static ModContentPack FindSourceMod(this Type type)
        => LoadedModManager.RunningMods.FirstOrDefault(mcp => mcp.assemblies.loadedAssemblies.Contains(type.Assembly));

    public static void RunClassConstructor(this Type type) => RuntimeHelpers.RunClassConstructor(type.TypeHandle);
}
