using System;
using System.Collections.Generic;
using System.Linq;

namespace LunarFramework.Utility;

public class ExtensionPoint<TC, T>
{
    private readonly List<Extension> Extensions = [];

    private readonly T defaultValue;

    public ExtensionPoint() { }

    public ExtensionPoint(T defaultValue)
    {
        this.defaultValue = defaultValue;
    }

    public T Apply(TC context) => Apply(context, defaultValue);

    public T Apply(TC context, T initialValue)
    {
        return Extensions.Aggregate(initialValue, (current, extension) => extension.Func(context, current));
    }

    public void AddModifier(int priority, Func<TC, T, T> modifier)
    {
        AddExtension(new Extension(modifier, priority));
    }

    public void AddModifier(int priority, Func<T, T> modifier)
    {
        AddExtension(new Extension((_, val) => modifier(val), priority));
    }

    public void AddSupplier(int priority, Func<TC, T> supplier)
    {
        AddExtension(new Extension((ctx, _) => supplier(ctx), priority));
    }

    public void AddSupplier(int priority, Func<T> supplier)
    {
        AddExtension(new Extension((_, _) => supplier(), priority));
    }

    public void AddObserver(int priority, Action<TC, T> observer)
    {
        AddExtension(new Extension((ctx, val) => { observer(ctx, val); return val; }, priority));
    }

    public void AddObserver(int priority, Action<TC> observer)
    {
        AddExtension(new Extension((ctx, val) => { observer(ctx); return val; }, priority));
    }

    public void AddObserver(int priority, Action<T> observer)
    {
        AddExtension(new Extension((_, val) => { observer(val); return val; }, priority));
    }

    private void AddExtension(Extension extension)
    {
        Extensions.Add(extension);
        Extensions.Sort((a, b) => a.Priority - b.Priority);
    }

    private readonly struct Extension
    {
        public readonly Func<TC, T, T> Func;
        public readonly int Priority;

        public Extension(Func<TC, T, T> func, int priority)
        {
            Func = func;
            Priority = priority;
        }
    }
}
