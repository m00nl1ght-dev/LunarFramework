using System;
using System.Collections.Generic;
using LunarFramework.Bootstrap;
using RimWorld;
using UnityEngine;
using Verse;

namespace LunarFramework.Utility;

public static class CommonExtensions
{
    public static bool IsInitialized(this LunarAPI lunarAPI)
    {
        return lunarAPI != null && lunarAPI.Component.LoadingState == LoadingState.Initialized;
    }

    public static string ToStringPretty(this Version version)
    {
        if (version.Major < 0 || version.Minor < 0) return version.ToString();
        if (version.Build <= 0) return version.ToString(2);
        if (version.Revision <= 0) return version.ToString(3);
        return version.ToString();
    }

    public static bool IsDeepWater(this TerrainDef def)
    {
        return def == TerrainDefOf.WaterDeep || def == TerrainDefOf.WaterOceanDeep;
    }

    public static bool IsNormalWater(this TerrainDef def)
    {
        return IsDeepWater(def) || def == TerrainDefOf.WaterShallow || def == TerrainDefOf.WaterOceanShallow;
    }

    public static T RandomElementSeeded<T>(this List<T> list, int seed, T fallback = default)
    {
        if (list.Count == 0) return fallback;
        return list[Rand.RangeSeeded(0, list.Count, seed)];
    }

    public static Vector3 ToVec3(this IntVec3 vec) => new(vec.x, vec.y, vec.z);
}
