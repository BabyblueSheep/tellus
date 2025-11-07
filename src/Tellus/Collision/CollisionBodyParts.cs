using System.Drawing;
using System.Numerics;

namespace Tellus.Collision;

public interface ICollisionBody
{
    public Vector2 BodyOffset { get; }
    public IEnumerable<ICollisionBodyPart> BodyParts { get; }
}

public interface ICollisionBodyPart
{
    public int ShapeType { get; }
    public Vector2 BodyPartCenter { get; }
    public Vector4 DecimalFields { get; }
    public Point IntegerFields { get; }
}

public struct CircleCollisionBodyPart : ICollisionBodyPart
{
    public Vector2 Center;
    public float Radius;
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
    public readonly Vector4 DecimalFields => new Vector4(Radius, 0, 0, 0);
    public readonly Point IntegerFields => new Point(_vertexCount, 0);
}

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