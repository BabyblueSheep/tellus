using System.Drawing;
using System.Numerics;
using System;
using Tellus.Math.Shapes;

namespace Tellus.Collision;

public sealed class CollisionBody
{
    public Vector2 Offset { get; set; }
    private List<CollisionPolygon> _polygons;

    public float BroadRadius { get; private set; }

    public CollisionBody()
    {
        _polygons = [];
    }

    public CollisionBody(params CollisionPolygon[] polygons) : base()
    {
        
        
    }

    public void Add(CollisionPolygon polygon)
    {
        _polygons.Add(polygon);

        foreach (var vertex in polygon.Vertices)
        {
            BroadRadius = MathF.Max(BroadRadius, vertex.Length());
        }
    }

    public void Clear()
    {
        _polygons.Clear();
        BroadRadius = 0;
    }
}