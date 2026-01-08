using MoonWorks.Input;
using System.Numerics;

namespace Tellus.Collision.Individual;

public static partial class IndividualCollisionHandler
{
    public static Vector2 ResolveBodyBodyCollisions(CollisionBody bodyMovable, IEnumerable<CollisionBody> bodyListImmovable)
    {
        static float GetProjectionOverlap((float, float) projectionOne, (float, float) projectionTwo)
        {
            float start = MathF.Max(projectionOne.Item1, projectionTwo.Item1);
            float end = MathF.Min(projectionOne.Item2, projectionTwo.Item2);

            float result = end - start;
            float direction = projectionOne.Item1 > projectionTwo.Item1 ? 1 : -1;
            return result * direction;
        }

        static (bool, float, Vector2) DoBodyPartsOverlap(CollisionPolygon bodyPartMain, Vector2 offsetMain, CollisionPolygon bodyPartSub, Vector2 offsetSub)
        {
            var minimumTransitionVectorLength = 999999.9f;
            Vector2 minimumTransitionVectorDirection = Vector2.Zero;

            foreach (var normal in bodyPartMain.Normals)
            {
                var shapeOneProjection = ProjectVerticesOnAxis(bodyPartMain.Vertices, offsetMain, normal);
                var shapeTwoProjection = ProjectVerticesOnAxis(bodyPartSub.Vertices, offsetSub, normal);

                if (!DoProjectionsOverlap(shapeOneProjection, shapeTwoProjection))
                {
                    return (false, 0f, Vector2.Zero);
                }

                float currentMtvLength = GetProjectionOverlap(shapeOneProjection, shapeTwoProjection);
                if (System.Math.Abs(minimumTransitionVectorLength) > System.Math.Abs(currentMtvLength))
                {
                    minimumTransitionVectorLength = currentMtvLength;
                    minimumTransitionVectorDirection = normal;
                }
            }

            foreach (var normal in bodyPartSub.Normals)
            {
                var shapeOneProjection = ProjectVerticesOnAxis(bodyPartMain.Vertices, offsetMain, normal);
                var shapeTwoProjection = ProjectVerticesOnAxis(bodyPartSub.Vertices, offsetSub, normal);

                if (!DoProjectionsOverlap(shapeOneProjection, shapeTwoProjection))
                {
                    return (false, 0f, Vector2.Zero);
                }

                float currentMtvLength = GetProjectionOverlap(shapeOneProjection, shapeTwoProjection);
                if (System.Math.Abs(minimumTransitionVectorLength) > System.Math.Abs(currentMtvLength))
                {
                    minimumTransitionVectorLength = currentMtvLength;
                    minimumTransitionVectorDirection = normal;
                }
            }

            return (true, minimumTransitionVectorLength, minimumTransitionVectorDirection.);
        }

        var totalMinimumTransitionVector = Vector2.Zero;

        for (int iteration = 0; iteration < 16; iteration++)
        {
            var hasCollidedWithAnythingThisIteration = false;

            var smallestCurrentMinimumTransitionVectorLength = 999999.9f;
            var smallestCurrentMinimumTransitionVector = Vector2.Zero;

            foreach (var bodyImmovable in bodyListImmovable)
            {
                if (!bodyMovable.IsWithinNarrowRange(bodyImmovable))
                    continue;

                foreach (var bodyPartImmovable in bodyImmovable)
                {
                    foreach (var bodyPartMovable in bodyMovable)
                    {
                        var overlapInfo = DoBodyPartsOverlap(bodyPartMovable, bodyMovable.Offset + totalMinimumTransitionVector, bodyPartImmovable, bodyImmovable.Offset);

                        var doBodyPartsCollide = overlapInfo.Item1;
                        var minimumTransitionVectorLength = overlapInfo.Item2;
                        var minimumTransitionVectorDirection = overlapInfo.Item3;

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
            }
        }

        return totalMinimumTransitionVector;
    }
}
