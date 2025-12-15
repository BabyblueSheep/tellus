using MoonWorks.Graphics;
using System.Runtime.InteropServices;
using Tellus.Graphics;

namespace Tellus;

public static class InternalUtils
{
    private static void GenerateDataFromBackend(string backend, out ShaderFormat shaderFormat, out string extension, out string entryPointName)
    {
        switch (backend)
        {
            case "vulkan":
                shaderFormat = ShaderFormat.SPIRV;
                extension = "spv";
                entryPointName = "main";
                break;

            case "metal":
                shaderFormat = ShaderFormat.MSL;
                extension = "msl";
                entryPointName = "main0";
                break;

            case "direct3d11":
                shaderFormat = ShaderFormat.DXBC;
                extension = "dxbc";
                entryPointName = "main";
                break;

            case "direct3d12":
                shaderFormat = ShaderFormat.DXIL;
                extension = "dxil";
                entryPointName = "main";
                break;

            default:
                throw new ArgumentException("This shouldn't happen!");
        }
    }

    // Taken from
    // https://github.com/MoonsideGames/MoonWorks/blob/main/src/Graphics/GraphicsDevice.cs#L538
    internal unsafe static void LoadShaderFromManifest(GraphicsDevice graphicsDevice, string shaderName, ShaderCreateInfo createInfo, out Shader shader)
    {
        GenerateDataFromBackend(graphicsDevice.Backend, out ShaderFormat shaderFormat, out string extension, out string entryPointName);

        ShaderCreateInfo properShaderCreateInfo = createInfo with { Format = shaderFormat };
        string filepath = $"Tellus.{shaderName}.{extension}";
        var assembly = typeof(InternalUtils).Assembly;
        using var stream = assembly.GetManifestResourceStream(filepath);
        
        if (stream == null)
        {
            throw new ArgumentException($"Shader {filepath} doesn't exist!");
        }

        var buffer = NativeMemory.Alloc((nuint)stream.Length);
        var span = new Span<byte>(buffer, (int)stream.Length);
        stream.ReadExactly(span);

        shader = Shader.Create(
            graphicsDevice,
            span,
            entryPointName,
            properShaderCreateInfo
        );

        NativeMemory.Free(buffer);
    }

    internal unsafe static void LoadShaderFromManifest(GraphicsDevice graphicsDevice, string shaderName, ComputePipelineCreateInfo createInfo, out ComputePipeline shader)
    {
        GenerateDataFromBackend(graphicsDevice.Backend, out ShaderFormat shaderFormat, out string extension, out string entryPointName);

        createInfo.Format = shaderFormat;
        string filepath = $"Tellus.{shaderName}.{extension}";
        var assembly = typeof(InternalUtils).Assembly;
        using var stream = assembly.GetManifestResourceStream(filepath);

        if (stream == null)
        {
            throw new ArgumentException($"Shader {filepath} doesn't exist!");
        }

        var buffer = NativeMemory.Alloc((nuint)stream.Length);
        var span = new Span<byte>(buffer, (int)stream.Length);
        stream.ReadExactly(span);

        shader = ComputePipeline.Create(
            graphicsDevice,
            span,
            entryPointName,
            createInfo
        );

        NativeMemory.Free(buffer);
    }
}
