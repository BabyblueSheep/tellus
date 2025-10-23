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
    public IColliderShape[] GetColliderShapes();
}

public interface IColliderShape { }

public struct CircleCollider : IColliderShape
{
    public Vector2 Center;
    public float Radius;

    public CircleCollider(Vector2 center, float radius)
    {
        Center = center;
        Radius = radius;
    }
}

public struct RectangleCollider : IColliderShape
{
    public Vector2 Center;
    public float Angle;
    public float SideHalfLength;
    public float SideHalfWidth;

    public RectangleCollider(Vector2 center, float angle, float halfLength, float halfWidth)
    {
        Center = center;
        Angle = angle;
        SideHalfLength = halfLength;
        SideHalfWidth = halfWidth;
    }
}
