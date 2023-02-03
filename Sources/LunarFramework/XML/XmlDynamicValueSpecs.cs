using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace LunarFramework.XML;

public interface IXmlDynamicValueSpecs<out T> where T : class
{
    public Spec<T> DefaultSpec { get; }

    public string NameAttribute { get; }

    public Spec<T> Get(string name);

    public Spec<T> GetOrThrow(string name);

    public Spec<T> GetForNode(XmlNode node, bool resolveFromNodeName = false);

    public T Build(XmlNode node, bool resolveFromNodeName = false);
}

public class XmlDynamicValueSpecs<T> : IXmlDynamicValueSpecs<T> where T : class
{
    public Spec<T> DefaultSpec { get; set; }

    public string NameAttribute { get; set; }

    public IReadOnlyDictionary<string, Spec<T>> Specs => _specs;
    private readonly Dictionary<string, Spec<T>> _specs = new();

    public IReadOnlyList<Func<string, Spec<T>>> Fallback => _fallback;
    private readonly List<Func<string, Spec<T>>> _fallback = new(3);

    public void InheritFrom(IXmlDynamicValueSpecs<T> other)
    {
        RegisterFallback(other.Get);
        DefaultSpec ??= other.DefaultSpec;
        NameAttribute ??= other.NameAttribute;
    }

    public Spec<T> Get(string name)
        => _specs.TryGetValue(name, out var spec) ? spec : Fallback.Select(f => f(name)).FirstOrDefault(f => f != null);

    public Spec<T> GetOrThrow(string name)
        => Get(name) ?? throw new Exception($"XML dynamic value {typeof(T).Name.ToLower()} with name {name} not found");

    public void Register(string name, Spec<T> spec)
        => _specs.Add(name, spec);

    public void RegisterFallback(Func<string, Spec<T>> fallback)
        => _fallback.Add(fallback);

    public T Build(XmlNode node, bool resolveFromNodeName = false)
        => GetForNode(node, resolveFromNodeName)(node) ?? throw new Exception($"Invalid node <{node.Name}>");

    public Spec<T> GetForNode(XmlNode node, bool resolveFromNodeName = false)
    {
        var specName = resolveFromNodeName ? node.Name : node.Attributes?.GetNamedItem(NameAttribute ?? "spec")?.Value;
        if (specName != null) return GetOrThrow(specName);

        if (node.HasSimpleTextValue() && node.InnerText.StartsWith("$"))
            return GetOrThrow(node.InnerText.Substring(1));

        return DefaultSpec;
    }
}
