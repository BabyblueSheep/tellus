using MoonWorks.Graphics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    public sealed class BodyBufferStorage : GraphicsResource
    {
        private readonly Dictionary<string, (int, int)> _bodyListToRange;

        private readonly TransferBuffer _bodyPartDataTransferBuffer;
        public Buffer BodyPartDataBuffer { get; }

        private readonly TransferBuffer _bodyDataTransferBuffer;
        public Buffer BodyDataBuffer { get; }

        public int ValidBodyCount { get; private set; }

        public BodyBufferStorage(GraphicsDevice device, uint bodyPartCount = 1024, uint bodyCount = 128) : base(device)
        {
            _bodyListToRange = [];

            _bodyPartDataTransferBuffer = TransferBuffer.Create<CollisionBodyPartData>(
                Device,
                TransferBufferUsage.Upload,
                bodyPartCount
            );

            BodyPartDataBuffer = Buffer.Create<CollisionBodyPartData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                bodyPartCount
            );

            _bodyDataTransferBuffer = TransferBuffer.Create<CollisionBodyData>(
                Device,
                TransferBufferUsage.Upload,
                bodyCount
            );

            BodyDataBuffer = Buffer.Create<CollisionBodyData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite,
                bodyCount
            );
        }

        public (int, int) GetBodyRange(string? bodyName)
        {
            if (bodyName == null)
                return (0, ValidBodyCount);
            return _bodyListToRange[bodyName];
        }

        public void UploadData(CommandBuffer commandBuffer, (string, IEnumerable<ICollisionBody>)[] bodyListList)
        {
            _bodyListToRange.Clear();

            var bodyDataUploadSpan = _bodyDataTransferBuffer.Map<CollisionBodyData>(true);
            var bodyPartDataUploadSpan = _bodyPartDataTransferBuffer.Map<CollisionBodyPartData>(true);

            int bodyDataIndex = 0;
            int bodyPartDataIndex = 0;

            foreach (var bodyListListItem in bodyListList)
            {
                int bodyListIndexStart = bodyDataIndex;

                foreach (var body in bodyListListItem.Item2)
                {
                    bodyDataUploadSpan[bodyDataIndex].BodyPartIndexStart = bodyPartDataIndex;

                    foreach (var bodyPart in body.BodyParts)
                    {
                        bodyPartDataUploadSpan[bodyPartDataIndex].CollisionBodyIndex = bodyDataIndex;
                        bodyPartDataUploadSpan[bodyPartDataIndex].ShapeType = bodyPart.ShapeType;
                        bodyPartDataUploadSpan[bodyPartDataIndex].Center = bodyPart.BodyPartCenter;
                        bodyPartDataUploadSpan[bodyPartDataIndex].DecimalFields = bodyPart.DecimalFields;
                        bodyPartDataUploadSpan[bodyPartDataIndex].IntegerFields = bodyPart.IntegerFields;

                        bodyPartDataIndex++;
                    }

                    bodyDataUploadSpan[bodyDataIndex].BodyPartIndexLength = bodyPartDataIndex - bodyDataUploadSpan[bodyDataIndex].BodyPartIndexStart;
                    bodyDataUploadSpan[bodyDataIndex].Offset = body.BodyOffset;

                    bodyDataIndex++;
                }

                _bodyListToRange.Add(bodyListListItem.Item1, (bodyListIndexStart, bodyDataIndex - bodyListIndexStart));
            }

            _bodyDataTransferBuffer.Unmap();
            _bodyPartDataTransferBuffer.Unmap();

            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.UploadToBuffer(_bodyDataTransferBuffer, BodyDataBuffer, true);
            copyPass.UploadToBuffer(_bodyPartDataTransferBuffer, BodyPartDataBuffer, true);
            commandBuffer.EndCopyPass(copyPass);
            
            ValidBodyCount = bodyDataIndex;
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    _bodyPartDataTransferBuffer.Dispose();
                    BodyPartDataBuffer.Dispose();
                    _bodyDataTransferBuffer.Dispose();
                    BodyDataBuffer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
