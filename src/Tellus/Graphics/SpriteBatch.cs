using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Storage;
using System.Drawing;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using WellspringCS;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = MoonWorks.Graphics.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace Tellus.Graphics;

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

public sealed class SpriteBatch : GraphicsResource
{
    private struct DrawOperation
    {
        public int TextureIndex;
        public Vector2 TextureOrigin;
        public Rectangle TextureSourceRectangle;
        public Vector2 Position;
        public float Rotation;
        public Vector2 Scale;
        public Color Color;
        public float Depth;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    private struct SpriteInstanceData
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

    [StructLayout(LayoutKind.Sequential)]
    private struct ComputeUniforms
    {
        public Matrix4x4 TransformationMatrix;
        public Vector2 TextureSize;
    }

    private readonly ComputePipeline _computePipeline;

    private readonly TransferBuffer _instanceTransferBuffer;
    private readonly Buffer _instanceBuffer;
    private readonly Buffer _vertexBuffer;
    private readonly Buffer _indexBuffer;

    private bool _hasBeginBeenCalled;

    private SpriteSortMode _savedSpriteSortMode;
    private ColorTargetBlendState _savedColorTargetBlendState;
    private SamplerCreateInfo _savedSamplerCreateInfo;
    private DepthStencilState _savedDepthStencilState;
    private RasterizerState _savedRasterizerState;
    private Shader? _savedVertexShader;
    private Shader? _savedFragmentShader;
    private Matrix4x4 _savedTransformationMatrix;

    private ComputeUniforms _computeUniforms;

    private readonly Shader _defaultVertexShader;
    private readonly Shader _defaultFragmentShader;

    private List<Texture> _drawOperationTextures;
    private List<DrawOperation> _drawOperations;

    private const uint MAXIMUM_SPRITE_AMOUNT = 2048;
    private const uint MAXIMUM_VERTEX_AMOUNT = MAXIMUM_SPRITE_AMOUNT * 4;
    private const uint MAXIMUM_INDEX_AMOUNT = MAXIMUM_SPRITE_AMOUNT * 6;

    public SpriteBatch(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        Utils.LoadShaderFromManifest(Device, "TexturedQuad.vert", new ShaderCreateInfo()
        {
            Stage = ShaderStage.Vertex,
            NumUniformBuffers = 1,
        }, out _defaultVertexShader);

        Utils.LoadShaderFromManifest(Device, "TexturedQuad.frag", new ShaderCreateInfo()
        {
            Stage = ShaderStage.Fragment,
            NumStorageTextures = 1,
            NumSamplers = 1,
        }, out _defaultFragmentShader);

        Utils.LoadShaderFromManifest(Device, "SpriteBatch.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 1,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 64, ThreadCountY = 1, ThreadCountZ = 1
        }, out _computePipeline);

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

    public void Draw
    (
        Texture texture,
        Vector2 textureOrigin, 
        Rectangle textureSourceRectangle, 
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
        CommandBuffer commandBuffer, 
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

        if (_drawOperations.Count == 0)
        {
            return;
        }    

        void AddInstanceDataToBuffer(ref Span<SpriteInstanceData> span, int index, DrawOperation operation)
        {
            span[index].Position = new Vector3(operation.Position, operation.Depth);
            span[index].Rotation = operation.Rotation;
            span[index].Scale = operation.Scale;
            span[index].Color = operation.Color.ToVector4();
            span[index].TextureOrigin = operation.TextureOrigin;
            span[index].TextureSourceRectangle = new Vector4(operation.TextureSourceRectangle.X, operation.TextureSourceRectangle.Y, operation.TextureSourceRectangle.Width, operation.TextureSourceRectangle.Height); ;
        }

        var cameraMatrix = Matrix4x4.CreateOrthographicOffCenter
        (
            0,
            textureToDrawTo.Width,
            textureToDrawTo.Height,
            0,
            0,
            -1f
        );

        GraphicsPipeline graphicsPipeline;
        Sampler sampler;

        void DrawBatch(Texture textureToSample, uint spriteAmount)
        {
            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.UploadToBuffer(_instanceTransferBuffer, _instanceBuffer, true);
            commandBuffer.EndCopyPass(copyPass);

            var lastComputePass = commandBuffer.BeginComputePass
            (
                new StorageBufferReadWriteBinding(_vertexBuffer, true)
            );
            _computeUniforms.TextureSize = new Vector2(textureToSample.Width, textureToSample.Height);

            lastComputePass.BindComputePipeline(_computePipeline);
            lastComputePass.BindStorageBuffers(_instanceBuffer);
            commandBuffer.PushComputeUniformData(_computeUniforms);
            lastComputePass.Dispatch((spriteAmount + 63) / 64, 1, 1);
            commandBuffer.EndComputePass(lastComputePass);

            commandBuffer.PushVertexUniformData(cameraMatrix);
            renderPass.BindGraphicsPipeline(graphicsPipeline);
            renderPass.BindVertexBuffers(_vertexBuffer);
            renderPass.BindIndexBuffer(_indexBuffer, IndexElementSize.ThirtyTwo);
            renderPass.BindFragmentSamplers(new TextureSamplerBinding(textureToSample, sampler));
            renderPass.DrawIndexedPrimitives(spriteAmount * 6, 1, 0, 0, 0);
        }

        #region Prepare render state
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
        graphicsPipeline = GraphicsPipeline.Create(Device, graphicsPipelineCreateInfo);
        sampler = Sampler.Create(Device, _savedSamplerCreateInfo);
        
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

        _computeUniforms.TransformationMatrix = _savedTransformationMatrix;
        #endregion

        #region Render
        DrawOperation previousDrawOperation = _drawOperations[0];

        var instanceDataSpan = _instanceTransferBuffer.Map<SpriteInstanceData>(true);
        var highestInstanceIndex = 0;

        AddInstanceDataToBuffer(ref instanceDataSpan, highestInstanceIndex, previousDrawOperation);
        highestInstanceIndex++;


        for (int i = 1; i < _drawOperations.Count; i++)
        {
            DrawOperation currentDrawOperation = _drawOperations[i];

            if (currentDrawOperation.TextureIndex != previousDrawOperation.TextureIndex || highestInstanceIndex >= MAXIMUM_SPRITE_AMOUNT)
            {
                Texture textureToSample = _drawOperationTextures[previousDrawOperation.TextureIndex];
                _instanceTransferBuffer.Unmap();

                DrawBatch(textureToSample, (uint)highestInstanceIndex);

                instanceDataSpan = _instanceTransferBuffer.Map<SpriteInstanceData>(true);
                highestInstanceIndex = 0;
            }

            AddInstanceDataToBuffer(ref instanceDataSpan, highestInstanceIndex, currentDrawOperation);
            highestInstanceIndex++;

            previousDrawOperation = currentDrawOperation;
        }

        DrawOperation lastDrawOperation = _drawOperations[^1];
        Texture lastTextureToSample = _drawOperationTextures[lastDrawOperation.TextureIndex];
        _instanceTransferBuffer.Unmap();

        DrawBatch(lastTextureToSample, (uint)highestInstanceIndex);
        #endregion

        _hasBeginBeenCalled = false;

        graphicsPipeline.Dispose();
        sampler.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                _computePipeline.Dispose();
                _instanceTransferBuffer.Dispose();
                _instanceBuffer.Dispose();
                _vertexBuffer.Dispose();
                _indexBuffer.Dispose();
                _defaultVertexShader?.Dispose();
                _defaultFragmentShader?.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
