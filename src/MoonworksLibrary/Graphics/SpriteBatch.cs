using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Storage;
using System.Drawing;
using System.Numerics;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = MoonWorks.Graphics.Color;

namespace MoonworksLibrary.Graphics;

public class SpriteBatch
{
    private struct CachedSpriteDrawInformation
    {
        public Texture Texture;
        public Vector2 TextureOrigin;
        public Rectangle TextureSourceRectangle;
        public Vector2 Position;
        public float Rotation;
        public Vector2 Scale;
        public Color Color;
        public float Depth;
    }

    private GraphicsPipeline _graphicsPipeline;
    private Sampler _sampler;
    private TransferBuffer _vertexTransferBuffer;
    private Buffer _vertexBuffer;
    private Buffer _indexBuffer;

    private CachedSpriteDrawInformation[] _cachedSpriteDrawInformation;
    private uint _lastCachedSpriteDrawInformationIndex;

    private const uint MAXIMUM_SPRITE_AMOUNT = 2048;
    private const uint MAXIMUM_VERTEX_AMOUNT = MAXIMUM_SPRITE_AMOUNT * 4;
    private const uint MAXIMUM_INDEX_AMOUNT = MAXIMUM_SPRITE_AMOUNT * 4;

    public SpriteBatch(GraphicsDevice graphicsDevice, Shader vertexShader, Shader fragmentShader, TextureFormat renderTextureFormat)
    {
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

        _sampler = Sampler.Create(graphicsDevice, SamplerCreateInfo.PointClamp);

        _vertexTransferBuffer = TransferBuffer.Create<PositionTextureColorVertex>(
            graphicsDevice,
            TransferBufferUsage.Upload,
            MAXIMUM_VERTEX_AMOUNT
        );

        _vertexBuffer = Buffer.Create<PositionTextureColorVertex>
        (
            graphicsDevice,
            BufferUsageFlags.Vertex,
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

        _cachedSpriteDrawInformation = new CachedSpriteDrawInformation[MAXIMUM_SPRITE_AMOUNT];
    }

    public void RenderSprite(Texture texture, Vector2 textureOrigin, Rectangle textureSourceRectangle, Vector2 position, float rotation, Vector2 scale, Color color, float depth)
    {
        _cachedSpriteDrawInformation[0] = new CachedSpriteDrawInformation()
        {
            Texture = texture,
            TextureOrigin = textureOrigin,
            TextureSourceRectangle = textureSourceRectangle,
            Position = position,
            Rotation = rotation,
            Scale = scale,
            Color = color,
            Depth = depth
        };
    }

    public void RenderBatch(GraphicsDevice graphicsDevice, Texture textureToDrawTo)
    {
        Matrix4x4 cameraMatrix = Matrix4x4.CreateOrthographicOffCenter
        (
            0,
            640,
            480,
            0,
            0,
            -1f
        );

        MoonWorks.Graphics.CommandBuffer cmdbuf = graphicsDevice.AcquireCommandBuffer();
        _vertexTransferBuffer
    }
}
