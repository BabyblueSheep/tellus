using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Tellus.Math;
using static MoonWorks.Graphics.VertexStructs;

namespace Tellus.Collision.Individual;

public static partial class IndividualCollisionHandler
{
    public static bool ComputeLineBodyHit((Vector2, Vector2) line, CollisionBody body)
    {
        var lineStart = line.Item1;
        var lineEnd = line.Item2;

        foreach (var bodyPart in body)
        {
            foreach (var side in bodyPart.Sides)
            {
                var bodyPartLineStart = side.Item1 + body.Offset;
                var bodyPartLineEnd = side.Item2 + body.Offset;

                var didIntersect = GetLineLineIntersection(bodyPartLineStart, bodyPartLineEnd, lineStart, lineEnd);
                if (didIntersect.Item1)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
