using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UnityEngine;
using Verse;

namespace LunarFramework.XML;

public static class XmlDynamicValueSupport
{
    #region Common Functions

    public static readonly Func<float, float, float> FuncAdd = (a, b) => a + b;
    public static readonly Func<float, float, float> FuncMultiply = (a, b) => a * b;
    public static readonly Func<float, float, float> FuncSubtract = (a, b) => a - b;
    public static readonly Func<float, float, float> FuncDivideSafely = (a, b) => b == 0 ? 0 : a / b;

    public static readonly Func<string, string, string> FuncAppend = (a, b) => a.Trim() + " " + b;
    public static readonly Func<string, string, string> FuncPrepend = (a, b) => b.Trim() + " " + a;
    public static readonly Func<string, string, string> FuncAppendLine = (a, b) => a.Trim() + "\n\n" + b;
    public static readonly Func<string, string, string> FuncPrependLine = (a, b) => b.Trim() + "\n\n" + a;
    public static readonly Func<string, string, string> FuncAppendPar = (a, b) => AppendPar(a.Trim(), b.Trim());
    public static readonly Func<string, string, string> FuncPrependPar = (a, b) => PrependPar(a.Trim(), b.Trim());

    public static readonly Func<bool, bool, bool> FuncAnd = (a, b) => a && b;
    public static readonly Func<bool, bool, bool> FuncOr = (a, b) => a || b;

    #endregion

    #region Registration Helpers

    public static void RegisterBasicNumericSuppliers<TC>(this XmlDynamicValueSpecs<Supplier<float, TC>> specs)
    {
        specs.DefaultSpec = FixedValue<float, TC>(float.Parse);
        specs.Register("sum", AggregateSupplierList<float, TC>(FuncAdd));
        specs.Register("product", AggregateSupplierList<float, TC>(FuncMultiply));
    }

    public static void RegisterBasicNumericModifiers<TC>(this XmlDynamicValueSpecs<Modifier<float, TC>> specs)
    {
        specs.DefaultSpec = DyadicModifierVerbose<float, TC>((_, v) => v);
        specs.Register("add", DyadicModifierVerbose<float, TC>(FuncAdd));
        specs.Register("multiply", DyadicModifierVerbose<float, TC>(FuncMultiply));
        specs.Register("subtract", DyadicModifierVerbose<float, TC>(FuncSubtract));
        specs.Register("divide", DyadicModifierVerbose<float, TC>(FuncDivideSafely));
        specs.Register("curve", InterpolationCurve<TC>);
        specs.Register("replace", specs.DefaultSpec);
    }

    public static void RegisterBasicStringSuppliers<TC>(this XmlDynamicValueSpecs<Supplier<string, TC>> specs)
    {
        specs.DefaultSpec = FixedValue<string, TC>(s => s);
        specs.Register("translate", SupplierWithParam<string, TC>((s, _) => s.Translate()));
    }

    public static void RegisterBasicStringModifiers<TC>(this XmlDynamicValueSpecs<Modifier<string, TC>> specs)
    {
        specs.DefaultSpec = DyadicModifierVerbose<string, TC>((_, v) => v);
        specs.Register("append", DyadicModifierVerbose<string, TC>(FuncAppend));
        specs.Register("prepend", DyadicModifierVerbose<string, TC>(FuncPrepend));
        specs.Register("appendLine", DyadicModifierVerbose<string, TC>(FuncAppendLine));
        specs.Register("prependLine", DyadicModifierVerbose<string, TC>(FuncPrependLine));
        specs.Register("appendPar", DyadicModifierVerbose<string, TC>(FuncAppendPar));
        specs.Register("prependPar", DyadicModifierVerbose<string, TC>(FuncPrependPar));
        specs.Register("replace", specs.DefaultSpec);
    }

    public static void RegisterBasicBoolSuppliers<TC>(this XmlDynamicValueSpecs<Supplier<bool, TC>> specs)
    {
        var allOf = AggregateSupplierList<bool, TC>(FuncAnd);
        var anyOf = AggregateSupplierList<bool, TC>(FuncOr);
        specs.DefaultSpec = FixedValue<bool, TC>(bool.Parse).Or(allOf);
        specs.Register("allOf", allOf);
        specs.Register("anyOf", anyOf);
    }

