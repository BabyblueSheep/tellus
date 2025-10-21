using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Storage;
using System.Drawing;
using System.Numerics;
using Tellus.Graphics;

using Color = MoonWorks.Graphics.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace TellusExampleProject;

internal class SpritebatchGame : Game
{
    private readonly SpriteBatch _spriteBatch;

    private readonly Texture _spriteTexture1;
    private readonly Texture _spriteTexture2;
    private readonly Texture _spriteTexture3;
    private Texture _depthTexture;
    private float _time;

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
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        var resourceUploader = new ResourceUploader(GraphicsDevice);

        _spriteTexture1 = resourceUploader.CreateTexture2DFromCompressed(
            RootTitleStorage,
            "Assets/image1.png",
            TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler
        );

        _spriteTexture2 = resourceUploader.CreateTexture2DFromCompressed(
            RootTitleStorage,
            "Assets/image2.png",
            TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler
        );

        _spriteTexture3 = resourceUploader.CreateTexture2DFromCompressed(
            RootTitleStorage,
            "Assets/image3.png",
            TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler
        );

        resourceUploader.Upload();
        resourceUploader.Dispose();

        _depthTexture = Texture.Create2D(GraphicsDevice, "Depth Texture", 1, 1, TextureFormat.D16Unorm, TextureUsageFlags.DepthStencilTarget);
    }

    protected override void Update(TimeSpan delta)
    {
        _time += delta.Milliseconds * 0.01f;
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
                _spriteTexture1,
                new Vector2(0, 0),
                new Rectangle(0, 0, 8, 8),
                new Vector2(350, 200),
                0,
                new Vector2(128, 128),
                Color.Red,
                0.3f
            );

            _spriteBatch.Draw
            (
                _spriteTexture3,
                new Vector2(0, 0),
                new Rectangle(0, 0, 64, 64),
                new Vector2(300, 300),
                0,
                new Vector2(128, 128),
                Color.Green,
                0.6f
            );

            _spriteBatch.Draw
            (
                _spriteTexture2,
                new Vector2(0, 0),
                new Rectangle(0, 0, 64, 64),
                new Vector2(400, 300),
                0,
                new Vector2(128, 128),
                Color.Blue,
                1f
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
