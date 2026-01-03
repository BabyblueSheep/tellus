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
    const float EPSILON = 0.0001f;

    private static List<Vector2> BodyPartToVertices(CollisionBodyPart bodyPart, ICollisionBody body)
    {
        var vertices = new List<Vector2>();
        
        switch (bodyPart.ShapeType)
        {
            case CollisionBodyPartShapeType.Circle:
                var vertexAmount = bodyPart.IntegerFields.X;
                var radius = bodyPart.DecimalFields.X;
                for (int i = 0; i < vertexAmount; i++)
                {
                    vertices.Add(bodyPart.BodyPartCenter + new Vector2(MathF.Cos(MathF.Tau * i / vertexAmount), MathF.Sin(MathF.Tau * i / vertexAmount)) * radius);
                }
                break;
            case CollisionBodyPartShapeType.Rectangle:
                var angle = bodyPart.DecimalFields.Z;
                var sine = MathF.Sin(angle);
                var cosine = MathF.Cos(angle);

                var sideA = bodyPart.DecimalFields.X;
                var sideB = bodyPart.DecimalFields.Y;

                vertices.Add(new Vector2(-sideA * 0.5f, -sideB * 0.5f));
                vertices.Add(new Vector2(sideA * 0.5f, -sideB * 0.5f));
                vertices.Add(new Vector2(sideA * 0.5f, sideB * 0.5f));
                vertices.Add(new Vector2(-sideA * 0.5f, sideB * 0.5f));
                for (int i = 0; i < 4; i++)
                {
                    var newX = (vertices[i].X * cosine) + (vertices[i].Y * (-sine));
                    var newY = (vertices[i].X * sine) + (vertices[i].Y * cosine);
                    vertices[i] = new Vector2(newX, newY);

                    vertices[i] += bodyPart.BodyPartCenter;
                }
                break;
            case CollisionBodyPartShapeType.Triangle:
                vertices.Add(bodyPart.BodyPartCenter);
                vertices.Add(new Vector2(bodyPart.DecimalFields.X, bodyPart.DecimalFields.Y));
                vertices.Add(new Vector2(bodyPart.DecimalFields.Z, bodyPart.DecimalFields.W));
                break;
        }

        for (int i = 0; i < vertices.Count; i++)
        {
            vertices[i] = vertices[i] + body.BodyOffset;
        }

        return vertices;
    }

    private static bool DoProjectionsOverlap(Vector2 projectionOne, Vector2 projectionTwo)
    {
        return projectionOne.X <= projectionTwo.Y && projectionOne.Y >= projectionTwo.X;
    }

    private static Vector2 ProjectVerticesOnAxis(List<Vector2> vertices, Vector2 axis)
    {
        float minProjectionPosition = Vector2.Dot(vertices[0], axis);
        float maxProjectionPosition = minProjectionPosition;

        for (int i = 1; i < vertices.Count; i++)
        {
            float currentProjectionPosition = Vector2.Dot(vertices[i], axis);
            minProjectionPosition = MathF.Min(minProjectionPosition, currentProjectionPosition);
            maxProjectionPosition = MathF.Max(maxProjectionPosition, currentProjectionPosition);
        }

        return new Vector2(minProjectionPosition, maxProjectionPosition);
    }

    private static float Cross(Vector2 x, Vector2 y)
    {
        return x.X * y.Y - x.Y * y.X;
    }

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