    public static void RegisterBasicBoolModifiers<TC>(this XmlDynamicValueSpecs<Modifier<bool, TC>> specs)
    {
        specs.DefaultSpec = DyadicModifier<bool, TC>((_, v) => v);
        specs.Register("and", DyadicModifier<bool, TC>(FuncAnd));
        specs.Register("or", DyadicModifier<bool, TC>(FuncOr));
        specs.Register("replace", specs.DefaultSpec);
    }

    public static void RegisterBasicListSuppliers<T, TC>(this XmlDynamicValueSpecs<Supplier<List<T>, TC>> specs, Func<XmlNode, T> loadFunc = null)
    {
        if (loadFunc == null && typeof(T).IsValueType) throw new Exception("Must specify a loadFunc for value types");
        specs.DefaultSpec = FixedList<T, TC>(loadFunc ?? (node => DirectXmlToObject.ObjectFromXml<T>(node, false)));
    }

    public static void RegisterBasicListModifiers<T, TC>(this XmlDynamicValueSpecs<Modifier<List<T>, TC>> specs, IEqualityComparer<T> eqComp = null)
    {
        specs.DefaultSpec = DyadicModifierVerbose<List<T>, TC>((_, v) => v);
        specs.Register("add", DyadicModifierVerbose<List<T>, TC>((a, b) => b.Union(a, eqComp).ToList()));
        specs.Register("remove", DyadicModifierVerbose<List<T>, TC>((a, b) => a.Except(b, eqComp).ToList()));
        specs.Register("intersect", DyadicModifierVerbose<List<T>, TC>((a, b) => b.Intersect(a, eqComp).ToList()));
        specs.Register("replace", specs.DefaultSpec);
    }

    public static void Register<T, TC>(this XmlDynamicValueSpecs<Supplier<T, TC>> specs, string name, Supplier<T, TC> supplier)
        => specs.Register(name, _ => supplier);

    public static void Register<T, TC>(this XmlDynamicValueSpecs<Modifier<T, TC>> specs, string name, Modifier<T, TC> supplier)
        => specs.Register(name, _ => supplier);

    #endregion

    #region Supplier Helpers

    public static Supplier<T, TC> FixedValue<T, TC>(XmlNode node, Func<string, T> parser)
    {
        if (!node.HasSimpleTextValue()) return null;
        var value = parser(node.InnerText);
        return _ => value;
    }

    public static Supplier<T, TC> Convert<T, TS, TC>(Supplier<TS, TC> supplier, Func<TS, T> convertFunc)
    {
        return ctx => convertFunc(supplier(ctx));
    }

    public static Supplier<T, TC> SupplierWithParam<T, TC>(XmlNode node, Func<string, TC, T> supplier)
    {
        if (!node.HasSimpleTextValue()) throw new Exception($"Element <{node.Name}> is not a text node");
        var param = node.InnerText;
        return ctx => supplier(param, ctx);
    }

    public static Supplier<T, TC> SupplierWithParam<T, TC, TE>(XmlNode node, Func<TE, TC, T> supplier) where TE : struct
    {
        if (!node.HasSimpleTextValue()) throw new Exception($"Element <{node.Name}> is not a text node");
        if (Enum.TryParse<TE>(node.InnerText, out var param)) return ctx => supplier(param, ctx);
        throw new Exception($"Value {node.InnerText} in element <{node.Name}> is not a valid {typeof(TE).Name}");
    }

    public static Supplier<T, TC> AggregateSupplierList<T, TC>(XmlNode node, Func<T, T, T> combineOperation)
    {
        if (node.HasSimpleTextValue()) return XmlDynamicValue<T, TC>.SupplierSpecs.Build(node);

        List<Supplier<T, TC>> suppliers = new();

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            suppliers.Add(XmlDynamicValue<T, TC>.SupplierSpecs.Build(childNode, true));
        }

        if (suppliers.Count == 0) throw new Exception($"Invalid node <{node.Name}>");

