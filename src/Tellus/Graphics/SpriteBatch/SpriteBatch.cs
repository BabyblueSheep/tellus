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
        InternalUtils.LoadShaderFromManifest(device, "TexturedQuad.vert", new ShaderCreateInfo()
        {
            Stage = ShaderStage.Vertex,
            NumUniformBuffers = 1,
        }, out _defaultVertexShader);

        InternalUtils.LoadShaderFromManifest(device, "TexturedQuad.frag", new ShaderCreateInfo()
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
    /// <param name="transformationMatrix">An optional transformation matrix to be applied to the vertices.</param>
    public void DrawBatch(CommandBuffer commandBuffer, RenderPass renderPass, Texture textureToDrawTo, SpriteInstanceContainer spriteContainer, Matrix4x4? transformationMatrix)
    {
        Matrix4x4 actualTransformationMatrix = transformationMatrix ?? Matrix4x4.Identity;
        var cameraMatrix = Matrix4x4.CreateOrthographicOffCenter
        (
            0,
            textureToDrawTo.Width,
            textureToDrawTo.Height,
            0,
            0,
            -1
        );

        var uniforms = new VertexUniforms()
        {
            TransformationMatrix = actualTransformationMatrix * cameraMatrix,
        };

        commandBuffer.PushVertexUniformData(uniforms);
        renderPass.BindGraphicsPipeline(_graphicsPipeline);
        renderPass.BindVertexBuffers(spriteContainer.VertexBuffer);
        renderPass.BindIndexBuffer(spriteContainer.IndexBuffer, IndexElementSize.ThirtyTwo);

        foreach (var batchInformation in  spriteContainer.BatchInformationList)
        {
            renderPass.BindFragmentSamplers(new TextureSamplerBinding(batchInformation.Texture, _sampler));
            renderPass.DrawIndexedPrimitives((uint)batchInformation.Length * 6, 1, (uint)batchInformation.StartSpriteIndex * 6, 0, 0);
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
