using MoonWorks.Graphics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public static partial class CollisionHandler
{
    /// <summary>
    /// Provides a convenient way to upload information about bodies and body parts to GPU buffers.
    /// </summary>
    public sealed class BodyStorageBufferBundle : GraphicsResource
    {
        private readonly Dictionary<string, (int, int)> _bufferSegments;

        private readonly TransferBuffer _bodyPartDataTransferBuffer;

        internal Buffer BodyPartDataBuffer { get; }

        private readonly TransferBuffer _bodyDataTransferBuffer;

        internal Buffer BodyDataBuffer { get; }

        private readonly int _bodyCount;
        private readonly int _bodyPartCount;

        /// <summary>
        /// The amount of bodies in the body buffer that contain valid information.
        /// </summary>
        public int ValidBodyCount { get; private set; }

        public BodyStorageBufferBundle(GraphicsDevice device, uint bodyPartCount = 1024, uint bodyCount = 128) : base(device)
        {
            _bufferSegments = [];

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

            _bodyCount = (int)bodyCount;
            _bodyPartCount = (int)bodyPartCount;
        }

        /// <summary>
        /// Gets the offset and length of a body buffer segment.
        /// </summary>
        /// <param name="bufferSegmentName">The name of the buffer segment. <c>null</c> returns a range of the whole buffer.</param>
        /// <returns>The offset and length.</returns>
        public (int, int) GetBodySegmentRange(string? bufferSegmentName)
        {
            if (bufferSegmentName == null)
                return (0, ValidBodyCount);
            return _bufferSegments[bufferSegmentName];
        }

        /// <summary>
        /// Uploads body and body part information to the buffers and defines buffer segments.
        /// </summary>
        /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
        /// <param name="nameSegmentPairList">A list of pairs of segment names and body collections.</param>
        public void UploadData(CommandBuffer commandBuffer, IEnumerable<(string, IEnumerable<ICollisionBody>)> nameSegmentPairList)
        {
            _bufferSegments.Clear();

            var bodyDataUploadSpan = _bodyDataTransferBuffer.Map<CollisionBodyData>(true);
            var bodyPartDataUploadSpan = _bodyPartDataTransferBuffer.Map<CollisionBodyPartData>(true);

            int bodyDataIndex = 0;
            int bodyPartDataIndex = 0;

            foreach (var nameSegmentPair in nameSegmentPairList)
            {
                int bodyListIndexStart = bodyDataIndex;

                foreach (var body in nameSegmentPair.Item2)
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
                        if (bodyPartDataIndex >= _bodyPartCount)
                            throw new IndexOutOfRangeException("Attempting to store more body parts than can fit in the buffer!");
                    }

                    bodyDataUploadSpan[bodyDataIndex].BodyPartIndexLength = bodyPartDataIndex - bodyDataUploadSpan[bodyDataIndex].BodyPartIndexStart;
                    bodyDataUploadSpan[bodyDataIndex].Offset = body.BodyOffset;

                    bodyDataIndex++;
                    if (bodyDataIndex >= _bodyCount)
                        throw new IndexOutOfRangeException("Attempting to store more bodies than can fit in the buffer!");
                }

                _bufferSegments.Add(nameSegmentPair.Item1, (bodyListIndexStart, bodyDataIndex - bodyListIndexStart));
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
