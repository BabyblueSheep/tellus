using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Tellus.Math;
using static MoonWorks.Graphics.VertexStructs;

namespace Tellus.Collision.Individual;

public static partial class CollisionHandler
{
    public static Vector2 RestrictLine((Vector2, Vector2) line, IEnumerable<CollisionBody> bodyListImmovable)
    {
        var lineStart = line.Item1;
        var lineEnd = line.Item2;

        var smallestNewLength = (lineEnd - lineStart).Length();
        var newPoint = lineEnd;

        foreach (var body in bodyListImmovable)
        {
            foreach (var bodyPart in body)
            {
                foreach (var side in bodyPart.Sides)
                {
                    var bodyPartLineStart = side.Item1 + body.Offset;
                    var bodyPartLineEnd = side.Item2 + body.Offset;

                    var didIntersect = GetLineLineIntersection(bodyPartLineStart, bodyPartLineEnd, lineStart, lineEnd);
                    if (didIntersect.Item1)
                    {
                        float newLength = (didIntersect.Item2 - lineStart).Length();
                        if (newLength < smallestNewLength)
                        {
                            smallestNewLength = newLength;
                            newPoint = didIntersect.Item2;
                        }
                    }
                }
            }
        }

        return newPoint;
    }
}
