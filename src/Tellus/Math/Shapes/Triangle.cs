using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Tellus.Math.Shapes;

public readonly record struct Triangle
{
    public Vector2 PointOne { get; init; }
    public Vector2 PointTwo { get; init; }
    public Vector2 PointThree { get; init; }

    public Triangle(Vector2 pointOne, Vector2 pointTwo, Vector2 pointThree)
    {
        PointOne = pointOne;
        PointTwo = pointTwo;
        PointThree = pointThree;
    }
}
