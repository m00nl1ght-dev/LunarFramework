using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace LunarFramework.Bootstrap;

[Serializable]
public class Manifest
{
    public string Name { get; set; }
    public string PackageId { get; set; }
    public string Authors { get; set; }

    public CompatibilityList Compatibility { get; set; }

    public List<Component> Components { get; set; } = new();
    
    internal static Manifest ReadFromFile(string file)
    {
        var serializer = new XmlSerializer(typeof(Manifest));
        using var reader = new StreamReader(file);
        return (Manifest) serializer.Deserialize(reader);
    }
    
    internal static void WriteToFile(Manifest manifest, string file)
    {
        var serializer = new XmlSerializer(typeof(Manifest));
        using var writer = new StreamWriter(file);
        serializer.Serialize(writer, manifest);
    }

    [Serializable]
    public struct CompatibilityList
    {
        public List<CompatibilityEntry> Lunar;
        public List<CompatibilityEntry> Refuse;
    }
    
    [Serializable]
    public struct CompatibilityEntry
    {
        public string PackageId;
        public string MinVersion;
    }

    [Serializable]
    public struct Component
    {
        public string AssemblyName { get; set; }
        public bool AllowNonLunarSource { get; set; }
        public List<string> Aliases { get; set; }
    }
}