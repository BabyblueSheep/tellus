using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Tellus.Collision.Individual;

// https://dyn4j.org/2010/01/sat/
// https://www.metanetsoftware.com/technique/tutorialA.html
public static partial class CollisionHandler
{
    const float EPSILON = 0.0001f;

    private static bool DoProjectionsOverlap((float, float) projectionOne, (float, float) projectionTwo)
    {
        return projectionOne.Item1 <= projectionTwo.Item2 && projectionOne.Item2 >= projectionTwo.Item1;
    }

    private static (float, float) ProjectVerticesOnAxis(ReadOnlySpan<Vector2> vertices, Vector2 offset, Vector2 axis)
    {
        float minProjectionPosition = Vector2.Dot(vertices[0] + offset, axis);
        float maxProjectionPosition = minProjectionPosition;

        for (int i = 1; i < vertices.Length; i++)
        {
            float currentProjectionPosition = Vector2.Dot(vertices[i] + offset, axis);
            minProjectionPosition = MathF.Min(minProjectionPosition, currentProjectionPosition);
            maxProjectionPosition = MathF.Max(maxProjectionPosition, currentProjectionPosition);
        }

        return (minProjectionPosition, maxProjectionPosition);
    }

    private static float Cross(Vector2 x, Vector2 y)
    {
        return x.X * y.Y - x.Y * y.X;
    }

    // https://stackoverflow.com/questions/563198/how-do-you-detect-where-two-line-segments-intersect/565282#565282
    // https://github.com/pgkelley4/line-segments-intersect/blob/master/js/line-segments-intersect.js

    // https://en.wikipedia.org/wiki/Line%E2%80%93line_intersection
    // https://mathworld.wolfram.com/Line-LineIntersection.html
    // https://theswissbay.ch/pdf/Gentoomen%20Library/Game%20Development/Programming/Graphics%20Gems%203.pdf
    private static (bool, Vector2) GetLineLineIntersection(Vector2 lineOneStart, Vector2 lineOneEnd, Vector2 lineTwoStart, Vector2 lineTwoEnd)
    {
        var lineOneDirection = lineOneEnd - lineOneStart;
        var lineTwoDirection = lineTwoEnd - lineTwoStart;
        var originDifference = lineTwoStart - lineOneEnd;

        float lineOneNominator = Cross(originDifference, lineTwoDirection);
        float lineTwoNominator = Cross(originDifference, lineOneDirection);
        float denominator = Cross(lineOneDirection, lineTwoDirection);


        bool denominatorIsZero = System.Math.Abs(denominator) < EPSILON;
        bool nominatorTwoIsZero = System.Math.Abs(lineTwoNominator) < EPSILON;

        bool areLinesCollinear = denominatorIsZero && nominatorTwoIsZero;
        bool areLinesParallel = denominatorIsZero && !nominatorTwoIsZero;
        bool areLinesIntersecting = !denominatorIsZero;

        if (areLinesCollinear)
        {
            return (false, Vector2.Zero); // TODO: figure out what to do here
        }
        else if (areLinesParallel)
        {
            return (false, Vector2.Zero);
        }
        else if (areLinesIntersecting)
        {
            float lineProgressOne = lineOneNominator / denominator;
            float lineProgressTwo = lineTwoNominator / denominator;

            if ((lineProgressOne >= 0.0 && lineProgressOne <= 1.0) && (lineProgressTwo >= 0.0 && lineProgressTwo <= 1.0))
            {
                var intersectionPoint = Vector2.Lerp(lineTwoStart, lineTwoEnd, lineProgressTwo);
                return (true, Vector2.Zero);
            }
            return (false, Vector2.Zero);
        }
        else
        {
            return (false, Vector2.Zero);
        }
    }
}
