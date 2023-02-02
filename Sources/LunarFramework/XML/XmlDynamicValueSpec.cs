using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace LunarFramework.XML;

public class XmlDynamicValueSpec<T, TC>
{
    public XmlDynamicValueSpec<bool, TC> ConditionSpec { get; set; }

    public Registry<XmlDynamicValueSupplierSpec<T, TC>> SupplierSpecs { get; } = new();
    public Registry<XmlDynamicValueModifierSpec<T, TC>> ModifierSpecs { get; } = new();

    public void InheritFrom(XmlDynamicValueSpec<T, TC> other)
    {
        ConditionSpec ??= other.ConditionSpec;
        SupplierSpecs.InheritFrom(other.SupplierSpecs);
        ModifierSpecs.InheritFrom(other.ModifierSpecs);
    }

    public Func<TC, T> BuildSupplier(XmlNode node, bool resolveFromNodeName = false)
        => SupplierSpecs.GetForNode(node, resolveFromNodeName)(node, this) ?? throw new Exception($"Invalid node <{node.Name}>");

    public Func<TC, T, T> BuildModifier(XmlNode node, bool resolveFromNodeName = false)
        => ModifierSpecs.GetForNode(node, resolveFromNodeName)(node, this) ?? throw new Exception($"Invalid node <{node.Name}>");

    public class Registry<TS> where TS : class
    {
        public TS DefaultSpec { get; set; }

        public string SpecNameAttribute { get; set; }

        public IReadOnlyDictionary<string, TS> Specs => _specs;
        private readonly Dictionary<string, TS> _specs = new();

        public IReadOnlyList<Func<string, TS>> Fallback => _fallback;
        private readonly List<Func<string, TS>> _fallback = new(3);

        public void InheritFrom(Registry<TS> other)
        {
            RegisterFallback(other.Get);
            DefaultSpec ??= other.DefaultSpec;
            SpecNameAttribute ??= other.SpecNameAttribute;
        }

        public TS Get(string name)
            => _specs.TryGetValue(name, out var spec) ? spec : Fallback.Select(f => f(name)).LastOrDefault(f => f != null);

        public TS GetOrThrow(string name)
            => Get(name) ?? throw new Exception($"XML dynamic value supplier with name {name} not found");

        public void Register(string name, TS spec)
            => _specs.Add(name, spec);

        public void RegisterFallback(Func<string, TS> fallback)
            => _fallback.Add(fallback);

        public TS GetForNode(XmlNode node, bool resolveFromNodeName = false)
        {
            var specName = resolveFromNodeName ? node.Name : node.Attributes?.GetNamedItem(SpecNameAttribute ?? "spec")?.Value;
            if (specName != null) return GetOrThrow(specName);

            if (node.HasSimpleTextValue() && node.InnerText.StartsWith("$"))
                return GetOrThrow(node.InnerText.Substring(1));

            return DefaultSpec;
        }
    }
}

public delegate Func<TC, T> XmlDynamicValueSupplierSpec<T, TC>(XmlNode node, XmlDynamicValueSpec<T, TC> spec);

public delegate Func<TC, T, T> XmlDynamicValueModifierSpec<T, TC>(XmlNode node, XmlDynamicValueSpec<T, TC> spec);
