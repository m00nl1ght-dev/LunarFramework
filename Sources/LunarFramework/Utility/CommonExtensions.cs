using System;
using System.Collections.Generic;
using System.Linq;
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

    public static Rect MoveBy(this Rect rect, float x, float y) => new(rect.x + x, rect.y + y, rect.width, rect.height);

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T> source) => source.Where(e => e != null);

    public static bool DictEquals<TKey, TValue>(this IDictionary<TKey, TValue> first, IDictionary<TKey, TValue> second)
    {
        if (ReferenceEquals(first, second))
            return true;

        if (first == null || second == null)
            return false;

        if (first.Count != second.Count)
            return false;

        var valueComparer = EqualityComparer<TValue>.Default;

        foreach (var entry in first)
        {
            if (!second.TryGetValue(entry.Key, out var secondValue))
                return false;

            if (!valueComparer.Equals(entry.Value, secondValue))
                return false;
        }

        return true;
    }

    public static int DictHashCode<TKey, TValue>(this IDictionary<TKey, TValue> dict)
    {
        if (dict == null)
            return 0;

        int hash = 17;

        var keyComparer = EqualityComparer<TKey>.Default;
        var valueComparer = EqualityComparer<TValue>.Default;

        foreach (var entry in dict.OrderBy(k => k.Key))
        {
            int keyHash = keyComparer.GetHashCode(entry.Key);
            int valueHash = entry.Value != null ? valueComparer.GetHashCode(entry.Value) : 0;

            unchecked
            {
                hash = hash * 31 + keyHash;
                hash = hash * 31 + valueHash;
            }
        }

        return hash;
    }
}
