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
    public static Vector2 ResolveBodyBodyCollisions(CollisionBody bodyMovable, IEnumerable<CollisionBody> bodyListImmovable)
    {
        var totalMinimumTransitionVector = Vector2.Zero;

        for (int iteration = 0; iteration < 16; iteration++)
        {
        }
            /*
            static float GetProjectionOverlap(Vector2 projectionOne, Vector2 projectionTwo)
            {
                float start = MathF.Max(projectionOne.X, projectionTwo.X);
                float end = MathF.Min(projectionOne.Y, projectionTwo.Y);

                float result = end - start;
                float direction = projectionOne.X > projectionTwo.X ? 1 : -1;
                return result * direction;
            }

            static Vector4 DoBodyPartsOverlap(List<Vector2> verticesOne, List<Vector2> verticesTwo)
            {
                var minimumTransitionVectorLength = 999999.9f;
                Vector2 minimumTransitionVectorDirection = Vector2.Zero;

                for (int i = 0; i < verticesOne.Count; i++)
                {
                    int j = (i == (verticesOne.Count - 1)) ? 0 : (i + 1);
                    var vertexOne = verticesOne[i];
                    var vertexTwo = verticesOne[j];

                    var edge = vertexTwo - vertexOne;
                    var normal = new Vector2(-edge.Y, edge.X);
                    var axis = normal.SafeNormalize(Vector2.UnitX);

                    var shapeOneProjection = ProjectVerticesOnAxis(verticesOne, axis);
                    var shapeTwoProjection = ProjectVerticesOnAxis(verticesTwo, axis);

                    if (!DoProjectionsOverlap(shapeOneProjection, shapeTwoProjection))
                    {
                        return Vector4.Zero;
                    }

                    float currentMtvLength = GetProjectionOverlap(shapeOneProjection, shapeTwoProjection);
                    if (System.Math.Abs(minimumTransitionVectorLength) > System.Math.Abs(currentMtvLength))
                    {
                        minimumTransitionVectorLength = currentMtvLength;
                        minimumTransitionVectorDirection = axis;
                    }
                }

                for (int i = 0; i < verticesTwo.Count; i++)
                {
                    int j = (i == (verticesTwo.Count - 1)) ? 0 : (i + 1);
                    var vertexOne = verticesTwo[i];
                    var vertexTwo = verticesTwo[j];

                    var edge = vertexTwo - vertexOne;
                    var normal = new Vector2(-edge.Y, edge.X);
                    var axis = normal.SafeNormalize(Vector2.UnitX);

                    var shapeOneProjection = ProjectVerticesOnAxis(verticesOne, axis);
                    var shapeTwoProjection = ProjectVerticesOnAxis(verticesTwo, axis);

                    if (!DoProjectionsOverlap(shapeOneProjection, shapeTwoProjection))
                    {
                        return Vector4.Zero;
                    }

                    float currentMtvLength = GetProjectionOverlap(shapeOneProjection, shapeTwoProjection);
                    if (System.Math.Abs(minimumTransitionVectorLength) > System.Math.Abs(currentMtvLength))
                    {
                        minimumTransitionVectorLength = currentMtvLength;
                        minimumTransitionVectorDirection = axis;
                    }
                }

                return new Vector4(1f, minimumTransitionVectorLength, minimumTransitionVectorDirection.X, minimumTransitionVectorDirection.Y);
            }

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

            var bodyVerticesMovable = new List<List<Vector2>>();
            foreach (var bodyPart in bodyMovable.BodyParts)
            {
                var vertexList = BodyPartToVertices(bodyPart, bodyMovable);
                bodyVerticesMovable.Add(vertexList);
            }

            var totalMinimumTransitionVector = Vector2.Zero;

            for (int iteration = 0; iteration < 16; iteration++)
            {
                var hasCollidedWithAnythingThisIteration = false;

                var smallestCurrentMinimumTransitionVectorLength = 999999.9f;
                var smallestCurrentMinimumTransitionVector = Vector2.Zero;

                foreach (var bodyPartsMovable in bodyVerticesMovable)
                {
                    foreach (var bodiesImmovable in verticesListImmovable)
                    {
                        foreach (var bodyPartsImmovable in bodiesImmovable.Item2)
                        {
                            var overlapInfo = DoBodyPartsOverlap(bodyPartsMovable, bodyPartsImmovable);

                            var doBodyPartsCollide = overlapInfo.X != 0f;
                            var minimumTransitionVectorLength = overlapInfo.Y;
                            var minimumTransitionVectorDirection = new Vector2(overlapInfo.Z, overlapInfo.W);

                            if (!doBodyPartsCollide)
                                continue;

                            if (System.Math.Abs(minimumTransitionVectorLength) < EPSILON)
                                continue;

                            hasCollidedWithAnythingThisIteration = true;

                            if (System.Math.Abs(smallestCurrentMinimumTransitionVectorLength) > System.Math.Abs(minimumTransitionVectorLength))
                            {
                                var minimumTransitionVector = minimumTransitionVectorDirection * minimumTransitionVectorLength;

                                smallestCurrentMinimumTransitionVectorLength = minimumTransitionVectorLength;
                                smallestCurrentMinimumTransitionVector = minimumTransitionVector;
                            }
                        }
                    }
                }

                if (!hasCollidedWithAnythingThisIteration)
                {
                    break;
                }
                else
                {
                    totalMinimumTransitionVector += smallestCurrentMinimumTransitionVector;
                    for (int i = 0; i < bodyVerticesMovable.Count; i++)
                    {
                        for (int j = 0; j < bodyVerticesMovable[i].Count; j++)
                        {
                            bodyVerticesMovable[i][j] += smallestCurrentMinimumTransitionVector;
                        }
                    }
                }
            }

            return totalMinimumTransitionVector;
            */
        }
}
