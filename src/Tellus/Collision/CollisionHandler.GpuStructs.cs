﻿using MoonWorks;
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

// THINGS TO DOCUMENT:
// max shapes allowed: SHAPE_DATA_AMOUNT (2048) (hard limit but i may expand)

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
        public int Flags;

        [FieldOffset(44)]
        public int Padding2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    private struct CollisionResultData
    {
        [FieldOffset(0)]
        public int CollisionBodyIndexOne;

        [FieldOffset(4)]
        public int CollisionBodyIndexTwo;
    }

    record struct CollisionComputeUniforms(uint ShapeDataBufferOneLength, uint ShapeDataBufferTwoLength, uint ColliderShapeResultBufferLength);
}
