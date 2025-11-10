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

internal sealed class MovingObject : ICollisionBody
{
    public Vector2 Center;
    public Vector2 Velocity;
    public bool HasCollidedThisFrame;
    public float Radius;

    public Random Random = new Random();

    public Vector2 BodyOffset => Center;
    public IEnumerable<ICollisionBodyPart> BodyParts => [
    new CircleCollisionBodyPart(Vector2.Zero, Radius, 16),
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
    private readonly CollisionHandler.BodyStorageBuffer _storageBufferPlayerBody;
    private readonly CollisionHandler.BodyStorageBuffer _storageBufferStaticBodies;
    private readonly CollisionHandler.BodyStorageBuffer _storageBufferMovingBodies;
    private readonly CollisionHandler.RayCasterStorageBuffer _rayBuffer;
    private readonly CollisionHandler.HitResultStorageBuffer _storageBufferHitResult;
    private readonly CollisionHandler.ResolutionResultStorageBuffer _storageBufferPlayerResolutionResult;
    private readonly CollisionHandler.ResolutionResultStorageBuffer _storageBufferMovingResolutionResult;

    private readonly PlayerObject _playerObject;
    private readonly List<WallObject> _staticObjects;
    private readonly List<MovingObject> _movingObjects;

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

        _movingObjects = [];
        var random = new Random();
        for (int i = 0; i < 16; i++)
        {
            _movingObjects.Add(new MovingObject()
            {
                Center = new Vector2(random.NextSingle() * 300 + 200, random.NextSingle() * 250 + 100),
                Radius = random.NextSingle() * 8 + 8,
                Velocity = Vector2.Normalize(new Vector2(random.NextSingle() * 2 - 1, random.NextSingle() * 2 - 1)) * (random.NextSingle() * 24 + 8),
            });
        }

        _collisionHandler = new CollisionHandler(GraphicsDevice);
        _storageBufferPlayerBody = new CollisionHandler.BodyStorageBuffer(GraphicsDevice);
        _storageBufferStaticBodies = new CollisionHandler.BodyStorageBuffer(GraphicsDevice);
        _storageBufferMovingBodies = new CollisionHandler.BodyStorageBuffer(GraphicsDevice);
        _storageBufferHitResult = new CollisionHandler.HitResultStorageBuffer(GraphicsDevice);
        _storageBufferPlayerResolutionResult = new CollisionHandler.ResolutionResultStorageBuffer(GraphicsDevice);
        _storageBufferMovingResolutionResult = new CollisionHandler.ResolutionResultStorageBuffer(GraphicsDevice);
        _rayBuffer = new CollisionHandler.RayCasterStorageBuffer(GraphicsDevice, createDownloadBuffer: true);

        _storageBufferStaticBodies.UploadData(commandBuffer, _staticObjects);

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
            _playerObject.Center.X -= 32;
        if (Inputs.Keyboard.IsDown(KeyCode.D))
            _playerObject.Center.X += 32;
        if (Inputs.Keyboard.IsDown(KeyCode.W))
            _playerObject.Center.Y -= 32;
        if (Inputs.Keyboard.IsDown(KeyCode.S))
            _playerObject.Center.Y += 32;

        foreach (var movingObject in _movingObjects)
        {
            if (movingObject.Random.Next(16) == 0)
            {
                movingObject.Velocity = Vector2.Normalize(new Vector2(movingObject.Random.NextSingle() * 2 - 1, movingObject.Random.NextSingle() * 2 - 1)) * (movingObject.Random.NextSingle() * 24 + 8);
            }
            movingObject.Center += movingObject.Velocity;
        }

        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();

        _storageBufferPlayerBody.UploadData(commandBuffer, [_playerObject]);
        _storageBufferMovingBodies.UploadData(commandBuffer, _movingObjects);

        _storageBufferPlayerResolutionResult.ClearData(commandBuffer);
        _collisionHandler.ComputeCollisionResolutions(commandBuffer, _storageBufferPlayerBody, _storageBufferStaticBodies, _storageBufferPlayerResolutionResult);
        _storageBufferPlayerResolutionResult.DownloadData(commandBuffer);

        _storageBufferMovingResolutionResult.ClearData(commandBuffer);
        _collisionHandler.ComputeCollisionResolutions(commandBuffer, _storageBufferMovingBodies, _storageBufferStaticBodies, _storageBufferMovingResolutionResult);
        _storageBufferMovingResolutionResult.DownloadData(commandBuffer);

        var fence = GraphicsDevice.SubmitAndAcquireFence(commandBuffer);
        GraphicsDevice.WaitForFence(fence);
        GraphicsDevice.ReleaseFence(fence);

        commandBuffer = GraphicsDevice.AcquireCommandBuffer();

       

        foreach (var collisionResult in _storageBufferPlayerResolutionResult.GetData([_playerObject]))
        {
            ICollisionBody item = collisionResult.Item1;
            Vector2 pushVector = collisionResult.Item2;

            if (item is PlayerObject player)
            {
                player.Center += pushVector;
            }
        }

        foreach (var collisionResult in _storageBufferMovingResolutionResult.GetData(_movingObjects.Select(x => (ICollisionBody)x).ToList()))
        {
            ICollisionBody item = collisionResult.Item1;
            Vector2 pushVector = collisionResult.Item2;

            if (item is MovingObject moving)
            {
                moving.Center += pushVector;
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

            foreach (var objectCollider in _movingObjects)
            {
                _spriteBatch.Draw(
                    _circleSprite,
                    new Vector2(objectCollider.Radius),
                    new Rectangle(0, 0, (int)_circleSprite.Width, (int)_circleSprite.Height),
                    objectCollider.Center,
                    0f,
                    new Vector2(objectCollider.Radius * 2f),
                    Color.Blue,
                    0.3f
                );
            }

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
