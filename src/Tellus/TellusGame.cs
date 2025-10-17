using MoonWorks;
using MoonWorks.Graphics;
using System.Drawing;
using System.Numerics;
using Tellus.Graphics;

using Color = MoonWorks.Graphics.Color;

namespace Tellus;

internal class TellusGame : Game
{
    private readonly SpriteBatch _spriteBatch;

    private readonly Texture _spriteTexture;
    private Texture _depthTexture;
    private float _time;

    public TellusGame
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
        _spriteBatch = new SpriteBatch(GraphicsDevice, RootTitleStorage, MainWindow.SwapchainFormat, TextureFormat.D16Unorm);

        var resourceUploader = new ResourceUploader(GraphicsDevice);

        _spriteTexture = resourceUploader.CreateTexture2DFromCompressed(
            RootTitleStorage,
            "Assets/image.png",
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
                _depthTexture = Texture.Create2D(GraphicsDevice, "Depth Texture", swapchainTexture.Width, swapchainTexture.Height, TextureFormat.D16Unorm, TextureUsageFlags.DepthStencilTarget);
            }

            var renderPass = cmdbuf.BeginRenderPass(
                new DepthStencilTargetInfo(_depthTexture, 1),
                new ColorTargetInfo(swapchainTexture, Color.CornflowerBlue)
            );

            _spriteBatch.Begin();

            _spriteBatch.Draw
            (
                new Vector2(0, 0),
                new Vector4(0, 0, 1f, 1f),
                new Vector2(150, 150),
                0,
                new Vector2(128, 128),
                Color.White,
                0.5f
            );

            _spriteBatch.Draw
            (
                new Vector2(0, 0),
                new Vector4(0, 0, 1f, 1f),
                new Vector2(150, 130),
                0,
                new Vector2(128, 128),
                Color.Blue,
                0.6f
            );

            _spriteBatch.Draw
            (
                new Vector2(0, 0),
                new Vector4(0, 0, 1f, 1f),
                new Vector2(150, 180),
                0,
                new Vector2(128, 128),
                Color.Red,
                0.4f
            );

            _spriteBatch.End(cmdbuf, renderPass, swapchainTexture, _spriteTexture);

            cmdbuf.EndRenderPass(renderPass);
        }
        GraphicsDevice.Submit(cmdbuf);
    }
}
