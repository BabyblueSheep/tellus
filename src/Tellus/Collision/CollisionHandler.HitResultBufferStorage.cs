using Microsoft.VisualBasic;
using MoonWorks.Graphics;
using MoonWorks.Storage;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Buffer = MoonWorks.Graphics.Buffer;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    public sealed class HitResultStorageBuffer : GraphicsResource
    {
        private readonly TransferBuffer _uploadBuffer;
        private readonly TransferBuffer _downloadBuffer;
        public Buffer Buffer { get; }

        public uint CollisionResultAmount { get; private set; }

        public HitResultStorageBuffer(GraphicsDevice device, uint collisionResultAmount = 512) : base(device)
        {
            _uploadBuffer = TransferBuffer.Create<CollisionHitData>(
                Device,
                TransferBufferUsage.Upload,
                collisionResultAmount + 1
            );

            _downloadBuffer = TransferBuffer.Create<CollisionHitData>(
                Device,
                TransferBufferUsage.Download,
                collisionResultAmount + 1
            );

            Buffer = Buffer.Create<CollisionHitData>
            (
                Device,
                BufferUsageFlags.ComputeStorageWrite,
                collisionResultAmount + 1
            );

            var transferUploadSpan = _uploadBuffer.Map<int>(false);
            for (int i = 0; i < collisionResultAmount + 1; i += 1)
            {
                transferUploadSpan[i * 2] = 0;
                transferUploadSpan[i * 2 + 1] = 0;
            }
            _uploadBuffer.Unmap();

            CollisionResultAmount = collisionResultAmount;
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

        unsafe public IEnumerable<(ICollisionBody, ICollisionBody)> GetData(IList<ICollisionBody> bodyListOne, IList<ICollisionBody> bodyListTwo)
        {
            var tempTransferDownloadSpan = _downloadBuffer.Map<int>(false, 0);
            int collisionResultAmount = tempTransferDownloadSpan[0];
            _downloadBuffer.Unmap();

            var transferDownloadSpan = _downloadBuffer.Map<CollisionHitData>(true, 8);

            List<(ICollisionBody, ICollisionBody)> resultList = [];

            for (int i = 0; i < collisionResultAmount; i++)
            {
                CollisionHitData resultData = transferDownloadSpan[i];
                int indexOne = resultData.CollisionBodyIndexOne;
                int indexTwo = resultData.CollisionBodyIndexTwo;

                resultList.Add((bodyListOne[indexOne], bodyListTwo[indexTwo]));
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
