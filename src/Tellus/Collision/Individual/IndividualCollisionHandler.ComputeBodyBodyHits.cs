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
    public static bool ComputeBodyBodyHits(ICollisionBody bodyOne, ICollisionBody bodyTwo)
    {
        static bool DoBodyPartsOverlap(List<Vector2> verticesMain, List<Vector2> verticesSub)
        {
            for (int i = 0; i < verticesMain.Count; i++)
            {
                int j = (i == (verticesMain.Count - 1)) ? 0 : (i + 1);
                var vertexOne = verticesMain[i];
                var vertexTwo = verticesMain[j];

                var edge = vertexTwo - vertexOne;
                var normal = new Vector2(-edge.Y, edge.X);
                var axis = normal.SafeNormalize(Vector2.UnitX);

                var shapeOneProjection = ProjectVerticesOnAxis(verticesMain, axis);
                var shapeTwoProjection = ProjectVerticesOnAxis(verticesSub, axis);

                if (!DoProjectionsOverlap(shapeOneProjection, shapeTwoProjection))
                {
                    return false;
                }
            }

            return true;
        }

        var bodyVerticesOne = new List<List<Vector2>>();
        foreach (var bodyPart in bodyOne.BodyParts)
        {
            var vertexList = BodyPartToVertices(bodyPart, bodyOne);
            bodyVerticesOne.Add(vertexList);
        }

        var bodyVerticesTwo = new List<List<Vector2>>();
        foreach (var bodyPart in bodyTwo.BodyParts)
        {
            var vertexList = BodyPartToVertices(bodyPart, bodyTwo);
            bodyVerticesTwo.Add(vertexList);
        }

        foreach (var bodyPartsOne in bodyVerticesOne)
        {
            foreach (var bodyPartsTwo in bodyVerticesTwo)
            {
                bool doBodyPartsOverlap = DoBodyPartsOverlap(bodyPartsOne, bodyPartsTwo);
                if (doBodyPartsOverlap)
                {
                    return true;
                }
                doBodyPartsOverlap = DoBodyPartsOverlap(bodyPartsTwo, bodyPartsOne);
                if (doBodyPartsOverlap)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
