using MoonWorks.Graphics;
using System.Numerics;
using Buffer = MoonWorks.Graphics.Buffer;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    public sealed class ResolutionResultBufferStorage : GraphicsResource
    {
        private readonly TransferBuffer _uploadBuffer;
        private readonly TransferBuffer _downloadBuffer;
        public Buffer Buffer { get; }

        public int CollisionResultAmount { get; private set; }

        public ResolutionResultBufferStorage(GraphicsDevice device, uint collisionResultAmount = 512) : base(device)
        {
            _uploadBuffer = TransferBuffer.Create<CollisionResolutionData>(
                Device,
                TransferBufferUsage.Upload,
                collisionResultAmount + 1
            );

            _downloadBuffer = TransferBuffer.Create<CollisionResolutionData>(
                Device,
                TransferBufferUsage.Download,
                collisionResultAmount + 1
            );

            Buffer = Buffer.Create<CollisionResolutionData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite,
                collisionResultAmount + 1
            );

            var transferUploadSpan = _uploadBuffer.Map<int>(false);
            for (int i = 0; i < collisionResultAmount + 1; i += 1)
            {
                transferUploadSpan[i * 3] = 0;
                transferUploadSpan[i * 3 + 1] = 0;
                transferUploadSpan[i * 3 + 2] = 0;
            }
            _uploadBuffer.Unmap();

            CollisionResultAmount = (int)collisionResultAmount;
        }

        public void ClearData(CommandBuffer commandBuffer)
        {
            var collisionResultCopyPass = commandBuffer.BeginCopyPass();
            collisionResultCopyPass.UploadToBuffer(_uploadBuffer, Buffer, true);
            commandBuffer.EndCopyPass(collisionResultCopyPass);
        }

        public void DownloadData(CommandBuffer commandBuffer)
        {
            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.DownloadFromBuffer(Buffer, _downloadBuffer);
            commandBuffer.EndCopyPass(copyPass);
        }

        public IEnumerable<(ICollisionBody, Vector2)> GetData(IList<ICollisionBody> bodyList)
        {
            var tempTransferDownloadSpan = _downloadBuffer.Map<int>(false, 0);
            int collisionResultAmount = tempTransferDownloadSpan[0];
            _downloadBuffer.Unmap();

            var transferDownloadSpan = _downloadBuffer.Map<CollisionResolutionData>(true, 16);

            List<(ICollisionBody, Vector2)> resultList = [];

            for (int i = 0; i < collisionResultAmount; i++)
            {
                CollisionResolutionData resultData = transferDownloadSpan[i];
                int index = resultData.CollisionBodyIndex;
                Vector2 vector = resultData.TotalMinimumTransitionVector;

                resultList.Add((bodyList[index], vector));
            }

            _downloadBuffer.Unmap();

            foreach (var result in resultList)
            {
                yield return result;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    _uploadBuffer.Dispose();
                    _downloadBuffer.Dispose();
                    Buffer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
