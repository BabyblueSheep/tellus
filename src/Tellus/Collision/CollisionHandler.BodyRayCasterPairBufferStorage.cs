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
        private readonly TransferBuffer _pairDataTransferBuffer;
        public Buffer PairDataBuffer { get; }

        public uint PairCount { get; private set; }
        public uint ValidPairCount { get; private set; }

        public BodyRayCasterPairBufferStorage(GraphicsDevice device, uint pairCount = 1024) : base(device)
        {
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

            PairCount = pairCount;
        }

        public void UploadData(CommandBuffer commandBuffer, IList<ICollisionBody> bodyList, IList<ICollisionRayCaster> rayCasterList)
        {
            var bodyDataUploadSpan = _pairDataTransferBuffer.Map<CollisionBodyRayCasterPair>(true);

            int pairIndex = 0;
            for (int i = 0; i < bodyList.Count; i++)
            {
                for (int j = 0; j < rayCasterList.Count; j++)
                {
                    if (ReferenceEquals(bodyList[i], rayCasterList[j]))
                    {
                        bodyDataUploadSpan[pairIndex].BodyIndex = i;
                        bodyDataUploadSpan[pairIndex].RayCasterIndex = j;
                        pairIndex++;
                    }
                }
            }

            _pairDataTransferBuffer.Unmap();

            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.UploadToBuffer(_pairDataTransferBuffer, PairDataBuffer, true);
            commandBuffer.EndCopyPass(copyPass);

            ValidPairCount = (uint)pairIndex;
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