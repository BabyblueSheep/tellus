using MoonWorks.Graphics;
using System.Numerics;
using Buffer = MoonWorks.Graphics.Buffer;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace Tellus.Collision;

public static partial class CollisionHandler
{
    /// <summary>
    /// Provides a convenient way to download information about body resolutions to GPU buffers.
    /// </summary>
    public sealed class ResolutionResultStorageBufferBundle : GraphicsResource
    {
        private readonly TransferBuffer _uploadBuffer;
        private readonly TransferBuffer _downloadBuffer;
        internal Buffer Buffer { get; }

        /// <summary>
        /// The total amount of hit results that can be stored in the buffer.
        /// </summary>
        public int ResolutionAmount { get; private set; }

        public ResolutionResultStorageBufferBundle(GraphicsDevice device, uint resolutionAmount = 512) : base(device)
        {
            _uploadBuffer = TransferBuffer.Create<CollisionResolutionData>(
                Device,
                TransferBufferUsage.Upload,
                resolutionAmount + 1
            );

            _downloadBuffer = TransferBuffer.Create<CollisionResolutionData>(
                Device,
                TransferBufferUsage.Download,
                resolutionAmount + 1
            );

            Buffer = Buffer.Create<CollisionResolutionData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite,
                resolutionAmount + 1
            );

            var transferUploadSpan = _uploadBuffer.Map<int>(false);
            for (int i = 0; i < resolutionAmount + 1; i += 1)
            {
                transferUploadSpan[i * 3] = 0;
                transferUploadSpan[i * 3 + 1] = 0;
                transferUploadSpan[i * 3 + 2] = 0;
            }
            _uploadBuffer.Unmap();

            ResolutionAmount = (int)resolutionAmount;
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
        /// Gives a pair of bodies and translation vectors needed to resolve collisions.
        /// </summary>
        /// <param name="bodyList">The list of bodies.</param>
        /// <returns>A list of pairs of bodies and vectors.</returns>
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
