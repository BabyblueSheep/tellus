using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Graphics.Font;
using MoonWorks.Storage;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using Tellus.Graphics;
using Tellus.Graphics.SpriteBatch;
using Tellus.Math;
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
        public int Texture;
        public Vector2 Position;
        public float Rotation;
        public Vector2 Scale;
        public Color Color;
        public Color OffsetColor;
        public float Depth;
    }

    private readonly Texture[] _squareSprites;
    private Texture _depthTexture;
    private readonly SpriteBatch.SpriteInstanceContainer _spriteOperationContainer;
    private readonly SpriteBatch _spriteBatch;

    private readonly Font _sofiaSans;
    private readonly TextBatch _textBatch;
    private readonly GraphicsPipeline _fontPipeline;

    private readonly Object[] _objects;
    //private double _fps;
    //private readonly Stopwatch _stopwatch;

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
            MainWindow.SwapchainFormat, GraphicsDevice.SupportedDepthStencilFormat
        );
        _spriteOperationContainer = new SpriteBatch.SpriteInstanceContainer(GraphicsDevice, 30000);

        _textBatch = new TextBatch(GraphicsDevice);

        var resourceUploader = new ResourceUploader(GraphicsDevice);

        _squareSprites = new Texture[8];
        for (int i = 0; i < 8; i++)
        {
            _squareSprites[i] = resourceUploader.CreateTexture2DFromCompressed(
                RootTitleStorage,
                $"resources/image{i}.png",
                TextureFormat.R8G8B8A8Unorm,
                TextureUsageFlags.Sampler
            );
        }

        _sofiaSans = Font.Load(GraphicsDevice, RootTitleStorage, "resources/SofiaSans.ttf");

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

        _objects = new Object[30000];
        var random = new Random();
        for (int i = 0; i < _objects.Length; i++)
        {
            _objects[i] = new Object()
            {
                Texture = random.Next(8),
                Position = new Vector2(random.NextSingle() * 500, random.NextSingle() * 500),
                Rotation = random.NextSingle() * MathF.PI * 2,
                Scale = Vector2.One * 25,
                Color = new Color(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle() * 0.5f + 0.5f),
                OffsetColor = new Color(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle() * 0.5f),
                Depth = random.NextSingle(),
            };
        }

        GraphicsDevice.SetSwapchainParameters(MainWindow, SwapchainComposition.SDR, PresentMode.Immediate);

        //_stopwatch = new Stopwatch();
        //_stopwatch.Start();
    }

    protected override void Update(TimeSpan delta)
    {
        
    }

    protected override void Step()
    {

    }

    protected override void Draw(double alpha)
    {
        //_stopwatch.Restart();

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

            /*if (_stopwatch.ElapsedMilliseconds == 0)
                _fps = double.PositiveInfinity;
            else
                _fps = 1000 / _stopwatch.ElapsedMilliseconds;

            //_fps = _stopwatch.ElapsedMilliseconds;*/

            _spriteOperationContainer.ClearSprites();
            for (int i = 0; i < _objects.Length; i++)
            {
                var instance = _objects[i];
                _spriteOperationContainer.PushSprite
                (
                    _squareSprites[instance.Texture],
                    null,
                    new SpriteBatch.SpriteParameters() with
                    {
                        TransformationMatrix = 
                            PlanarMatrix4x4.CreateScaleCentered(instance.Scale.X, instance.Scale.Y) *
                            //PlanarMatrix4x4.CreateRotationCentered(instance.Rotation) *
                            PlanarMatrix4x4.CreateTranslation(instance.Position),
                        TintColor = instance.Color,
                        OffsetColor = instance.OffsetColor,
                        Depth = instance.Depth
                    }
                );
            }

            _spriteOperationContainer.SortSprites(SpriteBatch.SpriteSortMode.Texture);

            _spriteOperationContainer.CreateVertexInfo(commandBuffer);
            _spriteBatch.DrawBatch(commandBuffer, renderPass, swapchainTexture, _spriteOperationContainer, null);

            /*_textBatch.Start();
            _textBatch.Add(
                _sofiaSans,
                $"{(int)_fps}",
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

            //renderPass.BindGraphicsPipeline(_fontPipeline);
            //_textBatch.Render(renderPass, cameraMatrix);*/

            commandBuffer.EndRenderPass(renderPass);
        }
        GraphicsDevice.Submit(commandBuffer);
    }

    protected override void Destroy()
    {
        _depthTexture.Dispose();
    }
}
