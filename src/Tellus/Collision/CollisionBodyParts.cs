using System.Drawing;
using System.Numerics;

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
    public IEnumerable<ICollisionBodyPart> BodyParts { get; }
}

/// <summary>
/// Contains all information needed to transfer a body part to a GPU buffer.
/// </summary>
/// <remarks>
/// Don't implement this yourself. Instead, use provided structs that implement this interface.
/// </remarks>
public interface ICollisionBodyPart
{
    public int ShapeType { get; }
    public Vector2 BodyPartCenter { get; }
    public Vector4 DecimalFields { get; }
    public Point IntegerFields { get; }
}

public struct CollisionBodyPart
{
    public int ShapeType { get; private set; }
    public Vector2 BodyPartCenter { get; private set; }
    public Vector4 DecimalFields { get; private set; }
    public Point IntegerFields { get; private set; }

    public static CollisionBodyPart CreateCircle(Vector2 center, float radius, int vertexCount)
    {
        var bodyPart = new CollisionBodyPart();

        bodyPart.BodyPartCenter = center;
        bodyPart.DecimalFields = new Vector4(radius, 0, 0, 0);
        bodyPart.IntegerFields = new Point(vertexCount, 0);

        return bodyPart;
    }
}

/// <summary>
/// A body part that represents a circular polygon.
/// </summary>
public struct CircleCollisionBodyPart : ICollisionBodyPart
{
    public Vector2 Center;
    private float _radius;
    public float Radius
    {
        get => _radius;
        set => _radius = MathF.Max(0, value);
    }
    private int _vertexCount;
    public int VertexCount 
    {
        get => _vertexCount;
        set
        {
            _vertexCount = Math.Clamp(value, 3, 16);
        } 
    }

    public CircleCollisionBodyPart(Vector2 center, float radius, int vertexCount)
    {
        Center = center;
        Radius = radius;
        VertexCount = vertexCount;
    }

    public readonly int ShapeType => 0;
    public readonly Vector2 BodyPartCenter => Center;
    public readonly Vector4 DecimalFields => new Vector4(_radius, 0, 0, 0);
    public readonly Point IntegerFields => new Point(_vertexCount, 0);
}

/// <summary>
/// A body part that represents a rectangle that can freely be rotated.
/// </summary>
public struct RectangleCollisionBodyPart : ICollisionBodyPart
{
    public Vector2 Center;
    public float Angle;
    public Vector2 SideLengths;

    public RectangleCollisionBodyPart(Vector2 center, float angle, Vector2 sideLengths)
    {
        Center = center;
        Angle = angle;
        SideLengths = sideLengths;
    }

    public readonly int ShapeType => 1;
    public readonly Vector2 BodyPartCenter => Center;
    public readonly Vector4 DecimalFields => new Vector4(Angle, SideLengths.X, SideLengths.Y, 0);
    public readonly Point IntegerFields => new Point(0, 0);
}

/// <summary>
/// A body part that represents a triangle composed of any three points.
/// </summary>
public struct TriangleCollisionBodyPart : ICollisionBodyPart
{
    public Vector2 PointOne;
    public Vector2 PointTwo;
    public Vector2 PointThree;

    public TriangleCollisionBodyPart(Vector2 pointOne, Vector2 pointTwo, Vector2 pointThree)
    {
        PointOne = pointOne;
        PointTwo = pointTwo;
        PointThree = pointThree;
    }

    public readonly int ShapeType => 2;
    public readonly Vector2 BodyPartCenter => PointOne;
    public readonly Vector4 DecimalFields => new Vector4(PointTwo, PointThree.X, PointThree.Y);
    public readonly Point IntegerFields => new Point(0, 0);
}