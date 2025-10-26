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
    public IEnumerable<Vector2> ShapeVertices { get; }
    public IEnumerable<(int, int)> ShapeIndexRanges { get; }
}

public static class ColliderShapeProvider
{
    public static IEnumerable<Vector2> GetCircleVertices(Vector2 offset, float radius, int pointAmount)
    {
        for (int i = 0; i < pointAmount; i++)
        {
            float angle = (i / (float)pointAmount) * MathF.Tau;
            yield return offset + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
    }

    public static IEnumerable<Vector2> GetRectangleVertices(Vector2 offset, float sideA, float sideB)
    {
        yield return offset;
        yield return offset + new Vector2(sideA, 0);
        yield return offset + new Vector2(0, sideB);
        yield return offset + new Vector2(sideA, sideB);
    }

    public static IEnumerable<Vector2> GetLineVertices(Vector2 offset, Vector2 start, Vector2 end)
    {
        yield return offset + start;
        yield return offset + end;
    }

    public static IEnumerable<(int, int)> GetConnectedShapeIndices(int offset, int pointAmount)
    {
        yield return (offset, pointAmount);
    }
}