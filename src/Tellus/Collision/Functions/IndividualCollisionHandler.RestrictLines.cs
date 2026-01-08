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
    public static List<float> RestrictLines(ICollisionLineCollection lineCollection, IEnumerable<ICollisionBody> bodyListImmovable)
    {
        List<(ICollisionBody, List<List<Vector2>>)> verticesListImmovable = [];
        foreach (var body in bodyListImmovable)
        {
            var bodyPartList = new List<List<Vector2>>();
            foreach (var bodyPart in body.BodyParts)
            {
                var vertexList = BodyPartToVertices(bodyPart, body);
                bodyPartList.Add(vertexList);
            }
            verticesListImmovable.Add((body, bodyPartList));
        }

        var newLengths = new List<float>();
        foreach (var originalLine in lineCollection.Lines)
        {
            var line = originalLine;
            line.Origin += lineCollection.OriginOffset;

            var lineStart = line.Origin;
            var lineEnd = line.IsVectorFixedPoint ? line.ArbitraryVector : line.Origin + line.ArbitraryVector * line.Length;

            var smallestNewLength = line.Length;

            foreach (var body in verticesListImmovable)
            {
                foreach (var bodyPart in body.Item2)
                {
                    for (int i = 0; i < bodyPart.Count; i++)
                    {
                        int j = (i == (bodyPart.Count - 1)) ? 0 : (i + 1);

                        var bodyPartLineStart = bodyPart[i];
                        var bodyPartLineEnd = bodyPart[j];

                        (bool, Vector2) didIntersect = GetLineLineIntersection(bodyPartLineStart, bodyPartLineEnd, lineStart, lineEnd);
                        if (didIntersect.Item1)
                        {
                            float newLength = (didIntersect.Item2 - lineStart).Length();
                            if (newLength < smallestNewLength)
                            {
                                smallestNewLength = newLength;
                            }
                        }
                    }
                }
            }

            newLengths.Add(smallestNewLength);
        }

        return newLengths;
    }
}
