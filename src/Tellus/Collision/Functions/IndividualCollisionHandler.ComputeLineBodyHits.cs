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
    public static bool ComputeLineBodyHits(CollisionBody body, CollisionLineCollection lineCollection)
    {
        foreach (var line in lineCollection)
        {
            var lineStart = line.Start + lineCollection.Offset;
            var lineEnd = line.End + lineCollection.Offset;

            foreach (var bodyPart in body)
            {
                foreach (var side in bodyPart.Sides)
                {
                    var bodyPartLineStart = side.Item1 + body.Offset;
                    var bodyPartLineEnd = side.Item2 + body.Offset;

                    (bool, Vector2) didIntersect = GetLineLineIntersection(bodyPartLineStart, bodyPartLineEnd, lineStart, lineEnd);
                    if (didIntersect.Item1)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
