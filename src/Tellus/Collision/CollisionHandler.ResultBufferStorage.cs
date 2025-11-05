using MoonWorks.Graphics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    public sealed class ResultStorageBuffer : GraphicsResource
    {
        public TransferBuffer CollisionResultsTransferUploadBuffer { get; private set; }
        public TransferBuffer CollisionResultsTransferDownloadBuffer { get; private set; }
        public Buffer CollisionResultsBuffer { get; private set; }

        public uint CollisionResultAmount { get; private set; }

        public ResultStorageBuffer(GraphicsDevice device, uint collisionResultAmount = 512) : base(device)
        {
            CollisionResultsTransferUploadBuffer = TransferBuffer.Create<CollisionResolutionData>(
                Device,
                TransferBufferUsage.Upload,
                collisionResultAmount + 1
            );

            CollisionResultsTransferDownloadBuffer = TransferBuffer.Create<CollisionResolutionData>(
                Device,
                TransferBufferUsage.Download,
                collisionResultAmount + 1
            );

            CollisionResultsBuffer = Buffer.Create<CollisionResolutionData>
            (
                Device,
                BufferUsageFlags.ComputeStorageWrite,
                collisionResultAmount + 1
            );

            var transferUploadSpan = CollisionResultsTransferUploadBuffer.Map<int>(false);
            for (int i = 0; i < collisionResultAmount + 1; i += 1)
            {
                transferUploadSpan[i] = 0;
            }
            CollisionResultsTransferUploadBuffer.Unmap();

            CollisionResultAmount = collisionResultAmount;
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    CollisionResultsTransferUploadBuffer.Dispose();
                    CollisionResultsTransferDownloadBuffer.Dispose();
                    CollisionResultsBuffer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
    }
}
