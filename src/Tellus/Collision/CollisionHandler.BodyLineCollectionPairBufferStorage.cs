using MoonWorks.Graphics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    public sealed class BodyLineCollectionPairBufferStorage : GraphicsResource
    {
        private readonly Dictionary<string, (int, int)> _pairListToRange;

        private readonly TransferBuffer _pairDataTransferBuffer;
        public Buffer PairDataBuffer { get; }

        public int ValidPairCount { get; private set; }

        public BodyLineCollectionPairBufferStorage(GraphicsDevice device, uint pairCount = 1024) : base(device)
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

        public (int, int) GetPairRange(string? bodyName)
        {
            if (bodyName == null)
                return (0, ValidPairCount);
            return _pairListToRange[bodyName];
        }

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