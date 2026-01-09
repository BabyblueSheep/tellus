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
using System.Linq;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Tellus.Collision;
using Tellus.Collision.Individual;
using Tellus.Graphics;
using Tellus.Graphics.SpriteBatch;
using Tellus.Math;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = MoonWorks.Graphics.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace TellusExampleProject;

internal sealed class PlayerObject
{
    public Vector2 Center;
    public Vector2 Velocity;
    public bool HasCollidedThisFrame;
    public float Radius;

    public Vector2 LaserEnd;

    public required CollisionBody CollisionBody;

    /*
    public readonly List<CollisionLine> SavedLines = [];

    public Vector2 OriginOffset => Center;
    public List<CollisionLine> Lines => [
        CollisionLine.CreateFiniteLengthRay(Vector2.Zero, Velocity.SafeNormalize(Vector2.Zero), Velocity.Length()) with { CanBeRestricted = true },
        CollisionLine.CreateFixedPointLineSegment(Vector2.Zero, LaserEnd, (LaserEnd - Center).Length()),
    ];
    public int LineVelocityIndex => 0;
    */
}

internal sealed class WallObject
{
    public Vector2 Center;

    public List<Tellus.Math.Shapes.Rectangle> Rectangles;
    public List<Tellus.Math.Shapes.Triangle> Triangles;

    public CollisionBody CollisionBody;

    public WallObject(List<Tellus.Math.Shapes.Rectangle> rectangles, List<Tellus.Math.Shapes.Triangle> triangles)
    {
        Rectangles = rectangles;
        Triangles = triangles;

        CollisionBody = new CollisionBody();
        foreach (var rectangle in rectangles)
        {
            CollisionBody.Add(new CollisionPolygon(rectangle));
        }
        foreach (var triangle in triangles)
        {
            CollisionBody.Add(new CollisionPolygon(triangle));
        }
    }
}

internal sealed class MovingObject
{
    public Vector2 Center;
    public Vector2 ActualVelocity;

    public bool HasCollidedThisFrame;
    public float Radius;

    public Random Random = new Random();

    public required CollisionBody CollisionBody;

    /*
    public Vector2 BodyOffset => Center;
    public IEnumerable<CollisionBodyPart> BodyParts => [
        CollisionBodyPart.CreateCircle(Vector2.Zero, Radius, 16),
    ];
    public float BroadRadius { get; set; }

    public Vector2 OriginOffset => Center;
    public List<CollisionLine> Lines => [
        CollisionLine.CreateFiniteLengthRay(Vector2.Zero, ActualVelocity.SafeNormalize(Vector2.Zero), ActualVelocity.Length()) with { CanBeRestricted = true },
    ];
    public int LineVelocityIndex => 0;*/
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
    private float _timer;

    private readonly PlayerObject _playerObject;
    private readonly List<WallObject> _staticObjects;
    private readonly List<MovingObject> _movingObjects;

    private readonly Texture _circleSprite;
    private readonly Texture _squareSprite;
    private Texture _depthTexture;
    private readonly SpriteBatch.SpriteInstanceContainer _spriteOperationContainer;
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
        _spriteBatch = new SpriteBatch
        (
            GraphicsDevice,
            ColorTargetBlendState.PremultipliedAlphaBlend,
            SamplerCreateInfo.PointWrap,
            new DepthStencilState()
            {
                EnableDepthTest = true,
                EnableDepthWrite = true,
                CompareOp = CompareOp.LessOrEqual,
            },
            RasterizerState.CCW_CullNone,
            null, null,
            MainWindow.SwapchainFormat, TextureFormat.D16Unorm
        );
        _spriteOperationContainer = new SpriteBatch.SpriteInstanceContainer(GraphicsDevice);

        _depthTexture = Texture.Create2D(GraphicsDevice, "Depth Texture", 1, 1, TextureFormat.D16Unorm, TextureUsageFlags.DepthStencilTarget);
        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();

