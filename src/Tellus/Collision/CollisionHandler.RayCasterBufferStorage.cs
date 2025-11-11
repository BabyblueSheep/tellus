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
    public sealed class RayCasterBufferStorage : GraphicsResource
    {
        private readonly Dictionary<string, (int, int)> _rayCasterListToRange;

        private readonly TransferBuffer _rayDataUploadBuffer;
        private readonly TransferBuffer? _rayDataDownloadBuffer;
        public Buffer RayDataBuffer { get; }

        private readonly TransferBuffer _rayCasterDataTransferBuffer;
        public Buffer RayCasterDataBuffer { get; }

        public int ValidRayCasterCount { get; private set; }

        public RayCasterBufferStorage(GraphicsDevice device, uint rayCount = 1024, uint rayCasterCount = 128, bool createDownloadBuffer = false) : base(device)
        {
            _rayCasterListToRange = [];

            _rayDataUploadBuffer = TransferBuffer.Create<CollisionRayData>(
                Device,
                TransferBufferUsage.Upload,
                rayCount
            );

            RayDataBuffer = Buffer.Create<CollisionRayData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite,
                rayCount
            );

            if (createDownloadBuffer)
            {
                _rayDataDownloadBuffer = TransferBuffer.Create<CollisionRayData>(
                    Device,
                    TransferBufferUsage.Download,
                    rayCount
                );
            }

            _rayCasterDataTransferBuffer = TransferBuffer.Create<CollisionRayCasterData>(
                Device,
                TransferBufferUsage.Upload,
                rayCasterCount
            );

            RayCasterDataBuffer = Buffer.Create<CollisionRayCasterData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead | BufferUsageFlags.ComputeStorageWrite,
                rayCasterCount
            );
        }

        public (int, int) GetRayCasterRange(string? bodyName)
        {
            if (bodyName == null)
                return (0, ValidRayCasterCount);
            return _rayCasterListToRange[bodyName];
        }

        public void UploadData(CommandBuffer commandBuffer, (string, IEnumerable<ICollisionRayCaster>)[] rayCasterListList)
        {
            _rayCasterListToRange.Clear();

            var rayCasterDataUploadSpan = _rayCasterDataTransferBuffer.Map<CollisionRayCasterData>(true);
            var rayDataUploadSpan = _rayDataUploadBuffer.Map<CollisionRayData>(true);

            int rayCasterDataIndex = 0;
            int rayDataIndex = 0;

            foreach (var rayCasterListItem in rayCasterListList)
            {
                int rayCasterListIndexStart = rayCasterDataIndex;

                foreach (var rayCaster in rayCasterListItem.Item2)
                {
                    rayCasterDataUploadSpan[rayCasterDataIndex].RayIndexStart = rayDataIndex;

                    foreach (var ray in rayCaster.Rays)
                    {
                        rayDataUploadSpan[rayDataIndex].RayOrigin = ray.RayOrigin;
                        rayDataUploadSpan[rayDataIndex].RayDirection = ray.RayDirection;
                        rayDataUploadSpan[rayDataIndex].RayLength = ray.RayLength;

                        rayDataUploadSpan[rayDataIndex].Flags = 0;
                        rayDataUploadSpan[rayDataIndex].Flags |= (ray.CanBeRestricted ? 1 : 0);

                        rayDataIndex++;
                    }

                    rayCasterDataUploadSpan[rayCasterDataIndex].RayIndexLength = rayDataIndex - rayCasterDataUploadSpan[rayCasterDataIndex].RayIndexStart;
                    rayCasterDataUploadSpan[rayCasterDataIndex].Offset = rayCaster.RayOriginOffset;
                    rayCasterDataUploadSpan[rayCasterDataIndex].RayVelocityIndex = rayCaster.RayVelocityIndex + rayCasterDataUploadSpan[rayCasterDataIndex].RayIndexStart;

                    rayCasterDataIndex++;
                }

                _rayCasterListToRange.Add(rayCasterListItem.Item1, (rayCasterListIndexStart, rayCasterDataIndex - rayCasterListIndexStart));
            }

            _rayCasterDataTransferBuffer.Unmap();
            _rayDataUploadBuffer.Unmap();

            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.UploadToBuffer(_rayCasterDataTransferBuffer, RayCasterDataBuffer, true);
            copyPass.UploadToBuffer(_rayDataUploadBuffer, RayDataBuffer, true);
            commandBuffer.EndCopyPass(copyPass);

            ValidRayCasterCount = rayCasterDataIndex;
        }

        public void DownloadData(CommandBuffer commandBuffer)
        {
            if (_rayDataDownloadBuffer == null)
            {
                throw new NullReferenceException($"{nameof(_rayDataDownloadBuffer)} is null!");
            }

            var copyPass = commandBuffer.BeginCopyPass();
            copyPass.DownloadFromBuffer(RayDataBuffer, _rayDataDownloadBuffer);
            commandBuffer.EndCopyPass(copyPass);
        }

        public IEnumerable<(ICollisionRayCaster, IList<CollisionRay>)> GetData(IEnumerable<ICollisionRayCaster> rayCasterList)
        {
            if (_rayDataDownloadBuffer == null)
            {
                throw new NullReferenceException($"{nameof(_rayDataDownloadBuffer)} is null!");
            }

            var transferDownloadSpan = _rayDataDownloadBuffer.Map<CollisionRayData>(true);

            List<(ICollisionRayCaster, List<CollisionRay>)> resultList = [];

            int i = 0;
            foreach (var rayCaster in rayCasterList)
            {
                List<CollisionRay> rays = [];
                foreach (var ray in rayCaster.Rays)
                {
                    rays.Add(ray with
                    {
                        RayOrigin = transferDownloadSpan[i].RayOrigin,
                        RayDirection = transferDownloadSpan[i].RayDirection,
                        RayLength = transferDownloadSpan[i].RayLength,
                    });
                    i++;
                }
                resultList.Add((rayCaster, rays));
            }

            _rayDataDownloadBuffer.Unmap();

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
                    _rayDataUploadBuffer.Dispose();
                    _rayDataDownloadBuffer?.Dispose();
                    RayDataBuffer.Dispose();
                    _rayCasterDataTransferBuffer.Dispose();
                    RayCasterDataBuffer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
