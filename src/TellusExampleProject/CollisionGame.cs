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

internal sealed class PlayerObject : ICollisionBody, ICollisionRayCaster
{
    public Vector2 Center;
    public bool HasCollidedThisFrame;
    public float Radius;

    public Vector2 BodyOffset => Center;
    public IEnumerable<ICollisionBodyPart> BodyParts => [
    new CircleCollisionBodyPart(Vector2.Zero, Radius, 16),
    ];

    public readonly List<CollisionRay> SavedRays = [];

    public Vector2 RayOriginOffset => Center;
    public IEnumerable<CollisionRay> Rays => [
        new CollisionRay(Vector2.Zero, Vector2.UnitY, 200),
        new CollisionRay(Vector2.Zero, Vector2.UnitX, 200),
        new CollisionRay(Vector2.Zero, -Vector2.UnitY, 200),
        new CollisionRay(Vector2.Zero, -Vector2.UnitX, 200),
        new CollisionRay(Vector2.Zero, Vector2.Normalize((Vector2.UnitX + Vector2.UnitY)), 200),
        new CollisionRay(Vector2.Zero, Vector2.Normalize((Vector2.UnitX - Vector2.UnitY)), 200),
        new CollisionRay(Vector2.Zero, Vector2.Normalize((-Vector2.UnitX + Vector2.UnitY)), 200),
        new CollisionRay(Vector2.Zero, Vector2.Normalize((-Vector2.UnitX - Vector2.UnitY)), 200),
    ];
}

internal sealed class WallObject : ICollisionBody
{
    public Vector2 Center;

    public List<RectangleCollisionBodyPart> RectangleParts = [];
    public List<TriangleCollisionBodyPart> TriangleParts = [];

    public Vector2 BodyOffset => Center;

