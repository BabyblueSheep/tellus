using System.Numerics;
using Tellus.Math;
using Tellus.Math.Shapes;

namespace Tellus.Collision;

/// <summary>
/// A convex polygon composed of vertices. Used for collision detection.
/// </summary>
public sealed class CollisionPolygon
{
    private readonly Vector2[] _vertices;
    private readonly (Vector2, Vector2)[] _sides;
    private readonly Vector2[] _normals;

    /// <summary>
    /// The vertices of the polygon.
    /// </summary>
    public ReadOnlySpan<Vector2> Vertices => new(_vertices);
    /// <summary>
    /// The sides of the polygon.
    /// </summary>
    public ReadOnlySpan<(Vector2, Vector2)> Sides => new(_sides);
    /// <summary>
    /// The normalized normals of the polygon.
    /// </summary>
    public ReadOnlySpan<Vector2> Normals => new(_normals);

    /// <summary>
    /// Constructs a polygon that approximates a circle.
    /// </summary>
    /// <param name="circle">The parameters of a circle to use.</param>
    /// <param name="vertexCount">The amount of vertices to use. Can not go below 3.</param>
    public CollisionPolygon(Circle circle, uint vertexCount = 16)
    {
        vertexCount = uint.Max(vertexCount, 3);
        _vertices = new Vector2[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            _vertices[i] = circle.Center + new Vector2(MathF.Cos(MathF.Tau * i / vertexCount), MathF.Sin(MathF.Tau * i / vertexCount)) * circle.Radius;
        }

        _sides = new (Vector2, Vector2)[_vertices.Length];
        _normals = new Vector2[_vertices.Length];
        FormSides();
    }

    /// <summary>
    /// Constructs a polygon that represents a rotateable rectangle.
    /// </summary>
    /// <param name="rectangle">The parameters of a rectangle to use.</param>
    public CollisionPolygon(Math.Shapes.Rectangle rectangle)
    {
        var sine = (float)System.Math.Sin(rectangle.Angle);
        var cosine = (float)System.Math.Cos(rectangle.Angle);

        var sideA = rectangle.Width;
        var sideB = rectangle.Height;

        _vertices = new Vector2[4];

        _vertices[0] = new Vector2(-sideA * 0.5f, -sideB * 0.5f);
        _vertices[1] = new Vector2(sideA * 0.5f, -sideB * 0.5f);
        _vertices[2] = new Vector2(sideA * 0.5f, sideB * 0.5f);
        _vertices[3] = new Vector2(-sideA * 0.5f, sideB * 0.5f);
        for (int i = 0; i < 4; i++)
        {
            var newX = (_vertices[i].X * cosine) + (_vertices[i].Y * (-sine));
            var newY = (_vertices[i].X * sine) + (_vertices[i].Y * cosine);
            _vertices[i] = new Vector2(newX, newY);

            _vertices[i] += rectangle.Center;
        }

        _sides = new (Vector2, Vector2)[_vertices.Length];
        _normals = new Vector2[_vertices.Length];
        FormSides();
    }

    /// <summary>
    /// Constructs a polygon that represents a triangle.
    /// </summary>
    /// <param name="triangle">The parameters of a triangle to use.</param>
    public CollisionPolygon(Triangle triangle)
    {
        _vertices = new Vector2[3];
        _vertices[0] = triangle.PointOne;
        _vertices[1] = triangle.PointTwo;
        _vertices[2] = triangle.PointThree;

        _sides = new (Vector2, Vector2)[_vertices.Length];
        _normals = new Vector2[_vertices.Length];
        FormSides();
    }

    private void FormSides()
    {
        for (int i = 0; i < _vertices.Length; i++)
        {
            int j = (i == (_vertices.Length - 1)) ? 0 : (i + 1);
            var vertexOne = _vertices[i];
            var vertexTwo = _vertices[j];

            var edge = vertexTwo - vertexOne;
            _sides[i] = (vertexOne, vertexTwo);
            _normals[i] = new Vector2(-edge.Y, edge.X);
            _normals[i] = Vector2.Normalize(_normals[i]);
        }
    }
}