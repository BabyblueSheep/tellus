using System.Drawing;
using System.Numerics;
using System;

namespace Tellus.Collision;

/// <summary>
/// Describes a "body" composed of several "body parts" that get tested for collision detection.
/// </summary>
public interface ICollisionBody
{
    /// <summary>
    /// A position offset applied to all body parts.
    /// </summary>
    /// <remarks>
    /// Used to keep body parts in local space instead of world space.
    /// </remarks>
    public Vector2 BodyOffset { get; }

    /// <summary>
    /// Gets all body parts that compose the body.
    /// </summary>
    public IEnumerable<CollisionBodyPart> BodyParts { get; }
}


/// <summary>
/// Describes information needed to create a "body part", represented as a convex polygon.
/// </summary>
/// <remarks>
/// Don't construct this manually. Use provided static functions for convenience.
/// </remarks>
public struct CollisionBodyPart
{
    /// <summary>
    /// The type, or "shape ID", of the body part. Used to figure out how to use fields.
    /// </summary>
    public int ShapeType { get; private set; }

    /// <summary>
    /// The center of the body part in local space.
    /// </summary>
    public Vector2 BodyPartCenter { get; private set; }

    /// <summary>
    /// Four arbitrary floating point fields. Usage is determined by <see cref="ShapeType"/>.
    /// </summary>
    public Vector4 DecimalFields { get; private set; }

    /// <summary>
    /// Two arbitrary integer fields. Usage is determined by <see cref="ShapeType"/>.
    /// </summary>
    public Point IntegerFields { get; private set; }

    /// <summary>
    /// Creates a body part that represents a polygon that approximates a circle.
    /// </summary>
    /// <param name="center">The center of the body part in local space.</param>
    /// <param name="radius">The radius of the "circle".</param>
    /// <param name="vertexCount">The amount of vertices the body part will use.</param>
    /// <returns>The body part.</returns>
    /// <remarks><paramref name="radius"/>'s absolute value is used, and <paramref name="vertexCount"/> is clamped between 3 and 16.</remarks>
    public static CollisionBodyPart CreateCircle(Vector2 center, float radius, int vertexCount)
    {
        vertexCount = System.Math.Clamp(vertexCount, 3, 16);
        radius = System.Math.Abs(radius);
        var bodyPart = new CollisionBodyPart
        {
            ShapeType = 0,
            BodyPartCenter = center,
            DecimalFields = new Vector4(radius, 0, 0, 0),
            IntegerFields = new Point(vertexCount, 0)
        };

        return bodyPart;
    }

    /// <summary>
    /// Creates a body part that represents a rectangle arbitrarily rotated.
    /// </summary>
    /// <param name="center">The center of the body part in local space.</param>
    /// <param name="sideFullLengths">The full width and height of the rectangle.</param>
    /// <param name="angle">The angle to rotate the rectangle at.</param>
    /// <returns>The body part.</returns>
    /// <remarks>The absolute value of <paramref name="sideFullLengths"/>'s elements are used.</remarks>
    public static CollisionBodyPart CreateRectangle(Vector2 center, Vector2 sideFullLengths, float angle)
    {
        sideFullLengths = Vector2.Abs(sideFullLengths);
        var bodyPart = new CollisionBodyPart
        {
            ShapeType = 1,
            BodyPartCenter = center,
            DecimalFields = new Vector4(sideFullLengths.X, sideFullLengths.Y, angle, 0),
            IntegerFields = new Point(0, 0)
        };

        return bodyPart;
    }

    public static CollisionBodyPart CreateRectangle(Rectangle rectangle, float angle)
    {
        var sideFullLengths = new Vector2(rectangle.Width, rectangle.Height);
        var bodyPart = new CollisionBodyPart
        {
            ShapeType = 1,
            BodyPartCenter = new Vector2(rectangle.X, rectangle.Y) + sideFullLengths * 0.5f,
            DecimalFields = new Vector4(sideFullLengths.X, sideFullLengths.Y, angle, 0),
            IntegerFields = new Point(0, 0)
        };

        return bodyPart;
    }

    /// <summary>
    /// Creates a body part that represents a triangle.
    /// </summary>
    /// <param name="pointOne">The first point of the triangle in local space.</param>
    /// <param name="pointTwo">The second point of the triangle in local space.</param>
    /// <param name="pointThree">The third point of the triangle in local space.</param>
    /// <returns>The body part.</returns>
    public static CollisionBodyPart CreateTriangle(Vector2 pointOne, Vector2 pointTwo, Vector2 pointThree)
    {
        var bodyPart = new CollisionBodyPart
        {
            ShapeType = 2,
            BodyPartCenter = pointOne,
            DecimalFields = new Vector4(pointTwo.X, pointTwo.Y, pointThree.X, pointThree.Y),
            IntegerFields = new Point(0, 0)
        };

        return bodyPart;
    }
}
