using MoonWorks.Graphics;
using MoonWorks.Storage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = MoonWorks.Graphics.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace Tellus.Graphics.SpriteBatch;

public sealed partial class SpriteBatch : GraphicsResource
{
    private struct DrawOperation
    {
        public int TextureIndex;
        public Vector2 TextureOrigin;
        public Rectangle TextureSourceRectangle;
        public Vector2 Position;
        public float Rotation;
        public Vector2 Scale;
        public Color Color;
        public float Depth;
    }

    public sealed class SpriteOperationContainer : GraphicsResource
    {
        private readonly ComputePipeline _computePipeline;

        private readonly int _maximumSpriteAmount;
        internal uint SpriteAmount { get; private set; }

        internal Texture? TextureToSample { get; private set; }

        private readonly TransferBuffer _instanceTransferBuffer;
        private readonly Buffer _spriteInstanceBuffer;
        internal Buffer VertexBuffer { get; }
        internal Buffer IndexBuffer { get; }

        private readonly List<Texture> _drawOperationTextures;
        private readonly Dictionary<Texture, int> _drawOperationTextureIndices;
        private int _drawOperationHighestTextureIndex;
        private List<DrawOperation> _drawOperations;

        public SpriteOperationContainer(GraphicsDevice device, uint maxSpriteAmount = 2048) : base(device)
        {
            Utils.LoadShaderFromManifest(device, "SpriteBatch.comp", new ComputePipelineCreateInfo()
            {
                NumReadonlyStorageBuffers = 1,
                NumReadWriteStorageBuffers = 1,
                NumUniformBuffers = 1,
                ThreadCountX = 64,
                ThreadCountY = 1,
                ThreadCountZ = 1
            }, out _computePipeline);

            _maximumSpriteAmount = (int)maxSpriteAmount;

            uint maxVertexAmount = maxSpriteAmount * 4;
            uint maxIndexAmount = maxSpriteAmount * 6;

            _instanceTransferBuffer = TransferBuffer.Create<SpriteInstanceData>
            (
                device,
                TransferBufferUsage.Upload,
                maxSpriteAmount
            );

            _spriteInstanceBuffer = Buffer.Create<SpriteInstanceData>
            (
                device,
                BufferUsageFlags.ComputeStorageRead,
                maxSpriteAmount
            );

            VertexBuffer = Buffer.Create<PositionTextureColorVertex>
            (
                device,
                BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.Vertex,
                maxVertexAmount
            );

            IndexBuffer = Buffer.Create<uint>
            (
                device,
                BufferUsageFlags.Index,
                maxIndexAmount
            );

            TransferBuffer indexTransferBuffer = TransferBuffer.Create<uint>(
                device,
                TransferBufferUsage.Upload,
                maxIndexAmount
            );

            var indexSpan = indexTransferBuffer.Map<uint>(false);
            for (int i = 0, j = 0; i < maxIndexAmount; i += 6, j += 4)
            {
                indexSpan[i] = (uint)j;
                indexSpan[i + 1] = (uint)j + 1;
                indexSpan[i + 2] = (uint)j + 2;
                indexSpan[i + 3] = (uint)j + 3;
                indexSpan[i + 4] = (uint)j + 2;
                indexSpan[i + 5] = (uint)j + 1;
            }
            indexTransferBuffer.Unmap();

            var commandBuffer = device.AcquireCommandBuffer();
            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.UploadToBuffer(indexTransferBuffer, IndexBuffer, false);
            commandBuffer.EndCopyPass(copyPass);
            device.Submit(commandBuffer);

            indexTransferBuffer.Dispose();

            _drawOperationTextures = [];
            _drawOperations = [];
        }

        public void ClearSprites()
        {
            _drawOperations.Clear();
            _drawOperationTextures.Clear();
            _drawOperationTextureIndices.Clear();
            _drawOperationHighestTextureIndex = 0;
        }

        public void PushSprite
        (
            Texture texture,
            Vector2 textureOrigin,
            Rectangle textureSourceRectangle,
            Vector2 position,
            float rotation,
            Vector2 scale,
            Color color,
            float depth
        )
        {
            int textureIndex;
            if (_drawOperationTextureIndices.TryGetValue(texture, out int value))
            {
                textureIndex = value;
            }
            else
            {
                _drawOperationTextures.Add(texture);
                _drawOperationTextureIndices.Add(texture, _drawOperationHighestTextureIndex);
                textureIndex = _drawOperationHighestTextureIndex;
                _drawOperationHighestTextureIndex++;
            }
            var drawOperation = new DrawOperation()
            {
                TextureIndex = textureIndex,
                TextureOrigin = textureOrigin,
                TextureSourceRectangle = textureSourceRectangle,
                Position = position,
                Rotation = rotation,
                Scale = scale,
                Color = color,
                Depth = depth
            };
            _drawOperations.Add(drawOperation);
        }

