using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Graphics.Font;
using MoonWorks.Storage;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using Tellus.Graphics;
using Tellus.Graphics.SpriteBatch;
using static WellspringCS.Wellspring;
using Color = MoonWorks.Graphics.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;
using HorizontalAlignment = MoonWorks.Graphics.Font.HorizontalAlignment;
using Rectangle = System.Drawing.Rectangle;
using VerticalAlignment = MoonWorks.Graphics.Font.VerticalAlignment;

namespace TellusExampleProject;

internal class SpritebatchGame : Game
{
    private struct Object
    {
        public Vector2 Position;
        public float Rotation;
        public Vector2 Scale;
        public Color Color;
    }

    private readonly Texture _squareSprite;
    private Texture _depthTexture;
    private readonly SpriteBatch.SpriteOperationContainer _spriteOperationContainer;
    private readonly SpriteBatch _spriteBatch;

    private readonly Font _sofiaSans;
    private readonly TextBatch _textBatch;
    private readonly GraphicsPipeline _fontPipeline;

    private readonly Object[] _objects;
    private double _fps;

    public SpritebatchGame
    (
       AppInfo appInfo,
       WindowCreateInfo windowCreateInfo,
       FramePacingSettings framePacingSettings,
       bool debugMode = false
    ) : base
    (
       appInfo,
       windowCreateInfo,
       framePacingSettings,
       ShaderFormat.SPIRV | ShaderFormat.DXIL | ShaderFormat.MSL | ShaderFormat.DXBC,
       debugMode
    )
    {
        _spriteBatch = new SpriteBatch
        (
            GraphicsDevice,
            ColorTargetBlendState.PremultipliedAlphaBlend,
            SamplerCreateInfo.PointWrap,
            new DepthStencilState()
            {
                EnableDepthTest = true,
                EnableDepthWrite = true,
                CompareOp = CompareOp.LessOrEqual,
            },
            RasterizerState.CCW_CullNone,
            null, null,
            MainWindow.SwapchainFormat, TextureFormat.D16Unorm
        );
        _spriteOperationContainer = new SpriteBatch.SpriteOperationContainer(GraphicsDevice, 5000000);

        _textBatch = new TextBatch(GraphicsDevice);

        var resourceUploader = new ResourceUploader(GraphicsDevice);

        _squareSprite = resourceUploader.CreateTexture2DFromCompressed(
            RootTitleStorage,
            "Assets/image2.png",
            TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler
        );

        _sofiaSans = Font.Load(GraphicsDevice, RootTitleStorage, "Assets/SofiaSans.ttf");

        var fontPipelineCreateInfo = new GraphicsPipelineCreateInfo
        {
            VertexShader = GraphicsDevice.TextVertexShader,
            FragmentShader = GraphicsDevice.TextFragmentShader,
            VertexInputState = GraphicsDevice.TextVertexInputState,
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = MultisampleState.None,
            DepthStencilState = DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = [
                    new ColorTargetDescription
                    {
                        Format = MainWindow.SwapchainFormat,
                        BlendState = ColorTargetBlendState.PremultipliedAlphaBlend
                    }
                ]
            }
        };

        _fontPipeline = GraphicsPipeline.Create(GraphicsDevice, fontPipelineCreateInfo);

        resourceUploader.Upload();
        resourceUploader.Dispose();

        _depthTexture = Texture.Create2D(GraphicsDevice, "Depth Texture", 1, 1, TextureFormat.D16Unorm, TextureUsageFlags.DepthStencilTarget);

        _objects = new Object[1000000];
        var random = new Random();
        for (int i = 0; i < _objects.Length; i++)
        {
            _objects[i] = new Object()
            {
                Position = new Vector2(random.NextSingle() * 500, random.NextSingle() * 500),
                Rotation = 0,
                Scale = Vector2.One * 25,
                Color = new Color(random.NextSingle(), random.NextSingle(), random.NextSingle(), 1f)
            };
        }
    }

    protected override void Update(TimeSpan delta)
    {
        //_fps = 1 / accumulatedUpdateTime.TotalSeconds;
    }

    protected override void Draw(double alpha)
    {
        CommandBuffer commandBuffer = GraphicsDevice.AcquireCommandBuffer();
        Texture swapchainTexture = commandBuffer.AcquireSwapchainTexture(MainWindow);
        if (swapchainTexture != null)
        {
            if (_depthTexture.Width != swapchainTexture.Width || _depthTexture.Height != swapchainTexture.Height)
            {
                _depthTexture.Dispose();
                _depthTexture = Texture.Create2D(GraphicsDevice, "Depth Texture", swapchainTexture.Width, swapchainTexture.Height, TextureFormat.D16Unorm, TextureUsageFlags.DepthStencilTarget);
            }

            var renderPass = commandBuffer.BeginRenderPass(
                new DepthStencilTargetInfo(_depthTexture, 1),
                new ColorTargetInfo(swapchainTexture, Color.CornflowerBlue)
            );

            _spriteOperationContainer.ClearSprites();
            for (int i = 0; i < _objects.Length; i++)
            {
                var instance = _objects[i];
                _spriteOperationContainer.PushSprite
                (
                    _squareSprite,
                    instance.Scale * 0.5f,
                    new Rectangle(0, 0, (int)instance.Scale.X, (int)instance.Scale.Y),
                    instance.Position,
                    instance.Rotation,
                    instance.Scale,
                    instance.Color,
                    1f
                );
            }

            _spriteBatch.DrawFullBatch(commandBuffer, renderPass, swapchainTexture, _spriteOperationContainer, null);

            _textBatch.Start();
            _textBatch.Add(
                _sofiaSans,
                $"{_fps}",
                32,
                Matrix4x4.CreateTranslation(512, 32, 0),
                Color.White,
                HorizontalAlignment.Left,
                VerticalAlignment.Middle
            );
            _textBatch.UploadBufferData(commandBuffer);

            var cameraMatrix = Matrix4x4.CreateOrthographicOffCenter
            (
                0,
                swapchainTexture.Width,
                swapchainTexture.Height,
                0,
                0,
                -1f
            );

            renderPass.BindGraphicsPipeline(_fontPipeline);
            _textBatch.Render(renderPass, cameraMatrix);

            commandBuffer.EndRenderPass(renderPass);
        }
        GraphicsDevice.Submit(commandBuffer);
    }

    protected override void Destroy()
    {
        _depthTexture.Dispose();
    }
}
