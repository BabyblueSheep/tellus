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
    public sealed class BodyStorageBuffer : GraphicsResource
    {
        private readonly TransferBuffer _bodyPartDataTransferBuffer;
        private readonly Buffer _bodyPartDataBuffer;

        private readonly TransferBuffer _bodyDataTransferBuffer;
        private readonly Buffer _bodyDataBuffer;

        public uint BodyPartCount { get; private set; }
        public uint BodyCount { get; private set; }

        public BodyStorageBuffer(GraphicsDevice device, uint bodyPartCount = 1024, uint bodyCount = 128) : base(device)
        {
            _bodyPartDataTransferBuffer = TransferBuffer.Create<CollisionBodyPartData>(
                Device,
                TransferBufferUsage.Upload,
                bodyPartCount
            );

            _bodyPartDataBuffer = Buffer.Create<CollisionBodyPartData>
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

            _bodyDataBuffer = Buffer.Create<CollisionBodyData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                bodyCount
            );

            BodyPartCount = bodyPartCount;
            BodyCount = bodyCount;
        }

        public void UploadData(CommandBuffer commandBuffer, IEnumerable<ICollisionBody> bodyList)
        {
            var bodyDataUploadSpan = _bodyDataTransferBuffer.Map<CollisionBodyData>(true);
            var bodyPartDataUploadSpan = _bodyPartDataTransferBuffer.Map<CollisionBodyPartData>(true);

            int bodyDataIndex = 0;
            int bodyPartDataIndex = 0;

            foreach (var body in bodyList)
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

            _bodyDataTransferBuffer.Unmap();
            _bodyPartDataTransferBuffer.Unmap();

            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.UploadToBuffer(_bodyDataTransferBuffer, _bodyDataBuffer, true);
            copyPass.UploadToBuffer(_bodyPartDataTransferBuffer, _bodyPartDataBuffer, true);
            commandBuffer.EndCopyPass(copyPass);
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    _bodyPartDataTransferBuffer.Dispose();
                    _bodyPartDataBuffer.Dispose();
                    _bodyDataTransferBuffer.Dispose();
                    _bodyDataBuffer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
