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
    public static bool ComputeLineBodyHits(ICollisionBody body, ICollisionLineCollection lineCollection)
    {
        var bodyVertices = new List<List<Vector2>>();
        foreach (var bodyPart in body.BodyParts)
        {
            var vertexList = BodyPartToVertices(bodyPart, body);
            bodyVertices.Add(vertexList);
        }

        foreach (var originalLine in lineCollection.Lines)
        {
            var line = originalLine;
            line.Origin += lineCollection.OriginOffset;

            var lineStart = line.Origin;
            var lineEnd = line.IsVectorFixedPoint ? line.ArbitraryVector : line.Origin + line.ArbitraryVector * line.Length;

            foreach (var bodyPart in bodyVertices)
            {
                for (int i = 0; i < bodyPart.Count; i++)
                {
                    int j = (i == (bodyPart.Count - 1)) ? 0 : (i + 1);

                    var bodyPartLineStart = bodyPart[i];
                    var bodyPartLineEnd = bodyPart[j];

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
