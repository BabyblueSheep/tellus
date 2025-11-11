using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Tellus.Collision;

public interface ICollisionRayCaster
{
    public Vector2 RayOriginOffset { get; }
    public IEnumerable<CollisionRay> Rays { get; }
    public int RayVelocityIndex => 0;
}

public struct CollisionRay
{
    public Vector2 RayOrigin;
    public Vector2 RayDirection;
    public float RayLength;
    public bool CanBeRestricted;

    public CollisionRay(Vector2 origin, Vector2 direction, float length)
    {
        RayOrigin = origin;
        RayDirection = direction;
        RayLength = length;

        CanBeRestricted = true;
    }

}