using MoonWorks.Math;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
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
    public int VertexCount;

    public readonly int ShapeType => 0;
    public readonly Vector2 ShapeCenter => Center;
    public readonly Vector4 ShapeDecimalFields => new Vector4(Radius, 0, 0, 0);
    public readonly Point ShapeIntegerFields => new Point(VertexCount, 0);
}

public struct RectangleColliderShape : IColliderShape
{
    public Vector2 Center;
    public float Angle;
    public Vector2 SideLengths;

    public readonly int ShapeType => 1;
    public readonly Vector2 ShapeCenter => Center;
    public readonly Vector4 ShapeDecimalFields => new Vector4(Angle, SideLengths.X, SideLengths.Y, 0);
    public readonly Point ShapeIntegerFields => new Point(0, 0);
}