using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;
using MoonWorks.Storage;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http.Headers;
using System.Numerics;
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

    private ColliderTestCircle _colliderTest1;
    private List<ColliderTestCircle> _colliderTest2;

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
            Radius = 64,
            VertexCount = 12,
        };
        _colliderTest2 = 
        [
            new ColliderTestCircle()
            {
                Center = new Vector2(120, 50),
                Radius = 128,
                VertexCount = 6,
            },
            new ColliderTestCircle()
            {
                Center = new Vector2(280, 180),
                Radius = 87,
                VertexCount = 6,
            },
            new ColliderTestCircle()
            {
                Center = new Vector2(530, 6),
                Radius = 14,
                VertexCount = 6,
            },
            new ColliderTestCircle()
            {
                Center = new Vector2(250, 531),
                Radius = 30,
                VertexCount = 6,
            },
        ];

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
    }

    protected override void Update(TimeSpan delta)
    {
        _colliderTest1.CollidedThisFrame = false;
        foreach (var collider in _colliderTest2)
        {
            collider.CollidedThisFrame = false;
        }

        _colliderTest1.Center = new Vector2(Inputs.Mouse.X, Inputs.Mouse.Y);

        var colliderList = _colliderTest2.Select(collider => (IHasColliderShapes)collider).ToList();
        var collisionResults = _collisionHandler.HandleCollisions([_colliderTest1], colliderList);
        foreach (var collisionResult in collisionResults)
        {
            ColliderTestCircle item1 = (ColliderTestCircle)collisionResult.Item1;
            ColliderTestCircle item2 = (ColliderTestCircle)collisionResult.Item2;

            item1.CollidedThisFrame = true;
            item2.CollidedThisFrame = true;
        }
    }

    protected override void Draw(double alpha)
    {
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
                _colliderTest1.CollidedThisFrame ? Color.Red : Color.White,
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

            _spriteBatch.End(cmdbuf, renderPass, swapchainTexture, swapchainTexture.Format, TextureFormat.D16Unorm);

            cmdbuf.EndRenderPass(renderPass);
        }
        GraphicsDevice.Submit(cmdbuf);
    }

    protected override void Destroy()
    {
        _depthTexture.Dispose();
    }
}
