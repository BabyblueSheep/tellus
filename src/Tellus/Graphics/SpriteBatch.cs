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

public enum SpriteSortMode
{
    Deferred,
    Texture,
    BackToFront,
    FrontToBack,
}

public class SpriteBatch : GraphicsResource
{
    private struct DrawOperation
    {
        public int TextureIndex;
        public Vector2 TextureOrigin;
        public Vector4 TextureSourceRectangle; // TODO: change this to Rectangle
        public Vector2 Position;
        public float Rotation;
        public Vector2 Scale;
        public Color Color;
        public float Depth;
    }

    private GraphicsPipeline? _graphicsPipeline;
    private readonly ComputePipeline _computePipeline;

    private readonly TransferBuffer _instanceTransferBuffer;
    private int _highestInstanceIndex;
    private readonly Buffer _instanceBuffer;
    private readonly Buffer _vertexBuffer;
    private readonly Buffer _indexBuffer;

    private Sampler? _sampler;

    private bool _hasBeginBeenCalled;

    private SpriteSortMode _savedSpriteSortMode;
    private ColorTargetBlendState _savedColorTargetBlendState;
    private SamplerCreateInfo _savedSamplerCreateInfo;
    private DepthStencilState _savedDepthStencilState;
    private RasterizerState _savedRasterizerState;
    private Shader? _savedVertexShader;
    private Shader? _savedFragmentShader;
    private Matrix4x4 _savedTransformationMatrix;

    private readonly Shader _defaultVertexShader;
    private readonly Shader _defaultFragmentShader;

    record struct SpriteBatchComputeUniforms(Vector2 TextureSize);
    private SpriteBatchComputeUniforms _spriteBatchComputeUniforms;

    private List<Texture> _drawOperationTextures;
    private List<DrawOperation> _drawOperations;

    private const uint MAXIMUM_SPRITE_AMOUNT = 2048;
    private const uint MAXIMUM_VERTEX_AMOUNT = MAXIMUM_SPRITE_AMOUNT * 4;
    private const uint MAXIMUM_INDEX_AMOUNT = MAXIMUM_SPRITE_AMOUNT * 6;

