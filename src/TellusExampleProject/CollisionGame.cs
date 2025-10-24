using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Storage;
using System.Drawing;
using System.Numerics;
using Tellus.Collision;
using Tellus.Graphics;

using Color = MoonWorks.Graphics.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace TellusExampleProject;

internal class ColliderTestCircle : IHasColliderShapes
{
    public Vector2 Center;
    public float Radius;

    public IColliderShape[] GetColliderShapes()
    {
        var test1 = new CircleCollider()
        {
            Center = Center,
            Radius = Radius
        };

        return [test1];
    }
}

internal class CollisionGame : Game
{
    private readonly CollisionHandler _collisionHandler;
    private readonly SpriteBatch _spriteBatch;

    private ColliderTestCircle _colliderTest1;
    private ColliderTestCircle _colliderTest2;

    private readonly Texture _spriteTexture;
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
            Radius = 5
        };
        _colliderTest2 = new ColliderTestCircle()
        {
            Center = new Vector2(30, 0),
            Radius = 5
        };

        _collisionHandler = new CollisionHandler(GraphicsDevice);

        var resourceUploader = new ResourceUploader(GraphicsDevice);

        _spriteTexture = resourceUploader.CreateTexture2DFromCompressed(
            RootTitleStorage,
            "Assets/image1.png",
            TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler
        );

        resourceUploader.Upload();
        resourceUploader.Dispose();

        _depthTexture = Texture.Create2D(GraphicsDevice, "Depth Texture", 1, 1, TextureFormat.D16Unorm, TextureUsageFlags.DepthStencilTarget);
    }

    protected override void Update(TimeSpan delta)
    {
        var collisionResults = _collisionHandler.HandleCircleCircleCollision([_colliderTest1], [_colliderTest2]);
        foreach (var collisionResult in collisionResults)
        {
            ColliderTestCircle item1 = (ColliderTestCircle)collisionResult.Item1;
            ColliderTestCircle item2 = (ColliderTestCircle)collisionResult.Item2;
            Logger.LogInfo($"{item1.Center} {item1.Radius}; {item2.Center} {item2.Radius}");
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
                SpriteSortMode.Deferred,
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
                _spriteTexture,
                new Vector2(0, 0),
                new Rectangle(0, 0, 8, 8),
                new Vector2(350, 200),
                0,
                new Vector2(128, 128),
                Color.Red,
                0.3f
            );

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
