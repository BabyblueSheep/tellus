using MoonWorks.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Graphics.SpriteBatch;

public sealed partial class SpriteBatch : GraphicsResource
{
    public sealed class StorageBufferBundle : GraphicsResource
    {
        private readonly TransferBuffer _instanceTransferBuffer;
        internal Buffer SpriteInstanceBuffer { get; }

        internal Buffer VertexBuffer;
        internal Buffer IndexBuffer;

        public StorageBufferBundle(GraphicsDevice device, uint maxSpriteAmount = 2048) : base(device)
        {
            uint maxVertexAmount = maxSpriteAmount * 4;
            uint maxIndexAmount = maxSpriteAmount * 6;

            _instanceTransferBuffer = TransferBuffer.Create<SpriteInstanceData>
            (
                Device,
                TransferBufferUsage.Upload,
                maxSpriteAmount
            );

            SpriteInstanceBuffer = Buffer.Create<SpriteInstanceData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                maxSpriteAmount
            );

            VertexBuffer = Buffer.Create<PositionTextureColorVertex>
            (
                Device,
                BufferUsageFlags.ComputeStorageWrite | BufferUsageFlags.Vertex,
                maxVertexAmount
            );

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

            var commandBuffer = Device.AcquireCommandBuffer();
            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.UploadToBuffer(indexTransferBuffer, IndexBuffer, false);
            commandBuffer.EndCopyPass(copyPass);
            Device.Submit(commandBuffer);

            indexTransferBuffer.Dispose();
        }
    }
}