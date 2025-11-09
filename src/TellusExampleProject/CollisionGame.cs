using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Graphics.Font;
using MoonWorks.Input;
using MoonWorks.Storage;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http.Headers;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Tellus.Collision;
using Tellus.Graphics;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = MoonWorks.Graphics.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace TellusExampleProject;

internal abstract class CollidingObject
{
    public bool HasCollidedThisFrame;
}

internal class CircleCollidingObject : CollidingObject, ICollisionBody
{
    public Vector2 Center;
    public float Radius;

    public Vector2 BodyOffset => Center;
    public IEnumerable<ICollisionBodyPart> BodyParts => [
        new CircleCollisionBodyPart(Vector2.Zero, Radius, 16)
    ];
}

internal sealed class PlayerObject : CircleCollidingObject, ICollisionRayCaster
{
    public Vector2 RayOriginOffset => Center;

    public readonly List<CollisionRay> SavedRays = [];

    public IEnumerable<CollisionRay> Rays => [
        new CollisionRay(Vector2.Zero, Vector2.UnitY, 200)    
    ];
}

internal sealed class RectangleCollidingObject : CollidingObject, ICollisionBody
{
    public Vector2 Center;
    public float Angle;
    public Vector2 SideLengths;

    public Vector2 BodyOffset => Center;
    public IEnumerable<ICollisionBodyPart> BodyParts => [
        new RectangleCollisionBodyPart(Vector2.Zero, Angle, SideLengths)
    ];
}

internal sealed class TriangleCollidingObject : CollidingObject, ICollisionBody
{
    public Vector2 PointOne;
    public Vector2 PointTwo;
    public Vector2 PointThree;

    public Vector2 BodyOffset => PointOne;
    public IEnumerable<ICollisionBodyPart> BodyParts => [
        new TriangleCollisionBodyPart(Vector2.Zero, PointTwo - PointOne, PointThree - PointOne)
    ];
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct PositionColorVertex : IVertexType
{
    [FieldOffset(0)]
    public Vector4 Position;

    [FieldOffset(16)]
    public Vector4 Color;

    public static VertexElementFormat[] Formats { get; } =
    [
        VertexElementFormat.Float4,
        VertexElementFormat.Float4,
    ];

    public static uint[] Offsets { get; } =
    [
        0,
        16
    ];
}

internal class CollisionGame : Game
{
    private readonly CollisionHandler _collisionHandler;
    private readonly CollisionHandler.BodyStorageBuffer _storageBufferBodiesOne;
    private readonly CollisionHandler.BodyStorageBuffer _storageBufferBodiesTwo;
    private readonly CollisionHandler.RayCasterStorageBuffer _rayBuffer;
    private readonly CollisionHandler.HitResultStorageBuffer _storageBufferHitResult;
    private readonly CollisionHandler.ResolutionResultStorageBuffer _storageBufferResolutionResult;

    private readonly PlayerObject _playerObject;
    private readonly List<CollidingObject> _targetObjects;

    private readonly Texture _circleSprite;
    private readonly Texture _squareSprite;
    private Texture _depthTexture;
    private readonly SpriteBatch _spriteBatch;

    private readonly Buffer _triangleVertexBuffer;
    private int _triangleCount;
    private readonly GraphicsPipeline _trianglePipeline;

    private readonly TransferBuffer _lineVertexTransferBuffer;
    private readonly Buffer _lineVertexBuffer;
    private readonly GraphicsPipeline _linePipeline;

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
        _depthTexture = Texture.Create2D(GraphicsDevice, "Depth Texture", 1, 1, TextureFormat.D16Unorm, TextureUsageFlags.DepthStencilTarget);
        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();

        #region Images
        var resourceUploader = new ResourceUploader(GraphicsDevice);

        _circleSprite = resourceUploader.CreateTexture2DFromCompressed(
            RootTitleStorage,
            "Assets/image_circle.png",
            TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler
        );

        _squareSprite = resourceUploader.CreateTexture2DFromCompressed(
            RootTitleStorage,
            "Assets/image2.png",
            TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler
        );

        resourceUploader.Upload();
        resourceUploader.Dispose();

        #endregion

        #region Colliders
        _playerObject = new PlayerObject()
        {
            Radius = 16
        };

        _targetObjects = new List<CollidingObject>(2);

