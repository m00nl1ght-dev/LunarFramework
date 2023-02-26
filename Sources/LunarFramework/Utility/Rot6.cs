using System;
using Verse;

namespace LunarFramework.Utility;

public struct Rot6 : IEquatable<Rot6>
{
    private byte _index;
    private float _angle;

    public bool IsValid => _index < 6;

    public static Rot6 Invalid => new() { _index = 66 };

    public int Index
    {
        get => _index;
        set => _index = (byte) ((value % 6 + 6) % 6);
    }

    public float Angle
    {
        get => _angle;
        set => _angle = (value % 360f + 360f) % 360f;
    }

    public Rot6(int index, float angle)
    {
        Index = index;
        Angle = angle;
    }

    public void Rotate(RotationDirection rotDir)
    {
        switch (rotDir)
        {
            case RotationDirection.Clockwise:
                Index++;
                Angle += 60f;
                break;
            case RotationDirection.Counterclockwise:
                Index--;
                Angle -= 60f;
                break;
            case RotationDirection.Opposite:
                Index += 3;
                Angle += 180f;
                break;
        }
    }

    public Rot6 Rotated(RotationDirection rotDir)
    {
        return rotDir switch
        {
            RotationDirection.Clockwise => new Rot6(_index + 1, _angle + 60f),
            RotationDirection.Counterclockwise => new Rot6(_index - 1, _angle - 60f),
            RotationDirection.Opposite => new Rot6(_index + 3, _angle + 180f),
            _ => this
        };
    }

    public Rot6 RotatedCW() => new(_index + 1, _angle + 60f);

    public Rot6 RotatedCCW() => new(_index - 1, _angle - 60f);

    public Rot6 Opposite => new(_index + 3, _angle + 180f);

    public Rot4 AsRot4()
    {
        if (!IsValid) return Rot4.Invalid;
        return Rot4.FromAngleFlat(_angle);
    }

    public bool Adjacent(Rot6 other)
    {
        return (_index + 1) % 6 == other._index || (_index + 5) % 6 == other._index;
    }

    public bool IsSameOrAdjacent(Rot6 other)
    {
        return _index == other._index || (_index + 1) % 6 == other._index || (_index + 5) % 6 == other._index;
    }

    public bool IsOpposite(Rot6 other)
    {
        return (_index + 3) % 6 == other._index;
    }

    public bool IsSameOrOpposite(Rot6 other)
    {
        return _index == other._index || (_index + 3) % 6 == other._index;
    }

    public bool IsRotatedCW(Rot6 other)
    {
        return (_index + 1) % 6 == other._index;
    }

    public bool IsRotatedCCW(Rot6 other)
    {
        return (_index + 5) % 6 == other._index;
    }

    public float Slant => AngleSlant(_angle);

    public static float AngleSlant(float angle) => 1f - Math.Abs(angle % 90f - 45f) / 45f;

    public static bool operator ==(Rot6 a, Rot6 b) => a._index == b._index;

    public static bool operator !=(Rot6 a, Rot6 b) => a._index != b._index;

    public bool Equals(Rot6 other)
    {
        return _index == other._index;
    }

    public override bool Equals(object obj)
    {
        return obj is Rot6 other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _index.GetHashCode();
    }

    public override string ToString()
    {
        return $"{nameof(Index)}: {Index}, {nameof(Angle)}: {Angle}";
    }

    public static float MidPoint(Rot6 a, Rot6 b)
    {
        return MidPoint(a._angle, b._angle);
    }

    public static float MidPoint(float a, float b)
    {
        if (a > b) (a, b) = (b, a);
        if (b - a > 180) b -= 360;
        var f = (b + a) / 2;
        if (f < 0) f += 360;
        return f;
    }
}
