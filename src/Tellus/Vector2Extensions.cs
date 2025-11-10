using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Tellus;

public static class Vector2Extensions
{
    public static bool HasNaNs(this Vector2 vector)
    {
        return float.IsNaN(vector.X) || float.IsNaN(vector.Y);
    }

    public static Vector2 SafeNormalize(this Vector2 vector, Vector2 defaultValue)
    {
        return vector == Vector2.Zero || vector.HasNaNs() ? defaultValue : Vector2.Normalize(vector);
    }
}