        #region Images
        var resourceUploader = new ResourceUploader(GraphicsDevice);

        _circleSprite = resourceUploader.CreateTexture2DFromCompressed(
            RootTitleStorage,
            "resources/image_circle.png",
            TextureFormat.R8G8B8A8Unorm,
            TextureUsageFlags.Sampler
        );

        _squareSprite = resourceUploader.CreateTexture2DFromCompressed(
            RootTitleStorage,
            "resources/image2.png",
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
            CollisionBody = new CollisionBody(new CollisionPolygon(new Tellus.Math.Shapes.Circle(16f)))
        };

        _staticObjects = [];

        var wallObject = new WallObject(
        [
            new Tellus.Math.Shapes.Rectangle(new Vector2(0, 0), 100f, 150f, 0.0),
            new Tellus.Math.Shapes.Rectangle(new Vector2(50, 100), 50f, 100f, Math.PI * 0.25)
        ], 
        [])
        {
            Center = new Vector2(300, 200),
        };
        _staticObjects.Add(wallObject);

        wallObject = new WallObject(
        [
            new Tellus.Math.Shapes.Rectangle(new Vector2(-150, 0), 50f, 350f, 0.0),
            new Tellus.Math.Shapes.Rectangle(new Vector2(50, 100), 50f, 100f, Math.PI * 0.25)
        ],
        [
            new Tellus.Math.Shapes.Triangle(new Vector2(-125, -125), new Vector2(-125, -125 + 50), new Vector2(-125 + 50, -125))
        ])
        {
            Center = new Vector2(200, 200),
        };
        _staticObjects.Add(wallObject);

        wallObject = new WallObject
        {
            Center = new Vector2(500, 150),
            CollisionBody =
            [
                new CollisionPolygon(new Tellus.Math.Shapes.Rectangle(new Vector2(0, -100), 300f, 50f, 0.0)),
                new CollisionPolygon(new Tellus.Math.Shapes.Rectangle(new Vector2(125, -50), 50f, 50f, 0.0)),
                new CollisionPolygon(new Tellus.Math.Shapes.Rectangle(new Vector2(150, 75), 50f, 2000f, 0.0)),
                new CollisionPolygon(new Tellus.Math.Shapes.Triangle(new Vector2(0, -76), new Vector2(100, -76), new Vector2(100, -26))),
                new CollisionPolygon(new Tellus.Math.Shapes.Triangle(new Vector2(100, -26), new Vector2(125, -26), new Vector2(125, 48))),
            ]
        };
        _staticObjects.Add(wallObject);

        wallObject = new WallObject
        {
            Center = new Vector2(300, 400),
            CollisionBody =
            [
                new CollisionPolygon(new Tellus.Math.Shapes.Rectangle(new Vector2(150, 0), 500f, 50f, -Math.PI * 0.125)),
                new CollisionPolygon(new Tellus.Math.Shapes.Rectangle(new Vector2(-150, 0), 500f, 75f, Math.PI * 0.25)),
            ]
        };
        _staticObjects.Add(wallObject);

