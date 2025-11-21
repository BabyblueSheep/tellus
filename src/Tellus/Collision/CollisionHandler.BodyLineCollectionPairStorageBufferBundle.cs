using MoonWorks.Graphics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    /// <summary>
    /// Provides a convenient way to upload information about body-line connection pairs to GPU buffers.
    /// </summary>
    public sealed class BodyLineCollectionPairStorageBufferBundle : GraphicsResource
    {
        private readonly Dictionary<string, (int, int)> _pairListToRange;

        private readonly TransferBuffer _pairDataTransferBuffer;

        public Buffer PairDataBuffer { get; }

        /// <summary>
        /// The amount of pairs in the pair buffer that contain valid information.
        /// </summary>
        public int ValidPairCount { get; private set; }

        public BodyLineCollectionPairStorageBufferBundle(GraphicsDevice device, uint pairCount = 1024) : base(device)
        {
            _pairListToRange = [];

            _pairDataTransferBuffer = TransferBuffer.Create<CollisionBodyLineCollectionPair>
            (
                Device,
                TransferBufferUsage.Upload,
                pairCount
            );

            PairDataBuffer = Buffer.Create<CollisionBodyLineCollectionPair>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                pairCount
            );
        }

        /// <summary>
        /// Gets the offset and length of a pair buffer segment.
        /// </summary>
        /// <param name="bufferSegmentName">The name of the buffer segment. <c>null</c> returns a range of the whole buffer.</param>
        /// <returns>The offset and length.</returns>
        public (int, int) GetPairRange(string? bufferSegmentName)
        {
            if (bufferSegmentName == null)
                return (0, ValidPairCount);
            return _pairListToRange[bufferSegmentName];
        }

        /// <summary>
        /// Uploads body and body part information to the buffers and defines buffer segments.
        /// </summary>
        /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
        /// <param name="nameSegmentPairList">A list of triads of segment names, body collections and line collection collections.</param>
        public void UploadData(CommandBuffer commandBuffer, (string, IList<ICollisionBody>, IList<ICollisionLineCollection>)[] bodyLineCollectionListPairList)
        {
            _pairListToRange.Clear();

            var bodyDataUploadSpan = _pairDataTransferBuffer.Map<CollisionBodyLineCollectionPair>(true);

            int pairIndex = 0;

            foreach (var bodyLineCollectionListPair in bodyLineCollectionListPairList)
            {
                int pairListIndexStart = pairIndex;

                for (int i = 0; i < bodyLineCollectionListPair.Item2.Count; i++)
                {
                    for (int j = 0; j < bodyLineCollectionListPair.Item3.Count; j++)
                    {
                        if (ReferenceEquals(bodyLineCollectionListPair.Item2[i], bodyLineCollectionListPair.Item3[j]))
                        {
                            bodyDataUploadSpan[pairIndex].BodyIndex = i;
                            bodyDataUploadSpan[pairIndex].LineCollectionIndex = j;
                            pairIndex++;
                        }
                    }
                }

                _pairListToRange.Add(bodyLineCollectionListPair.Item1, (pairListIndexStart, pairIndex - pairListIndexStart));
            }

            _pairDataTransferBuffer.Unmap();

            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.UploadToBuffer(_pairDataTransferBuffer, PairDataBuffer, true);
            commandBuffer.EndCopyPass(copyPass);

            ValidPairCount = pairIndex;
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    _pairDataTransferBuffer.Dispose();
                    PairDataBuffer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}