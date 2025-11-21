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
    /// <summary>
    /// Provides a convenient way to upload information about line collections and lines to GPU buffers.
    /// </summary>
    public sealed class LineCollectionStorageBufferBundle : GraphicsResource
    {
        private readonly Dictionary<string, (int, int)> _lineCollectionListToRange;

        private readonly TransferBuffer _lineDataUploadBuffer;
        private readonly TransferBuffer? _lineDataDownloadBuffer;
        public Buffer LineDataBuffer { get; }

        private readonly TransferBuffer _lineCasterDataTransferBuffer;
        public Buffer LineCollectionDataBuffer { get; }

        /// <summary>
        /// The amount of lines in the line buffer that contain valid information.
        /// </summary>
        public int ValidLineCollectionCount { get; private set; }

        public LineCollectionStorageBufferBundle(GraphicsDevice device, uint lineCount = 1024, uint lineCollectionCount = 128, bool createDownloadBuffer = false) : base(device)
        {
            _lineCollectionListToRange = [];

            _lineDataUploadBuffer = TransferBuffer.Create<CollisionLineData>(
                Device,
                TransferBufferUsage.Upload,
                lineCount
            );

            LineDataBuffer = Buffer.Create<CollisionLineData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite,
                lineCount
            );

            if (createDownloadBuffer)
            {
                _lineDataDownloadBuffer = TransferBuffer.Create<CollisionLineData>(
                    Device,
                    TransferBufferUsage.Download,
                    lineCount
                );
            }

            _lineCasterDataTransferBuffer = TransferBuffer.Create<CollisionLineCollectionData>(
                Device,
                TransferBufferUsage.Upload,
                lineCollectionCount
            );

            LineCollectionDataBuffer = Buffer.Create<CollisionLineCollectionData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite,
                lineCollectionCount
            );
        }

        /// <summary>
        /// Gets the offset and length of a body buffer segment.
        /// </summary>
        /// <param name="bufferSegmentName">The name of the buffer segment. <c>null</c> returns a range of the whole buffer.</param>
        /// <returns>The offset and length.</returns>
        public (int, int) GetLineCollectionRange(string? bufferSegmentName)
        {
            if (bufferSegmentName == null)
                return (0, ValidLineCollectionCount);
            return _lineCollectionListToRange[bufferSegmentName];
        }

        /// <summary>
        /// Uploads line collection and line information to the buffers and defines buffer segments.
        /// </summary>
        /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
        /// <param name="lineCollectionListList">A list of pairs of segment names and line collection collections.</param>
        public void UploadData(CommandBuffer commandBuffer, (string, IEnumerable<ICollisionLineCollection>)[] lineCollectionListList)
        {
            _lineCollectionListToRange.Clear();

            var lineCollectionDataUploadSpan = _lineCasterDataTransferBuffer.Map<CollisionLineCollectionData>(true);
            var lineDataUploadSpan = _lineDataUploadBuffer.Map<CollisionLineData>(true);

            int lineCollectionDataIndex = 0;
            int lineDataIndex = 0;

            foreach (var lineCollectionListItem in lineCollectionListList)
            {
                int lineCollectionListIndexStart = lineCollectionDataIndex;

                foreach (var lineCollection in lineCollectionListItem.Item2)
                {
                    lineCollectionDataUploadSpan[lineCollectionDataIndex].LineIndexStart = lineDataIndex;

                    foreach (var line in lineCollection.Lines)
                    {
                        lineDataUploadSpan[lineDataIndex].Origin = line.Origin;
                        lineDataUploadSpan[lineDataIndex].Vector = line.ArbitraryVector;
                        lineDataUploadSpan[lineDataIndex].Length = line.Length;

                        lineDataUploadSpan[lineDataIndex].Flags = 0;
                        lineDataUploadSpan[lineDataIndex].Flags |= (line.CanBeRestricted ? 1 : 0);
                        lineDataUploadSpan[lineDataIndex].Flags |= (line.IsVectorFixedPoint ? 2 : 0);

                        lineDataIndex++;
                    }

                    lineCollectionDataUploadSpan[lineCollectionDataIndex].LineIndexLength = lineDataIndex - lineCollectionDataUploadSpan[lineCollectionDataIndex].LineIndexStart;
                    lineCollectionDataUploadSpan[lineCollectionDataIndex].Offset = lineCollection.OriginOffset;
                    lineCollectionDataUploadSpan[lineCollectionDataIndex].LineVelocityIndex = lineCollection.LineVelocityIndex + lineCollectionDataUploadSpan[lineCollectionDataIndex].LineIndexStart;

                    lineCollectionDataIndex++;
                }

                _lineCollectionListToRange.Add(lineCollectionListItem.Item1, (lineCollectionListIndexStart, lineCollectionDataIndex - lineCollectionListIndexStart));
            }

            _lineCasterDataTransferBuffer.Unmap();
            _lineDataUploadBuffer.Unmap();

            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.UploadToBuffer(_lineCasterDataTransferBuffer, LineCollectionDataBuffer, true);
            copyPass.UploadToBuffer(_lineDataUploadBuffer, LineDataBuffer, true);
            commandBuffer.EndCopyPass(copyPass);

            ValidLineCollectionCount = lineCollectionDataIndex;
        }

        public void DownloadData(CommandBuffer commandBuffer)
        {
            if (_lineDataDownloadBuffer == null)
            {
                throw new NullReferenceException($"{nameof(_lineDataDownloadBuffer)} is null!");
            }

            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.DownloadFromBuffer(LineDataBuffer, _lineDataDownloadBuffer);
            commandBuffer.EndCopyPass(copyPass);
        }

        public IEnumerable<(ICollisionLineCollection, IList<CollisionLine>)> GetData(IEnumerable<ICollisionLineCollection> lineCollectionList)
        {
            if (_lineDataDownloadBuffer == null)
            {
                throw new NullReferenceException($"{nameof(_lineDataDownloadBuffer)} is null!");
            }

            var transferDownloadSpan = _lineDataDownloadBuffer.Map<CollisionLineData>(true);

            List<(ICollisionLineCollection, List<CollisionLine>)> resultList = [];

            int i = 0;
            foreach (var lineCollection in lineCollectionList)
            {
                List<CollisionLine> lines = [];
                foreach (var line in lineCollection.Lines)
                {
                    lines.Add(line with
                    {
                        Origin = transferDownloadSpan[i].Origin,
                        ArbitraryVector = transferDownloadSpan[i].Vector,
                        Length = transferDownloadSpan[i].Length,
                        CanBeRestricted = (transferDownloadSpan[i].Flags & 1) == 1,
                        IsVectorFixedPoint = (transferDownloadSpan[i].Flags & 2) == 2,
                    });

                    i++;
                }
                resultList.Add((lineCollection, lines));
            }

            _lineDataDownloadBuffer.Unmap();

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
                    _lineDataUploadBuffer.Dispose();
                    _lineDataDownloadBuffer?.Dispose();
                    LineDataBuffer.Dispose();
                    _lineCasterDataTransferBuffer.Dispose();
                    LineCollectionDataBuffer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
