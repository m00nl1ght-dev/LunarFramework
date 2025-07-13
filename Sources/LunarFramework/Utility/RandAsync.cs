using UnityEngine;
using Verse;

namespace LunarFramework.Utility;

public static class RandAsync
{
    public static int Int(int seed)
    {
        return MurmurHash.GetInt((uint) seed, 0u);
    }

    public static float Value(int seed)
    {
        return (float) (((double) MurmurHash.GetInt((uint) seed, 0u) - int.MinValue) / uint.MaxValue);
    }

    public static bool Chance(float chance, int seed)
    {
        return chance > 0f && (chance >= 1f || Value(seed) < chance);
    }

    public static float Range(float minI, float maxI, int seed)
    {
        return maxI <= minI ? minI : Value(seed) * (maxI - minI) + minI;
    }

    public static int Range(int minI, int maxE, int seed)
    {
        return maxE <= minI ? minI : minI + Mathf.Abs(Int(seed) % (maxE - minI));
    }
}
