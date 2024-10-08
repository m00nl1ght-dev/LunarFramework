using System;
using System.Collections.Generic;
using System.Linq;
using LunarFramework.Internal.Patches;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace LunarFramework.GUI;

public static class LunarGUI
{
    private static readonly object ColorBufferTex = new Texture2D(1, 1);
    private static Texture2D ColorBuffer => (Texture2D) ColorBufferTex;

    public static void BeginScrollView(Rect frameRect, ref Rect viewRect, ref Vector2 scrollPosition)
    {
        var fitsView = viewRect.height <= frameRect.height;
        viewRect = new(0f, 0f, frameRect.width - (fitsView ? 0f : 25f), Mathf.Max(viewRect.height, frameRect.height));
        Widgets.BeginScrollView(frameRect, ref scrollPosition, viewRect);
    }

    public static void EndScrollView()
    {
        Widgets.EndScrollView();
    }

    public static bool Button(LayoutRect layout, string label, string tooltip = null)
        => Button(layout.Abs(layout.Horizontal ? -1f : 28f), label, tooltip);

    public static bool Button(Rect rect, string label, string tooltip = null)
    {
        if (LanguageDatabase.activeLanguage != LanguageDatabase.defaultLanguage) tooltip ??= label;
        if (tooltip != null) TooltipHandler.TipRegion(rect, tooltip);
        return Widgets.ButtonText(rect, label);
    }

    public static void Checkbox(Rect rect, ref bool value, bool paintable = false)
    {
        var before = value;
        Widgets.Checkbox(rect.x, rect.y, ref value, rect.height, !UnityEngine.GUI.enabled, paintable);
        if (value != before) UnityEngine.GUI.changed = true;
    }

    public static void Checkbox(LayoutRect layout, ref bool value, string label, string tooltip = null)
        => Checkbox(layout.Abs(layout.Horizontal ? -1f : Text.LineHeight), ref value, label, tooltip);

    public static void Checkbox(Rect rect, ref bool value, string label, string tooltip = null)
    {
        if (LanguageDatabase.activeLanguage != LanguageDatabase.defaultLanguage) tooltip ??= label;
        if (tooltip != null) TooltipHandler.TipRegion(rect, tooltip);

        var before = value;
        HighlightOnHover(rect);
        Widgets.CheckboxLabeled(rect, label, ref value, !UnityEngine.GUI.enabled);
        if (value != before) UnityEngine.GUI.changed = true;
    }

    public static void TextField(LayoutRect layout, ref string value)
        => TextField(layout.Abs(layout.Horizontal ? -1f : 28f), ref value);

    public static void TextField(Rect rect, ref string value)
        => value = Widgets.TextField(rect, value);

    public static void IntField(LayoutRect layout, ref int value, ref string editBuffer, int min, int max)
        => IntField(layout.Abs(layout.Horizontal ? -1f : 28f), ref value, ref editBuffer, min, max);

    public static void IntField(Rect rect, ref int value, ref string editBuffer, int min, int max)
        => Widgets.TextFieldNumeric(rect, ref value, ref editBuffer, min, max);

    public static void Slider(LayoutRect layout, ref float value, float min, float max)
        => Slider(layout.Abs(layout.Horizontal ? -1f : 22f), ref value, min, max);

    public static void Slider(Rect rect, ref float value, float min, float max)
    {
        #if RW_1_4
        var newValue = Widgets.HorizontalSlider_NewTemp(rect, value, min, max);
        #else
        var newValue = Widgets.HorizontalSlider(rect, value, min, max);
        #endif

        if (newValue != value) UnityEngine.GUI.changed = true;
        value = newValue;
    }

    public static void Slider(LayoutRect layout, ref int value, int min, int max)
        => Slider(layout.Abs(layout.Horizontal ? -1f : 22f), ref value, min, max);

    public static void Slider(Rect rect, ref int value, int min, int max)
    {
        float intval = value;
        Slider(rect, ref intval, min, max);
        value = (int) intval;
    }

