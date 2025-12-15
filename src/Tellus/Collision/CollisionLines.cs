using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Tellus.Math;

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

/// <summary>
/// Describes information about a line or ray used in collision.
/// </summary>
public struct CollisionLine
{
    /// <summary>
    /// The origin position of the line in local space.
    /// </summary>
    public Vector2 Origin;

    /// <summary>
    /// If <see cref="IsVectorFixedPoint"/> is true, holds a position point in world space. Otherwise, holds the normalized direction of the line.
    /// </summary>
    public Vector2 ArbitraryVector;

    /// <summary>
    /// The length of the line.
    /// </summary>
    public float Length;

    /// <summary>
    /// Whether the line can be restricted by bodies.
    /// </summary>
    public bool CanBeRestricted;

    /// <summary>
    /// Determines how <see cref="ArbitraryVector"/> is interpreted.
    /// </summary>
    public bool IsVectorFixedPoint;

    /// <summary>
    /// Creates a ray with a direction and a finite length.
    /// </summary>
    /// <param name="origin">The origin of the ray in local space.</param>
    /// <param name="direction">The normalized direction of the ray.</param>
    /// <param name="length">The length of the ray.</param>
    /// <returns>The ray.</returns>
    public static CollisionLine CreateFiniteLengthRay(Vector2 origin, Vector2 direction, float length)
    {
        var line = new CollisionLine
        {
            Origin = origin,
            ArbitraryVector = direction.SafeNormalize(Vector2.Zero),
            Length = System.Math.Abs(length),

            CanBeRestricted = false,
            IsVectorFixedPoint = false
        };

        return line;
    }

    /// <summary>
    /// Creates a line 
    /// </summary>
    /// <param name="startPoint"></param>
    /// <param name="endPoint"></param>
    /// <param name="length"></param>
    /// <returns></returns>
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