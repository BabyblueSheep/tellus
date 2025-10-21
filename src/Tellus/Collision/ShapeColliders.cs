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
}

public struct LineCollider : IColliderShape
{
    public Vector2 PointOne;
    public Vector2 PointTwo;
}
