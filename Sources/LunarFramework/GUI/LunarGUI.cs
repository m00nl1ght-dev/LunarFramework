using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace LunarFramework.GUI;

public static class LunarGUI
{
    private static readonly object ColorBufferTex = new Texture2D(1, 1);
    private static Texture2D ColorBuffer => (Texture2D) ColorBufferTex;

    private static bool _wasChanged;
    private static bool _wasEnabled = true;

    public static void PushChanged(bool changed = false)
    {
        _wasChanged = UnityEngine.GUI.changed;
        UnityEngine.GUI.changed = changed;
    }
    
    public static bool PopChanged()
    {
        var changed = UnityEngine.GUI.changed;
        UnityEngine.GUI.changed = changed || _wasChanged;
        return changed;
    }
    
    public static void PushEnabled(bool enabled)
    {
        _wasEnabled = UnityEngine.GUI.enabled;
        UnityEngine.GUI.enabled = enabled;
    }
    
    public static bool PopEnabled()
    {
        var enabled = UnityEngine.GUI.enabled;
        UnityEngine.GUI.enabled = _wasEnabled;
        return enabled;
    }

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
        => Button(layout.Abs(layout.Horizontal ? -1f : 30f), label, tooltip);
    
    public static bool Button(Rect rect, string label, string tooltip = null)
    {
        tooltip ??= label;
        TooltipHandler.TipRegion(rect, tooltip);
        return Widgets.ButtonText(rect, label);
    }

    public static void Checkbox(LayoutRect layout, ref bool value, string label, string tooltip = null)
        => Checkbox(layout.Abs(layout.Horizontal ? -1f : Text.LineHeight), ref value, label, tooltip);

    public static void Checkbox(Rect rect, ref bool value, string label, string tooltip = null)
    {
        tooltip ??= label;
        if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
        TooltipHandler.TipRegion(rect, tooltip);
        Widgets.CheckboxLabeled(rect, label, ref value);
    }

    public static void TextField(LayoutRect layout, ref string value)
        => TextField(layout.Abs(layout.Horizontal ? -1f : 28f), ref value);
    
    public static void TextField(Rect rect, ref string value)
    {
        value = Widgets.TextField(rect, value);
    }

    public static void IntField(LayoutRect layout, ref int value, ref string editBuffer, int min, int max)
        => IntField(layout.Abs(layout.Horizontal ? -1f : 28f), ref value, ref editBuffer, min, max);
    
    public static void IntField(Rect rect, ref int value, ref string editBuffer, int min, int max)
    {
        Widgets.TextFieldNumeric(rect, ref value, ref editBuffer, min, max);
    }

    public static void Slider(LayoutRect layout, ref float value, float min, float max)
        => Slider(layout.Abs(layout.Horizontal ? -1f : 22f), ref value, min, max);
    
    public static void Slider(Rect rect, ref float value, float min, float max)
    {
        var newValue = Widgets.HorizontalSlider_NewTemp(rect, value, min, max);
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
        Widgets.FloatRange(rect, rect.GetHashCode(), ref floatRange, min, max);
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
    
    public static void LabelDouble(LayoutRect layout, string labelLeft, string labelCenter, string tooltip = null)
    {
        var labelLeftSize = Text.CalcSize(labelLeft);
        var labelCenterSize = Text.CalcSize(labelCenter);
        var rect = layout.Abs(layout.Horizontal ? -1f : Mathf.Max(labelLeftSize.y, labelCenterSize.y));
        var leftRect = rect.LeftHalf();
        var centerRect = rect.RightHalf();
        leftRect.width = labelLeftSize.x;
        centerRect.xMin -= labelCenterSize.x * 0.5f;
        if (tooltip != null) TooltipHandler.TipRegion(rect, tooltip);
        Widgets.Label(leftRect, labelLeft);
        Widgets.Label(centerRect, labelCenter);
    }
    
    public static void Dropdown<T>(LayoutRect layout, T value, List<T> potentialValues, Action<T> action, Func<T, string> textFunc = null)
        => Dropdown(layout.Abs(layout.Horizontal ? -1f : 28f), value, potentialValues, action, textFunc);

    public static void Dropdown<T>(Rect rect, T value, List<T> potentialValues, Action<T> action, Func<T, string> textFunc = null)
    {
        textFunc ??= o => o.ToString();
        if (Widgets.ButtonText(rect, textFunc.Invoke(value)))
        {
            var options = potentialValues.Select(e => 
                new FloatMenuOption(textFunc.Invoke(e), () => { action(e); UnityEngine.GUI.changed = true; })).ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    public static void Dropdown<T>(LayoutRect layout, T value, Action<T> action, string translationKeyPrefix = null) where T : Enum
        => Dropdown(layout.Abs(layout.Horizontal ? -1f : 28f), value, action, translationKeyPrefix);
    
    public static void Dropdown<T>(Rect rect, T value, Action<T> action, string translationKeyPrefix = null) where T : Enum
    {
        Dropdown(rect, value, typeof(T).GetEnumValues().Cast<T>().ToList(), action, 
            translationKeyPrefix == null ? null : e => (translationKeyPrefix + "." + e).Translate());
    }
    
    public static void DrawQuad(Rect quad, Color color)
    {
        ColorBuffer.wrapMode = TextureWrapMode.Repeat;
        ColorBuffer.SetPixel(0, 0, color);
        ColorBuffer.Apply();
        UnityEngine.GUI.DrawTexture(quad, ColorBuffer);
    }
}