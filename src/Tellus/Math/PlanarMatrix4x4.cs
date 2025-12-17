using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Tellus.Math;

public static class PlanarMatrix4x4
{
    public static Matrix4x4 CreateScale(Vector2 scales)
        => Matrix4x4.CreateScale(scales.X, scales.Y, 1f);

    public static Matrix4x4 CreateScale(Vector2 scales, Vector2 centerPoint)
        => Matrix4x4.CreateScale(scales.X, scales.Y, 1f, new Vector3(centerPoint, 1f));

    public static Matrix4x4 CreateScale(float scaleX, float scaleY)
        => Matrix4x4.CreateScale(scaleX, scaleY, 1f);

    public static Matrix4x4 CreateScale(float scaleX, float scaleY, Vector2 centerPoint)
        => Matrix4x4.CreateScale(scaleX, scaleY, 1f, new Vector3(centerPoint, 1f));

    public static Matrix4x4 CreateScale(float scale)
        => Matrix4x4.CreateScale(scale, scale, 1f);

    public static Matrix4x4 CreateScale(float scale, Vector2 centerPoint)
    => Matrix4x4.CreateScale(scale, scale, 1f, new Vector3(centerPoint, 1f));

    public static Matrix4x4 CreateScaleCentered(Vector2 scales)
        => Matrix4x4.CreateScale(scales.X, scales.Y, 1f, new Vector3(0.5f, 0.5f, 1f));

    public static Matrix4x4 CreateScaleCentered(float scaleX, float scaleY)
        => Matrix4x4.CreateScale(scaleX, scaleY, 1f, new Vector3(0.5f, 0.5f, 1f));
    public static Matrix4x4 CreateScaleCentered(float scale)
        => Matrix4x4.CreateScale(scale, scale, 1f, new Vector3(0.5f, 0.5f, 1f));

    public static Matrix4x4 CreateTranslation(Vector2 position)
        => Matrix4x4.CreateTranslation(position.X, position.Y, 0);

    public static Matrix4x4 CreateTranslation(float xPosition, float yPosition)
        => Matrix4x4.CreateTranslation(xPosition, yPosition, 0);

    public static Matrix4x4 CreateRotation(float radians)
        => Matrix4x4.CreateRotationZ(radians);

    public static Matrix4x4 CreateRotation(float radians, Vector2 centerPoint)
        => Matrix4x4.CreateRotationZ(radians, new Vector3(centerPoint, 1f));

    public static Matrix4x4 CreateRotationCentered(float radians)
        => Matrix4x4.CreateRotationZ(radians, new Vector3(0.5f, 0.5f, 1f));
}
