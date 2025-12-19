using MoonWorks.Graphics;
using MoonWorks.Storage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http.Headers;
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

    /// <summary>
    /// Defines flags for flipping/mirroring sprites.
    /// </summary>
    [Flags]
    public enum SpriteFlipOptions
    {
        /// <summary>
        /// No flipping.
        /// </summary>
        None = 0,
        /// <summary>
        /// Render the sprite reversed along the X axis.
        /// </summary>
        FlipHorizontal = 1,
        /// <summary>
        /// Render the sprite reversed along the Y axis.
        /// </summary>
        FlipVertical = 2,
    }

    /// <summary>
    /// Defines possible parameters a sprite could be drawn with.
    /// </summary>
    public struct SpriteParameters
    {
        /// <summary>
        /// A matrix describing affine transformations to be applied to the sprite.
        /// </summary>
        public Matrix4x4 TransformationMatrix = Matrix4x4.Identity;
        /// <summary>
        /// The color to tint the sprite with. Tint acts like a color multiplicator.
        /// </summary>
        public Color TintColor = Color.White;
        /// <summary>
        /// The color to offset the sprite with. Offset gets added to the sprite after it's tinted.
        /// </summary>
        public Color OffsetColor = Color.Transparent;
        /// <summary>
        /// Options regarding sprite flipping to use.
        /// </summary>
        public SpriteFlipOptions FlipOptions = SpriteFlipOptions.None;
        /// <summary>
        /// The "depth value" of the sprite. Used for z-buffering and sorting.
        /// </summary>
        public float Depth = 0f;

        public SpriteParameters() { }
    }

    /// <summary>
    /// Contains a list of sprite instances to be drawn.
    /// </summary>
    public sealed class SpriteInstanceContainer : GraphicsResource
    {
        private struct SpriteInstance
        {
            public int TextureIndex;
            public Rectangle TextureSourceRectangle;
            public Matrix4x4 TransformationMatrix;
            public Color TintColor;
            public Color OffsetColor;
            public SpriteFlipOptions FlipOptions;
            public float Depth;
        }

        internal struct BatchInformation
        {
            public int StartSpriteIndex;
            public int Length;
            public Texture Texture;
        }

        // arbitrary
        private const int MAXIMUM_SPRITE_AMOUNT = 1048576;

        private int _maximumSpriteAmount;

        private TransferBuffer _vertexTransferBuffer;
        internal Buffer VertexBuffer { get; private set; }
        internal Buffer IndexBuffer { get; private set; }

        private readonly List<Texture> _spriteTextures;
        private readonly Dictionary<Texture, int> __spriteTextureIndices;

        private SpriteInstance[] _spriteInstances;
        private int[] _spriteInstanceIndices;
        private int _currentSpriteAmount;

        private class SpriteInstanceTextureComparer : IComparer<int>
        {
            public SpriteInstance[] Instances;

            public int Compare(int a, int b)
            {
                return Instances[a].TextureIndex.CompareTo(Instances[b].TextureIndex);
            }
        }

        private class SpriteInstanceDepthFrontToBackComparer : IComparer<int>
        {
            public SpriteInstance[] Instances;

            public int Compare(int a, int b)
            {
                return Instances[a].Depth.CompareTo(Instances[b].Depth);
            }
        }

        private class SpriteInstanceDepthBackToFrontComparer : IComparer<int>
        {
            public SpriteInstance[] Instances;

            public int Compare(int a, int b)
            {
                return Instances[b].Depth.CompareTo(Instances[a].Depth);
            }
        }

        private static readonly SpriteInstanceTextureComparer _textureComparer = new SpriteInstanceTextureComparer();
        private static readonly SpriteInstanceDepthFrontToBackComparer _frontToBackComparer = new SpriteInstanceDepthFrontToBackComparer();
        private static readonly SpriteInstanceDepthBackToFrontComparer _backToFrontComparer = new SpriteInstanceDepthBackToFrontComparer();

        private bool _resizeBuffers;

        internal List<BatchInformation> BatchInformationList;

        public SpriteInstanceContainer(GraphicsDevice device, int maxSpriteAmount = 2048) : base(device)
        {
            _spriteInstances = new SpriteInstance[maxSpriteAmount];
            _spriteInstanceIndices = new int[maxSpriteAmount];

            _maximumSpriteAmount = maxSpriteAmount;
            CommandBuffer commandBuffer = device.AcquireCommandBuffer();
            ResizeBuffers(commandBuffer, (uint)maxSpriteAmount, false);
            device.Submit(commandBuffer);

            _spriteTextures = [];
            __spriteTextureIndices = new Dictionary<Texture, int>(ReferenceEqualityComparer.Instance);

            BatchInformationList = [];
        }

        private void ResizeBuffers(CommandBuffer commandBuffer, uint size, bool cycle)
        {
            uint maxVertexAmount = size * 4;
            uint maxIndexAmount = size * 6;

            _vertexTransferBuffer?.Dispose();
            _vertexTransferBuffer = TransferBuffer.Create<PositionTextureColorVertex>
            (
                Device,
                TransferBufferUsage.Upload,
                maxVertexAmount
            );

            VertexBuffer?.Dispose();
            VertexBuffer = Buffer.Create<PositionTextureColorVertex>
            (
                Device,
                BufferUsageFlags.Vertex,
                maxVertexAmount
            );

            IndexBuffer?.Dispose();
            IndexBuffer = Buffer.Create<uint>
            (
                Device,
                BufferUsageFlags.Index,
                maxIndexAmount
            );

            TransferBuffer indexTransferBuffer = TransferBuffer.Create<uint>(
                Device,
                TransferBufferUsage.Upload,
                maxIndexAmount
            );

            var indexSpan = indexTransferBuffer.Map<uint>(cycle);
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

            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.UploadToBuffer(indexTransferBuffer, IndexBuffer, cycle);
            commandBuffer.EndCopyPass(copyPass);

            indexTransferBuffer.Dispose();
        }

        /// <summary>
        /// Clears all stored sprites.
        /// </summary>
        public void ClearSprites()
        {
            _currentSpriteAmount = 0;
            
            _spriteTextures.Clear();
            __spriteTextureIndices.Clear();
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
            if (_currentSpriteAmount >= _maximumSpriteAmount)
            {
                int nextPowerOfTwo = 1;
                while (nextPowerOfTwo <= _currentSpriteAmount)
                    nextPowerOfTwo *= 2;

                if (nextPowerOfTwo > MAXIMUM_SPRITE_AMOUNT)
                {
                    throw new Exception("Buffers would be too large!");
                }
                else
                {
                    Array.Resize(ref _spriteInstances, nextPowerOfTwo);
                    Array.Resize(ref _spriteInstanceIndices, nextPowerOfTwo);

                    _maximumSpriteAmount = nextPowerOfTwo;
                    _resizeBuffers = true;
                }
            }

            if (!__spriteTextureIndices.TryGetValue(texture, out int textureIndex))
            {
                textureIndex = _spriteTextures.Count;
                _spriteTextures.Add(texture);
                __spriteTextureIndices.Add(texture, textureIndex);
            }

            _spriteInstances[_currentSpriteAmount].TextureIndex = textureIndex;
            _spriteInstances[_currentSpriteAmount].TextureSourceRectangle = textureSourceRectangle ?? new Rectangle(0, 0, (int)texture.Width, (int)texture.Height);
            _spriteInstances[_currentSpriteAmount].TransformationMatrix = parameters.TransformationMatrix;
            _spriteInstances[_currentSpriteAmount].TintColor = parameters.TintColor;
            _spriteInstances[_currentSpriteAmount].OffsetColor = parameters.OffsetColor;
            _spriteInstances[_currentSpriteAmount].FlipOptions = parameters.FlipOptions;
            _spriteInstances[_currentSpriteAmount].Depth = parameters.Depth;

            _spriteInstanceIndices[_currentSpriteAmount] = _currentSpriteAmount;

            _currentSpriteAmount++;
        }

        /// <summary>
        /// Sorts all contained sprites according to <see cref="SpriteSortMode"/>.
        /// </summary>
        /// <param name="spriteSortMode">The sorting mode to use.</param>
        public void SortSprites(SpriteSortMode spriteSortMode)
        {
            IComparer<int> comparer = _textureComparer;

            switch (spriteSortMode)
            {
                case SpriteSortMode.Texture:
                    _textureComparer.Instances = _spriteInstances;
                    comparer = _textureComparer;
                    break;
                case SpriteSortMode.FrontToBack:
                    _frontToBackComparer.Instances = _spriteInstances;
                    comparer = _frontToBackComparer;
                    break;
                case SpriteSortMode.BackToFront:
                    _backToFrontComparer.Instances = _spriteInstances;
                    comparer = _backToFrontComparer;
                    break;
            }

            Array.Sort(_spriteInstanceIndices, 0, _currentSpriteAmount, comparer);
        }

        /// <summary>
        /// Converts sprites to vertex information and uploads them to the GPU.
        /// </summary>
        /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
        public void CreateVertexInfo(CommandBuffer commandBuffer)
        {
            BatchInformationList.Clear();

            if (_currentSpriteAmount == 0)
                return;

            if (_resizeBuffers)
            {
                ResizeBuffers(commandBuffer, (uint)_maximumSpriteAmount, true);
                _resizeBuffers = false;
            }

            void AddInstanceDataToBuffer(ref Span<PositionTextureColorVertex> span, int index, SpriteInstance operation)
            {
                var texture = _spriteTextures[operation.TextureIndex];
                var textureSize = new Vector2(texture.Width, texture.Height);
                var tintColorVector = operation.TintColor.ToVector4();
                var offsetColorVector = operation.OffsetColor.ToVector4();

                var textureMinX = operation.TextureSourceRectangle.X / textureSize.X;
                var textureMinY = operation.TextureSourceRectangle.Y / textureSize.Y;
                var textureMaxX = textureMinX + operation.TextureSourceRectangle.Width / textureSize.X;
                var textureMaxY = textureMinY + operation.TextureSourceRectangle.Height / textureSize.Y;

                if ((operation.FlipOptions & SpriteFlipOptions.FlipHorizontal) != 0)
                {
                    (textureMinX, textureMaxX) = (textureMaxX, textureMinX);
                }
                if ((operation.FlipOptions & SpriteFlipOptions.FlipVertical) != 0)
                {
                    (textureMinY, textureMaxY) = (textureMaxY, textureMinY);
                }

                span[index * 4 + 0].Position = new Vector4(Vector3.Transform(new Vector3(0, 0, operation.Depth), operation.TransformationMatrix), 1);
                span[index * 4 + 0].TintColor = tintColorVector;
                span[index * 4 + 0].OffsetColor = offsetColorVector;
                span[index * 4 + 0].TexCoord = new Vector2(textureMinX, textureMinY);

                span[index * 4 + 1].Position = new Vector4(Vector3.Transform(new Vector3(1, 0, operation.Depth), operation.TransformationMatrix), 1);
                span[index * 4 + 1].TintColor = tintColorVector;
                span[index * 4 + 1].OffsetColor = offsetColorVector;
                span[index * 4 + 1].TexCoord = new Vector2(textureMaxX, textureMinY);

                span[index * 4 + 2].Position = new Vector4(Vector3.Transform(new Vector3(0, 1, operation.Depth), operation.TransformationMatrix), 1);
                span[index * 4 + 2].TintColor = tintColorVector;
                span[index * 4 + 2].OffsetColor = offsetColorVector;
                span[index * 4 + 2].TexCoord = new Vector2(textureMinX, textureMaxY);

                span[index * 4 + 3].Position = new Vector4(Vector3.Transform(new Vector3(1, 1, operation.Depth), operation.TransformationMatrix), 1);
                span[index * 4 + 3].TintColor = tintColorVector;
                span[index * 4 + 3].OffsetColor = offsetColorVector;
                span[index * 4 + 3].TexCoord = new Vector2(textureMaxX, textureMaxY);
            }

            var instanceDataSpan = _vertexTransferBuffer.Map<PositionTextureColorVertex>(true);

            int batchIndexStart = 0;
            int previousTextureIndex = _spriteInstances[_spriteInstanceIndices[0]].TextureIndex;
            for (int i = 0; i < _currentSpriteAmount; i++)
            {
                SpriteInstance currentDrawOperation = _spriteInstances[_spriteInstanceIndices[i]];

                AddInstanceDataToBuffer(ref instanceDataSpan, i, currentDrawOperation);

                if (previousTextureIndex != currentDrawOperation.TextureIndex)
                {
                    BatchInformationList.Add(new BatchInformation()
                    {
                        StartSpriteIndex = batchIndexStart,
                        Length = i - batchIndexStart,
                        Texture = _spriteTextures[previousTextureIndex],
                    });

                    batchIndexStart = i;
                }

                previousTextureIndex = currentDrawOperation.TextureIndex;
            }

            BatchInformationList.Add(new BatchInformation()
            {
                StartSpriteIndex = batchIndexStart,
                Length = _currentSpriteAmount - batchIndexStart,
                Texture = _spriteTextures[previousTextureIndex],
            });

            _vertexTransferBuffer.Unmap();

            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.UploadToBuffer(_vertexTransferBuffer, VertexBuffer, true);
            commandBuffer.EndCopyPass(copyPass);
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