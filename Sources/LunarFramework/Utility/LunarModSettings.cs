using System;
using System.Collections.Generic;
using System.Linq;
using LunarFramework.GUI;
using UnityEngine;
using Verse;

namespace LunarFramework.Utility;

public abstract class LunarModSettings : ModSettings
{
    public IReadOnlyDictionary<string, Entry> Entries => _entries;

    protected virtual LayoutParams LayoutParams => new() { Spacing = 10f };

    protected virtual string TranslationKeyPrefix => "";

    private readonly Dictionary<string, Entry> _entries = new();
    private readonly List<Tab> _tabs = new();

    private readonly LunarAPI _component;
    private readonly LayoutRect _layout;

    private List<TabRecord> _tabRecords;
    private Vector2 _scrollPos;
    private Rect _viewRect;
    private Tab _tab;

    protected LunarModSettings(LunarAPI component)
    {
        _component = component;
        _layout = new LayoutRect(component);

        var fields = GetType().GetFields().Where(f => f.FieldType.IsSubclassOf(typeof(Entry)));

        foreach (var field in fields)
        {
            if (!field.IsInitOnly) throw new Exception($"Entry field {field.Name} is not readonly");
            var entry = (Entry) field.GetValue(this);
            if (entry == null) throw new Exception($"Entry field {field.Name} has null value");
            _entries.Add(field.Name, entry);
            entry.Name = field.Name;
        }
    }

    protected static Entry<T> MakeEntry<T>(T defaultValue) => new ValueEntry<T>(defaultValue);

    protected static Entry<List<T>> MakeEntry<T>(List<T> defaultValue) => new ListEntry<T>(defaultValue);

    protected static Entry<Dictionary<K, V>> MakeEntry<K, V>(Dictionary<K, V> defaultValue) => new DictionaryEntry<K, V>(defaultValue);

    protected Tab MakeTab(string name, Action<LayoutRect> content, Func<bool> condition = null)
    {
        var tab = new Tab(name, content, condition);
        _tabs.Add(tab);
        _tab ??= tab;
        return tab;
    }

    public void DoSettingsWindowContents(Rect rect)
    {
        if (!_component.IsInitialized())
        {
            _layout.BeginRoot(rect);
            LunarGUI.Label(_layout, "An error occured while loading this mod. Check the log file for more information.");
            _layout.End();
            return;
        }

        void SelectTab(Tab tab)
        {
            if (_tab != tab)
            {
                _viewRect.height = rect.height - 90f;
                _tab = tab;
            }
        }

        _tabRecords ??= _tabs
            .Where(tab => tab.Condition == null || tab.Condition())
            .Select(tab => new TabRecord(Label(tab.LabelTk), () => SelectTab(tab), () => _tab == tab)).ToList();

        var innerRect = rect;

        innerRect.yMin += 35;
        innerRect.yMax -= 12;

        Widgets.DrawMenuSection(innerRect);
        TabDrawer.DrawTabs(innerRect, _tabRecords);

        innerRect = innerRect.ContractedBy(18f);

        LunarGUI.BeginScrollView(innerRect, ref _viewRect, ref _scrollPos);
        _layout.BeginRoot(_viewRect, LayoutParams);
        _tab?.Content(_layout);
        _viewRect.height = Mathf.Max(_viewRect.height, _layout.OccupiedSpace);
        _layout.End();
        LunarGUI.EndScrollView();
    }

    protected virtual string Label(string translationKey) => (TranslationKeyPrefix + "." + translationKey).Translate();

    public abstract class Entry : IExposable
    {
        public string Name { get; internal set; }

        public abstract void ExposeData();
        public abstract void Reset();
    }

    public abstract class Entry<T> : Entry
    {
        public T Value;

        public T DefaultValue { get; }

        public static implicit operator T(Entry<T> entry) => entry.Value;

        protected Entry(T defaultValue)
        {
            DefaultValue = defaultValue;
            Value = DefaultValue;
        }

        public override void Reset()
        {
            Value = DefaultValue;
        }
    }

    public class ValueEntry<T> : Entry<T>
    {
        internal ValueEntry(T defaultValue) : base(defaultValue) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Value, Name, DefaultValue);
        }
    }

    public class ListEntry<T> : Entry<List<T>>
    {
        internal ListEntry(List<T> defaultValue) : base(defaultValue)
        {
            Value = NewDefaultInstance();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref Value, Name, LookMode.Value);
            Value ??= NewDefaultInstance();
        }

        public override void Reset()
        {
            Value = NewDefaultInstance();
        }

        private List<T> NewDefaultInstance()
        {
            return DefaultValue == null ? null : new List<T>(DefaultValue);
        }
    }

    public class DictionaryEntry<K, V> : Entry<Dictionary<K, V>>
    {
        internal DictionaryEntry(Dictionary<K, V> defaultValue) : base(defaultValue)
        {
            Value = NewDefaultInstance();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref Value, Name, LookMode.Value);
            Value ??= NewDefaultInstance();
        }

        public override void Reset()
        {
            Value = NewDefaultInstance();
        }

        private Dictionary<K, V> NewDefaultInstance()
        {
            return DefaultValue == null ? null : new Dictionary<K, V>(DefaultValue);
        }
    }

    public class Tab
    {
        public string LabelTk { get; }

        internal readonly Action<LayoutRect> Content;
        internal readonly Func<bool> Condition;

        internal Tab(string labelTk, Action<LayoutRect> content, Func<bool> condition = null)
        {
            LabelTk = labelTk;
            Content = content;
            Condition = condition;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        foreach (var entry in Entries.Values) entry.ExposeData();
        _tabRecords = null;
    }

    public virtual void ResetAll()
    {
        foreach (var entry in Entries.Values) entry.Reset();
    }
}
