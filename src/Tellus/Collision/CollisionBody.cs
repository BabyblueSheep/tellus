using System.Drawing;
using System.Numerics;
using System;
using Tellus.Math.Shapes;

namespace Tellus.Collision;

public sealed class CollisionBody
{
    public Vector2 Offset { get; set; }
    public List<CollisionPolygon> Polygons { get; }

    public CollisionBody()
    {

    }
}