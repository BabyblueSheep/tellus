using MoonWorks.Graphics;
using MoonWorks.Storage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Buffer = MoonWorks.Graphics.Buffer;
using Color = MoonWorks.Graphics.Color;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace Tellus.Graphics.SpriteBatch;

public sealed partial class SpriteBatch : GraphicsResource
{
    /// <summary>
    /// Defines sprite sort rendering options.
    /// </summary>
    public enum SpriteSortMode
    {
        /// <summary>
        /// Sprites are sorted by texture.
        /// </summary>
        Texture,
        /// <summary>
        /// Sprites are sorted by depth in back-to-front order.
        /// </summary>
        BackToFront,
        /// <summary>
        /// Sprites are sorted by depth in front-to-back order.
        /// </summary>
        FrontToBack,
    }

    private struct SpriteInstance
    {
        public int TextureIndex;
        public Rectangle TextureSourceRectangle;
        public Matrix4x4 TransformationMatrix;
        public Color TintColor;
        public float Depth;
    }

    public struct SpriteParameters
    {
        public Matrix4x4 TransformationMatrix = Matrix4x4.Identity;
        public Color TintColor = Color.White;
        public Color OverlayColor = Color.Transparent;
        public float Depth = 0f;

        public SpriteParameters() { }
    }

    /// <summary>
    /// Contains a list of sprite instances to be drawn.
    /// </summary>
    public sealed class SpriteInstanceContainer : GraphicsResource
    {
        private readonly int _maximumSpriteAmount;
        internal uint SpriteAmount { get; private set; }

        internal Texture? TextureToSample { get; private set; }

        private readonly TransferBuffer _vertexTransferBuffer;
        internal Buffer VertexBuffer { get; }
        internal Buffer IndexBuffer { get; }

        private readonly List<Texture> _spriteTextures;
        private readonly Dictionary<Texture, int> __spriteTextureIndices;
        private List<SpriteInstance> _spriteInstances;

