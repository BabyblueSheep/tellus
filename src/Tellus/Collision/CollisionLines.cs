using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Tellus.Collision;

public interface ICollisionLineCollection
{
    public Vector2 OriginOffset { get; }
    public IEnumerable<CollisionLine> Lines { get; }
    public int LineVelocityIndex => 0;
}

public struct CollisionLine
{
    public Vector2 Origin;
    public Vector2 ArbitraryVector;
    public float Length;

    public bool CanBeRestricted;
    public bool IsVectorFixedPoint;

    public static CollisionLine CreateFiniteLengthRay(Vector2 origin, Vector2 direction, float length)
    {
        var line = new CollisionLine();
        line.Origin = origin;
        line.ArbitraryVector = direction;
        line.Length = length;

        line.CanBeRestricted = false;
        line.IsVectorFixedPoint = false;

        return line;
    }

    public static CollisionLine CreateFixedPointLineSegment(Vector2 startPoint, Vector2 endPoint)
    {
        var line = new CollisionLine();
        line.Origin = startPoint;
        line.ArbitraryVector = endPoint;
        line.Length = (endPoint - startPoint).Length();

        line.CanBeRestricted = false;
        line.IsVectorFixedPoint = true;

        return line;
    }
}