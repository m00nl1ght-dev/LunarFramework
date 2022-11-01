using UnityEngine;

namespace LunarFramework.GUI;

public struct LayoutParams
{
    public Directional<float> Margin;
    public float Spacing;
    
    public float DefaultSize;
    
    public bool Horizontal;
    public bool Reversed;

    public float SizeOf(Rect rect) => Horizontal ? rect.width : rect.height;

    public Rect ApplyMargin(Rect rect)
    {
        return new Rect(
            rect.x + Margin.Left, 
            rect.y + Margin.Top, 
            rect.width - Margin.Left - Margin.Right, 
            rect.height - Margin.Top - Margin.Bottom
        );
    }
}

public struct Directional<T>
{
    public T Top;
    public T Bottom;
    public T Left;
    public T Right;

    public Directional(T value)
    {
        Top = value;
        Bottom = value;
        Left = value;
        Right = value;
    }
    
    public Directional(T tbValue, T lrValue)
    {
        Top = tbValue;
        Bottom = tbValue;
        Left = lrValue;
        Right = lrValue;
    }
    
    public Directional(T tValue, T bValue, T lValue, T rValue)
    {
        Top = tValue;
        Bottom = bValue;
        Left = lValue;
        Right = rValue;
    }
}