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
    public sealed class StorageBuffer : GraphicsResource
    {
        public TransferBuffer BodyPartDataTransferBufferOne { get; private set; }
        public TransferBuffer BodyPartDataTransferBufferTwo { get; private set; }
        public Buffer BodyPartDataBufferOne { get; private set; }
        public Buffer BodyPartDataBufferTwo { get; private set; }

        public TransferBuffer BodyDataTransferBufferOne { get; private set; }
        public TransferBuffer BodyDataTransferBufferTwo { get; private set; }
        public Buffer BodyDataBufferOne { get; private set; }
        public Buffer BodyDataBufferTwo { get; private set; }

        public TransferBuffer CollisionResultsTransferUploadBuffer { get; private set; }
        public TransferBuffer CollisionResultsTransferDownloadBuffer { get; private set; }
        public Buffer CollisionResultsBuffer { get; private set; }

        public uint BodyPartCountOne { get; private set; }
        public uint BodyCountOne { get; private set; }
        public uint BodyPartCountTwo { get; private set; }
        public uint BodyCountTwo { get; private set; }
        public uint CollisionResultAmount { get; private set; }

        public StorageBuffer(GraphicsDevice device, uint bodyPartCountOne = 2048, uint bodyCountOne = 128, uint bodyPartCountTwo = 2048, uint bodyCountTwo = 128, uint collisionResultAmount = 2048) : base(device)
        {
            BodyPartDataTransferBufferOne = TransferBuffer.Create<CollisionBodyPartData>(
                Device,
                TransferBufferUsage.Upload,
                bodyPartCountOne
            );

            BodyPartDataTransferBufferTwo = TransferBuffer.Create<CollisionBodyPartData>(
                Device,
                TransferBufferUsage.Upload,
                bodyPartCountTwo
            );

            BodyPartDataBufferOne = Buffer.Create<CollisionBodyPartData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                bodyPartCountOne
            );

            BodyPartDataBufferTwo = Buffer.Create<CollisionBodyPartData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                bodyPartCountTwo
            );

            BodyDataTransferBufferOne = TransferBuffer.Create<CollisionBodyData>(
                Device,
                TransferBufferUsage.Upload,
                bodyCountOne
            );

            BodyDataTransferBufferTwo = TransferBuffer.Create<CollisionBodyData>(
                Device,
                TransferBufferUsage.Upload,
                bodyCountTwo
            );

            BodyDataBufferOne = Buffer.Create<CollisionBodyData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                bodyCountOne
            );

            BodyDataBufferTwo = Buffer.Create<CollisionBodyData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                bodyCountTwo
            );

            CollisionResultsTransferUploadBuffer = TransferBuffer.Create<CollisionResultData>(
                Device,
                TransferBufferUsage.Upload,
                collisionResultAmount + 1
            );

            CollisionResultsTransferDownloadBuffer = TransferBuffer.Create<CollisionResultData>(
                Device,
                TransferBufferUsage.Download,
                collisionResultAmount + 1
            );

            CollisionResultsBuffer = Buffer.Create<CollisionResultData>
            (
                Device,
                BufferUsageFlags.ComputeStorageWrite,
                collisionResultAmount + 1
            );

            var transferUploadSpan = CollisionResultsTransferUploadBuffer.Map<int>(false);
            for (int i = 0; i < collisionResultAmount + 1; i += 1)
            {
                transferUploadSpan[i] = 0;
            }
            CollisionResultsTransferUploadBuffer.Unmap();

            BodyPartCountOne = bodyPartCountOne;
            BodyCountOne = bodyCountOne;
            BodyPartCountTwo = bodyPartCountTwo;
            BodyCountTwo = bodyCountTwo;
            CollisionResultAmount = collisionResultAmount;
        }

        public void ResizeBodyPartBuffersOne(uint newBodyPartCount)
        {
            BodyPartDataTransferBufferOne = TransferBuffer.Create<CollisionBodyPartData>(
                Device,
                TransferBufferUsage.Upload,
                newBodyPartCount
            );

            BodyPartDataBufferOne = Buffer.Create<CollisionBodyPartData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                newBodyPartCount
            );

            BodyPartCountOne = newBodyPartCount;
        }

        public void ResizeBodyBuffersOne(uint newBodyCount)
        {
            BodyDataTransferBufferOne = TransferBuffer.Create<CollisionBodyData>(
                Device,
                TransferBufferUsage.Upload,
                newBodyCount
            );

            BodyDataBufferOne = Buffer.Create<CollisionBodyData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                newBodyCount
            );

            BodyCountOne = newBodyCount;
        }

        public void ResizeBodyPartBuffersTwo(uint newBodyPartCount)
        {
            BodyPartDataTransferBufferTwo = TransferBuffer.Create<CollisionBodyPartData>(
                Device,
                TransferBufferUsage.Upload,
                newBodyPartCount
            );

            BodyPartDataBufferTwo = Buffer.Create<CollisionBodyPartData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                newBodyPartCount
            );

            BodyPartCountTwo = newBodyPartCount;
        }

        public void ResizeBodyBuffersTwo(uint newBodyCount)
        {
            BodyDataTransferBufferTwo = TransferBuffer.Create<CollisionBodyData>(
                Device,
                TransferBufferUsage.Upload,
                newBodyCount
            );

            BodyDataBufferTwo = Buffer.Create<CollisionBodyData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                newBodyCount
            );

            BodyCountTwo = newBodyCount;
        }

        public void ResizeCollisionResultBuffers(uint newCollisionResultAmount)
        {
            CollisionResultsTransferUploadBuffer = TransferBuffer.Create<CollisionResultData>(
                Device,
                TransferBufferUsage.Upload,
                newCollisionResultAmount + 1
            );

            CollisionResultsTransferDownloadBuffer = TransferBuffer.Create<CollisionResultData>(
                Device,
                TransferBufferUsage.Download,
                newCollisionResultAmount + 1
            );

            CollisionResultsBuffer = Buffer.Create<CollisionResultData>
            (
                Device,
                BufferUsageFlags.ComputeStorageWrite,
                newCollisionResultAmount + 1
            );

            var transferUploadSpan = CollisionResultsTransferUploadBuffer.Map<int>(false);
            for (int i = 0; i < newCollisionResultAmount + 1; i += 1)
            {
                transferUploadSpan[i] = 0;
            }
            CollisionResultsTransferUploadBuffer.Unmap();

            CollisionResultAmount = newCollisionResultAmount;
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    BodyPartDataTransferBufferOne.Dispose();
                    BodyPartDataTransferBufferTwo.Dispose();
                    BodyPartDataBufferOne.Dispose();
                    BodyPartDataBufferTwo.Dispose();
                    BodyDataTransferBufferOne.Dispose();
                    BodyDataTransferBufferTwo.Dispose();
                    BodyDataBufferOne.Dispose();
                    BodyDataBufferTwo.Dispose();
                    CollisionResultsTransferUploadBuffer.Dispose();
                    CollisionResultsTransferDownloadBuffer.Dispose();
                    CollisionResultsBuffer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
