using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace LunarFramework.XML;

public static class XmlDynamicValueSupport
{
    public static Func<TC, T, T> BuildDyadicModifier<TC, T>(XmlNode node, XmlDynamicValueSpec<T, TC> spec, Func<T, T, T> dyadicOperation)
    {
        if (node.ChildNodes.Count == 0)
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
}
