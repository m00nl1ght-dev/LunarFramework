using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UnityEngine;
using Verse;

namespace LunarFramework.XML;

public static class XmlDynamicValueSupport
{
    public static Func<TC, T> FixedValue<T, TC>(XmlNode node, Func<string, T> parser)
    {
        if (!node.HasSimpleTextValue()) return null;
        var value = parser(node.InnerText);
        return _ => value;
    }

    public static Func<TC, bool> ValueInRange<T, TC>(XmlNode node, XmlDynamicValueSpec<T, TC> spec, Func<TC, T> supplier)
        where T : IComparable<T>, IEquatable<T>
    {
        if (node.HasSimpleTextValue())
        {
            var equalSupplier = spec.BuildSupplier(node);
            return ctx => equalSupplier(ctx).Equals(supplier(ctx));
        }

        Func<TC, T> minSupplier = null;
        Func<TC, T> maxSupplier = null;

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            if (childNode.Name == "min")
            {
                if (minSupplier != null) throw new Exception($"There can only be one <{childNode.Name}> element in a <{node.Name}> node");
                minSupplier = spec.BuildSupplier(childNode);
            }
            else if (childNode.Name == "max")
            {
                if (maxSupplier != null) throw new Exception($"There can only be one <{childNode.Name}> element in a <{node.Name}> node");
                maxSupplier = spec.BuildSupplier(childNode);
            }
            else
            {
                throw new Exception($"Unknown element <{childNode.Name}> in node <{node.Name}>");
            }
        }

        if (minSupplier != null && maxSupplier != null)
            return ctx => minSupplier(ctx).CompareTo(supplier(ctx)) <= 0 && maxSupplier(ctx).CompareTo(supplier(ctx)) >= 0;
        if (minSupplier != null)
            return ctx => minSupplier(ctx).CompareTo(supplier(ctx)) <= 0;
        if (maxSupplier != null)
            return ctx => maxSupplier(ctx).CompareTo(supplier(ctx)) >= 0;

