using System.Drawing;
using System.Numerics;
using System;
using Tellus.Math.Shapes;

namespace Tellus.Collision;

public sealed class CollisionPolygon
{
    public Vector2[] Vertices { get; }

    public CollisionPolygon(Circle circle)
    {
        uint vertexCount = 32;
        Vertices = new Vector2[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            Vertices[i] = circle.Center + new Vector2(MathF.Cos(MathF.Tau * i / vertexCount), MathF.Sin(MathF.Tau * i / vertexCount)) * circle.Radius;
        }
    }

    public CollisionPolygon(Math.Shapes.Rectangle rectangle)
    {
        var sine = (float)System.Math.Sin(rectangle.Angle);
        var cosine = (float)System.Math.Cos(rectangle.Angle);

        var sideA = rectangle.Width;
        var sideB = rectangle.Height;

        Vertices = new Vector2[4];

        Vertices[0] = new Vector2(-sideA * 0.5f, -sideB * 0.5f);
        Vertices[1] = new Vector2(sideA * 0.5f, -sideB * 0.5f);
        Vertices[2] = new Vector2(sideA * 0.5f, sideB * 0.5f);
        Vertices[3] = new Vector2(-sideA * 0.5f, sideB * 0.5f);
        for (int i = 0; i < 4; i++)
        {
            var newX = (Vertices[i].X * cosine) + (Vertices[i].Y * (-sine));
            var newY = (Vertices[i].X * sine) + (Vertices[i].Y * cosine);
            Vertices[i] = new Vector2(newX, newY);

            Vertices[i] += rectangle.Center;
        }
    }

    public CollisionPolygon(Triangle triangle)
    {
        Vertices = new Vector2[3];
        Vertices[0] = triangle.PointOne;
        Vertices[1] = triangle.PointTwo;
        Vertices[2] = triangle.PointThree;
    }
}