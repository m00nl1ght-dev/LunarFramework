using System;
using System.Xml;

namespace LunarFramework.XML;

[Serializable]
public abstract class XmlDynamicValue<T, TC>
{
    protected abstract XmlDynamicValueSpec<T, TC> RootSpec { get; }

    private Func<TC, T, T> _root;

    public T Get(TC context, T baseValue = default)
    {
        return _root == null ? baseValue : _root(context, baseValue);
    }

    public void LoadDataFromXmlCustom(XmlNode xmlRoot)
    {
        _root = RootSpec.BuildModifier(xmlRoot);
    }
}
