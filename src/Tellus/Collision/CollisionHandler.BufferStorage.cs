﻿using MoonWorks.Graphics;
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

        public TransferBuffer CollisionResultsTransferUploadBuffer { get; private set; }
        public TransferBuffer CollisionResultsTransferDownloadBuffer { get; private set; }
        public Buffer CollisionResultsBuffer { get; private set; }

        public uint BodyPartAmount { get; private set; }
        public uint CollisionResultAmount { get; private set; }

        public StorageBuffer(GraphicsDevice device, uint bodyPartAmount = 2048, uint collisionResultAmount = 2048) : base(device)
        {
            BodyPartDataTransferBufferOne = TransferBuffer.Create<CollisionBodyPartData>(
                Device,
                TransferBufferUsage.Upload,
                bodyPartAmount
            );

            BodyPartDataTransferBufferTwo = TransferBuffer.Create<CollisionBodyPartData>(
                Device,
                TransferBufferUsage.Upload,
                bodyPartAmount
            );

            BodyPartDataBufferOne = Buffer.Create<CollisionBodyPartData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                bodyPartAmount
            );

            BodyPartDataBufferTwo = Buffer.Create<CollisionBodyPartData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                bodyPartAmount
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

            BodyPartAmount = bodyPartAmount;
            CollisionResultAmount = collisionResultAmount;
        }

        public void ResizeBodyPartBuffers(uint newBodyPartAmount)
        {
            BodyPartDataTransferBufferOne = TransferBuffer.Create<CollisionBodyPartData>(
                Device,
                TransferBufferUsage.Upload,
                newBodyPartAmount
            );

            BodyPartDataTransferBufferTwo = TransferBuffer.Create<CollisionBodyPartData>(
                Device,
                TransferBufferUsage.Upload,
                newBodyPartAmount
            );

            BodyPartDataBufferOne = Buffer.Create<CollisionBodyPartData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                newBodyPartAmount
            );

            BodyPartDataBufferTwo = Buffer.Create<CollisionBodyPartData>
            (
                Device,
                BufferUsageFlags.ComputeStorageRead,
                newBodyPartAmount
            );

            BodyPartAmount = newBodyPartAmount;
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
                    CollisionResultsTransferUploadBuffer.Dispose();
                    CollisionResultsTransferDownloadBuffer.Dispose();
                    CollisionResultsBuffer.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
