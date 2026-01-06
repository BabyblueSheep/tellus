using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Tellus.Math.Shapes;

public readonly record struct Rectangle
{
    public Vector2 Center { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public double Angle { get; init; }

    public static Rectangle Unit
    {
        get => new Rectangle(Vector2.Zero, 1f, 1f, 0);
    }

    public Rectangle(System.Drawing.Rectangle rectangle)
    {
        Center = new Vector2(rectangle.X + rectangle.Width * 0.5f, rectangle.Y + rectangle.Height * 0.5f);
        Width = rectangle.Width;
        Height = rectangle.Height;
        Angle = 0f;
    }

    public Rectangle(float size)
    {
        Center = Vector2.Zero;
        Width = size;
        Height = size;
        Angle = 0f;
    }

    public Rectangle(float size, double angle)
    {
        Center = Vector2.Zero;
        Width = size;
        Height = size;
        Angle = angle;
    }

    public Rectangle(float width, float height)
    {
        Center = Vector2.Zero;
        Width = width;
        Height = height;
        Angle = 0f;
    }

    public Rectangle(Vector2 center, float width, float height)
    {
        Center = center;
        Width = width;
        Height = height;
        Angle = 0f;
    }

    public Rectangle(float width, float height, double angle)
    {
        Center = Vector2.Zero;
        Width = width;
        Height = height;
        Angle = angle;
    }

    public Rectangle(Vector2 center, float size, double angle)
    {
        Center = center;
        Width = size;
        Height = size;
        Angle = angle;
    }

    public Rectangle(Vector2 center, float width, float height, double angle)
    {
        Center = center;
        Width = width;
        Height = height;
        Angle = angle;
    }
}
