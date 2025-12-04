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

public static partial class BatchCollisionHandler
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

        [FieldOffset(12)]
        public int Padding;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct CollisionLineCollectionData
    {
        [FieldOffset(0)]
        public int LineIndexStart;

        [FieldOffset(4)]
        public int LineIndexLength;

        [FieldOffset(8)]
        public Vector2 Offset;

        [FieldOffset(16)]
        public int LineVelocityIndex;

        [FieldOffset(20)]
        public int Padding;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct CollisionLineData
    {
        [FieldOffset(0)]
        public Vector2 Origin;

        [FieldOffset(8)]
        public Vector2 Vector;

        [FieldOffset(16)]
        public float Length;

        [FieldOffset(20)]
        public int Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    private struct CollisionBodyLineCollectionPair
    {
        [FieldOffset(0)]
        public int BodyIndex;

        [FieldOffset(4)]
        public int LineCollectionIndex;
    }

    record struct ComputeBodyBodyHitsUniforms(int BodyDataBufferOneStartIndex, int BodyDataBufferOneLength, int BodyDataBufferTwoStartIndex, int BodyDataBufferTwoLength, int ColliderShapeResultBufferLength);
    record struct ResolveBodyBodyCollisionsUniforms(int BodyDataBufferOneStartIndex, int BodyDataBufferOneLength, int BodyDataBufferTwoStartIndex, int BodyDataBufferTwoLength, int ColliderShapeResultBufferLength);
    record struct ComputeLineBodyHitsUniforms(int BodyDataBufferStartIndex, int BodyDataBufferLength, int LineCollectionDataBufferStartIndex, int LineCollectionDataBufferLength, int ColliderShapeResultBufferLength);
    record struct RestrictLinesUniforms(int BodyDataBufferStartIndex, int BodyDataBufferLength, int LineCollectionDataBufferStartIndex, int LineCollectionDataBufferLength);
    record struct IncrementLineCollectionOffsetsUniforms(int LineCollectionDataBufferStartIndex, int LineCollectionDataBufferLength);
    record struct IncrementLineCollectionBodiesOffsetsUniforms(int PairDataBufferStartIndex, int PairDataBufferLength);
}
