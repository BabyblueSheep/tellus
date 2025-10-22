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

public struct LineCollider : IColliderShape
{
    public Vector2 StartPoint;
    public Vector2 EndPoint;

    public LineCollider(Vector2 startPoint, Vector2 endPoint)
    {
        StartPoint = startPoint;
        EndPoint = endPoint;
    }
}