    public IEnumerable<ICollisionBodyPart> BodyParts => RectangleParts.Cast<ICollisionBodyPart>().Concat(TriangleParts.Cast<ICollisionBodyPart>());
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
    private readonly List<WallObject> _staticObjects;

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
            Radius = 16,
            Center = new Vector2(150, 150)
        };

        _staticObjects = [];

        var wallObject = new WallObject();
        wallObject.Center = new Vector2(200, 200);
        wallObject.RectangleParts.Add(new RectangleCollisionBodyPart(new Vector2(-150, 0), 0f, new Vector2(50, 350)));
        wallObject.RectangleParts.Add(new RectangleCollisionBodyPart(new Vector2(0, -150), 0f, new Vector2(350, 50)));
        wallObject.TriangleParts.Add(new TriangleCollisionBodyPart(new Vector2(-125, -125), new Vector2(-125, -125 + 50), new Vector2(-125 + 50, -125)));
        _staticObjects.Add(wallObject);

        wallObject = new WallObject();
        wallObject.Center = new Vector2(500, 150);
        wallObject.RectangleParts.Add(new RectangleCollisionBodyPart(new Vector2(0, -100), 0f, new Vector2(300, 50)));
        wallObject.RectangleParts.Add(new RectangleCollisionBodyPart(new Vector2(125, -50), 0f, new Vector2(50, 50)));
        wallObject.RectangleParts.Add(new RectangleCollisionBodyPart(new Vector2(150, 75), 0f, new Vector2(50, 200)));
        wallObject.TriangleParts.Add(new TriangleCollisionBodyPart(new Vector2(0, -76), new Vector2(100, -76), new Vector2(100, -26)));
        wallObject.TriangleParts.Add(new TriangleCollisionBodyPart(new Vector2(100, -26), new Vector2(125, -26), new Vector2(125, 48)));
        _staticObjects.Add(wallObject);

        wallObject = new WallObject();
        wallObject.Center = new Vector2(300, 400);
        wallObject.RectangleParts.Add(new RectangleCollisionBodyPart(new Vector2(150, 0), -MathF.PI * 0.125f, new Vector2(500, 50)));
        wallObject.RectangleParts.Add(new RectangleCollisionBodyPart(new Vector2(-150, 0), MathF.PI * 0.25f, new Vector2(500, 75)));
        _staticObjects.Add(wallObject);

        /*var random = new Random();
        for (int i = 0; i < 80; i++)
        {
            Vector2 center = new Vector2(random.NextSingle() * 900, random.NextSingle() * 900);
            int shapeType = random.Next(3);
            switch (shapeType)
            {
                case 0:
                    _staticObjects.Add(new CircleCollidingObject()
                    {
                        Radius = random.NextSingle() * 24 + 8,
                        Center = center,
                    });
                    break;
                case 1:
                    _staticObjects.Add(new RectangleCollidingObject()
                    {
                        Center = center,
                        Angle = random.NextSingle() * MathF.Tau,
                        SideLengths = new Vector2(random.NextSingle() * 56 + 8, random.NextSingle() * 56 + 8),
                    });
                    break;
                case 2:
                    _staticObjects.Add(new TriangleCollidingObject()
                    {
                        PointOne = center,
                        PointTwo = center + new Vector2(random.NextSingle() * 24 + 8, 0),
                        PointThree = center + new Vector2(random.NextSingle() * 24 + 8, random.NextSingle() * 24 + 8)
                    });
                    break;
            }
        }*/

        _collisionHandler = new CollisionHandler(GraphicsDevice);
        _storageBufferBodiesOne = new CollisionHandler.BodyStorageBuffer(GraphicsDevice);
        _storageBufferBodiesTwo = new CollisionHandler.BodyStorageBuffer(GraphicsDevice);
        _storageBufferHitResult = new CollisionHandler.HitResultStorageBuffer(GraphicsDevice);
        _storageBufferResolutionResult = new CollisionHandler.ResolutionResultStorageBuffer(GraphicsDevice);
        _rayBuffer = new CollisionHandler.RayCasterStorageBuffer(GraphicsDevice, createDownloadBuffer: true);

        _storageBufferBodiesTwo.UploadData(commandBuffer, _staticObjects.Select(item => (ICollisionBody)item));

        #endregion

        #region Triangle rendering stuff
        _triangleVertexBuffer = Buffer.Create<PositionColorVertex>
        (
            GraphicsDevice,
            BufferUsageFlags.Vertex,
            1024
        );

        TransferBuffer vertexBuffer = TransferBuffer.Create<PositionColorVertex>
        (
            GraphicsDevice,
            TransferBufferUsage.Upload,
            1024
        );

        var vertexSpan = vertexBuffer.Map<PositionColorVertex>(false);
        foreach (var colliderObject in _staticObjects)
        {
            foreach (var triangle in colliderObject.TriangleParts)
            {
                vertexSpan[_triangleCount * 3].Position = new Vector4(triangle.PointOne + colliderObject.Center, 0f, 1f);
                vertexSpan[_triangleCount * 3 + 1].Position = new Vector4(triangle.PointTwo + colliderObject.Center, 0f, 1f);
                vertexSpan[_triangleCount * 3 + 2].Position = new Vector4(triangle.PointThree + colliderObject.Center, 0f, 1f);

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

        var hitResults = _storageBufferHitResult.GetData([_playerObject], _staticObjects.Select(item => (ICollisionBody)item).ToList());

        foreach (var collisionResult in hitResults)
        {
            ICollisionBody item1 = collisionResult.Item1;
            ICollisionBody item2 = collisionResult.Item2;

            if (item1 is PlayerObject player)
            {
                player.HasCollidedThisFrame = true;
            }
        }

        var resolutionResults = _storageBufferResolutionResult.GetData([_playerObject]);
        foreach (var collisionResult in resolutionResults)
        {
            ICollisionBody item = collisionResult.Item1;
            Vector2 pushVector = collisionResult.Item2;

            if (item is PlayerObject player)
            {
                player.Center += pushVector;
            }
        }

        /*
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
        */
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

            
            foreach (var objectCollider in _staticObjects)
            {
                _spriteBatch.Draw(
                    _circleSprite,
                    Vector2.One * 4,
                    new Rectangle(0, 0, (int)_circleSprite.Width, (int)_circleSprite.Height),
                    objectCollider.Center,
                    0,
                    Vector2.One * 8,
                    Color.Black * 0.5f,
                    0.5f
                );
                foreach (var rectangle in objectCollider.RectangleParts)
                {
                    _spriteBatch.Draw(
                        _squareSprite,
                        rectangle.SideLengths * 0.5f,
                        new Rectangle(0, 0, (int)_circleSprite.Width, (int)_circleSprite.Height),
                        rectangle.Center + objectCollider.Center,
                        rectangle.Angle,
                        rectangle.SideLengths,
                        Color.White,
                        0.6f
                    );
                }
                /*
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
                }*/
            }
            

            _spriteBatch.End(commandBuffer, renderPass, swapchainTexture, swapchainTexture.Format, TextureFormat.D16Unorm);

            commandBuffer.PushVertexUniformData(cameraMatrix);

            renderPass.BindGraphicsPipeline(_trianglePipeline);
            renderPass.BindVertexBuffers(_triangleVertexBuffer);
            renderPass.DrawPrimitives((uint)_triangleCount * 3, 1, 0, 0);

            /*
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
            */

            commandBuffer.EndRenderPass(renderPass);
        }
        GraphicsDevice.Submit(commandBuffer);
    }

    protected override void Destroy()
    {
        _depthTexture.Dispose();
    }
}
