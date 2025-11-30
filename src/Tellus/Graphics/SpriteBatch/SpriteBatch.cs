using MoonWorks.Graphics;
using System.Numerics;

namespace Tellus.Graphics.SpriteBatch;

/// <summary>
/// Wraps settings about how sprites should be drawn and can draw a group of sprites under those settings.
/// </summary>
public sealed partial class SpriteBatch : GraphicsResource
{
    private readonly GraphicsPipeline _graphicsPipeline;
    private readonly Sampler _sampler;

    private readonly Shader _defaultVertexShader;
    private readonly Shader _defaultFragmentShader;

    public SpriteBatch
    (
        GraphicsDevice device,

        ColorTargetBlendState? colorTargetBlendState,
        SamplerCreateInfo? samplerCreateInfo,
        DepthStencilState? depthStencilState,
        RasterizerState? rasterizerState,
        Shader? vertexShader, Shader? fragmentShader,

        TextureFormat drawTextureFormat, TextureFormat? depthTextureFormat
    ) : base(device)
    {
        Utils.LoadShaderFromManifest(device, "TexturedQuad.vert", new ShaderCreateInfo()
        {
            Stage = ShaderStage.Vertex,
            NumUniformBuffers = 1,
        }, out _defaultVertexShader);

        Utils.LoadShaderFromManifest(device, "TexturedQuad.frag", new ShaderCreateInfo()
        {
            Stage = ShaderStage.Fragment,
            NumStorageTextures = 1,
            NumSamplers = 1,
        }, out _defaultFragmentShader);

        ColorTargetBlendState actualColorTargetBlendState = colorTargetBlendState ?? ColorTargetBlendState.PremultipliedAlphaBlend;
        SamplerCreateInfo actualSamplerCreateInfo = samplerCreateInfo ?? SamplerCreateInfo.PointClamp;
        DepthStencilState actualDepthStencilState = depthStencilState ?? DepthStencilState.Disable;
        RasterizerState actualRasterizerState = rasterizerState ?? RasterizerState.CCW_CullNone;
        Shader actualVertexShader = vertexShader ?? _defaultVertexShader;
        Shader actualFragmentShader = fragmentShader ?? _defaultFragmentShader;

        var graphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo()
        {
            VertexShader = actualVertexShader,
            FragmentShader = actualFragmentShader,
            VertexInputState = VertexInputState.CreateSingleBinding<PositionTextureColorVertex>(),
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = actualRasterizerState,
            MultisampleState = MultisampleState.None,
            DepthStencilState = DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo()
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription()
                    {
                        Format = drawTextureFormat,
                        BlendState = actualColorTargetBlendState,
                    }
                ],
            },
        };
        if (depthTextureFormat.HasValue)
        {
            graphicsPipelineCreateInfo.DepthStencilState = actualDepthStencilState;
            graphicsPipelineCreateInfo.TargetInfo.DepthStencilFormat = depthTextureFormat.Value;
            graphicsPipelineCreateInfo.TargetInfo.HasDepthStencilTarget = true;
        }
        _graphicsPipeline = GraphicsPipeline.Create(Device, graphicsPipelineCreateInfo);

        _sampler = Sampler.Create(Device, actualSamplerCreateInfo);
    }

    /// <summary>
    /// Draws a given batch of sprites.
    /// </summary>
    /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
    /// <param name="renderPass">The current <see cref="RenderPass"/>.</param>
    /// <param name="textureToDrawTo">The texture to draw to (the render target).</param>
    /// <param name="spriteContainer">The container with the sprite batch.</param>
    public void DrawBatch(CommandBuffer commandBuffer, RenderPass renderPass, Texture textureToDrawTo, SpriteInstanceContainer spriteContainer)
    {
        var cameraMatrix = Matrix4x4.CreateOrthographicOffCenter
        (
            0,
            textureToDrawTo.Width,
            textureToDrawTo.Height,
            0,
            0,
            -1f
        );

        var uniforms = new VertexUniforms()
        {
            CameraMatrix = cameraMatrix,
        };

        commandBuffer.PushVertexUniformData(uniforms);
        renderPass.BindGraphicsPipeline(_graphicsPipeline);
        renderPass.BindVertexBuffers(spriteContainer.VertexBuffer);
        renderPass.BindIndexBuffer(spriteContainer.IndexBuffer, IndexElementSize.ThirtyTwo);
        renderPass.BindFragmentSamplers(new TextureSamplerBinding(spriteContainer.TextureToSample, _sampler));
        renderPass.DrawIndexedPrimitives(spriteContainer.SpriteAmount * 6, 1, 0, 0, 0);
    }

    /// <summary>
    /// Draws all contained batches of sprites.
    /// </summary>
    /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
    /// <param name="renderPass">The current <see cref="RenderPass"/>.</param>
    /// <param name="textureToDrawTo">The texture to draw to (the render target).</param>
    /// <param name="spriteContainer">The container with the sprite batch.</param>
    /// <param name="transformationMatrix">An optional transformation matrix to be applied to the vertices.</param>
    public void DrawFullBatch
    (
        CommandBuffer commandBuffer, RenderPass renderPass, Texture textureToDrawTo, SpriteInstanceContainer spriteContainer,
        Matrix4x4? transformationMatrix
    )
    {
        int offset = 0;

        while (true)
        {
            int? nextSpriteIndex = spriteContainer.UploadData(commandBuffer, offset);

            spriteContainer.CreateVertexInfo(commandBuffer, transformationMatrix);
            DrawBatch(commandBuffer, renderPass, textureToDrawTo, spriteContainer);

            if (nextSpriteIndex is null)
                break;

            offset = nextSpriteIndex.Value;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                _defaultFragmentShader.Dispose();
                _defaultVertexShader.Dispose();
                _graphicsPipeline.Dispose();
                _sampler.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
