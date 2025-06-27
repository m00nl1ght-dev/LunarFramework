using System;
using System.Collections.Generic;

#if RW_1_6_OR_GREATER
using Verse;
#else
using System.IO;
using System.Xml.Serialization;
#endif

namespace LunarFramework.Bootstrap;

[Serializable]
public class Manifest
{
    public string Name;
    public string PackageId;
    public string Authors;

    public string MinGameVersion;

    public CompatibilityList Compatibility;

    public List<Component> Components = new();

    internal static Manifest ReadFromFile(string file)
    {
        #if RW_1_6_OR_GREATER

        return DirectXmlLoader.ItemFromXmlFile<Manifest>(file);

        #else

        var serializer = new XmlSerializer(typeof(Manifest));
        using var reader = new StreamReader(file);
        return (Manifest) serializer.Deserialize(reader);

        #endif
    }

    [Serializable]
    public class CompatibilityList
    {
        public List<Entry> Lunar;
        public List<Entry> Refuse;
    }

    [Serializable]
    public class Entry
    {
        public string PackageId;
        public string MinVersion;
    }

    [Serializable]
    public class Component
    {
        public string AssemblyName;
        public bool AllowNonLunarSource;
        public List<string> Aliases;
        public List<string> DependsOn;
    }
}
