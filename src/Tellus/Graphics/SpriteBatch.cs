using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Storage;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = MoonWorks.Graphics.Color;

namespace Tellus.Graphics;

[StructLayout(LayoutKind.Explicit, Size = 56)]
file struct SpriteInstanceData
{
    [FieldOffset(0)]
    public Vector3 Position;

    [FieldOffset(12)]
    public float Rotation;

    [FieldOffset(16)]
    public Vector2 Scale;

    [FieldOffset(24)]
    public Vector2 TextureOrigin;

    [FieldOffset(32)]
    public Vector4 Color;

    [FieldOffset(48)]
    public Vector4 TextureSourceRectangle;
}

[StructLayout(LayoutKind.Explicit, Size = 48)]
file struct PositionTextureColorVertex : IVertexType
{
    [FieldOffset(0)]
    public Vector4 Position;

    [FieldOffset(16)]
    public Vector2 TexCoord;

    [FieldOffset(32)]
    public Vector4 Color;

    public static VertexElementFormat[] Formats { get; } =
    [
        VertexElementFormat.Float4,
        VertexElementFormat.Float2,
        VertexElementFormat.Float4
    ];

    public static uint[] Offsets { get; } =
    [
        0,
        16,
        32
    ];
}

public class SpriteBatch : GraphicsResource
{
    private readonly GraphicsPipeline _graphicsPipeline;
    private readonly ComputePipeline _computePipeline;

    private readonly TransferBuffer _instanceTransferBuffer;
    private int _highestInstanceIndex;
    private readonly Buffer _instanceBuffer;
    private readonly Buffer _vertexBuffer;
    private readonly Buffer _indexBuffer;

    private readonly Sampler _sampler;

    record struct SpriteBatchComputeUniforms(Vector2 TextureSize);
    private SpriteBatchComputeUniforms _spriteBatchComputeUniforms;

    private const uint MAXIMUM_SPRITE_AMOUNT = 2048;
    private const uint MAXIMUM_VERTEX_AMOUNT = MAXIMUM_SPRITE_AMOUNT * 4;
    private const uint MAXIMUM_INDEX_AMOUNT = MAXIMUM_SPRITE_AMOUNT * 6;

