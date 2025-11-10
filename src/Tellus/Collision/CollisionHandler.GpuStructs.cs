using MoonWorks;
using MoonWorks.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Mail;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    private struct CollisionBodyPartData
    {
        [FieldOffset(0)]
        public int CollisionBodyIndex;

        [FieldOffset(4)]
        public int ShapeType;

        [FieldOffset(8)]
        public Vector2 Center;

        [FieldOffset(16)]
        public Vector4 DecimalFields;

        [FieldOffset(32)]
        public Point IntegerFields;

        [FieldOffset(40)]
        public int Padding1;

        [FieldOffset(44)]
        public int Padding2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct CollisionBodyData
    {
        [FieldOffset(0)]
        public int BodyPartIndexStart;

        [FieldOffset(4)]
        public int BodyPartIndexLength;

        [FieldOffset(8)]
        public Vector2 Offset;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    private struct CollisionHitData
    {
        [FieldOffset(0)]
        public int CollisionBodyIndexOne;

        [FieldOffset(4)]
        public int CollisionBodyIndexTwo;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct CollisionResolutionData
    {
        [FieldOffset(0)]
        public int CollisionBodyIndex;

        [FieldOffset(4)]
        public Vector2 TotalMinimumTransitionVector;

        [FieldOffset(8)]
        public int Padding;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct CollisionRayCasterData
    {
        [FieldOffset(0)]
        public int RayIndexStart;

        [FieldOffset(4)]
        public int RayIndexLength;

        [FieldOffset(8)]
        public Vector2 Offset;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct CollisionRayData
    {
        [FieldOffset(0)]
        public Vector2 RayOrigin;

        [FieldOffset(8)]
        public Vector2 RayDirection;

        [FieldOffset(16)]
        public float RayLength;

        [FieldOffset(20)]
        public int RayVelocityIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    private struct CollisionBodyRayCasterPair
    {
        [FieldOffset(0)]
        public int BodyIndex;

        [FieldOffset(4)]
        public int RayCasterIndex;
    }

    record struct CollisionComputeUniforms(uint StoredBodyCountOne, uint StoredBodyCountTwo, uint ColliderShapeResultBufferLength);
    record struct RayComputeUniforms(uint StoredBodyCount, uint StoredRayCasterCount);
    record struct IncrementRaysUniforms(uint StoredRayCasterCount);
    record struct IncrementPairsUniforms(uint StoredPairCount);
}