        throw new Exception($"Invalid node <{node.Name}>");
    }

    public static XmlDynamicValueSupplierSpec<bool, TC> ValueInRange<T, TC>(string name, XmlDynamicValueSpec<T, TC> spec)
        where T : IComparable<T>, IEquatable<T>
    {
        var supplierSpec = spec.SupplierSpecs.Get(name);
        if (supplierSpec == null) return null;
        return (n, _) => ValueInRange(n, spec, supplierSpec(n, spec));
    }

    public static Func<TC, T> SupplierWithParam<T, TC>(XmlNode node, Func<string, TC, T> supplier)
    {
        if (!node.HasSimpleTextValue()) throw new Exception($"Element <{node.Name}> is not a text node");
        var param = node.InnerText;
        return ctx => supplier(param, ctx);
    }

    public static Func<TC, T> SupplierWithParam<T, TC, TE>(XmlNode node, Func<TE, TC, T> supplier) where TE : struct
    {
        if (!node.HasSimpleTextValue()) throw new Exception($"Element <{node.Name}> is not a text node");
        if (Enum.TryParse<TE>(node.InnerText, out var param)) return ctx => supplier(param, ctx);
        throw new Exception($"Value {node.InnerText} in element <{node.Name}> is not a valid {typeof(TE).Name}");
    }

    public static XmlDynamicValueModifierSpec<T, TC> DyadicModifier<T, TC>(Func<T, T, T> dyadicOperation, XmlDynamicValueSupplierSpec<T, TC> operandSpec)
    {
        return (n, r) =>
        {
            var operandSupplier = operandSpec(n, r);
            return (ctx, value) => dyadicOperation(value, operandSupplier(ctx));
        };
    }

    public static Func<TC, T, T> DyadicModifier<T, TC>(XmlNode node, XmlDynamicValueSpec<T, TC> spec, Func<T, T, T> dyadicOperation)
    {
        if (node.HasSimpleTextValue())
        {
            var supplier = spec.BuildSupplier(node);
            return (ctx, value) => dyadicOperation(value, supplier(ctx));
        }

        Func<TC, T> operandSupplier = null;
        List<Func<TC, T, T>> operandModifiers = null;
        Func<TC, bool, bool> conditions = null;

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            if (childNode.Name == "value")
            {
                if (operandSupplier != null) throw new Exception($"There can only be one <{childNode.Name}> element in a <{node.Name}> node");
                operandSupplier = spec.BuildSupplier(childNode);
            }
            else if (childNode.Name == "modifier")
            {
                operandModifiers ??= new();
                operandModifiers.Add(spec.BuildModifier(childNode));
            }
            else if (childNode.Name == "conditions" && spec.ConditionSpec != null)
            {
                if (conditions != null) throw new Exception($"There can only be one <{childNode.Name}> element in a <{node.Name}> node");
                conditions = spec.ConditionSpec.BuildModifier(childNode);
            }
            else
            {
                throw new Exception($"Unknown element <{childNode.Name}> in node <{node.Name}>");
            }
        }

        if (operandSupplier == null)
        {
            throw new Exception($"Required element <value> is missing in node <{node.Name}>");
        }

        if (operandModifiers == null)
        {
            if (conditions == null) return (ctx, value) => dyadicOperation(value, operandSupplier(ctx));
            return (ctx, value) => conditions(ctx, true) ? dyadicOperation(value, operandSupplier(ctx)) : value;
        }

        T ApplyModifiers(TC ctx, T op) => operandModifiers.Aggregate(op, (current, modifier) => modifier(ctx, current));

        if (conditions == null) return (ctx, value) => dyadicOperation(value, ApplyModifiers(ctx, operandSupplier(ctx)));
        return (ctx, value) => conditions(ctx, true) ? dyadicOperation(value, ApplyModifiers(ctx, operandSupplier(ctx))) : value;
    }

    public static Func<TC, T> AggregateSupplierList<T, TC>(XmlNode node, XmlDynamicValueSpec<T, TC> spec, Func<T, T, T> combineOperation)
    {
        if (node.HasSimpleTextValue()) return spec.BuildSupplier(node);

        List<Func<TC, T>> suppliers = new();

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            suppliers.Add(spec.BuildSupplier(childNode, true));
        }

        if (suppliers.Count == 0) throw new Exception($"Invalid node <{node.Name}>");

        return ctx => suppliers.Select(f => f(ctx)).Aggregate(combineOperation);
    }
    
    public static Func<TC, float, float> InterpolationCurve<TC>(XmlNode node, XmlDynamicValueSpec<float, TC> spec)
    {
        List<KeyValuePair<float, Func<TC, float>>> points = new();

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            var at = float.Parse(childNode.GetNamedChild("at").InnerText);
            var val = spec.BuildSupplier(childNode.GetNamedChild("is"));
            points.Add(new KeyValuePair<float, Func<TC, float>>(at, val));
        }
        
        if (points.Count < 2) throw new Exception($"At least two points must be defined for an interpolation");
        points.SortBy(e => e.Key);

        return (ctx, value) =>
        {
            var upperIdx = 0;
            while (upperIdx < points.Count && value >= points[upperIdx].Key) upperIdx++;

            if (upperIdx <= 0) 
                return points.First().Value(ctx);
            
            if (upperIdx >= points.Count) 
                return points.Last().Value(ctx);

            var upper = points[upperIdx];
            var lower = points[upperIdx - 1];
            
            var frac = Mathf.InverseLerp(lower.Key, upper.Key, value);
            return Mathf.Lerp(lower.Value(ctx), upper.Value(ctx), frac);
        };
    }

    public static void Register<T, TC>(this XmlDynamicValueSpec<T, TC>.Registry<XmlDynamicValueSupplierSpec<T, TC>> registry, string name, Func<TC, T> supplier)
        => registry.Register(name, (_, _) => supplier);

    public static void Register<T, TC>(this XmlDynamicValueSpec<T, TC>.Registry<XmlDynamicValueModifierSpec<T, TC>> registry, string name, Func<TC, T, T> supplier)
        => registry.Register(name, (_, _) => supplier);

    public static XmlDynamicValueSupplierSpec<T, TC> FixedValue<T, TC>(Func<string, T> parser)
        => (n, _) => FixedValue<T, TC>(n, parser);

    public static XmlDynamicValueSupplierSpec<T, TC> SupplierWithParam<T, TC>(Func<string, TC, T> supplier)
        => (n, _) => SupplierWithParam(n, supplier);

    public static XmlDynamicValueSupplierSpec<T, TC> SupplierWithParam<T, TC, TE>(Func<TE, TC, T> supplier) where TE : struct
        => (n, _) => SupplierWithParam(n, supplier);

    public static XmlDynamicValueModifierSpec<T, TC> DyadicModifier<T, TC>(Func<T, T, T> dyadicOperation)
        => (n, r) => DyadicModifier(n, r, dyadicOperation);

    public static XmlDynamicValueSupplierSpec<T, TC> AggregateSupplierList<T, TC>(Func<T, T, T> combineOperation)
        => (n, r) => AggregateSupplierList(n, r, combineOperation);

    public static bool HasSimpleTextValue(this XmlNode node)
        => node.ChildNodes.Count == 1 && node.FirstChild.NodeType == XmlNodeType.Text;

    public static XmlNode GetNamedChild(this XmlNode node, string name)
    {
        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            if (childNode.Name == name) return childNode;
        }
        
        throw new Exception($"Node <{node.Name}> is missing required element <{name}>");
    }
}