    public SpriteBatch(GraphicsDevice graphicsDevice, TitleStorage titleStorage, TextureFormat renderTextureFormat) : base(graphicsDevice)
    {
        #region Create pipelines
        var vertexShader = ShaderCross.Create(
            graphicsDevice,
            titleStorage,
            "Assets/TexturedQuad.vert.hlsl",
            "main",
            ShaderCross.ShaderFormat.HLSL,
            ShaderStage.Vertex
        );

        var fragmentShader = ShaderCross.Create(
            graphicsDevice,
            titleStorage,
             "Assets/TexturedQuad.frag.hlsl",
            "main",
            ShaderCross.ShaderFormat.HLSL,
            ShaderStage.Fragment
        );

        var graphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            VertexInputState = VertexInputState.CreateSingleBinding<PositionTextureColorVertex>(),
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = MultisampleState.None,
            DepthStencilState = DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo()
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription()
                    {
                        Format = renderTextureFormat,
                        BlendState = ColorTargetBlendState.PremultipliedAlphaBlend,
                    }
                ],
            },
        };
        _graphicsPipeline = GraphicsPipeline.Create(graphicsDevice, graphicsPipelineCreateInfo);

        _computePipeline = ShaderCross.Create(
            graphicsDevice,
            titleStorage,
            "Assets/SpriteBatch.comp.hlsl",
            "main",
            ShaderCross.ShaderFormat.HLSL
        );
        #endregion

        _sampler = Sampler.Create(graphicsDevice, SamplerCreateInfo.PointClamp);

        #region Create buffers
        _instanceTransferBuffer = TransferBuffer.Create<SpriteInstanceData>
        (
            graphicsDevice,
            TransferBufferUsage.Upload,
            MAXIMUM_SPRITE_AMOUNT
        );

        _instanceBuffer = Buffer.Create<SpriteInstanceData>
        (
            graphicsDevice,
            BufferUsageFlags.ComputeStorageRead,
            MAXIMUM_SPRITE_AMOUNT
        );

        _highestInstanceIndex = 0;

        _vertexBuffer = Buffer.Create<PositionTextureColorVertex>
        (
            graphicsDevice,
            BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.Vertex,
            MAXIMUM_VERTEX_AMOUNT
        );

        _indexBuffer = Buffer.Create<uint>
        (
            graphicsDevice,
            BufferUsageFlags.Index,
            MAXIMUM_INDEX_AMOUNT
        );

        TransferBuffer indexTransferBuffer = TransferBuffer.Create<uint>(
            graphicsDevice,
            TransferBufferUsage.Upload,
            MAXIMUM_INDEX_AMOUNT
        );

        var indexSpan = indexTransferBuffer.Map<uint>(false);
        for (int i = 0, j = 0; i < MAXIMUM_INDEX_AMOUNT; i += 6, j += 4)
        {
            indexSpan[i] = (uint)j;
            indexSpan[i + 1] = (uint)j + 1;
            indexSpan[i + 2] = (uint)j + 2;
            indexSpan[i + 3] = (uint)j + 3;
            indexSpan[i + 4] = (uint)j + 2;
            indexSpan[i + 5] = (uint)j + 1;
        }
        indexTransferBuffer.Unmap();

        var commandBuffer = graphicsDevice.AcquireCommandBuffer();
        var copyPass = commandBuffer.BeginCopyPass();
        copyPass.UploadToBuffer(indexTransferBuffer, _indexBuffer, false);
        commandBuffer.EndCopyPass(copyPass);
        graphicsDevice.Submit(commandBuffer);

        indexTransferBuffer.Dispose();
        #endregion
    }

    public void Begin()
    {
        _instanceTransferBuffer.Map(true);
        _highestInstanceIndex = 0;
    }

    public void Draw(Vector2 textureOrigin, Rectangle textureSourceRectangle, Vector2 position, float rotation, Vector2 scale, Color color, float depth)
    {
        var instanceData = _instanceTransferBuffer.MappedSpan<SpriteInstanceData>();

        instanceData[_highestInstanceIndex].Position = new Vector3(position, depth);
        instanceData[_highestInstanceIndex].Rotation = rotation;
        instanceData[_highestInstanceIndex].Scale = scale;
        instanceData[_highestInstanceIndex].Color = color.ToVector4();
        instanceData[_highestInstanceIndex].TextureOrigin = textureOrigin;

        _highestInstanceIndex++;
    }

    public void End(MoonWorks.Graphics.CommandBuffer commandBuffer, RenderPass renderPass, Texture textureToDrawTo, Texture textureToSample)
    {
        _instanceTransferBuffer.Unmap();

        var cameraMatrix = Matrix4x4.CreateOrthographicOffCenter
        (
            0,
            textureToDrawTo.Width,
            textureToDrawTo.Height,
            0,
            0,
            -1f
        );

        var copyPass = commandBuffer.BeginCopyPass();
        copyPass.UploadToBuffer(_instanceTransferBuffer, _instanceBuffer, true);
        commandBuffer.EndCopyPass(copyPass);

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(_vertexBuffer, true)
        );
        _spriteBatchComputeUniforms.TextureSize = new Vector2(textureToSample.Width, textureToSample.Height);
        computePass.BindComputePipeline(_computePipeline);
        computePass.BindStorageBuffers(_instanceBuffer);
        commandBuffer.PushComputeUniformData(_spriteBatchComputeUniforms);
        computePass.Dispatch(((uint)_highestInstanceIndex + 63) / 64, 1, 1);
        commandBuffer.EndComputePass(computePass);

        commandBuffer.PushVertexUniformData(cameraMatrix);
        renderPass.BindGraphicsPipeline(_graphicsPipeline);
        renderPass.BindVertexBuffers(_vertexBuffer);
        renderPass.BindIndexBuffer(_indexBuffer, IndexElementSize.ThirtyTwo);
        renderPass.BindFragmentSamplers(new TextureSamplerBinding(textureToSample, _sampler));
        renderPass.DrawIndexedPrimitives((uint)_highestInstanceIndex * 6, 1, 0, 0, 0);
    }
}
