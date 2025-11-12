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
using Tellus;
using Tellus.Collision;
using Tellus.Graphics;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = MoonWorks.Graphics.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace TellusExampleProject;

internal sealed class PlayerObject : ICollisionBody, ICollisionRayCaster
{
    public Vector2 Center;
    public Vector2 Velocity;
    public bool HasCollidedThisFrame;
    public float Radius;

    public Vector2 LaserEnd;

    public Vector2 BodyOffset => Center;
    public IEnumerable<ICollisionBodyPart> BodyParts => [
        new CircleCollisionBodyPart(Vector2.Zero, Radius, 16),
    ];

    public readonly List<CollisionRay> SavedRays = [];

    public Vector2 RayOriginOffset => Center;
    public IEnumerable<CollisionRay> Rays => [
        new CollisionRay(Vector2.Zero, Velocity.SafeNormalize(Vector2.Zero), Velocity.Length()),
        new CollisionRay(Vector2.Zero, (LaserEnd - Center).SafeNormalize(Vector2.Zero), (LaserEnd - Center).Length()) with { CanBeRestricted = false },
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

internal sealed class MovingObject : ICollisionBody, ICollisionRayCaster
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

    public Vector2 RayOriginOffset => Center;
    public IEnumerable<CollisionRay> Rays => [
        new CollisionRay(Vector2.Zero, Velocity.SafeNormalize(Vector2.Zero), Velocity.Length()),
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
    private readonly CollisionHandler.BodyBufferStorage _storageBufferStaticBodies;
    private readonly CollisionHandler.BodyBufferStorage _storageBufferMovingBodies;
    private readonly CollisionHandler.RayCasterBufferStorage _rayBuffer;
    private readonly CollisionHandler.HitResultBufferStorage _rayHitResultBuffer;
    private readonly CollisionHandler.ResolutionResultBufferStorage _resolutionResultBuffer;
    private readonly CollisionHandler.BodyRayCasterPairBufferStorage _pairBuffer;

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
            Center = new Vector2(150, 150),
            Velocity = Vector2.Zero,
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
                Velocity = Vector2.Normalize(new Vector2(random.NextSingle() * 2 - 1, random.NextSingle() * 2 - 1)) * 8,
            });
        }

        _collisionHandler = new CollisionHandler(GraphicsDevice);
        _storageBufferStaticBodies = new CollisionHandler.BodyBufferStorage(GraphicsDevice);
        _storageBufferMovingBodies = new CollisionHandler.BodyBufferStorage(GraphicsDevice);
        _rayHitResultBuffer = new CollisionHandler.HitResultBufferStorage(GraphicsDevice);
        _resolutionResultBuffer = new CollisionHandler.ResolutionResultBufferStorage(GraphicsDevice);
        _rayBuffer = new CollisionHandler.RayCasterBufferStorage(GraphicsDevice, createDownloadBuffer: true);
        _pairBuffer = new CollisionHandler.BodyRayCasterPairBufferStorage(GraphicsDevice);

        _storageBufferStaticBodies.UploadData
        (
            commandBuffer,
            [(nameof(_staticObjects), _staticObjects)]
        );

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

        _playerObject.Velocity = Vector2.Zero;
        if (Inputs.Keyboard.IsDown(KeyCode.A))
            _playerObject.Velocity.X -= 8;
        if (Inputs.Keyboard.IsDown(KeyCode.D))
            _playerObject.Velocity.X += 8;
        if (Inputs.Keyboard.IsDown(KeyCode.W))
            _playerObject.Velocity.Y -= 8;
        if (Inputs.Keyboard.IsDown(KeyCode.S))
            _playerObject.Velocity.Y += 8;

        _playerObject.LaserEnd = new Vector2(Inputs.Mouse.X, Inputs.Mouse.Y);

        foreach (var movingObject in _movingObjects)
        {
            movingObject.HasCollidedThisFrame = false;
            if (movingObject.Random.Next(16) == 0)
            {
                movingObject.Velocity = Vector2.Normalize(new Vector2(movingObject.Random.NextSingle() * 2 - 1, movingObject.Random.NextSingle() * 2 - 1)) * 8;
            }
        }

        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();

        var collisionBodies = _movingObjects.Cast<ICollisionBody>().Append(_playerObject).ToList();
        var collisionRayCasters = _movingObjects.Cast<ICollisionRayCaster>().Append(_playerObject).ToList();

        _storageBufferMovingBodies.UploadData
        (
            commandBuffer, 
            [(nameof(_movingObjects), _movingObjects), (nameof(_playerObject), [_playerObject])]
        );
        _rayBuffer.UploadData
        (
            commandBuffer,
            [(nameof(_movingObjects), _movingObjects), (nameof(_playerObject), [_playerObject])]
        );
        _pairBuffer.UploadData
        (
            commandBuffer,
            [(nameof(_movingObjects), collisionBodies, collisionRayCasters)]
        );

        _collisionHandler.RestrictRays
        (
            commandBuffer,
            _storageBufferStaticBodies, _storageBufferStaticBodies.GetBodyRange(null),
            _rayBuffer, _rayBuffer.GetRayCasterRange(null)
        );
        _collisionHandler.IncrementRayCasterBodiesOffsets
        (
            commandBuffer,
            _storageBufferMovingBodies,
            _rayBuffer,
            _pairBuffer, _pairBuffer.GetPairRange(null)
        );
        _rayBuffer.DownloadData(commandBuffer);

        _resolutionResultBuffer.ClearData(commandBuffer);
        _collisionHandler.ResolveBodyBodyCollisions
        (
            commandBuffer,
            _storageBufferMovingBodies, _storageBufferMovingBodies.GetBodyRange(null),
            _storageBufferStaticBodies, _storageBufferStaticBodies.GetBodyRange(null),
            _resolutionResultBuffer
        );
        _resolutionResultBuffer.DownloadData(commandBuffer);

        _rayHitResultBuffer.ClearData(commandBuffer);
        _collisionHandler.ComputeRayBodyHits
        (
            commandBuffer,
            _storageBufferMovingBodies, _storageBufferMovingBodies.GetBodyRange(nameof(_movingObjects)),
            _rayBuffer, _rayBuffer.GetRayCasterRange(nameof(_playerObject)),
            _rayHitResultBuffer
        );
        _rayHitResultBuffer.DownloadData(commandBuffer);

        var fence = GraphicsDevice.SubmitAndAcquireFence(commandBuffer);
        GraphicsDevice.WaitForFence(fence);
        GraphicsDevice.ReleaseFence(fence);

        foreach (var rayRestrictionResult in _rayBuffer.GetData(collisionRayCasters))
        {
            ICollisionRayCaster item = rayRestrictionResult.Item1;
            IList<CollisionRay> rays = rayRestrictionResult.Item2;

            if (item is PlayerObject player)
            {
                CollisionRay velocity = rays[item.RayVelocityIndex];
                player.Center += velocity.RayDirection * velocity.RayLength;

                player.SavedRays.Clear();
                
                foreach (var ray in rays)
                {
                    player.SavedRays.Add(ray);
                }
            }
            else if (item is MovingObject moving)
            {
                CollisionRay velocity = rays[item.RayVelocityIndex];
                moving.Center += velocity.RayDirection * velocity.RayLength;
            }
        }

        foreach (var collisionResult in _resolutionResultBuffer.GetData(collisionBodies))
        {
            ICollisionBody item = collisionResult.Item1;
            Vector2 pushVector = collisionResult.Item2;

            if (item is PlayerObject player)
            {
                player.Center += pushVector;
            }
            else if (item is MovingObject moving)
            {
                moving.Center += pushVector;
            }
        }

        foreach (var hitResult in _rayHitResultBuffer.GetData(_movingObjects.Cast<ICollisionBody>().ToList(), [(ICollisionRayCaster)_playerObject]))
        {
            ICollisionBody body = hitResult.Item1;
            ICollisionRayCaster rayCaster = hitResult.Item2;

            if (rayCaster is PlayerObject player && body is MovingObject moving)
            {
                moving.HasCollidedThisFrame = true;
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

            foreach (var objectCollider in _movingObjects)
            {
                _spriteBatch.Draw(
                    _circleSprite,
                    new Vector2(objectCollider.Radius),
                    new Rectangle(0, 0, (int)_circleSprite.Width, (int)_circleSprite.Height),
                    objectCollider.Center,
                    0f,
                    new Vector2(objectCollider.Radius * 2f),
                    objectCollider.HasCollidedThisFrame ? Color.Magenta : Color.Blue,
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