    public SpriteBatch(GraphicsDevice graphicsDevice, TitleStorage titleStorage, TextureFormat renderTextureFormat, TextureFormat depthTextureFormat) : base(graphicsDevice)
    {
        _defaultVertexShader = ShaderCross.Create(
            Device,
            titleStorage,
            "Assets/TexturedQuad.vert.hlsl",
            "main",
            ShaderCross.ShaderFormat.HLSL,
            ShaderStage.Vertex
        );

        _defaultFragmentShader = ShaderCross.Create(
            Device,
            titleStorage,
             "Assets/TexturedQuad.frag.hlsl",
            "main",
            ShaderCross.ShaderFormat.HLSL,
            ShaderStage.Fragment
        );

        _computePipeline = ShaderCross.Create(
            Device,
            titleStorage,
            "Assets/SpriteBatch.comp.hlsl",
            "main",
            ShaderCross.ShaderFormat.HLSL
        );

        _drawOperationTextures = [];
        _drawOperations = [];

        #region Create buffers
        _instanceTransferBuffer = TransferBuffer.Create<SpriteInstanceData>
        (
            Device,
            TransferBufferUsage.Upload,
            MAXIMUM_SPRITE_AMOUNT
        );

        _instanceBuffer = Buffer.Create<SpriteInstanceData>
        (
            Device,
            BufferUsageFlags.ComputeStorageRead,
            MAXIMUM_SPRITE_AMOUNT
        );

        _highestInstanceIndex = 0;

        _vertexBuffer = Buffer.Create<PositionTextureColorVertex>
        (
            Device,
            BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.Vertex,
            MAXIMUM_VERTEX_AMOUNT
        );

        _indexBuffer = Buffer.Create<uint>
        (
            Device,
            BufferUsageFlags.Index,
            MAXIMUM_INDEX_AMOUNT
        );

        TransferBuffer indexTransferBuffer = TransferBuffer.Create<uint>(
            Device,
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

        var commandBuffer = Device.AcquireCommandBuffer();
        var copyPass = commandBuffer.BeginCopyPass();
        copyPass.UploadToBuffer(indexTransferBuffer, _indexBuffer, false);
        commandBuffer.EndCopyPass(copyPass);
        graphicsDevice.Submit(commandBuffer);

        indexTransferBuffer.Dispose();
        #endregion
    }

    public void Begin
    (
        SpriteSortMode? spriteSortMode,
        ColorTargetBlendState? colorTargetBlendState,
        SamplerCreateInfo? samplerCreateInfo,
        DepthStencilState? depthStencilState,
        RasterizerState? rasterizerState,
        Shader? vertexShader,
        Shader? fragmentShader,
        Matrix4x4? transformationMatrix
    )
    {
        if (_hasBeginBeenCalled)
        {
            throw new InvalidOperationException("Begin was called twice!");
        }
        _hasBeginBeenCalled = true;

        _savedSpriteSortMode = spriteSortMode ?? SpriteSortMode.Deferred;
        _savedColorTargetBlendState = colorTargetBlendState ?? ColorTargetBlendState.PremultipliedAlphaBlend;
        _savedSamplerCreateInfo = samplerCreateInfo ?? SamplerCreateInfo.PointClamp;
        _savedDepthStencilState = depthStencilState ?? DepthStencilState.Disable;
        _savedRasterizerState = rasterizerState ?? RasterizerState.CCW_CullNone;
        _savedVertexShader = vertexShader ?? _defaultVertexShader;
        _savedFragmentShader = fragmentShader ?? _defaultFragmentShader;
        _savedTransformationMatrix = transformationMatrix ?? Matrix4x4.Identity;

        _drawOperations.Clear();
        _drawOperationTextures.Clear();
    }

    // TODO: make textureSourceRectangle Rectangle, it's only a Vector4 temporarily 
    public void Draw
    (
        Texture texture,
        Vector2 textureOrigin, 
        Vector4 textureSourceRectangle, 
        Vector2 position, 
        float rotation, 
        Vector2 scale, 
        Color color, 
        float depth
    )
    {
        if (!_hasBeginBeenCalled)
        {
            throw new InvalidOperationException("Begin hasn't been called!");
        }

        int textureIndex = _drawOperationTextures.IndexOf(texture);
        if (textureIndex == -1)
        {
            _drawOperationTextures.Add(texture);
            textureIndex = _drawOperationTextures.Count - 1;
        }
        var drawOperation = new DrawOperation
        {
            TextureIndex = textureIndex,
            TextureOrigin = textureOrigin,
            TextureSourceRectangle = textureSourceRectangle,
            Position = position,
            Rotation = rotation,
            Scale = scale,
            Color = color,
            Depth = depth
        };
        _drawOperations.Add(drawOperation);
    }

    public void End
    (
        MoonWorks.Graphics.CommandBuffer commandBuffer, 
        RenderPass renderPass, 
        Texture textureToDrawTo,
        TextureFormat drawTextureFormat,
        TextureFormat? depthTextureFormat
    )
    {
        if (!_hasBeginBeenCalled)
        {
            throw new InvalidOperationException("Begin hasn't been called!");
        }

        var graphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo()
        {
            VertexShader = _savedVertexShader,
            FragmentShader = _savedFragmentShader,
            VertexInputState = VertexInputState.CreateSingleBinding<PositionTextureColorVertex>(),
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = _savedRasterizerState,
            MultisampleState = MultisampleState.None,
            DepthStencilState = DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo()
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription()
                    {
                        Format = drawTextureFormat,
                        BlendState = _savedColorTargetBlendState,
                    }
                ],
            },
        };

        if (depthTextureFormat.HasValue)
        {
            graphicsPipelineCreateInfo.DepthStencilState = _savedDepthStencilState;
            graphicsPipelineCreateInfo.TargetInfo.DepthStencilFormat = depthTextureFormat.Value;
            graphicsPipelineCreateInfo.TargetInfo.HasDepthStencilTarget = true;
        }
        _graphicsPipeline = GraphicsPipeline.Create(Device, graphicsPipelineCreateInfo);

        _sampler = Sampler.Create(Device, _savedSamplerCreateInfo);

        var cameraMatrix = Matrix4x4.CreateOrthographicOffCenter
        (
            0,
            textureToDrawTo.Width,
            textureToDrawTo.Height,
            0,
            0,
            -1f
        );

        switch (_savedSpriteSortMode)
        {
            case SpriteSortMode.Deferred:
                break;
            case SpriteSortMode.Texture:
                _drawOperations = _drawOperations.OrderBy(x => x.TextureIndex).ToList();
                break;
            case SpriteSortMode.BackToFront:
                _drawOperations = _drawOperations.OrderBy(x => x.Depth).ToList();
                break;
            case SpriteSortMode.FrontToBack:
                _drawOperations = _drawOperations.OrderByDescending(x => x.Depth).ToList();
                break;
        }

        DrawOperation firstDrawOperation = _drawOperations[0];

