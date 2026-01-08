using System.Drawing;
using System.Numerics;
using System;
using Tellus.Math.Shapes;
using System.Collections;

namespace Tellus.Collision;

/// <summary>
/// A "body" composed of several polygons. Used for collision detection.
/// </summary>
/// <remarks>
/// All vertices are in local space. An offset is stored and applied to go into world space.
/// </remarks>
public sealed class CollisionBody : IEnumerable<CollisionPolygon>
{
    /// <summary>
    /// An offset to be applied to vertices.
    /// </summary>
    public Vector2 Offset { get; set; }

    private readonly List<CollisionPolygon> _polygons;

    /// <summary>
    /// A radius that approximates the size of the body. Used for broad collision.
    /// </summary>
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

    /// <summary>
    /// Adds a polygon to the body.
    /// </summary>
    /// <param name="polygon">The polygon.</param>
    public void Add(CollisionPolygon polygon)
    {
        _polygons.Add(polygon);

        foreach (var vertex in polygon.Vertices)
        {
            BroadRadius = MathF.Max(BroadRadius, vertex.Length());
        }
    }

    /// <summary>
    /// Removes a polygon to the body.
    /// </summary>
    /// <param name="polygon">The polygon.</param>
    public void Remove(CollisionPolygon polygon)
    {
        var removed = _polygons.Remove(polygon);

        BroadRadius = 0;
        if (removed)
        {
            foreach (var presentPolygon in _polygons)
            {
                foreach (var vertex in presentPolygon.Vertices)
                {
                    BroadRadius = MathF.Max(BroadRadius, vertex.Length());
                }
            }
        }
    }

    /// <summary>
    /// Clears all polygons from the body.
    /// </summary>
    public void Clear()
    {
        _polygons.Clear();
        BroadRadius = 0;
    }

    /// <summary>
    /// Determines whether the this body and another one are within range for narrow collision.
    /// </summary>
    /// <param name="otherBody">The other body.</param>
    /// <returns>Whether the bodies are within range for narrow collision.</returns>
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