        var random = new Random();
        for (int i = 0; i < 2; i++)
        {
            Vector2 center = new Vector2(random.NextSingle() * 900, random.NextSingle() * 900);
            int shapeType = random.Next(3);
            switch (shapeType)
            {
                case 0:
                    _targetObjects.Add(new CircleCollidingObject()
                    {
                        Radius = random.NextSingle() * 24 + 8,
                        Center = center,
                    });
                    break;
                case 1:
                    _targetObjects.Add(new RectangleCollidingObject()
                    {
                        Center = center,
                        Angle = random.NextSingle() * MathF.Tau,
                        SideLengths = new Vector2(random.NextSingle() * 56 + 8, random.NextSingle() * 56 + 8),
                    });
                    break;
                case 2:
                    _targetObjects.Add(new TriangleCollidingObject()
                    {
                        PointOne = center,
                        PointTwo = center + new Vector2(random.NextSingle() * 24 + 8, 0),
                        PointThree = center + new Vector2(random.NextSingle() * 24 + 8, random.NextSingle() * 24 + 8)
                    });
                    break;
            }
        }

        _collisionHandler = new CollisionHandler(GraphicsDevice);
        _storageBufferBodiesOne = new CollisionHandler.BodyStorageBuffer(GraphicsDevice);
        _storageBufferBodiesTwo = new CollisionHandler.BodyStorageBuffer(GraphicsDevice);
        _storageBufferHitResult = new CollisionHandler.HitResultStorageBuffer(GraphicsDevice);
        _storageBufferResolutionResult = new CollisionHandler.ResolutionResultStorageBuffer(GraphicsDevice);
        _rayBuffer = new CollisionHandler.RayCasterStorageBuffer(GraphicsDevice, createDownloadBuffer: true);

        _storageBufferBodiesTwo.UploadData(commandBuffer, _targetObjects.Select(item => (ICollisionBody)item));

        #endregion

        #region Triangle rendering stuff
        _triangleVertexBuffer = Buffer.Create<PositionColorVertex>
        (
            GraphicsDevice,
            BufferUsageFlags.Vertex,
            80 * 3
        );

        TransferBuffer vertexBuffer = TransferBuffer.Create<PositionColorVertex>
        (
            GraphicsDevice,
            TransferBufferUsage.Upload,
            80 * 3
        );

        var vertexSpan = vertexBuffer.Map<PositionColorVertex>(false);
        foreach (var colliderObject in _targetObjects)
        {
            if (colliderObject is TriangleCollidingObject triangle)
            {
                vertexSpan[_triangleCount * 3].Position = new Vector4(triangle.PointOne, 0f, 1f);
                vertexSpan[_triangleCount * 3 + 1].Position = new Vector4(triangle.PointTwo, 0f, 1f);
                vertexSpan[_triangleCount * 3 + 2].Position = new Vector4(triangle.PointThree, 0f, 1f);

                vertexSpan[_triangleCount * 3].Color = new Vector4(1f, 1f, 1f, 1f);
                vertexSpan[_triangleCount * 3 + 1].Color = new Vector4(1f, 1f, 1f, 1f);
                vertexSpan[_triangleCount * 3 + 2].Color = new Vector4(1f, 1f, 1f, 1f);

                _triangleCount++;
            }
        }
        vertexBuffer.Unmap();

        var copyPass = commandBuffer.BeginCopyPass();
        copyPass.UploadToBuffer(vertexBuffer, _triangleVertexBuffer, false);
        commandBuffer.EndCopyPass(copyPass);

        vertexBuffer.Dispose();

        Shader vertexShader = ShaderCross.Create(
            GraphicsDevice,
            RootTitleStorage,
            "Assets/Position.vert.hlsl",
            "main",
            ShaderCross.ShaderFormat.HLSL,
            ShaderStage.Vertex
        );
        Shader fragmentShader = ShaderCross.Create(
            GraphicsDevice,
            RootTitleStorage,
            "Assets/SolidColor.frag.hlsl",
            "main",
            ShaderCross.ShaderFormat.HLSL,
            ShaderStage.Fragment
        );