        var instanceDataSpan = _instanceTransferBuffer.Map<SpriteInstanceData>(true);
        var highestInstanceIndex = 0;

        instanceDataSpan[highestInstanceIndex].Position = new Vector3(firstDrawOperation.Position, firstDrawOperation.Depth);
        instanceDataSpan[highestInstanceIndex].Rotation = firstDrawOperation.Rotation;
        instanceDataSpan[highestInstanceIndex].Scale = firstDrawOperation.Scale;
        instanceDataSpan[highestInstanceIndex].Color = firstDrawOperation.Color.ToVector4();
        instanceDataSpan[highestInstanceIndex].TextureOrigin = firstDrawOperation.TextureOrigin;
        instanceDataSpan[highestInstanceIndex].TextureSourceRectangle = firstDrawOperation.TextureSourceRectangle;
        highestInstanceIndex++;

        for (int i = 1; i < _drawOperations.Count; i++)
        {
            int previousIndex = Math.Max(i - 1, 0);
            DrawOperation currentDrawOperation = _drawOperations[i];
            DrawOperation previousDrawOperation = _drawOperations[previousIndex];

            if (currentDrawOperation.TextureIndex != previousDrawOperation.TextureIndex)
            {
                Texture textureToSample = _drawOperationTextures[previousDrawOperation.TextureIndex];
                _instanceTransferBuffer.Unmap();

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
                computePass.Dispatch(((uint)highestInstanceIndex + 63) / 64, 1, 1);
                commandBuffer.EndComputePass(computePass);

                commandBuffer.PushVertexUniformData(cameraMatrix);
                renderPass.BindGraphicsPipeline(_graphicsPipeline);
                renderPass.BindVertexBuffers(_vertexBuffer);
                renderPass.BindIndexBuffer(_indexBuffer, IndexElementSize.ThirtyTwo);
                renderPass.BindFragmentSamplers(new TextureSamplerBinding(textureToSample, _sampler));
                renderPass.DrawIndexedPrimitives((uint)highestInstanceIndex * 6, 1, 0, 0, 0);

                instanceDataSpan = _instanceTransferBuffer.Map<SpriteInstanceData>(true);
                highestInstanceIndex = 0;
            }

            instanceDataSpan[highestInstanceIndex].Position = new Vector3(currentDrawOperation.Position, currentDrawOperation.Depth);
            instanceDataSpan[highestInstanceIndex].Rotation = currentDrawOperation.Rotation;
            instanceDataSpan[highestInstanceIndex].Scale = currentDrawOperation.Scale;
            instanceDataSpan[highestInstanceIndex].Color = currentDrawOperation.Color.ToVector4();
            instanceDataSpan[highestInstanceIndex].TextureOrigin = currentDrawOperation.TextureOrigin;
            instanceDataSpan[highestInstanceIndex].TextureSourceRectangle = currentDrawOperation.TextureSourceRectangle;
            highestInstanceIndex++;
        }

        DrawOperation lastDrawOperation = _drawOperations[^1];
        Texture lastTextureToSample = _drawOperationTextures[lastDrawOperation.TextureIndex];
        _instanceTransferBuffer.Unmap();

        var lastCopyPass = commandBuffer.BeginCopyPass();
        lastCopyPass.UploadToBuffer(_instanceTransferBuffer, _instanceBuffer, true);
        commandBuffer.EndCopyPass(lastCopyPass);

        var lastComputePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(_vertexBuffer, true)
        );
        _spriteBatchComputeUniforms.TextureSize = new Vector2(lastTextureToSample.Width, lastTextureToSample.Height);
        lastComputePass.BindComputePipeline(_computePipeline);
        lastComputePass.BindStorageBuffers(_instanceBuffer);
        commandBuffer.PushComputeUniformData(_spriteBatchComputeUniforms);
        lastComputePass.Dispatch(((uint)highestInstanceIndex + 63) / 64, 1, 1);
        commandBuffer.EndComputePass(lastComputePass);

        commandBuffer.PushVertexUniformData(cameraMatrix);
        renderPass.BindGraphicsPipeline(_graphicsPipeline);
        renderPass.BindVertexBuffers(_vertexBuffer);
        renderPass.BindIndexBuffer(_indexBuffer, IndexElementSize.ThirtyTwo);
        renderPass.BindFragmentSamplers(new TextureSamplerBinding(lastTextureToSample, _sampler));
        renderPass.DrawIndexedPrimitives((uint)highestInstanceIndex * 6, 1, 0, 0, 0);

        _hasBeginBeenCalled = false;
    }
}
