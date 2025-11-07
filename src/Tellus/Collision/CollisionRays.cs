using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Tellus.Collision;

public interface ICollisionRayCaster
{
    public Vector2 RayOriginOffset { get; }
    public IEnumerable<CollisionRay> Rays { get; }
}

public struct CollisionRay
{
    public Vector2 RayOrigin;
    public Vector2 RayDirection;
    public float RayLength;
}