        return ctx => suppliers.Select(f => f(ctx)).Aggregate(combineOperation);
    }

    public static Supplier<bool, TC> ValueInRange<T, TC>(XmlNode node, Supplier<T, TC> supplier = null) where T : IComparable<T>, IEquatable<T>
    {
        if (node.HasSimpleTextValue())
        {
            if (supplier == null) throw new Exception($"Required element <value> is missing in node <{node.Name}>");
            var equalSupplier = XmlDynamicValue<T, TC>.SupplierSpecs.Build(node);
            return ctx => equalSupplier(ctx).Equals(supplier(ctx));
        }

        Supplier<T, TC> minSupplier = null;
        Supplier<T, TC> maxSupplier = null;

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            if (childNode.Name == "min")
            {
                if (minSupplier != null) throw new Exception($"There can only be one <{childNode.Name}> element in a <{node.Name}> node");
                minSupplier = XmlDynamicValue<T, TC>.SupplierSpecs.Build(childNode);
            }
            else if (childNode.Name == "max")
            {
                if (maxSupplier != null) throw new Exception($"There can only be one <{childNode.Name}> element in a <{node.Name}> node");
                maxSupplier = XmlDynamicValue<T, TC>.SupplierSpecs.Build(childNode);
            }
            else if (childNode.Name == "value")
            {
                supplier = XmlDynamicValue<T, TC>.SupplierSpecs.Build(childNode);
            }
            else
            {
                throw new Exception($"Unknown element <{childNode.Name}> in node <{node.Name}>");
            }
        }

        if (supplier == null) throw new Exception($"Required element <value> is missing in node <{node.Name}>");

        if (minSupplier != null && maxSupplier != null)
            return ctx => minSupplier(ctx).CompareTo(supplier(ctx)) <= 0 && maxSupplier(ctx).CompareTo(supplier(ctx)) >= 0;
        if (minSupplier != null)
            return ctx => minSupplier(ctx).CompareTo(supplier(ctx)) <= 0;
        if (maxSupplier != null)
            return ctx => maxSupplier(ctx).CompareTo(supplier(ctx)) >= 0;

        throw new Exception($"Invalid node <{node.Name}>");
    }

    public static Supplier<List<T>, TC> FixedList<T, TC>(XmlNode node, Func<XmlNode, T> elementLoadFunc)
    {
        if (node.HasSimpleTextValue()) throw new Exception($"Element <{node.Name}> must be a list node");

        List<T> elements = new();

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            elements.Add(elementLoadFunc(childNode));
        }

        return _ => elements;
    }

    #endregion

    #region Modifier Helpers

    public static Modifier<T, TC> DyadicModifier<T, TC>(XmlNode node, Func<T, T, T> dyadicOperation)
    {
        var operandSupplier = XmlDynamicValue<T, TC>.SupplierSpecs.Build(node);
        return (ctx, value) => dyadicOperation(value, operandSupplier(ctx));
    }

    public static Modifier<T, TC> DyadicModifierVerbose<T, TC>(XmlNode node, Func<T, T, T> dyadicOperation)
    {
        if (node.HasSimpleTextValue() || (typeof(ICollection).IsAssignableFrom(typeof(T)) && node.GetNamedChild("value", true) == null))
        {
            var supplier = XmlDynamicValue<T, TC>.SupplierSpecs.Build(node);
            return (ctx, value) => dyadicOperation(value, supplier(ctx));
        }

        Supplier<T, TC> operandSupplier = null;
        List<Modifier<T, TC>> operandModifiers = null;
        Supplier<bool, TC> conditions = null;

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            if (childNode.Name == "value")
            {
                if (operandSupplier != null) throw new Exception($"There can only be one <{childNode.Name}> element in a <{node.Name}> node");
                operandSupplier = XmlDynamicValue<T, TC>.SupplierSpecs.Build(childNode);
            }
            else if (childNode.Name == "modifier")
            {
                operandModifiers ??= new();
                operandModifiers.Add(XmlDynamicValue<T, TC>.ModifierSpecs.Build(childNode));
            }
            else if (childNode.Name == "conditions")
            {
                if (conditions != null) throw new Exception($"There can only be one <{childNode.Name}> element in a <{node.Name}> node");
                conditions = XmlDynamicValue<bool, TC>.SupplierSpecs.Build(childNode);
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
            return (ctx, value) => conditions(ctx) ? dyadicOperation(value, operandSupplier(ctx)) : value;
        }

        T ApplyModifiers(TC ctx, T op) => operandModifiers.Aggregate(op, (current, modifier) => modifier(ctx, current));

        if (conditions == null) return (ctx, value) => dyadicOperation(value, ApplyModifiers(ctx, operandSupplier(ctx)));
        return (ctx, value) => conditions(ctx) ? dyadicOperation(value, ApplyModifiers(ctx, operandSupplier(ctx))) : value;
    }

    public static Modifier<float, TC> InterpolationCurve<TC>(XmlNode node)
    {
        List<KeyValuePair<float, Supplier<float, TC>>> points = new();

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            var at = float.Parse(childNode.GetNamedChild("at").InnerText);
            var val = XmlDynamicValue<float, TC>.SupplierSpecs.Build(childNode.GetNamedChild("is"));
            points.Add(new KeyValuePair<float, Supplier<float, TC>>(at, val));
        }

        if (points.Count < 2) throw new Exception("At least two points must be defined for an interpolation");
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

    #endregion

    #region Spec Helpers

    public static Spec<T> Or<T>(this Spec<T> spec, Spec<T> other)
        => node => spec(node) ?? other(node);

    public static Spec<Supplier<T, TC>> FixedValue<T, TC>(Func<string, T> parser)
        => node => FixedValue<T, TC>(node, parser);

    public static Spec<Supplier<T, TC>> SupplierWithParam<T, TC>(Func<string, TC, T> supplier)
        => node => SupplierWithParam(node, supplier);

    public static Spec<Supplier<T, TC>> SupplierWithParam<T, TC, TE>(Func<TE, TC, T> supplier) where TE : struct
        => node => SupplierWithParam(node, supplier);

    public static Spec<Supplier<T, TC>> AggregateSupplierList<T, TC>(Func<T, T, T> combineOperation)
        => node => AggregateSupplierList<T, TC>(node, combineOperation);

    public static Spec<Supplier<bool, TC>> ValueInRange<T, TC>() where T : IComparable<T>, IEquatable<T>
        => node => ValueInRange<T, TC>(node);

    public static Spec<Supplier<bool, TC>> ValueInRange<T, TC>(Spec<Supplier<T, TC>> spec) where T : IComparable<T>, IEquatable<T>
        => spec == null ? null : node => ValueInRange(node, spec(node));

    public static Spec<Supplier<bool, TC>> ValueInRange<T, TC>(string name) where T : IComparable<T>, IEquatable<T>
        => ValueInRange(XmlDynamicValue<T, TC>.SupplierSpecs.Get(name));

    public static Spec<Supplier<T, TC>> Convert<T, TS, TC>(Spec<Supplier<TS, TC>> spec, Func<TS, T> convertFunc)
        => spec == null ? null : node => Convert(spec(node), convertFunc);

    public static Spec<Supplier<T, TC>> Convert<T, TS, TC>(string name, Func<TS, T> convertFunc)
        => Convert(XmlDynamicValue<TS, TC>.SupplierSpecs.Get(name), convertFunc);

    public static Func<string, Spec<Supplier<T, TC>>> Convert<T, TS, TC>(Func<TS, T> convertFunc)
        => name => Convert<T, TS, TC>(name, convertFunc);

    public static Spec<Supplier<List<T>, TC>> FixedList<T, TC>(Func<XmlNode, T> elementLoadFunc)
        => node => FixedList<T, TC>(node, elementLoadFunc);

    public static Spec<Modifier<T, TC>> DyadicModifier<T, TC>(Func<T, T, T> dyadicOperation)
        => node => DyadicModifier<T, TC>(node, dyadicOperation);

    public static Spec<Modifier<T, TC>> DyadicModifierVerbose<T, TC>(Func<T, T, T> dyadicOperation)
        => node => DyadicModifierVerbose<T, TC>(node, dyadicOperation);

    #endregion

    #region Common Helper Methods for Text and XML processing

    public static XmlNode GetNamedChild(this XmlNode node, string name, bool optional = false)
    {
        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            if (childNode.Name == name) return childNode;
        }

        if (optional) return null;
        throw new Exception($"Node <{node.Name}> is missing required element <{name}>");
    }

    public static T GetNamedChild<T>(this XmlNode node, string name, Func<string, T> parser, T defaultValue)
    {
        var element = node.GetNamedChild(name, true);
        return element == null ? defaultValue : parser(element.InnerText);
    }

    public static T GetNamedChild<T>(this XmlNode node, string name, Func<string, T> parser)
        => parser(node.GetNamedChild(name).InnerText);

    public static bool HasSimpleTextValue(this XmlNode node)
        => node.ChildNodes.Count == 1 && node.FirstChild.NodeType == XmlNodeType.Text;

    public static string AppendPar(string a, string b)
        => a.EndsWith(")") ? a.Substring(0, a.Length - 1) + ", " + b + ")" : a + " (" + b + ")";

    public static string PrependPar(string a, string b)
        => a.EndsWith(")") ? a.Substring(0, a.Length - 1) + ", " + b + ")" : b + " (" + a + ")";

    #endregion
}
