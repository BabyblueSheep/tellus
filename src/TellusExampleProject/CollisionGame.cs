using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Graphics.Font;
using MoonWorks.Input;
using MoonWorks.Storage;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http.Headers;
using System.Numerics;
using System.Reflection;
using Tellus.Collision;
using Tellus.Graphics;

using Color = MoonWorks.Graphics.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace TellusExampleProject;

internal class ColliderTestCircle : IHasColliderShapes
{
    public bool CollidedThisFrame;

    public Vector2 Center;
    public float Radius;
    public int VertexCount;

    public Vector2 ShapeOffset => Center;
    public IEnumerable<Vector2> ShapeVertices => ColliderShapeProvider.GetCircleVertices(Vector2.Zero, Radius, VertexCount);
    public IEnumerable<(int, int)> ShapeIndexRanges => ColliderShapeProvider.GetConnectedShapeIndices(0, VertexCount);
}

internal class CollisionGame : Game
{
    private readonly CollisionHandler _collisionHandler;
    private readonly SpriteBatch _spriteBatch;

    private readonly Font _jetbrainsMono;
    private readonly TextBatch _textBatch;
    private GraphicsPipeline _fontPipeline;

    private ColliderTestCircle _colliderTest1;
    private List<ColliderTestCircle> _colliderTest2;
    private List<ColliderTestCircle> _colliderTest3;

    private long collideMilliseconds;
    private long totalMilliseconds;

    private readonly Texture _circleSprite;
    private Texture _depthTexture;

    public CollisionGame
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
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _colliderTest1 = new ColliderTestCircle()
        {
            Center = new Vector2(0, 0),
            Radius = 8,
            VertexCount = 12,
        };

        _colliderTest2 = new List<ColliderTestCircle>(80);
        _colliderTest3 = new List<ColliderTestCircle>(80);

        var random = new Random();
        for (int i = 0; i < 80; i++)
        {
            _colliderTest2.Add( new ColliderTestCircle()
            {
                Center = new Vector2(random.Next(0, 400), random.Next(0, 400)),
                Radius = (float)random.NextDouble() * 31 + 1,
                VertexCount = 6,
            });

            _colliderTest3.Add(new ColliderTestCircle()
            {
                Center = new Vector2(random.Next(0, 400), random.Next(0, 400)),
                Radius = (float)random.NextDouble() * 31 + 1,
                VertexCount = 6,
            });
        }

        _collisionHandler = new CollisionHandler(GraphicsDevice);

        var resourceUploader = new ResourceUploader(GraphicsDevice);

        _circleSprite = resourceUploader.CreateTexture2DFromCompressed(
            RootTitleStorage,
            "Assets/image_circle.png",
            TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler
        );

        resourceUploader.Upload();
        resourceUploader.Dispose();

        _depthTexture = Texture.Create2D(GraphicsDevice, "Depth Texture", 1, 1, TextureFormat.D16Unorm, TextureUsageFlags.DepthStencilTarget);

