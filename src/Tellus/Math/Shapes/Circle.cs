using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Tellus.Math.Shapes;

public readonly record struct Circle
{
    public Vector2 Center { get; init; }
    public float Radius { get; init; }

    public static Circle Unit
    {
        get => new Circle(Vector2.Zero, 1);
    }

    public Circle(float radius)
    {
        Center = Vector2.Zero;
        Radius = radius;
    }

    public Circle(Vector2 center, float radius)
    {
        Center = center;
        Radius = radius;
    }
}
