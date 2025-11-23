using MoonWorks.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Tellus.Graphics.SpriteBatch;

public sealed partial class SpriteBatch : GraphicsResource
{
    private readonly GraphicsPipeline _graphicsPipeline;

    private static ComputePipeline? _computePipeline;

    private static Shader? _defaultVertexShader;
    private static Shader? _defaultFragmentShader;

    public static void Initialize(GraphicsDevice device)
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

        Utils.LoadShaderFromManifest(device, "SpriteBatch.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 1,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 64,
            ThreadCountY = 1,
            ThreadCountZ = 1
        }, out _computePipeline);
    }

    public SpriteBatch
    (
        GraphicsDevice device,

        SpriteSortMode? spriteSortMode,
        ColorTargetBlendState? colorTargetBlendState,
        SamplerCreateInfo? samplerCreateInfo,
        DepthStencilState? depthStencilState,
        RasterizerState? rasterizerState,
        Shader? vertexShader,
        Shader? fragmentShader,
        Matrix4x4? transformationMatrix
    ) : base(device)
    {
        if (_defaultVertexShader is null)
            throw new NullReferenceException($"{nameof(_defaultVertexShader)} is null. Did you forget to call {nameof(Initialize)}?");
        if (_defaultFragmentShader is null)
            throw new NullReferenceException($"{nameof(_defaultFragmentShader)} is null. Did you forget to call {nameof(Initialize)}?");

        SpriteSortMode actualSpriteSortMode = spriteSortMode ?? SpriteSortMode.Deferred;
        ColorTargetBlendState actualColorTargetBlendState = colorTargetBlendState ?? ColorTargetBlendState.PremultipliedAlphaBlend;
        SamplerCreateInfo actualCamplerCreateInfo = samplerCreateInfo ?? SamplerCreateInfo.PointClamp;
        DepthStencilState actualDepthStencilState = depthStencilState ?? DepthStencilState.Disable;
        RasterizerState actualRasterizerState = rasterizerState ?? RasterizerState.CCW_CullNone;
        Shader actualVertexShader = vertexShader ?? _defaultVertexShader;
        Shader actualFragmentShader = fragmentShader ?? _defaultFragmentShader;
        Matrix4x4 actualTransformationMatrix = transformationMatrix ?? Matrix4x4.Identity;

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
            graphicsPipelineCreateInfo.DepthStencilState = _savedDepthStencilState;
            graphicsPipelineCreateInfo.TargetInfo.DepthStencilFormat = depthTextureFormat.Value;
            graphicsPipelineCreateInfo.TargetInfo.HasDepthStencilTarget = true;
        }
        _graphicsPipeline = GraphicsPipeline.Create(Device, graphicsPipelineCreateInfo);
    }
    
    public void Begin()
    {

    }

    public void End(
        CommandBuffer commandBuffer,
        RenderPass renderPass,
        Texture textureToDrawTo,
        TextureFormat drawTextureFormat,
        TextureFormat? depthTextureFormat
    )
    {

    }
}
