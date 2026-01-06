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

    public float BroadRadius { get; }

    public static float CalculateBroadRadius(ICollisionBody body)
    {
        var longestDistance = 0f;
        foreach (var bodyPart in body.BodyParts)
        {
            var vertexList = bodyPart.ToVertices();
            foreach (var vertex in vertexList)
            {
                longestDistance = MathF.Max(longestDistance, vertex.Length());
            }
        }
        return longestDistance;
    }
}

public readonly record struct CollisionCircle(Vector2 Center, float Radius, uint VertexCount);
public readonly record struct CollisionRectangle(Vector2 Center, float Width, float Height, float Angle);
public readonly record struct CollisionTriangle(Vector2 PointOne, Vector2 PointTwo, Vector2 PointThree);

public class IndividualCollisionPolygon
{
    public Vector2[] Vertices { get; }

    public IndividualCollisionPolygon(CollisionCircle circle)
    {
        uint vertexCount = uint.Max(circle.VertexCount, 3);
        Vertices = new Vector2[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            Vertices[i] = circle.Center + new Vector2(MathF.Cos(MathF.Tau * i / vertexCount), MathF.Sin(MathF.Tau * i / vertexCount)) * circle.Radius;
        }
    }

    public IndividualCollisionPolygon(CollisionRectangle rectangle)
    {
        var sine = MathF.Sin(rectangle.Angle);
        var cosine = MathF.Cos(rectangle.Angle);

        var sideA = rectangle.Width;
        var sideB = rectangle.Height;

        Vertices = new Vector2[4];

        Vertices[0] = new Vector2(-sideA * 0.5f, -sideB * 0.5f);
        Vertices[1] = new Vector2(sideA * 0.5f, -sideB * 0.5f);
        Vertices[2] = new Vector2(sideA * 0.5f, sideB * 0.5f);
        Vertices[3] = new Vector2(-sideA * 0.5f, sideB * 0.5f);
        for (int i = 0; i < 4; i++)
        {
            var newX = (Vertices[i].X * cosine) + (Vertices[i].Y * (-sine));
            var newY = (Vertices[i].X * sine) + (Vertices[i].Y * cosine);
            Vertices[i] = new Vector2(newX, newY);

            Vertices[i] += rectangle.Center;
        }
    }

    public IndividualCollisionPolygon(CollisionTriangle triangle)
    {
        Vertices = new Vector2[3];
        Vertices[0] = triangle.PointOne;
        Vertices[1] = triangle.PointTwo;
        Vertices[2] = triangle.PointThree;
    }
}

public enum CollisionBodyPartShapeType : int
{
    Circle = 0,
    Rectangle = 1,
    Triangle = 2,
}

public class BatchCollisionBodyPart
{

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
    public CollisionBodyPartShapeType ShapeType { get; private set; }

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

    public IList<Vector2> BodyVertices { get; private set; }

