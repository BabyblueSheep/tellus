using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Tellus.Collision;

/// <summary>
/// Describes an object that contains multiple lines for collision detection.
/// </summary>
public interface ICollisionLineCollection
{
    /// <summary>
    /// A position offset applied to all lines.
    /// </summary>
    public Vector2 OriginOffset { get; }

    /// <summary>
    /// Gets all lines the object holds.
    /// </summary>
    public IEnumerable<CollisionLine> Lines { get; }

    /// <summary>
    /// The index of the line that will be treated as the "velocity" of the line collection.
    /// </summary>
    /// <remarks>
    /// The velocity increments the offset of the line collection, used to update positions on the GPU without downloading data to the CPU.
    /// </remarks>
    public int LineVelocityIndex => 0;
}

public struct CollisionLine
{
    public Vector2 Origin;
    public Vector2 ArbitraryVector;
    public float Length;

    public bool CanBeRestricted;
    public bool IsVectorFixedPoint;

    public static CollisionLine CreateFiniteLengthRay(Vector2 origin, Vector2 direction, float length)
    {
        var line = new CollisionLine
        {
            Origin = origin,
            ArbitraryVector = direction,
            Length = length,

            CanBeRestricted = false,
            IsVectorFixedPoint = false
        };

        return line;
    }

    public static CollisionLine CreateFixedPointLineSegment(Vector2 startPoint, Vector2 endPoint, float length)
    {
        var line = new CollisionLine
        {
            Origin = startPoint,
            ArbitraryVector = endPoint,
            Length = length,

            CanBeRestricted = false,
            IsVectorFixedPoint = true
        };

        return line;
    }
}