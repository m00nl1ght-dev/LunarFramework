using UnityEngine;

namespace LunarFramework.GUI;

public static class LunarGUI
{
    private static readonly object ColorBufferTex = new Texture2D(1, 1);
    private static Texture2D ColorBuffer => (Texture2D) ColorBufferTex;

    public static void DrawQuad(Rect quad, Color color)
    {
        ColorBuffer.wrapMode = TextureWrapMode.Repeat;
        ColorBuffer.SetPixel(0, 0, color);
        ColorBuffer.Apply();
        UnityEngine.GUI.DrawTexture(quad, ColorBuffer);
    }
}