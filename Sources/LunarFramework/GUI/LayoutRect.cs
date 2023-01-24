using System.Collections.Generic;
using UnityEngine;

namespace LunarFramework.GUI;

public class LayoutRect
{
    internal readonly LunarAPI LunarAPI;
    internal readonly Node Root = new(null);
    
    internal Node Current;

    public bool Horizontal => Current.LayoutParams.Horizontal;
    public float OccupiedSpace => Current.OccupiedSpaceTrimmed;
    
    private readonly Stack<bool> _changedStack = new();
    private readonly Stack<bool> _enabledStack = new();

    public LayoutRect(LunarAPI lunarAPI)
    {
        LunarAPI = lunarAPI;
    }

    public void BeginRoot(Rect rect, LayoutParams layoutParams = default)
    {
        if (Current != null)
        {
            LunarAPI.LogContext.Warn("Beginning layout before last one was ended.");
        }

        Current = Root;
        Current.Layout(rect, layoutParams);
    }
    
    public void BeginAbs()
    {
        BeginAbs(Current.LayoutParams.DefaultSize);
    }
    
    public void BeginAbs(float size)
    {
        BeginAbs(size, new LayoutParams {Horizontal = !Current.LayoutParams.Horizontal});
    }

    public void BeginAbs(float size, LayoutParams layoutParams)
    {
        Current.LayoutChild(size, layoutParams);
        Current = Current.Child;
    }
    
    public void BeginRel(float sizeRel)
    {
        BeginRel(sizeRel, new LayoutParams {Horizontal = !Current.LayoutParams.Horizontal});
    }
    
    public void BeginRel(float sizeRel, LayoutParams layoutParams)
    {
        BeginAbs(ToAbs(sizeRel), layoutParams);
    }
    
    public Rect Abs()
    {
        return Abs(Current.LayoutParams.DefaultSize);
    }

    public Rect Abs(float size)
    {
        return Current.NextRect(size);
    }
    
    public Rect Rel(float sizeRel)
    {
        return Abs(ToAbs(sizeRel));
    }

    public void End()
    {
        if (Current == null)
        {
            LunarAPI.LogContext.Warn("There is no layout to end.");
            return;
        }

        Current = Current.Parent;
    }

    public void PushChanged(bool changed = false)
    {
        _changedStack.Push(UnityEngine.GUI.changed);
        UnityEngine.GUI.changed = changed;
    }
    
    public bool PopChanged()
    {
        var changed = UnityEngine.GUI.changed;
        UnityEngine.GUI.changed = changed || _changedStack.Pop();
        return changed;
    }
    
    public void PushEnabled(bool enabled)
    {
        _enabledStack.Push(UnityEngine.GUI.enabled);
        UnityEngine.GUI.enabled = enabled;
    }
    
    public bool PopEnabled()
    {
        var enabled = UnityEngine.GUI.enabled;
        UnityEngine.GUI.enabled = _enabledStack.Pop();
        return enabled;
    }

    public static implicit operator Rect(LayoutRect layout) => layout.Current?.Rect ?? default;

    private float ToAbs(float sizeRel)
    {
        return sizeRel < 0 ? -1 : Mathf.Min(sizeRel, 1) * Current.LayoutParams.SizeOf(Current);
    }
    
    internal class Node
    {
        internal Rect Rect;
        internal LayoutParams LayoutParams;

        internal readonly Node Parent;
        internal Node Child => _child ??= new Node(this);
        private Node _child;

        public float OccupiedSpace { get; private set; }
        public float OccupiedSpaceTrimmed => Mathf.Max(0, OccupiedSpace - LayoutParams.Spacing);
        
        public static implicit operator Rect(Node node) => node?.Rect ?? default;

        internal Node(Node parent)
        {
            Parent = parent;
        }

        internal void Layout(Rect rect, LayoutParams layoutParams)
        {
            Rect = layoutParams.ApplyMargin(rect);
            LayoutParams = layoutParams;
            OccupiedSpace = 0;
        }

        internal void LayoutChild(float size, LayoutParams layoutParamsForChild)
        {
            Child.Layout(NextRect(size), layoutParamsForChild);
        }
        
        internal Rect NextRect(float size)
        {
            if (size < 0) size = Mathf.Max(LayoutParams.SizeOf(Rect) - OccupiedSpace, 0);
            
            var next = LayoutParams.Horizontal
                ? LayoutParams.Reversed
                    ? new Rect(Rect.x + Rect.width - OccupiedSpace - size, Rect.y, size, Rect.height)
                    : new Rect(Rect.x + OccupiedSpace, Rect.y, size, Rect.height)
                : LayoutParams.Reversed
                    ? new Rect(Rect.x, Rect.y + Rect.height - OccupiedSpace - size, Rect.width, size)
                    : new Rect(Rect.x, Rect.y + OccupiedSpace, Rect.width, size);
            
            OccupiedSpace += size + LayoutParams.Spacing;
            return next;
        }
    }
}