        _movingObjects = [];
        var random = new Random();
        for (int i = 0; i < 256; i++)
        {
            var radius = random.NextSingle() * 8 + 8;
            var movingObject = new MovingObject()
            {
                Center = new Vector2(random.NextSingle() * 300 + 200, random.NextSingle() * 250 + 100),
                Radius = radius,
                ActualVelocity = Vector2.Normalize(new Vector2(random.NextSingle() * 2 - 1, random.NextSingle() * 2 - 1)) * 8,
                CollisionBody = new CollisionBody(new CollisionPolygon(new Tellus.Math.Shapes.Circle(radius)))
            };
            _movingObjects.Add(movingObject);
        }

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
            /*
            foreach (var triangle in colliderObject.Parts)
            {
                if (triangle.ShapeType == CollisionBodyPartShapeType.Triangle)
                {
                    vertexSpan[_triangleCount * 3].Position = new Vector4(triangle.BodyPartCenter + colliderObject.Center, 0f, 1f);
                    vertexSpan[_triangleCount * 3 + 1].Position = new Vector4(new Vector2(triangle.DecimalFields.X, triangle.DecimalFields.Y) + colliderObject.Center, 0f, 1f);
                    vertexSpan[_triangleCount * 3 + 2].Position = new Vector4(new Vector2(triangle.DecimalFields.Z, triangle.DecimalFields.W) + colliderObject.Center, 0f, 1f);

                    vertexSpan[_triangleCount * 3].Color = new Vector4(1f, 1f, 1f, 1f);
                    vertexSpan[_triangleCount * 3 + 1].Color = new Vector4(1f, 1f, 1f, 1f);
                    vertexSpan[_triangleCount * 3 + 2].Color = new Vector4(1f, 1f, 1f, 1f);

                    _triangleCount++;
                }
            }
            */
        }
        vertexBuffer.Unmap();

        var copyPass = commandBuffer.BeginCopyPass();
        copyPass.UploadToBuffer(vertexBuffer, _triangleVertexBuffer, false);
        commandBuffer.EndCopyPass(copyPass);

        vertexBuffer.Dispose();

        Shader vertexShader = ShaderCross.Create(
            GraphicsDevice,
            RootTitleStorage,
            "resources/Position.vert.hlsl",
            "main",
            ShaderCross.ShaderFormat.HLSL,
            ShaderStage.Vertex
        );
        Shader fragmentShader = ShaderCross.Create(
            GraphicsDevice,
            RootTitleStorage,
            "resources/SolidColor.frag.hlsl",
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

        //GraphicsDevice.SetSwapchainParameters(MainWindow, SwapchainComposition.SDR, PresentMode.Immediate);
    }

    protected override void Step()
    {
        
    }

    protected override void Update(TimeSpan delta)
    {
        _timer += 0.01f;

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
                movingObject.ActualVelocity = Vector2.Normalize(new Vector2(movingObject.Random.NextSingle() * 2 - 1, movingObject.Random.NextSingle() * 2 - 1)) * (movingObject.Random.NextSingle() * 8 + 0.1f);
            }
        }

        /*
        for (int i = 0; i < _movingObjects.Count; i++)
        {
            var movingObject = _movingObjects[i];

            var newLengths = IndividualCollisionHandler.RestrictLines(movingObject, _staticObjects);
            var velocity = movingObject.Lines[movingObject.LineVelocityIndex];
            var newVelocityLength = newLengths[movingObject.LineVelocityIndex];
            movingObject.Center += velocity.ArbitraryVector * newVelocityLength;

            movingObject.Center += IndividualCollisionHandler.ResolveBodyBodyCollisions(movingObject, _staticObjects);
        }

        var playerObject = _playerObject;

        var newLengthsPlayer = IndividualCollisionHandler.RestrictLines(playerObject, _staticObjects);
        var velocityPlayer = playerObject.Lines[playerObject.LineVelocityIndex];
        var newVelocityLengthPlayer = newLengthsPlayer[playerObject.LineVelocityIndex];
        playerObject.Center += velocityPlayer.ArbitraryVector * newVelocityLengthPlayer;

        playerObject.SavedLines.Clear();
        for (int i = 0; i < newLengthsPlayer.Count; i++)
        {
            playerObject.SavedLines.Add(playerObject.Lines[i] with { Length = newLengthsPlayer[i] });
        }

        playerObject.Center += IndividualCollisionHandler.ResolveBodyBodyCollisions(playerObject, _staticObjects);

        for (int i = 0; i < _movingObjects.Count; i++)
        {
            var movingObject = _movingObjects[i];

            if (IndividualCollisionHandler.ComputeLineBodyHits(movingObject, playerObject))
            {
                movingObject.HasCollidedThisFrame = true;
            }
        }*/

        /*
        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();

        var collisionBodies = _movingObjects.Cast<ICollisionBody>().Append(_playerObject).ToList();
        var collisionRayCasters = _movingObjects.Cast<ICollisionLineCollection>().Append(_playerObject).ToList();

        _storageBufferMovingBodies.UploadData
        (
            commandBuffer, 
            [(nameof(_movingObjects), _movingObjects), (nameof(_playerObject), [_playerObject])]
        );
        _lineBuffer.UploadData
        (
            commandBuffer,
            [(nameof(_movingObjects), _movingObjects), (nameof(_playerObject), [_playerObject])]
        );
        _pairBuffer.UploadData
        (
            commandBuffer,
            [(nameof(_movingObjects), collisionBodies, collisionRayCasters)]
        );

        BatchCollisionHandler.RestrictLines
        (
            commandBuffer,
            _storageBufferStaticBodies, _storageBufferStaticBodies.GetBodySegmentRange(null),
            _lineBuffer, _lineBuffer.GetLineCollectionRange(null)
        );
        BatchCollisionHandler.IncrementLineCollectionBodiesOffsets
        (
            commandBuffer,
            _storageBufferMovingBodies,
            _lineBuffer,
            _pairBuffer, _pairBuffer.GetPairRange(null)
        );
        _lineBuffer.DownloadData(commandBuffer);

        
        _resolutionResultBuffer.ClearData(commandBuffer);
        BatchCollisionHandler.ResolveBodyBodyCollisions
        (
            commandBuffer,
            _storageBufferMovingBodies, _storageBufferMovingBodies.GetBodySegmentRange(null),
            _storageBufferStaticBodies, _storageBufferStaticBodies.GetBodySegmentRange(null),
            _resolutionResultBuffer
        );
        _resolutionResultBuffer.DownloadData(commandBuffer);

        _lineHitResultBuffer.ClearData(commandBuffer);
        BatchCollisionHandler.ComputeLineBodyHits
        (
            commandBuffer,
            _storageBufferMovingBodies, _storageBufferMovingBodies.GetBodySegmentRange(nameof(_movingObjects)),
            _lineBuffer, _lineBuffer.GetLineCollectionRange(nameof(_playerObject)),
            _lineHitResultBuffer
        );
        _lineHitResultBuffer.DownloadData(commandBuffer);

        var fence = GraphicsDevice.SubmitAndAcquireFence(commandBuffer);
        GraphicsDevice.WaitForFence(fence);
        GraphicsDevice.ReleaseFence(fence);

        foreach (var rayRestrictionResult in _lineBuffer.GetData(collisionRayCasters))
        {
            ICollisionLineCollection item = rayRestrictionResult.Item1;
            IList<CollisionLine> rays = rayRestrictionResult.Item2;

            if (item is PlayerObject player)
            {
                CollisionLine velocity = rays[item.LineVelocityIndex];
                player.Center += velocity.ArbitraryVector * velocity.Length;

                player.SavedLines.Clear();
                
                foreach (var ray in rays)
                {
                    player.SavedLines.Add(ray);
                }
            }
            else if (item is MovingObject moving)
            {
                CollisionLine velocity = rays[item.LineVelocityIndex];
                moving.Center += velocity.ArbitraryVector * velocity.Length;
            }
        }

        var t = _resolutionResultBuffer.GetData(collisionBodies);
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

        foreach (var hitResult in _lineHitResultBuffer.GetData(_movingObjects.Cast<ICollisionBody>().ToList(), [(ICollisionLineCollection)_playerObject]))
        {
            ICollisionBody body = hitResult.Item1;
            ICollisionLineCollection rayCaster = hitResult.Item2;

            if (rayCaster is PlayerObject player && body is MovingObject moving)
            {
                moving.HasCollidedThisFrame = true;
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

            _spriteOperationContainer.ClearSprites();

            _spriteOperationContainer.PushSprite(
                _circleSprite,
                null,
                new SpriteBatch.SpriteParameters() with
                {
                    TransformationMatrix =
                        PlanarMatrix4x4.CreateScaleCentered(_playerObject.Radius * 2) *
                        PlanarMatrix4x4.CreateTranslation(_playerObject.Center),
                    TintColor = Color.Red,
                    OffsetColor = Color.Lerp(Color.Lime with { A = 0 }, Color.Lime, MathF.Abs(MathF.Sin(_timer))),
                    Depth = 0.5f
                }
            );

            
            foreach (var objectCollider in _movingObjects)
            {
                _spriteOperationContainer.PushSprite(
                    _circleSprite,
                    null,
                    new SpriteBatch.SpriteParameters() with
                    {
                        TransformationMatrix =
                            PlanarMatrix4x4.CreateScaleCentered(objectCollider.Radius * 2) *
                            PlanarMatrix4x4.CreateTranslation(objectCollider.Center),
                        TintColor = objectCollider.HasCollidedThisFrame ? Color.Magenta : Color.Blue,
                        Depth = 0.3f
                    }
                );
            }

            foreach (var objectCollider in _staticObjects)
            {
                _spriteOperationContainer.PushSprite(
                    _circleSprite,
                    null,
                    new SpriteBatch.SpriteParameters() with
                    {
                        TransformationMatrix =
                            PlanarMatrix4x4.CreateScaleCentered(8) *
                            PlanarMatrix4x4.CreateTranslation(objectCollider.Center),
                        TintColor = Color.Black * 0.5f,
                        Depth = 0.5f
                    }
                );

                foreach (var rectangle in objectCollider.Parts)
                {
                    if (rectangle.ShapeType == CollisionBodyPartShapeType.Rectangle)
                    {
                        var scale = new Vector2(rectangle.DecimalFields.X, rectangle.DecimalFields.Y);

                        _spriteOperationContainer.PushSprite(
                            _squareSprite,
                            null,
                            new SpriteBatch.SpriteParameters() with
                            {
                                TransformationMatrix =
                                    PlanarMatrix4x4.CreateScaleCentered(scale) *
                                    PlanarMatrix4x4.CreateRotationCentered(rectangle.DecimalFields.Z) *
                                    PlanarMatrix4x4.CreateTranslation(rectangle.BodyPartCenter + objectCollider.Center),
                                TintColor = Color.White,
                                Depth = 0.5f
                            }
                        );
                    }
                }
            }

            _spriteOperationContainer.SortSprites(SpriteBatch.SpriteSortMode.BackToFront);
            _spriteOperationContainer.CreateVertexInfo(commandBuffer);
            _spriteBatch.DrawBatch(commandBuffer, renderPass, swapchainTexture, _spriteOperationContainer, null);
            
            commandBuffer.PushVertexUniformData(cameraMatrix);

            renderPass.BindGraphicsPipeline(_trianglePipeline);
            renderPass.BindVertexBuffers(_triangleVertexBuffer);
            renderPass.DrawPrimitives((uint)_triangleCount * 3, 1, 0, 0);

            
            int lineCount = 0;
            var lineVertexSpan = _lineVertexTransferBuffer.Map<PositionColorVertex>(false);
            foreach (var line in _playerObject.SavedLines)
            {
                lineVertexSpan[lineCount * 2].Position = new Vector4(line.Origin + _playerObject.OriginOffset, 0f, 1f);
                if (line.IsVectorFixedPoint)
                {
                    lineVertexSpan[lineCount * 2 + 1].Position = new Vector4(line.ArbitraryVector, 0f, 1f);
                }
                else
                {
                    lineVertexSpan[lineCount * 2 + 1].Position = new Vector4(line.Origin + line.ArbitraryVector * line.Length + _playerObject.OriginOffset, 0f, 1f);
                }

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
        BatchCollisionHandler.Dispose();
        _spriteOperationContainer.Dispose();
        _spriteBatch.Dispose();
        _depthTexture.Dispose();
    }
}
