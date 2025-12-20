using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Tellus.Math;

public static class Vector2Extensions
{
    public static Vector2 FromAngle(this float angle)
    {
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    public static float ToAngle(this Vector2 vector)
    {
        return MathF.Atan2(vector.Y, vector.X);
    }

    public static bool HasNaNs(this Vector2 vector)
    {
        return float.IsNaN(vector.X) || float.IsNaN(vector.Y);
    }

    public static Vector2 SafeNormalize(this Vector2 vector, Vector2 defaultValue)
    {
        return vector == Vector2.Zero || vector.HasNaNs() ? defaultValue : Vector2.Normalize(vector);
    }
}