        public void SortSprites(SpriteSortMode spriteSortMode)
        {
            switch (spriteSortMode)
            {
                case SpriteSortMode.Deferred:
                    break;
                case SpriteSortMode.Texture:
                    _drawOperations = _drawOperations.OrderBy(x => x.TextureIndex).ToList();
                    break;
                case SpriteSortMode.BackToFront:
                    _drawOperations = _drawOperations.OrderBy(x => x.Depth).ToList();
                    break;
                case SpriteSortMode.FrontToBack:
                    _drawOperations = _drawOperations.OrderByDescending(x => x.Depth).ToList();
                    break;
            }
        }

        public int? UploadData(CommandBuffer commandBuffer, int indexToStartAt = 0)
        {
            if (_drawOperations.Count == 0)
                return null;

            static void AddInstanceDataToBuffer(ref Span<SpriteInstanceData> span, int index, DrawOperation operation)
            {
                span[index].Position = new Vector3(operation.Position, operation.Depth);
                span[index].Rotation = operation.Rotation;
                span[index].Scale = operation.Scale;
                span[index].Color = operation.Color.ToVector4();
                span[index].TextureOrigin = operation.TextureOrigin;
                span[index].TextureSourceRectangle = new Vector4(operation.TextureSourceRectangle.X, operation.TextureSourceRectangle.Y, operation.TextureSourceRectangle.Width, operation.TextureSourceRectangle.Height); ;
            }

            DrawOperation previousDrawOperation = _drawOperations[indexToStartAt];

            var instanceDataSpan = _instanceTransferBuffer.Map<SpriteInstanceData>(true);
            var highestInstanceIndex = 0;

            AddInstanceDataToBuffer(ref instanceDataSpan, highestInstanceIndex, previousDrawOperation);
            highestInstanceIndex++;

            for (int i = indexToStartAt + 1; i < _drawOperations.Count; i++)
            {
                DrawOperation currentDrawOperation = _drawOperations[i];

                if (currentDrawOperation.TextureIndex != previousDrawOperation.TextureIndex || highestInstanceIndex >= _maximumSpriteAmount)
                {
                    _instanceTransferBuffer.Unmap();

                    var copyPass = commandBuffer.BeginCopyPass();
                    copyPass.UploadToBuffer(_instanceTransferBuffer, _spriteInstanceBuffer, true);
                    commandBuffer.EndCopyPass(copyPass);

                    SpriteAmount = (uint)highestInstanceIndex;
                    TextureToSample = _drawOperationTextures[previousDrawOperation.TextureIndex];

                    return i;
                }

                AddInstanceDataToBuffer(ref instanceDataSpan, highestInstanceIndex, currentDrawOperation);
                highestInstanceIndex++;

                previousDrawOperation = currentDrawOperation;
            }

            _instanceTransferBuffer.Unmap();

            var lastCopyPass = commandBuffer.BeginCopyPass();
            lastCopyPass.UploadToBuffer(_instanceTransferBuffer, _spriteInstanceBuffer, true);
            commandBuffer.EndCopyPass(lastCopyPass);

            SpriteAmount = (uint)highestInstanceIndex;
            DrawOperation lastDrawOperation = _drawOperations[^1];
            TextureToSample = _drawOperationTextures[lastDrawOperation.TextureIndex];

            return null;
        }

        public void CreateVertexInfo(CommandBuffer commandBuffer, Matrix4x4? transformationMatrix)
        {
            if (TextureToSample is null)
                throw new NullReferenceException($"{nameof(TextureToSample)} is null. Is {nameof(CreateVertexInfo)} being called before {nameof(UploadData)}?");

            Matrix4x4 actualTransformationMatrix = transformationMatrix ?? Matrix4x4.Identity;

            var computeUniforms = new ComputeUniforms()
            {
                TransformationMatrix = actualTransformationMatrix,
                TextureSize = new Vector2(TextureToSample.Width, TextureToSample.Height),
            };

            var computePass = commandBuffer.BeginComputePass
            (
                new StorageBufferReadWriteBinding(VertexBuffer, true)
            );

            computePass.BindComputePipeline(_computePipeline);
            computePass.BindStorageBuffers(_spriteInstanceBuffer);
            commandBuffer.PushComputeUniformData(computeUniforms);
            computePass.Dispatch((SpriteAmount + 63) / 64, 1, 1);
            commandBuffer.EndComputePass(computePass);
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    _computePipeline.Dispose();
                    
                    _spriteInstanceBuffer.Dispose();
                    _instanceTransferBuffer.Dispose();
                    VertexBuffer.Dispose();
                    IndexBuffer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}