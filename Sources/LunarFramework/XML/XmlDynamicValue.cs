using System.Xml;

namespace LunarFramework.XML;

public class XmlDynamicValue<T, TC>
{
    public static XmlDynamicValueSpecs<Supplier<T, TC>> SupplierSpecs { get; } = new() { NameAttribute = "supplier" };
    public static XmlDynamicValueSpecs<Modifier<T, TC>> ModifierSpecs { get; } = new() { NameAttribute = "operation" };

    private Modifier<T, TC> _root;

    public XmlDynamicValue() { }

    public XmlDynamicValue(Modifier<T, TC> root)
    {
        _root = root;
    }

    public XmlDynamicValue(Supplier<T, TC> root)
    {
        _root = (ctx, _) => root(ctx);
    }

    public XmlDynamicValue(T root)
    {
        _root = (_, _) => root;
    }

    public T Get(TC context, T baseValue = default)
    {
        return _root == null ? baseValue : _root(context, baseValue);
    }

    public void Apply(TC context, ref T value)
    {
        value = Get(context, value);
    }

    public void LoadDataFromXmlCustom(XmlNode xmlRoot)
    {
        _root = ModifierSpecs.Build(xmlRoot);
    }
}

public delegate T Supplier<out T, in TC>(TC context);

public delegate T Modifier<T, in TC>(TC context, T value);

public delegate T Spec<out T>(XmlNode node);
