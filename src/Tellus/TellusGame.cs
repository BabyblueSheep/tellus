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
        _spriteBatch = new SpriteBatch(GraphicsDevice, RootTitleStorage, MainWindow.SwapchainFormat);

        var resourceUploader = new ResourceUploader(GraphicsDevice);

        _spriteTexture = resourceUploader.CreateTexture2DFromCompressed(
            RootTitleStorage,
            "Assets/image.png",
            TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler
        );

        resourceUploader.Upload();
        resourceUploader.Dispose();
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
            var renderPass = cmdbuf.BeginRenderPass(
                new ColorTargetInfo(swapchainTexture, Color.CornflowerBlue)
            );

            _spriteBatch.Begin();

            _spriteBatch.Draw
            (
                new Vector2(0, 0),
                Rectangle.Empty,
                new Vector2(200, 200),
                _time,
                new Vector2(128, 128),
                Color.White,
                1f
            );

            _spriteBatch.Draw
            (
                new Vector2(64, 64),
                Rectangle.Empty,
                new Vector2(100, 100),
                _time,
                new Vector2(128, 128),
                Color.White,
                0.9f
            );

            _spriteBatch.Draw
            (
                new Vector2(0, 0),
                Rectangle.Empty,
                new Vector2(100, 100),
                _time,
                new Vector2(128, 128),
                Color.White,
                0.9f
            );

            _spriteBatch.End(cmdbuf, renderPass, swapchainTexture, _spriteTexture);

            cmdbuf.EndRenderPass(renderPass);
        }
        GraphicsDevice.Submit(cmdbuf);
    }
}
