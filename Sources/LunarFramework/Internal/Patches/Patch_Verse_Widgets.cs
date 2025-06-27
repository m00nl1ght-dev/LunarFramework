using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using LunarFramework.Patching;
using UnityEngine;
using Verse;

namespace LunarFramework.Internal.Patches;

[PatchGroup("Main")]
[HarmonyPatch(typeof(Widgets))]
internal static class Patch_Verse_Widgets
{
    internal static readonly Type Self = typeof(Patch_Verse_Widgets);

    [HarmonyReversePatch]
    [HarmonyPatch("FloatRange")]
    internal static void FloatRange_Custom(
        Rect rect, int id, ref FloatRange range,
        float min, float max, string labelKey,
        ToStringStyle valueStyle, float gap,
        GameFont sliderLabelFont,
        Color? sliderLabelColor,
        #if RW_1_6_OR_GREATER
        float roundTo,
        #endif
        Func<float, string> toString)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var pattern = TranspilerPattern.Build("ToStringByStyle")
                .MatchLdarg().Remove()
                .Match(OpCodes.Ldc_I4_1).Remove()
                .MatchCall(typeof(GenText), nameof(GenText.ToStringByStyle)).Remove()
                #if RW_1_6_OR_GREATER
                .Insert(OpCodes.Ldarg, 11)
                #else
                .Insert(OpCodes.Ldarg, 10)
                #endif
                .Insert(CodeInstruction.Call(Self, nameof(ToStringCustom)))
                .Lazy(2);

            return TranspilerPattern.Apply(instructions, pattern);
        }

        _ = Transpiler(null);
    }

    internal static void FloatRange_Custom(
        Rect rect, int id, ref FloatRange range,
        float min, float max, Func<float, string> toString,
        string labelKey = null, float gap = 0.0f,
        GameFont sliderLabelFont = GameFont.Tiny,
        Color? sliderLabelColor = null)
    {
        #if RW_1_6_OR_GREATER
        FloatRange_Custom(rect, id, ref range, min, max, labelKey, ToStringStyle.FloatTwo, gap, sliderLabelFont, sliderLabelColor, 0f, toString);
        #else
        FloatRange_Custom(rect, id, ref range, min, max, labelKey, ToStringStyle.FloatTwo, gap, sliderLabelFont, sliderLabelColor, toString);
        #endif
    }

    private static string ToStringCustom(float val, Func<float, string> func) => func(val);
}