    /// <summary>
    /// Transforms the shape definition into a set of vertices.
    /// </summary>
    /// <returns>The set of vertices.</returns>
    public readonly IList<Vector2> ToVertices()
    {
        Vector2[] vertices;

        switch (ShapeType)
        {
            case CollisionBodyPartShapeType.Circle:
                var vertexAmount = IntegerFields.X;
                var radius = DecimalFields.X;

                vertices = new Vector2[vertexAmount];
                for (int i = 0; i < vertexAmount; i++)
                {
                    vertices[i] = BodyPartCenter + new Vector2(MathF.Cos(MathF.Tau * i / vertexAmount), MathF.Sin(MathF.Tau * i / vertexAmount)) * radius;
                }
                break;
            case CollisionBodyPartShapeType.Rectangle:
                var angle = DecimalFields.Z;
                var sine = MathF.Sin(angle);
                var cosine = MathF.Cos(angle);

                var sideA = DecimalFields.X;
                var sideB = DecimalFields.Y;

                vertices = new Vector2[4];

                vertices[0] = new Vector2(-sideA * 0.5f, -sideB * 0.5f);
                vertices[1] = new Vector2(sideA * 0.5f, -sideB * 0.5f);
                vertices[2] = new Vector2(sideA * 0.5f, sideB * 0.5f);
                vertices[3] = new Vector2(-sideA * 0.5f, sideB * 0.5f);
                for (int i = 0; i < 4; i++)
                {
                    var newX = (vertices[i].X * cosine) + (vertices[i].Y * (-sine));
                    var newY = (vertices[i].X * sine) + (vertices[i].Y * cosine);
                    vertices[i] = new Vector2(newX, newY);

                    vertices[i] += BodyPartCenter;
                }
                break;
            case CollisionBodyPartShapeType.Triangle:
                vertices = new Vector2[3];

                vertices[0] = BodyPartCenter;
                vertices[1] = new Vector2(DecimalFields.X, DecimalFields.Y);
                vertices[2] = new Vector2(DecimalFields.Z, DecimalFields.W);
                break;
            default:
                vertices = [];
                break;
        }

        return vertices;
    }

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
            ShapeType = CollisionBodyPartShapeType.Circle,
            BodyPartCenter = center,
            DecimalFields = new Vector4(radius, 0, 0, 0),
            IntegerFields = new Point(vertexCount, 0)
        };
        bodyPart.BodyVertices = bodyPart.ToVertices();

        return bodyPart;
    }

    /// <summary>
    /// Creates a body part that represents a rectangle arbitrarily rotated.
    /// </summary>
    /// <param name="center">The center of the body part in local space.</param>
    /// <param name="sideFullLengths">The full width and height of the rectangle.</param>
    /// <param name="angle">The angle to rotate the rectangle with.</param>
    /// <returns>The body part.</returns>
    /// <remarks>The absolute value of <paramref name="sideFullLengths"/>'s elements are used.<br/>
    /// The center of the rectangle is used as the origin point for rotation.</remarks>
    public static CollisionBodyPart CreateRectangle(Vector2 center, Vector2 sideFullLengths, float angle)
    {
        sideFullLengths = Vector2.Abs(sideFullLengths);
        var bodyPart = new CollisionBodyPart
        {
            ShapeType = CollisionBodyPartShapeType.Rectangle,
            BodyPartCenter = center,
            DecimalFields = new Vector4(sideFullLengths.X, sideFullLengths.Y, angle, 0),
            IntegerFields = new Point(0, 0)
        };
        bodyPart.BodyVertices = bodyPart.ToVertices();

        return bodyPart;
    }

    /// <summary>
    /// Creates a body part that represents a rectangle arbitrarily rotated.
    /// </summary>
    /// <param name="rectangle">The position and size of the rectangle.</param>
    /// <param name="angle">The angle to rotate the rectangle with.</param>
    /// <returns>The body part.</returns>
    /// <remarks>The absolute value of <paramref name="sideFullLengths"/>'s elements are used.<br/>
    /// The center of the rectangle is used as the origin point for rotation.</remarks>
    public static CollisionBodyPart CreateRectangle(Rectangle rectangle, float angle)
    {
        var sideFullLengths = new Vector2(rectangle.Width, rectangle.Height);
        var bodyPart = new CollisionBodyPart
        {
            ShapeType = CollisionBodyPartShapeType.Rectangle,
            BodyPartCenter = new Vector2(rectangle.X, rectangle.Y) + sideFullLengths * 0.5f,
            DecimalFields = new Vector4(sideFullLengths.X, sideFullLengths.Y, angle, 0),
            IntegerFields = new Point(0, 0)
        };
        bodyPart.BodyVertices = bodyPart.ToVertices();

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
            ShapeType = CollisionBodyPartShapeType.Triangle,
            BodyPartCenter = pointOne,
            DecimalFields = new Vector4(pointTwo.X, pointTwo.Y, pointThree.X, pointThree.Y),
            IntegerFields = new Point(0, 0)
        };
        bodyPart.BodyVertices = bodyPart.ToVertices();

        return bodyPart;
    }
}