        _jetbrainsMono = Font.Load(GraphicsDevice, RootTitleStorage, "Assets/SofiaSans.ttf");
        _textBatch = new TextBatch(GraphicsDevice);

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
                        BlendState = ColorTargetBlendState.NonPremultipliedAlphaBlend
                    }
                ]
            }
        };

        _fontPipeline = GraphicsPipeline.Create(GraphicsDevice, fontPipelineCreateInfo);
    }

    protected override void Update(TimeSpan delta)
    {
        _colliderTest1.CollidedThisFrame = false;
        foreach (var collider in _colliderTest2)
        {
            collider.CollidedThisFrame = false;
        }

        _colliderTest1.Center = new Vector2(Inputs.Mouse.X, Inputs.Mouse.Y);

        var totalStopwatch = Stopwatch.StartNew();

        var colliderListOne = _colliderTest2.Select(collider => (IHasColliderShapes)collider).ToList();
        var colliderListTwo = _colliderTest3.Select(collider => (IHasColliderShapes)collider).ToList();

        var collideStopwatch = Stopwatch.StartNew();

        var collisionResults = _collisionHandler.HandleCollisions([_colliderTest1], colliderListOne);

        collideMilliseconds = collideStopwatch.ElapsedMilliseconds;

        foreach (var collisionResult in collisionResults)
        {
            ColliderTestCircle item1 = (ColliderTestCircle)collisionResult.Item1;
            ColliderTestCircle item2 = (ColliderTestCircle)collisionResult.Item2;

            //item1.CollidedThisFrame = true;
            //item2.CollidedThisFrame = true;
        }

        totalMilliseconds = totalStopwatch.ElapsedMilliseconds;
    }

    protected override void Draw(double alpha)
    {
        Matrix4x4 proj = Matrix4x4.CreateOrthographicOffCenter(
            0,
            MainWindow.Width,
            MainWindow.Height,
            0,
            0,
            -1
        );

        Matrix4x4 collideModel = Matrix4x4.CreateTranslation(10, 10, 0);
        Matrix4x4 totalModel = Matrix4x4.CreateTranslation(10, 40, 0);

        CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();
        Texture swapchainTexture = cmdbuf.AcquireSwapchainTexture(MainWindow);
        if (swapchainTexture != null)
        {
            if (_depthTexture.Width != swapchainTexture.Width || _depthTexture.Height != swapchainTexture.Height)
            {
                _depthTexture.Dispose();
                _depthTexture = Texture.Create2D(GraphicsDevice, "Depth Texture", swapchainTexture.Width, swapchainTexture.Height, TextureFormat.D16Unorm, TextureUsageFlags.DepthStencilTarget);
            }

            var renderPass = cmdbuf.BeginRenderPass(
                new DepthStencilTargetInfo(_depthTexture, 1),
                new ColorTargetInfo(swapchainTexture, Color.CornflowerBlue)
            );

            _spriteBatch.Begin(
                SpriteSortMode.FrontToBack,
                ColorTargetBlendState.PremultipliedAlphaBlend,
                SamplerCreateInfo.PointWrap,
                new DepthStencilState()
                {
                    EnableDepthTest = true,
                    EnableDepthWrite = true,
                    CompareOp = CompareOp.LessOrEqual,
                },
                RasterizerState.CCW_CullNone,
                null, null, null);

            _spriteBatch.Draw
            (
                _circleSprite,
                new Vector2(_colliderTest1.Radius),
                new Rectangle(0, 0, 64, 64),
                _colliderTest1.Center,
                0,
                new Vector2(_colliderTest1.Radius * 2),
                _colliderTest1.CollidedThisFrame ? Color.Red : Color.Blue,
                0.5f
            );

            foreach (var collider in _colliderTest2)
            {
                _spriteBatch.Draw
                (
                    _circleSprite,
                    new Vector2(collider.Radius),
                    new Rectangle(0, 0, 64, 64),
                    collider.Center,
                    0,
                    new Vector2(collider.Radius * 2),
                    Color.White,
                    0.4f
                );
            }

            /*foreach (var collider in _colliderTest3)
            {
                _spriteBatch.Draw
                (
                    _circleSprite,
                    new Vector2(collider.Radius),
                    new Rectangle(0, 0, 64, 64),
                    collider.Center,
                    0,
                    new Vector2(collider.Radius * 2),
                    Color.Blue,
                    0.4f
                );
            }*/

            _spriteBatch.End(cmdbuf, renderPass, swapchainTexture, swapchainTexture.Format, TextureFormat.D16Unorm);

            
            _textBatch.Start();
            _textBatch.Add(
                _jetbrainsMono,
                $"{collideMilliseconds} ms",
                16,
                collideModel,
                Color.Black,
                HorizontalAlignment.Left,
                VerticalAlignment.Middle
            );
            _textBatch.Add(
                _jetbrainsMono,
                $"{totalMilliseconds} ms",
                16,
                totalModel,
                Color.Black,
                HorizontalAlignment.Left,
                VerticalAlignment.Middle
            );
            _textBatch.UploadBufferData(cmdbuf);

            renderPass.BindGraphicsPipeline(_fontPipeline);
            _textBatch.Render(renderPass, proj);
            

            cmdbuf.EndRenderPass(renderPass);
        }
        GraphicsDevice.Submit(cmdbuf);
    }

    protected override void Destroy()
    {
        _depthTexture.Dispose();
    }
}