        var triangleGraphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            VertexInputState = VertexInputState.CreateSingleBinding<PositionColorVertex>(),
            PrimitiveType = PrimitiveType.TriangleList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = MultisampleState.None,
            DepthStencilState = DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo()
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription()
                    {
                        Format = MainWindow.SwapchainFormat,
                        BlendState = ColorTargetBlendState.Opaque,
                    }
                ],
            },
        };
        _trianglePipeline = GraphicsPipeline.Create(GraphicsDevice, triangleGraphicsPipelineCreateInfo);
        #endregion

        #region Line rendering stuff
        _lineVertexBuffer = Buffer.Create<PositionColorVertex>
        (
            GraphicsDevice,
            BufferUsageFlags.Vertex,
            16 * 2 * 2
        );

        _lineVertexTransferBuffer = TransferBuffer.Create<PositionColorVertex>
        (
            GraphicsDevice,
            TransferBufferUsage.Upload,
            16 * 2 * 2
        );

        var lineGraphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            VertexInputState = VertexInputState.CreateSingleBinding<PositionColorVertex>(),
            PrimitiveType = PrimitiveType.LineList,
            RasterizerState = RasterizerState.CCW_CullNone,
            MultisampleState = MultisampleState.None,
            DepthStencilState = DepthStencilState.Disable,
            TargetInfo = new GraphicsPipelineTargetInfo()
            {
                ColorTargetDescriptions =
                [
                    new ColorTargetDescription()
                    {
                        Format = MainWindow.SwapchainFormat,
                        BlendState = ColorTargetBlendState.Opaque,
                    }
                ],
            },
        };
        _linePipeline = GraphicsPipeline.Create(GraphicsDevice, lineGraphicsPipelineCreateInfo);
        #endregion

        GraphicsDevice.Submit(commandBuffer);
    }

    protected override void Update(TimeSpan delta)
    {
        _playerObject.HasCollidedThisFrame = false;
        foreach (var collider in _targetObjects)
        {
            collider.HasCollidedThisFrame = false;
        }

        if (Inputs.Keyboard.IsDown(KeyCode.A))
            _playerObject.Center.X -= 5;
        if (Inputs.Keyboard.IsDown(KeyCode.D))
            _playerObject.Center.X += 5;
        if (Inputs.Keyboard.IsDown(KeyCode.W))
            _playerObject.Center.Y -= 5;
        if (Inputs.Keyboard.IsDown(KeyCode.S))
            _playerObject.Center.Y += 5;

        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();

        _storageBufferBodiesOne.UploadData(commandBuffer, [_playerObject]);

        _storageBufferHitResult.ClearData(commandBuffer);
        _collisionHandler.ComputeCollisionHits(commandBuffer, _storageBufferBodiesOne, _storageBufferBodiesTwo, _storageBufferHitResult);
        _storageBufferHitResult.DownloadData(commandBuffer);

        _storageBufferResolutionResult.ClearData(commandBuffer);
        _collisionHandler.ComputeCollisionResolutions(commandBuffer, _storageBufferBodiesOne, _storageBufferBodiesTwo, _storageBufferResolutionResult);
        _storageBufferResolutionResult.DownloadData(commandBuffer);

        var fence = GraphicsDevice.SubmitAndAcquireFence(commandBuffer);
        GraphicsDevice.WaitForFence(fence);
        GraphicsDevice.ReleaseFence(fence);

        commandBuffer = GraphicsDevice.AcquireCommandBuffer();

        var hitResults = _storageBufferHitResult.GetData([_playerObject], _targetObjects.Select(item => (ICollisionBody)item).ToList());

        foreach (var collisionResult in hitResults)
        {
            CollidingObject item1 = (CollidingObject)collisionResult.Item1;
            CollidingObject item2 = (CollidingObject)collisionResult.Item2;

            item1.HasCollidedThisFrame = true;
            item2.HasCollidedThisFrame = true;
        }

        var resolutionResults = _storageBufferResolutionResult.GetData([_playerObject]);
        foreach (var collisionResult in resolutionResults)
        {
            CircleCollidingObject item1 = (CircleCollidingObject)collisionResult.Item1;
            item1.Center += collisionResult.Item2;
        }

        _rayBuffer.UploadData(commandBuffer, [_playerObject]);
        _collisionHandler.ComputeRayRestrictions(commandBuffer, _storageBufferBodiesTwo, _rayBuffer);
        _rayBuffer.DownloadData(commandBuffer);

        fence = GraphicsDevice.SubmitAndAcquireFence(commandBuffer);
        GraphicsDevice.WaitForFence(fence);
        GraphicsDevice.ReleaseFence(fence);
        
        var rayResults = _rayBuffer.GetData([_playerObject]);

        foreach (var data in rayResults)
        {
            if (data.Item1 is PlayerObject player)
            {
                player.SavedRays.Clear();
                foreach (var ray in data.Item2)
                {
                    player.SavedRays.Add(ray);
                }
            }
        }
    }

    protected override void Draw(double alpha)
    {
        CommandBuffer commandBuffer = GraphicsDevice.AcquireCommandBuffer();
        Texture swapchainTexture = commandBuffer.AcquireSwapchainTexture(MainWindow);
        if (swapchainTexture != null)
        {
            var cameraMatrix = Matrix4x4.CreateOrthographicOffCenter
            (
                0,
                swapchainTexture.Width,
                swapchainTexture.Height,
                0,
                0,
                -1f
            );

            if (_depthTexture.Width != swapchainTexture.Width || _depthTexture.Height != swapchainTexture.Height)
            {
                _depthTexture.Dispose();
                _depthTexture = Texture.Create2D(GraphicsDevice, "Depth Texture", swapchainTexture.Width, swapchainTexture.Height, TextureFormat.D16Unorm, TextureUsageFlags.DepthStencilTarget);
            }

            var renderPass = commandBuffer.BeginRenderPass(
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

            _spriteBatch.Draw(
                _circleSprite,
                new Vector2(_playerObject.Radius),
                new Rectangle(0, 0, (int)_circleSprite.Width, (int)_circleSprite.Height),
                _playerObject.Center,
                0f, 
                new Vector2(_playerObject.Radius * 2f),
                _playerObject.HasCollidedThisFrame ? Color.Red : Color.White,
                0.5f
            );

            foreach (var objectCollider in _targetObjects)
            {
                if (objectCollider is CircleCollidingObject circle)
                {
                    _spriteBatch.Draw(
                        _circleSprite,
                        new Vector2(circle.Radius),
                        new Rectangle(0, 0, (int)_circleSprite.Width, (int)_circleSprite.Height),
                        circle.Center,
                        0f,
                        new Vector2(circle.Radius * 2f),
                        Color.White,
                        0.6f
                    );
                }
                else if (objectCollider is RectangleCollidingObject rectangle)
                {
                    _spriteBatch.Draw(
                        _squareSprite,
                        rectangle.SideLengths * 0.5f,
                        new Rectangle(0, 0, (int)_circleSprite.Width, (int)_circleSprite.Height),
                        rectangle.Center,
                        rectangle.Angle,
                        rectangle.SideLengths,
                        Color.White,
                        0.6f
                    );
                }
            }

            _spriteBatch.End(commandBuffer, renderPass, swapchainTexture, swapchainTexture.Format, TextureFormat.D16Unorm);

            commandBuffer.PushVertexUniformData(cameraMatrix);

            renderPass.BindGraphicsPipeline(_trianglePipeline);
            renderPass.BindVertexBuffers(_triangleVertexBuffer);
            renderPass.DrawPrimitives((uint)_triangleCount * 3, 1, 0, 0);

            int lineCount = 0;
            var lineVertexSpan = _lineVertexTransferBuffer.Map<PositionColorVertex>(false);
            foreach (var ray in _playerObject.SavedRays)
            {
                lineVertexSpan[lineCount * 2].Position = new Vector4(ray.RayOrigin + _playerObject.RayOriginOffset, 0f, 1f);
                lineVertexSpan[lineCount * 2 + 1].Position = new Vector4(ray.RayOrigin + ray.RayDirection * ray.RayLength + _playerObject.RayOriginOffset, 0f, 1f);

                lineVertexSpan[lineCount * 2].Color = new Vector4(0f, 1f, 1f, 1f);
                lineVertexSpan[lineCount * 2 + 1].Color = new Vector4(0f, 1f, 1f, 1f);

                lineCount++;
            }
            _lineVertexTransferBuffer.Unmap();

            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.UploadToBuffer(_lineVertexTransferBuffer, _lineVertexBuffer, false);
            commandBuffer.EndCopyPass(copyPass);

            renderPass.BindGraphicsPipeline(_linePipeline);
            renderPass.BindVertexBuffers(_lineVertexBuffer);
            renderPass.DrawPrimitives((uint)lineCount * 3, 1, 0, 0);

            commandBuffer.EndRenderPass(renderPass);
        }
        GraphicsDevice.Submit(commandBuffer);
    }

    protected override void Destroy()
    {
        _depthTexture.Dispose();
    }
}
