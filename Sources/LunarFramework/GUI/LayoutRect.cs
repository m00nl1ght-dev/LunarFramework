using UnityEngine;

namespace LunarFramework.GUI;

public class LayoutRect
{
    internal readonly LunarAPI LunarAPI;
    internal readonly Node Root = new(null);
    
    internal Node Current;
    
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

        private float _occupiedSize;
        
        public static implicit operator Rect(Node node) => node?.Rect ?? default;

        internal Node(Node parent)
        {
            Parent = parent;
        }

        internal void Layout(Rect rect, LayoutParams layoutParams)
        {
            Rect = layoutParams.ApplyMargin(rect);
            LayoutParams = layoutParams;
            _occupiedSize = 0;
        }

        internal void LayoutChild(float size, LayoutParams layoutParamsForChild)
        {
            Child.Layout(NextRect(size), layoutParamsForChild);
        }
        
        internal Rect NextRect(float size)
        {
            if (size < 0) size = Mathf.Max(LayoutParams.SizeOf(Rect) - _occupiedSize, 0);
            
            var next = LayoutParams.Horizontal
                ? LayoutParams.Reversed
                    ? new Rect(Rect.x + Rect.width - _occupiedSize - size, Rect.y, size, Rect.height)
                    : new Rect(Rect.x + _occupiedSize, Rect.y, size, Rect.height)
                : LayoutParams.Reversed
                    ? new Rect(Rect.x, Rect.y + Rect.height - _occupiedSize - size, Rect.width, size)
                    : new Rect(Rect.x, Rect.y + _occupiedSize, Rect.width, size);
            
            _occupiedSize += size + LayoutParams.Spacing;
            return next;
        }
    }
}