using System;
using System.Reflection;
using System.Runtime.CompilerServices;

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
    
    public static void RunClassConstructor(this Type type) => RuntimeHelpers.RunClassConstructor(type.TypeHandle);
}
