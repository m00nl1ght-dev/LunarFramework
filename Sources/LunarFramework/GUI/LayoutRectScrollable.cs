using System;
using UnityEngine;

namespace LunarFramework.GUI;

public class LayoutRectScrollable : LayoutRect
{
    private Vector2 _scrollPosition;
    private Rect _scrollViewRect;

    public LayoutRectScrollable(LunarAPI lunarAPI) : base(lunarAPI) {}

    public IDisposable RootScrollable(Rect rect, LayoutParams layoutParams = default)
    {
        LunarGUI.BeginScrollView(rect, ref _scrollViewRect, ref _scrollPosition);
        BeginRoot(_scrollViewRect, layoutParams);
        return new EndTask(this);
    }

    public void ResetScroll()
    {
        _scrollPosition = Vector2.zero;
    }

    private class EndTask : IDisposable
    {
        private readonly LayoutRectScrollable Layout;

        public EndTask(LayoutRectScrollable layout)
        {
            Layout = layout;
        }

        public void Dispose()
        {
            Layout._scrollViewRect.height = Layout.OccupiedSpace;

            if (Layout.Current != Layout.RootNode)
            {
                Layout.LunarAPI.LogContext.Warn("Attempted to end layout node which is not the current node.");
            }
            else
            {
                Layout.End();
            }

            LunarGUI.EndScrollView();
        }
    }
}