    public static void RangeSlider(LayoutRect layout, ref FloatRange floatRange, float min, float max)
        => RangeSlider(layout.Abs(layout.Horizontal ? -1f : 28f), ref floatRange, min, max);

    public static void RangeSlider(Rect rect, ref FloatRange floatRange, float min, float max)
    {
        var before = floatRange;
        Widgets.FloatRange(rect.ContractedBy(2f, 0f), rect.GetHashCode(), ref floatRange, min, max);
        if (floatRange != before) UnityEngine.GUI.changed = true;
    }

    public static void RangeSlider(LayoutRect layout, ref FloatRange floatRange, float min, float max, Func<float, string> toString)
        => RangeSlider(layout.Abs(layout.Horizontal ? -1f : 28f), ref floatRange, min, max, toString);

    public static void RangeSlider(Rect rect, ref FloatRange floatRange, float min, float max, Func<float, string> toString)
    {
        var before = floatRange;
        Patch_Verse_Widgets.FloatRange_Custom(rect.ContractedBy(2f, 0f), rect.GetHashCode(), ref floatRange, min, max, toString);
        if (floatRange != before) UnityEngine.GUI.changed = true;
    }

    public static void Label(LayoutRect layout, string label, string tooltip = null)
        => Label(layout.Abs(layout.Horizontal ? Text.CalcSize(label).x : Text.CalcSize(label).y), label, tooltip);

    public static void Label(Rect rect, string label, string tooltip = null)
    {
        if (tooltip != null) TooltipHandler.TipRegion(rect, tooltip);
        Widgets.Label(rect, label);
    }

    public static void LabelCentered(LayoutRect layout, string labelCenter, string tooltip = null)
    {
        var labelCenterSize = Text.CalcSize(labelCenter);
        var rect = layout.Abs(layout.Horizontal ? -1f : labelCenterSize.y);
        var centerRect = rect.RightHalf();
        centerRect.xMin -= labelCenterSize.x * 0.5f;
        if (tooltip != null) TooltipHandler.TipRegion(rect, tooltip);
        Widgets.Label(centerRect, labelCenter);
    }

    public static void LabelDouble(LayoutRect layout, string labelLeft, string labelCenter, bool centered = true, string tooltip = null)
    {
        var labelLeftSize = Text.CalcSize(labelLeft);
        var labelCenterSize = Text.CalcSize(labelCenter);
        var rect = layout.Abs(layout.Horizontal ? -1f : Mathf.Max(labelLeftSize.y, labelCenterSize.y));
        var leftRect = rect.LeftHalf();
        var centerRect = rect.RightHalf();
        leftRect.width = labelLeftSize.x;
        if (centered) centerRect.xMin -= labelCenterSize.x * 0.5f;
        if (tooltip != null) TooltipHandler.TipRegion(rect, tooltip);
        Widgets.Label(leftRect, labelLeft);
        Widgets.Label(centerRect, labelCenter);
    }

    public static void Dropdown<T>(LayoutRect layout, T value, List<T> potentialValues, Action<T> action, Func<T, string> textFunc = null)
        => Dropdown(layout, value, potentialValues.AsEnumerable(), action, textFunc);

    public static void Dropdown<T>(LayoutRect layout, T value, IEnumerable<T> potentialValues, Action<T> action, Func<T, string> textFunc = null)
        => Dropdown(layout.Abs(layout.Horizontal ? -1f : 28f), value, potentialValues, action, textFunc);

    public static void Dropdown<T>(Rect rect, T value, List<T> potentialValues, Action<T> action, Func<T, string> textFunc = null)
        => Dropdown(rect, value, potentialValues.AsEnumerable(), action, textFunc);

