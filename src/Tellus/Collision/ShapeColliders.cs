using MoonWorks.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

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
    public Vector4 ShapeFields { get; }
}

public struct CircleColliderShape : IColliderShape
{
    public Vector2 Center;
    public float Radius;
    public int VertexCount;

    public readonly int ShapeType => 0;
    public readonly Vector2 ShapeCenter => Center;
    public readonly Vector4 ShapeFields => new Vector4(Radius, VertexCount, 0, 0);
}