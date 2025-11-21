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
    /// <summary>
    /// Provides a convenient way to download information about body-body or body-line hit results to GPU buffers.
    /// </summary>
    public sealed class HitResultBufferStorage : GraphicsResource
    {
        private readonly TransferBuffer _uploadBuffer;
        private readonly TransferBuffer _downloadBuffer;
        public Buffer Buffer { get; }

        /// <summary>
        /// The total amount of hit results that can be stored in the buffer.
        /// </summary>
        public int HitResultAmount { get; private set; }

        public HitResultBufferStorage(GraphicsDevice device, uint hitResultAmount = 512) : base(device)
        {
            _uploadBuffer = TransferBuffer.Create<CollisionHitData>(
                Device,
                TransferBufferUsage.Upload,
                hitResultAmount + 1
            );

            _downloadBuffer = TransferBuffer.Create<CollisionHitData>(
                Device,
                TransferBufferUsage.Download,
                hitResultAmount + 1
            );

            Buffer = Buffer.Create<CollisionHitData>
            (
                Device,
                BufferUsageFlags.ComputeStorageWrite,
                hitResultAmount + 1
            );

            var transferUploadSpan = _uploadBuffer.Map<int>(false);
            for (int i = 0; i < hitResultAmount + 1; i += 1)
            {
                transferUploadSpan[i * 2] = 0;
                transferUploadSpan[i * 2 + 1] = 0;
            }
            _uploadBuffer.Unmap();

            HitResultAmount = (int)hitResultAmount;
        }

        /// <summary>
        /// Sets the contents of the hit result buffer to zeros, clearing it.
        /// </summary>
        /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
        public void ClearData(CommandBuffer commandBuffer)
        {
            var collisionResultCopyPass = commandBuffer.BeginCopyPass();
            collisionResultCopyPass.UploadToBuffer(_uploadBuffer, Buffer, true);
            commandBuffer.EndCopyPass(collisionResultCopyPass);
        }

        /// <summary>
        /// Downloads hit results from the GPU to the CPU.
        /// </summary>
        /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
        public void DownloadData(CommandBuffer commandBuffer)
        {
            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.DownloadFromBuffer(Buffer, _downloadBuffer);
            commandBuffer.EndCopyPass(copyPass);
        }

        /// <summary>
        /// Maps downloaded hit results to provided body collections.
        /// </summary>
        /// <param name="bodyListOne">The first body collection.</param>
        /// <param name="bodyListTwo">The second body collection.</param>
        /// <returns>A list of body-body pairs that collided with each other.</returns>
        public IEnumerable<(ICollisionBody, ICollisionBody)> GetData(IList<ICollisionBody> bodyListOne, IList<ICollisionBody> bodyListTwo)
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

        /// <summary>
        /// Maps downloaded hit results to provided body and line collections.
        /// </summary>
        /// <param name="bodyListOne">The body collection.</param>
        /// <param name="bodyListTwo">The line collection.</param>
        /// <returns>A list of body-line pairs that collided with each other.</returns>
        public IEnumerable<(ICollisionBody, ICollisionLineCollection)> GetData(IList<ICollisionBody> bodyListOne, IList<ICollisionLineCollection> bodyListTwo)
        {
            var tempTransferDownloadSpan = _downloadBuffer.Map<int>(false, 0);
            int collisionResultAmount = tempTransferDownloadSpan[0];
            _downloadBuffer.Unmap();

            var transferDownloadSpan = _downloadBuffer.Map<CollisionHitData>(true, 8);

            List<(ICollisionBody, ICollisionLineCollection)> resultList = [];

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
