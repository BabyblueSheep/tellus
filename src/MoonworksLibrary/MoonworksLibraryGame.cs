using MoonWorks;
using MoonWorks.Graphics;
using MoonworksLibrary.Graphics;

namespace MoonworksLibrary;

internal class MoonworksLibraryGame : Game
{
    private SpriteBatch _spriteBatch;

    public MoonworksLibraryGame
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
        Shader vertexShader = ShaderCross.Create
        (
            GraphicsDevice,
            RootTitleStorage,
            "Assets/TexturedQuad.vert",
            "main",
            ShaderCross.ShaderFormat.HLSL,
            ShaderStage.Vertex
        );

        Shader fragmentShader = ShaderCross.Create
        (
            GraphicsDevice,
            RootTitleStorage,
            "Assets/TexturedQuad.frag",
            "main",
            ShaderCross.ShaderFormat.HLSL,
            ShaderStage.Fragment
        );

        _spriteBatch = new SpriteBatch(GraphicsDevice, RootTitleStorage, MainWindow.SwapchainFormat);
    }

    protected override void Update(TimeSpan delta)
    {
        
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

            cmdbuf.EndRenderPass(renderPass);

            _spriteBatch.RenderBatch(GraphicsDevice, swapchainTexture);
        }
        GraphicsDevice.Submit(cmdbuf);
    }
}