        public SpriteInstanceContainer(GraphicsDevice device, uint maxSpriteAmount = 2048) : base(device)
        {
            _maximumSpriteAmount = (int)maxSpriteAmount;

            uint maxVertexAmount = maxSpriteAmount * 4;
            uint maxIndexAmount = maxSpriteAmount * 6;

            _vertexTransferBuffer = TransferBuffer.Create<PositionTextureColorVertex>
            (
                device,
                TransferBufferUsage.Upload,
                maxVertexAmount
            );

            VertexBuffer = Buffer.Create<PositionTextureColorVertex>
            (
                device,
                BufferUsageFlags.Vertex,
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

            _spriteTextures = [];
            __spriteTextureIndices = new Dictionary<Texture, int>(ReferenceEqualityComparer.Instance);
            _spriteInstances = [];
        }

        /// <summary>
        /// Clears all stored sprites.
        /// </summary>
        public void ClearSprites()
        {
            _spriteInstances.Clear();
            _spriteTextures.Clear();
            __spriteTextureIndices.Clear();
            TextureToSample = null;
        }

        /// <summary>
        /// Adds a sprite to the batch of sprites to be rendered.
        /// </summary>
        /// <param name="texture">The texture to use.</param>
        /// <param name="textureSourceRectangle">An optional region on the texture which will be rendered.</param>
        /// <param name="parameters">All other sprite parameters.</param>
        public void PushSprite
        (
            Texture texture,
            Rectangle? textureSourceRectangle,
            SpriteParameters parameters
        )
        {
            if (!__spriteTextureIndices.TryGetValue(texture, out int textureIndex))
            {
                textureIndex = _spriteTextures.Count;
                _spriteTextures.Add(texture);
                __spriteTextureIndices.Add(texture, textureIndex);
            }

            var drawOperation = new SpriteInstance()
            {
                TextureIndex = textureIndex,
                TextureSourceRectangle = textureSourceRectangle ?? new Rectangle(0, 0, (int)texture.Width, (int)texture.Height),
                TransformationMatrix = parameters.TransformationMatrix,
                TintColor = parameters.TintColor,
                Depth = parameters.Depth
            };
            _spriteInstances.Add(drawOperation);

        }

        /// <summary>
        /// Sorts all contained sprites according to <see cref="SpriteSortMode"/>.
        /// </summary>
        /// <param name="spriteSortMode">The sorting mode to use.</param>
        public void SortSprites(SpriteSortMode spriteSortMode)
        {
            switch (spriteSortMode)
            {
                case SpriteSortMode.Texture:
                    _spriteInstances = _spriteInstances.OrderBy(x => x.TextureIndex).ToList();
                    break;
                case SpriteSortMode.BackToFront:
                    _spriteInstances = _spriteInstances.OrderBy(x => x.Depth).ToList();
                    break;
                case SpriteSortMode.FrontToBack:
                    _spriteInstances = _spriteInstances.OrderByDescending(x => x.Depth).ToList();
                    break;
            }
        }

        /// <summary>
        /// Converts sprites to vertex information and uploads them to the GPU.
        /// </summary>
        /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
        public int? CreateVertexInfo(CommandBuffer commandBuffer, int indexToStartAt = 0)
        {
            if (_spriteInstances.Count == 0)
                return null;

            void AddInstanceDataToBuffer(ref Span<PositionTextureColorVertex> span, int index, SpriteInstance operation)
            {
                Vector2 textureSize = new Vector2(_spriteTextures[operation.TextureIndex].Width, _spriteTextures[operation.TextureIndex].Height);

                span[index * 4 + 0].Position = new Vector4(Vector3.Transform(new Vector3(0, 0, operation.Depth), operation.TransformationMatrix), 1);
                span[index * 4 + 0].Color = operation.TintColor.ToVector4();
                span[index * 4 + 0].TexCoord = new Vector2(operation.TextureSourceRectangle.X, operation.TextureSourceRectangle.Y) / textureSize;

                span[index * 4 + 1].Position = new Vector4(Vector3.Transform(new Vector3(1, 0, operation.Depth), operation.TransformationMatrix), 1);
                span[index * 4 + 1].Color = operation.TintColor.ToVector4();
                span[index * 4 + 1].TexCoord = new Vector2(operation.TextureSourceRectangle.X + operation.TextureSourceRectangle.Width, operation.TextureSourceRectangle.Y) / textureSize;

                span[index * 4 + 2].Position = new Vector4(Vector3.Transform(new Vector3(0, 1, operation.Depth), operation.TransformationMatrix), 1);
                span[index * 4 + 2].Color = operation.TintColor.ToVector4();
                span[index * 4 + 2].TexCoord = new Vector2(operation.TextureSourceRectangle.X, operation.TextureSourceRectangle.Y + operation.TextureSourceRectangle.Height) / textureSize;

                span[index * 4 + 3].Position = new Vector4(Vector3.Transform(new Vector3(1, 1, operation.Depth), operation.TransformationMatrix), 1);
                span[index * 4 + 3].Color = operation.TintColor.ToVector4();
                span[index * 4 + 3].TexCoord = new Vector2(operation.TextureSourceRectangle.X + operation.TextureSourceRectangle.Width, operation.TextureSourceRectangle.Y + operation.TextureSourceRectangle.Height) / textureSize;
            }

            var instanceDataSpan = _vertexTransferBuffer.Map<PositionTextureColorVertex>(true);

            SpriteInstance previousDrawOperation = _spriteInstances[indexToStartAt];

            var highestInstanceIndex = 0;

            AddInstanceDataToBuffer(ref instanceDataSpan, highestInstanceIndex, previousDrawOperation);
            highestInstanceIndex++;

            for (int i = indexToStartAt + 1; i < _spriteInstances.Count; i++)
            {
                SpriteInstance currentDrawOperation = _spriteInstances[i];

                if (currentDrawOperation.TextureIndex != previousDrawOperation.TextureIndex || highestInstanceIndex >= _maximumSpriteAmount)
                {
                    _vertexTransferBuffer.Unmap();

                    var copyPass = commandBuffer.BeginCopyPass();
                    copyPass.UploadToBuffer(_vertexTransferBuffer, VertexBuffer, true);
                    commandBuffer.EndCopyPass(copyPass);

                    SpriteAmount = (uint)highestInstanceIndex;
                    TextureToSample = _spriteTextures[previousDrawOperation.TextureIndex];

                    return i;
                }

                AddInstanceDataToBuffer(ref instanceDataSpan, highestInstanceIndex, currentDrawOperation);
                highestInstanceIndex++;

                previousDrawOperation = currentDrawOperation;
            }

            _vertexTransferBuffer.Unmap();

            var lastCopyPass = commandBuffer.BeginCopyPass();
            lastCopyPass.UploadToBuffer(_vertexTransferBuffer, VertexBuffer, true);
            commandBuffer.EndCopyPass(lastCopyPass);

            SpriteAmount = (uint)highestInstanceIndex;
            SpriteInstance lastDrawOperation = _spriteInstances[^1];
            TextureToSample = _spriteTextures[lastDrawOperation.TextureIndex];

            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    _vertexTransferBuffer.Dispose();
                    VertexBuffer.Dispose();
                    IndexBuffer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}