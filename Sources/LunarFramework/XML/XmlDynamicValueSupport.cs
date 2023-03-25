using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
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
    public static readonly Func<bool, bool> FuncNot = a => !a;

    public static readonly Func<float, float> FuncZero = _ => 0f;
    public static readonly Func<float, float> FuncOne = _ => 1f;

    public static T FuncIdentity<T>(T a) => a;
    public static T FuncReplace<T>(T a, T b) => b;

    public static List<T> FuncUnion<T>(List<T> a, List<T> b) => b.Union(a).ToList();
    public static List<T> FuncExcept<T>(List<T> a, List<T> b) => b.Except(a).ToList();
    public static List<T> FuncIntersect<T>(List<T> a, List<T> b) => b.Intersect(a).ToList();

    #endregion

    #region Registration Helpers

    public static XmlDynamicValueSpecs<Supplier<T, TC>> DefaultSupplierSpecs<T, TC>()
    {
        var specs = new XmlDynamicValueSpecs<Supplier<T, TC>> { NameAttribute = "supplier" };

        if (specs is XmlDynamicValueSpecs<Supplier<float, TC>> sNum)
        {
            sNum.DefaultSpec = FixedValue<float, TC>(float.Parse);
            sNum.Register("sum", AggregateSupplierList<float, TC>(FuncAdd));
            sNum.Register("product", AggregateSupplierList<float, TC>(FuncMultiply));
        }
        else if (specs is XmlDynamicValueSpecs<Supplier<string, TC>> sStr)
        {
            sStr.DefaultSpec = FixedValue<string, TC>(s => s);
            sStr.Register("translate", SupplierWithParam<string, TC>((s, _) => s.Translate()));
            sStr.RegisterFallback(Transform<string, float, TC>(v => v.ToString("0.##")));
        }
        else if (specs is XmlDynamicValueSpecs<Supplier<bool, TC>> sBool)
        {
            var allOf = AggregateSupplierList<bool, TC>(FuncAnd);
            var anyOf = AggregateSupplierList<bool, TC>(FuncOr);
            var noneOf = Transform(anyOf, FuncNot);
            
            sBool.DefaultSpec = FixedValue<bool, TC>(bool.Parse).Or(allOf);
            
            sBool.Register("allOf", allOf);
            sBool.Register("anyOf", anyOf);
            sBool.Register("noneOf", noneOf);
            sBool.Register("not", noneOf);

            sBool.Register("valueInRange", ValueInRange<float, TC>());
            sBool.RegisterFallback(ValueInRange<float, TC>);
        }
        else if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = typeof(T).GetGenericArguments()[0];
            var method = MethodDls.MakeGenericMethod(elementType, typeof(TC));
            method.Invoke(null, new object[] { specs });
        }
        else if (GenTypes.IsDef(typeof(T)))
        {
            specs.DefaultSpec = FixedDef<T, TC>;
        }
        else if (ParseHelper.HandlesType(typeof(T)))
        {
            specs.DefaultSpec = FixedValue<T, TC>(ParseHelper.FromString<T>);
        }
        else
        {
            specs.DefaultSpec = node => FixedValue<T, TC>(DirectXmlToObject.ObjectFromXml<T>(node, false));
        }

        specs.Register("select", SelectValue<T, TC>);

        return specs;
    }

    public static XmlDynamicValueSpecs<Modifier<T, TC>> DefaultModifierSpecs<T, TC>()
    {
        var specs = new XmlDynamicValueSpecs<Modifier<T, TC>> { NameAttribute = "operation" };

        if (specs is XmlDynamicValueSpecs<Modifier<float, TC>> sNum)
        {
            sNum.DefaultSpec = DyadicModifierVerbose<float, TC>(FuncReplace, FuncIdentity);
            sNum.Register("add", DyadicModifierVerbose<float, TC>(FuncAdd, FuncZero));
            sNum.Register("multiply", DyadicModifierVerbose<float, TC>(FuncMultiply, FuncOne));
            sNum.Register("subtract", DyadicModifierVerbose<float, TC>(FuncSubtract, FuncZero));
            sNum.Register("divide", DyadicModifierVerbose<float, TC>(FuncDivideSafely, FuncOne));
            sNum.Register("curve", InterpolationCurve<TC>);
            sNum.Register("replace", sNum.DefaultSpec);
        }
        else if (specs is XmlDynamicValueSpecs<Modifier<string, TC>> sStr)
        {
            sStr.DefaultSpec = DyadicModifierVerbose<string, TC>(FuncReplace, FuncIdentity);
            sStr.Register("append", DyadicModifierVerbose<string, TC>(FuncAppend));
            sStr.Register("prepend", DyadicModifierVerbose<string, TC>(FuncPrepend));
            sStr.Register("appendLine", DyadicModifierVerbose<string, TC>(FuncAppendLine));
            sStr.Register("prependLine", DyadicModifierVerbose<string, TC>(FuncPrependLine));
            sStr.Register("appendPar", DyadicModifierVerbose<string, TC>(FuncAppendPar));
            sStr.Register("prependPar", DyadicModifierVerbose<string, TC>(FuncPrependPar));
            sStr.Register("replace", sStr.DefaultSpec);
        }
        else if (specs is XmlDynamicValueSpecs<Modifier<bool, TC>> sBool)
        {
            sBool.DefaultSpec = DyadicModifier<bool, TC>(FuncReplace);
            sBool.Register("and", DyadicModifier<bool, TC>(FuncAnd));
            sBool.Register("or", DyadicModifier<bool, TC>(FuncOr));
            sBool.Register("replace", sBool.DefaultSpec);
        }
        else if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = typeof(T).GetGenericArguments()[0];
            var method = MethodDlm.MakeGenericMethod(elementType, typeof(TC));
            method.Invoke(null, new object[] { specs });
        }
        else
        {
            specs.DefaultSpec = DyadicModifierVerbose<T, TC>(FuncReplace, FuncIdentity);
        }

        return specs;
    }

    private static readonly MethodInfo MethodDls = AccessTools.Method(typeof(XmlDynamicValueSupport), nameof(DefaultListSuppliers));
    private static readonly MethodInfo MethodDlm = AccessTools.Method(typeof(XmlDynamicValueSupport), nameof(DefaultListModifiers));

    private static void DefaultListSuppliers<T, TC>(this XmlDynamicValueSpecs<Supplier<List<T>, TC>> specs)
    {
        specs.DefaultSpec = SupplierList<T, TC>;
    }

    private static void DefaultListModifiers<T, TC>(this XmlDynamicValueSpecs<Modifier<List<T>, TC>> specs)
    {
        specs.DefaultSpec = DyadicModifierVerbose<List<T>, TC>(FuncReplace);
        specs.Register("add", DyadicModifierVerbose<List<T>, TC>(FuncUnion));
        specs.Register("remove", DyadicModifierVerbose<List<T>, TC>(FuncExcept));
        specs.Register("intersect", DyadicModifierVerbose<List<T>, TC>(FuncIntersect));
        specs.Register("replace", specs.DefaultSpec);
    }

    public static void Register<T, TC>(this XmlDynamicValueSpecs<Supplier<T, TC>> specs, string name, Supplier<T, TC> supplier)
        => specs.Register(name, _ => supplier);

    public static void Register<T, TC>(this XmlDynamicValueSpecs<Modifier<T, TC>> specs, string name, Modifier<T, TC> supplier)
        => specs.Register(name, _ => supplier);

    #endregion

    #region Supplier Helpers

    public static Supplier<T, TC> DefaultValue<T, TC>() => _ => default;

    public static Supplier<T, TC> FixedValue<T, TC>(T value) => _ => value;

    public static Supplier<T, TC> FixedValue<T, TC>(XmlNode node, Func<string, T> parser)
    {
        if (!node.HasSimpleTextValue()) return null;
        var value = parser(node.InnerText);
        return _ => value;
    }

    public static Supplier<T, TC> Transform<T, TS, TC>(Supplier<TS, TC> supplier, Func<TS, T> convertFunc)
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

    public static Supplier<List<T>, TC> SupplierList<T, TC>(XmlNode node)
    {
        if (node.HasSimpleTextValue())
        {
            var supplier = XmlDynamicValue<T, TC>.SupplierSpecs.Build(node);
            return ctx => new List<T> { supplier(ctx) };
        }

        List<Supplier<T, TC>> suppliers = new();

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            suppliers.Add(XmlDynamicValue<T, TC>.SupplierSpecs.Build(childNode));
        }

        return ctx => suppliers.Select(f => f(ctx)).ToList();
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

    public static Supplier<T, TC> SelectValue<T, TC>(XmlNode node)
    {
        if (node.HasSimpleTextValue()) throw new Exception($"Invalid select supplier node <{node.Name}>");

        List<(Supplier<T, TC>, Supplier<bool, TC>)> options = new();

        Supplier<T, TC> defaultSupplier = null;

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];

            if (childNode.Name == "option")
            {
                var valueNode = childNode.GetNamedChild("value");
                var valueSupplier = XmlDynamicValue<T, TC>.SupplierSpecs.Build(valueNode);

                var conditionsNode = childNode.GetNamedChild("conditions");
                var conditionsSupplier = XmlDynamicValue<bool, TC>.SupplierSpecs.Build(conditionsNode);

                options.Add((valueSupplier, conditionsSupplier));
            }
            else if (childNode.Name == "default")
            {
                if (defaultSupplier != null) throw new Exception($"There can only be one <{childNode.Name}> element in a <{node.Name}> node");
                defaultSupplier = XmlDynamicValue<T, TC>.SupplierSpecs.Build(childNode);
            }
            else
            {
                throw new Exception($"Unknown element <{childNode.Name}> in node <{node.Name}>");
            }
        }

        defaultSupplier ??= DefaultValue<T, TC>();

        return ctx =>
        {
            foreach (var (supplier, conditions) in options)
                if (conditions(ctx))
                    return supplier(ctx);

            return defaultSupplier(ctx);
        };
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

    public static Supplier<T, TC> FixedDef<T, TC>(XmlNode node)
    {
        if (!node.HasSimpleTextValue()) return null;
        var holder = new DefHolder<T>(node);
        return _ => holder.value;
    }

    public static Supplier<List<T>, TC> FixedDefList<T, TC>(XmlNode node)
    {
        if (node.HasSimpleTextValue())
        {
            var holder = new DefHolder<T>(node);
            return _ => new List<T> { holder.value };
        }

        List<T> elements = new();

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            string req = childNode.Attributes?["MayRequire"]?.Value.ToLower();
            string reqAny = childNode.Attributes?["MayRequireAnyOf"]?.Value.ToLower();
            DirectXmlCrossRefLoader.RegisterListWantsCrossRef(elements, childNode.InnerText, node.Name, req, reqAny);
        }

        return _ => elements;
    }

    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
    private class DefHolder<T>
    {
        public T value = default;

        public DefHolder(XmlNode node)
        {
            string req = node.Attributes?["MayRequire"]?.Value.ToLower();
            string reqAny = node.Attributes?["MayRequireAnyOf"]?.Value.ToLower();
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, nameof(value), node.InnerText, req, reqAny, typeof(T));
        }
    }

    #endregion

    #region Modifier Helpers

    public static Modifier<T, TC> DyadicModifier<T, TC>(XmlNode node, Func<T, T, T> dyadicOperation)
    {
        var operandSupplier = XmlDynamicValue<T, TC>.SupplierSpecs.Build(node);
        return (ctx, value) => dyadicOperation(value, operandSupplier(ctx));
    }

    public static Modifier<T, TC> DyadicModifierVerbose<T, TC>(XmlNode node, Func<T, T, T> dyadicOperation, Func<T, T> neutralOperand = null)
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

        if (operandSupplier == null && (operandModifiers == null || neutralOperand == null))
        {
            throw new Exception($"Required element <value> is missing in node <{node.Name}>");
        }

        if (operandModifiers == null)
        {
            if (conditions == null) return (ctx, value) => dyadicOperation(value, operandSupplier(ctx));
            return (ctx, value) => conditions(ctx) ? dyadicOperation(value, operandSupplier(ctx)) : value;
        }

        T ApplyModifiers(TC ctx, T value)
        {
            var baseOperand = operandSupplier != null ? operandSupplier(ctx) : neutralOperand(value);
            return operandModifiers.Aggregate(baseOperand, (current, modifier) => modifier(ctx, current));
        }

        if (conditions == null) return (ctx, value) => dyadicOperation(value, ApplyModifiers(ctx, value));
        return (ctx, value) => conditions(ctx) ? dyadicOperation(value, ApplyModifiers(ctx, value)) : value;
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

    public static Spec<Supplier<T, TC>> Transform<T, TS, TC>(Spec<Supplier<TS, TC>> spec, Func<TS, T> convertFunc)
        => spec == null ? null : node => Transform(spec(node), convertFunc);

    public static Spec<Supplier<T, TC>> Transform<T, TS, TC>(string name, Func<TS, T> convertFunc)
        => Transform(XmlDynamicValue<TS, TC>.SupplierSpecs.Get(name), convertFunc);

    public static Func<string, Spec<Supplier<T, TC>>> Transform<T, TS, TC>(Func<TS, T> convertFunc)
        => name => Transform<T, TS, TC>(name, convertFunc);

    public static Spec<Modifier<T, TC>> DyadicModifier<T, TC>(Func<T, T, T> dyadicOperation)
        => node => DyadicModifier<T, TC>(node, dyadicOperation);

    public static Spec<Modifier<T, TC>> DyadicModifierVerbose<T, TC>(Func<T, T, T> dyadicOperation, Func<T, T> neutralOperand = null)
        => node => DyadicModifierVerbose<T, TC>(node, dyadicOperation, neutralOperand);

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
