using System;
using System.Collections.Generic;
using System.Xml;

namespace LunarFramework.XML;

public class XmlDynamicValueSpec<T, TC>
{
    public XmlDynamicValueSpec<T, TC> BaseSpec { get; }

    public XmlDynamicValueSpec<bool, TC> ConditionSpec { get; set; }

    public XmlDynamicValueSupplierSpec<T, TC> DefaultSupplierSpec { get; set; }
    public XmlDynamicValueModifierSpec<T, TC> DefaultModifierSpec { get; set; }

    public string SupplierNameAttribute { get; set; } = "supplier";
    public string ModifierNameAttribute { get; set; } = "operation";

    public IReadOnlyDictionary<string, XmlDynamicValueSupplierSpec<T, TC>> SupplierSpecs => _supplierSpecs;
    private readonly Dictionary<string, XmlDynamicValueSupplierSpec<T, TC>> _supplierSpecs = new();

    public IReadOnlyDictionary<string, XmlDynamicValueModifierSpec<T, TC>> ModifierSpecs => _modifierSpecs;
    private readonly Dictionary<string, XmlDynamicValueModifierSpec<T, TC>> _modifierSpecs = new();

    public XmlDynamicValueSpec(XmlDynamicValueSpec<T, TC> baseSpec = null)
    {
        if (baseSpec != null)
        {
            BaseSpec = baseSpec;
            ConditionSpec = baseSpec.ConditionSpec;
            DefaultSupplierSpec = baseSpec.DefaultSupplierSpec;
            DefaultModifierSpec = baseSpec.DefaultModifierSpec;
            SupplierNameAttribute = baseSpec.SupplierNameAttribute;
            ModifierNameAttribute = baseSpec.ModifierNameAttribute;
        }
    }

    public XmlDynamicValueSupplierSpec<T, TC> GetSupplierSpec(string name)
        => _supplierSpecs.TryGetValue(name, out var spec) ? spec : BaseSpec?.GetSupplierSpec(name);

    public XmlDynamicValueSupplierSpec<T, TC> GetSupplierSpecOrThrow(string name)
        => GetSupplierSpec(name) ?? throw new Exception($"XML dynamic value supplier with name {name} not found");

    public void RegisterSupplierSpec(string name, XmlDynamicValueSupplierSpec<T, TC> spec)
        => _supplierSpecs.Add(name, spec);

    public void RegisterSupplier(string name, Func<TC, T> supplier)
        => _supplierSpecs.Add(name, (_, _) => supplier);

    public Func<TC, T> BuildSupplier(XmlNode node, bool resolveFromNodeName = false)
    {
        var specName = resolveFromNodeName ? node.Name : node.Attributes?.GetNamedItem(SupplierNameAttribute)?.Value;
        if (specName != null) return GetSupplierSpecOrThrow(specName)(node, this);

        if (node.ChildNodes.Count == 1 && node.FirstChild.NodeType == XmlNodeType.Text)
        {
            var spec = GetSupplierSpec(node.FirstChild.Value);
            if (spec != null) return spec(null, this);
        }

        return DefaultSupplierSpec(node, this);
    }

    public XmlDynamicValueModifierSpec<T, TC> GetModifierSpec(string name)
        => _modifierSpecs.TryGetValue(name, out var spec) ? spec : BaseSpec?.GetModifierSpec(name);

    public XmlDynamicValueModifierSpec<T, TC> GetModifierSpecOrThrow(string name)
        => GetModifierSpec(name) ?? throw new Exception($"XML dynamic value modifier with name {name} not found");

    public void RegisterModifierSpec(string name, XmlDynamicValueModifierSpec<T, TC> spec)
        => _modifierSpecs.Add(name, spec);

    public void RegisterModifier(string name, Func<TC, T, T> modifier)
        => _modifierSpecs.Add(name, (_, _) => modifier);

    public Func<TC, T, T> BuildModifier(XmlNode node, bool resolveFromNodeName = false)
    {
        var specName = resolveFromNodeName ? node.Name : node.Attributes?.GetNamedItem(ModifierNameAttribute)?.Value;
        if (specName != null) return GetModifierSpecOrThrow(specName)(node, this);

        if (node.ChildNodes.Count == 1 && node.FirstChild.NodeType == XmlNodeType.Text)
        {
            var spec = GetModifierSpec(node.FirstChild.Value);
            if (spec != null) return spec(null, this);
        }

        return DefaultModifierSpec(node, this);
    }
}

public delegate Func<TC, T> XmlDynamicValueSupplierSpec<T, TC>(XmlNode node, XmlDynamicValueSpec<T, TC> spec);

public delegate Func<TC, T, T> XmlDynamicValueModifierSpec<T, TC>(XmlNode node, XmlDynamicValueSpec<T, TC> spec);