    public static void Dropdown<T>(Rect rect, T value, IEnumerable<T> potentialValues, Action<T> action, Func<T, string> textFunc = null)
    {
        textFunc ??= o => o.ToString();
        if (Widgets.ButtonText(rect, textFunc.Invoke(value)))
        {
            var options = potentialValues
                .Select(e => new FloatMenuOption(textFunc.Invoke(e), () =>
                {
                    action(e);
                    UnityEngine.GUI.changed = true;
                }))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    public static void Dropdown<T>(LayoutRect layout, T value, Action<T> action, string translationKeyPrefix = null) where T : Enum
        => Dropdown(layout.Abs(layout.Horizontal ? -1f : 28f), value, action, translationKeyPrefix);

    public static void Dropdown<T>(Rect rect, T value, Action<T> action, string translationKeyPrefix = null) where T : Enum
    {
        Dropdown(
            rect, value, typeof(T).GetEnumValues().Cast<T>().ToList(), action,
            translationKeyPrefix == null ? null : e => (translationKeyPrefix + "." + e).Translate()
        );
    }

    public static void ToggleTableRow<T>(LayoutRect layout, T item, bool inverted, string label, params List<T>[] toggles)
    {
        layout.BeginAbs(Text.LineHeight, new LayoutParams { Horizontal = true, Reversed = true });

        HighlightOnHover(layout);

        foreach (var toggle in toggles.Reverse())
        {
            layout.PushChanged();
            layout.PushEnabled(toggle != null);

            var enabled = toggle != null && (toggle.Contains(item) ^ inverted);
            Checkbox(layout.Abs(Text.LineHeight), ref enabled, true);

            layout.PopEnabled();

            if (layout.PopChanged() && toggle != null)
            {
                if (toggle.Contains(item)) toggle.Remove(item);
                else toggle.Add(item);
            }

            layout.Abs(5f);
        }

        var labelRect = layout.Abs(-1f);
        Label(labelRect, label);

        if (UnityEngine.GUI.enabled && Widgets.ButtonInvisible(labelRect))
        {
            var anyWereOn = false;
            foreach (var toggle in toggles)
                if (toggle != null && toggle.Remove(item))
                    anyWereOn = true;
            if (!anyWereOn)
                foreach (var toggle in toggles)
                    toggle?.Add(item);
            PlayToggleSound(!anyWereOn);
            UnityEngine.GUI.changed = true;
        }

        layout.End();
    }

    public static void HighlightOnHover(Rect rect)
    {
        if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
    }

    public static void PlayToggleSound(bool on)
    {
        (on ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
    }

    public static void SeparatorLine(LayoutRect layout, float height = 1f)
    {
        var sep = layout.Abs(height);
        Widgets.DrawLineHorizontal(sep.x, sep.y, sep.width);
    }

    public static void DrawQuad(Rect quad, Color color)
    {
        ColorBuffer.wrapMode = TextureWrapMode.Repeat;
        ColorBuffer.SetPixel(0, 0, color);
        ColorBuffer.Apply();
        UnityEngine.GUI.DrawTexture(quad, ColorBuffer);
    }

    public static Window OpenGenericWindow(LunarAPI component, Vector2 size, Action<Window, LayoutRect> onGUI)
    {
        var window = new GenericWindow(component, size, onGUI);
        Find.WindowStack.Add(window);
        return window;
    }

    private class GenericWindow : Window
    {
        public override Vector2 InitialSize { get; }
        public Action<Window, LayoutRect> OnGUI { get; }

        public LayoutParams LayoutParams { get; set; } = new() { Spacing = 5f };

        private readonly LayoutRect _layout;
        private Vector2 _scrollPos;
        private Rect _viewRect;

        public GenericWindow(LunarAPI component, Vector2 initialSize, Action<Window, LayoutRect> onGUI)
        {
            _layout = new LayoutRect(component);
            InitialSize = initialSize;
            OnGUI = onGUI;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            BeginScrollView(rect, ref _viewRect, ref _scrollPos);
            _layout.BeginRoot(_viewRect, LayoutParams);
            OnGUI(this, _layout);
            _viewRect.height = Mathf.Max(_viewRect.height, _layout.OccupiedSpace);
            _layout.End();
            EndScrollView();
        }
    }
}
