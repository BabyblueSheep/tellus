using MoonWorks.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    public sealed class BodyRayCasterPairBufferStorage : GraphicsResource
    {
        private readonly Dictionary<string, (int, int)> _pairListToRange;

        private readonly TransferBuffer _pairDataTransferBuffer;
        public Buffer PairDataBuffer { get; }

        public int ValidPairCount { get; private set; }

        public BodyRayCasterPairBufferStorage(GraphicsDevice device, uint pairCount = 1024) : base(device)
        {
            _pairListToRange = [];

            _pairDataTransferBuffer = TransferBuffer.Create<CollisionBodyRayCasterPair>
            (
                Device,
                TransferBufferUsage.Upload,
                pairCount
            );

            PairDataBuffer = Buffer.Create<CollisionBodyRayCasterPair>
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

        public void UploadData(CommandBuffer commandBuffer, (string, IList<ICollisionBody>, IList<ICollisionRayCaster>)[] bodyRayCasterListPairList)
        {
            _pairListToRange.Clear();

            var bodyDataUploadSpan = _pairDataTransferBuffer.Map<CollisionBodyRayCasterPair>(true);

            int pairIndex = 0;

            foreach (var bodyRayCasterListPair in bodyRayCasterListPairList)
            {
                int pairListIndexStart = pairIndex;

                for (int i = 0; i < bodyRayCasterListPair.Item2.Count; i++)
                {
                    for (int j = 0; j < bodyRayCasterListPair.Item3.Count; j++)
                    {
                        if (ReferenceEquals(bodyRayCasterListPair.Item2[i], bodyRayCasterListPair.Item3[j]))
                        {
                            bodyDataUploadSpan[pairIndex].BodyIndex = i;
                            bodyDataUploadSpan[pairIndex].RayCasterIndex = j;
                            pairIndex++;
                        }
                    }
                }

                _pairListToRange.Add(bodyRayCasterListPair.Item1, (pairListIndexStart, pairIndex - pairListIndexStart));
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