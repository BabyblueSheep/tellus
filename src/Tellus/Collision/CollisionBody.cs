using System.Drawing;
using System.Numerics;
using System;
using Tellus.Math.Shapes;
using System.Collections;

namespace Tellus.Collision;

public sealed class CollisionBody : IEnumerable<CollisionPolygon>
{
    public Vector2 Offset { get; set; }
    private readonly List<CollisionPolygon> _polygons;

    public float BroadRadius { get; private set; }

    public CollisionBody()
    {
        _polygons = [];
    }

    public CollisionBody(params CollisionPolygon[] polygons) : this()
    {
        
        foreach (var polygon in polygons)
        {
            Add(polygon);
        }
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

    public bool IsWithinNarrowRange(CollisionBody otherBody)
    {
        var combinedBroadRadius = this.BroadRadius + otherBody.BroadRadius;
        return (this.Offset - otherBody.Offset).LengthSquared() < (combinedBroadRadius * combinedBroadRadius);
    }

    public IEnumerator<CollisionPolygon> GetEnumerator()
    {
        return _polygons.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}