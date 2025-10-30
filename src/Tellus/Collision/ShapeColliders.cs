using MoonWorks.Math;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Tellus.Collision;

namespace Tellus.Collision;

public interface IHasColliderShapes
{
    public Vector2 ShapeOffset { get; }
    public IEnumerable<IColliderShape> Shapes { get; }
}

public interface IColliderShape
{
    public int ShapeType { get; }
    public Vector2 ShapeCenter { get; }
    public Vector4 ShapeDecimalFields { get; }
    public Point ShapeIntegerFields { get; }
}

public struct CircleColliderShape : IColliderShape
{
    public Vector2 Center;
    public float Radius;
    private int _vertexCount;
    public int VertexCount 
    {
        get => _vertexCount;
        set
        {
            _vertexCount = Math.Clamp(_vertexCount, 3, 16);
        } 
    }

    public CircleColliderShape(Vector2 center, float radius, int vertexCount)
    {
        Center = center;
        Radius = radius;
        VertexCount = vertexCount;
    }

    public readonly int ShapeType => 0;
    public readonly Vector2 ShapeCenter => Center;
    public readonly Vector4 ShapeDecimalFields => new Vector4(Radius, 0, 0, 0);
    public readonly Point ShapeIntegerFields => new Point(_vertexCount, 0);
}

public struct RectangleColliderShape : IColliderShape
{
    public Vector2 Center;
    public float Angle;
    public Vector2 SideLengths;

    public RectangleColliderShape(Vector2 center, float angle, Vector2 sideLengths)
    {
        Center = center;
        Angle = angle;
        SideLengths = sideLengths;
    }

    public readonly int ShapeType => 1;
    public readonly Vector2 ShapeCenter => Center;
    public readonly Vector4 ShapeDecimalFields => new Vector4(Angle, SideLengths.X, SideLengths.Y, 0);
    public readonly Point ShapeIntegerFields => new Point(0, 0);
}

public struct TriangleColliderShape : IColliderShape
{
    public Vector2 PointOne;
    public Vector2 PointTwo;
    public Vector2 PointThree;

    public TriangleColliderShape(Vector2 firstPoint, Vector2 secondPoint, Vector2 thirdPoint)
    {
        PointOne = firstPoint;
        PointTwo = secondPoint;
        PointThree = thirdPoint;
    }

    public readonly int ShapeType => 2;
    public readonly Vector2 ShapeCenter => PointOne;
    public readonly Vector4 ShapeDecimalFields => new Vector4(PointTwo, PointThree.X, PointThree.Y);
    public readonly Point ShapeIntegerFields => new Point(0, 0);
}