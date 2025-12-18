using MoonWorks.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Tellus.Graphics.SpriteBatch;

public sealed partial class SpriteBatch
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct PositionTextureColorVertex : IVertexType
    {
        [FieldOffset(0)]
        public Vector4 Position;

        [FieldOffset(16)]
        public Vector2 TexCoord;

        [FieldOffset(32)]
        public Vector4 TintColor;

        [FieldOffset(48)]
        public Vector4 OffsetColor;

        public static VertexElementFormat[] Formats { get; } =
        [
            VertexElementFormat.Float4,
            VertexElementFormat.Float2,
            VertexElementFormat.Float4,
            VertexElementFormat.Float4,
        ];

        public static uint[] Offsets { get; } =
        [
            0,
            16,
            32,
            48
        ];
    }

    record struct VertexUniforms(Matrix4x4 TransformationMatrix);
}
