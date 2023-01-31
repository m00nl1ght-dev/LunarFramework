using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace LunarFramework.Utility;

public class RandInstance
{
    private uint _seed;

    public uint Iterations { get; set; }

    public int Seed
    {
        get => (int) _seed;

        set
        {
            _seed = (uint) value;
            Iterations = 0U;
        }
    }

    public RandInstance()
    {
        _seed = (uint) DateTime.Now.GetHashCode();
    }

    public RandInstance(int seed, uint iter = 0U)
    {
        Seed = seed;
        Iterations = iter;
    }

    public float Value => (float) ((MurmurHash.GetInt(_seed, Iterations++) - (double) int.MinValue) / uint.MaxValue);

    public int Int => MurmurHash.GetInt(_seed, Iterations++);

    public bool Bool => Value < 0.5;

    public int Range(int min, int max) => max <= min ? min : min + Mathf.Abs(Int % (max - min));

    public int RangeInclusive(int min, int max) => max <= min ? min : Range(min, max + 1);

    public float Range(float min, float max) => max <= (double) min ? min : Value * (max - min) + min;

    public bool Chance(float chance) => chance > 0.0 && (chance >= 1.0 || Value < (double) chance);

    public int RoundRandom(float f) => (int) f + (Value < f % 1.0 ? 1 : 0);

    public T RandomElement<T>(IEnumerable<T> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is not IList<T> objList)
            objList = source.ToList();
        if (objList.Count != 0)
            return objList[Range(0, objList.Count)];
        return default;
    }

    public T MaxByRandomIfEqual<T>(
        T elem1, float by1, T elem2, float by2, T elem3, float by3, T elem4, float by4,
        T elem5, float by5, T elem6, float by6, T elem7, float by7, T elem8, float by8,
        float eps = 0.0001f)
    {
        return GenMath.MaxBy(
            elem1, by1 + Range(0.0f, eps), elem2, by2 + Range(0.0f, eps), elem3, by3 + Range(0.0f, eps),
            elem4, by4 + Range(0.0f, eps), elem5, by5 + Range(0.0f, eps), elem6, by6 + Range(0.0f, eps),
            elem7, by7 + Range(0.0f, eps), elem8, by8 + Range(0.0f, eps));
    }
}
