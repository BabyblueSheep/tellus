using MoonWorks;
using MoonWorks.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Mail;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

// THINGS TO DOCUMENT:
// max shapes allowed: SHAPE_DATA_AMOUNT (2048) (hard limit but i may expand)
// max colliders allowed: COLLIDER_SHAPE_CONTAINER_AMOUNT (100) (todo: figure out how to not have maximum collider amount for collision result buffer)
// max vertices a shape can have: 16 (probably hard limit?)

public sealed class CollisionHandler : GraphicsResource
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    private struct ColliderShapeData
    {
        [FieldOffset(0)]
        public int ColliderIndex;

        [FieldOffset(4)]
        public int ShapeType;

        [FieldOffset(8)]
        public Vector2 Center;

        [FieldOffset(16)]
        public Vector4 DecimalFields;

        [FieldOffset(32)]
        public Point IntegerFields;

        [FieldOffset(40)]
        public int Padding1;

        [FieldOffset(44)]
        public int Padding2;
    }

    private readonly ComputePipeline _computePipeline;

    private readonly TransferBuffer _shapeDataTransferBufferOne;
    private readonly TransferBuffer _shapeDataTransferBufferTwo;
    private readonly Buffer _shapeDataBufferOne;
    private readonly Buffer _shapeDataBufferTwo;

    private readonly TransferBuffer _collisionResultsTransferUploadBuffer;
    private readonly TransferBuffer _collisionResultsTransferDownloadBuffer;
    private readonly Buffer _collisionResultsBuffer;

    record struct CollisionComputeUniforms(int ShapeDataBufferOneLength, int ShapeDataBufferTwoLength, int ColliderShapeResultBufferLength);
    private CollisionComputeUniforms _collisionComputeUniforms;

    private const int SHAPE_DATA_AMOUNT = 2048;
    private const int COLLIDER_SHAPE_CONTAINER_AMOUNT = 100;
    private const int COLLISION_RESULT_AMOUNT = COLLIDER_SHAPE_CONTAINER_AMOUNT * COLLIDER_SHAPE_CONTAINER_AMOUNT;

    public CollisionHandler(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        Utils.LoadShaderFromManifest(Device, "Assets.ComputeCollisions.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 2,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 16,
            ThreadCountZ = 1
        }, out _computePipeline);

        _shapeDataTransferBufferOne = TransferBuffer.Create<ColliderShapeData>(
            Device,
            TransferBufferUsage.Upload,
            SHAPE_DATA_AMOUNT
        );

        _shapeDataTransferBufferTwo = TransferBuffer.Create<ColliderShapeData>(
            Device,
            TransferBufferUsage.Upload,
            SHAPE_DATA_AMOUNT
        );

        _shapeDataBufferOne = Buffer.Create<ColliderShapeData>
        (
            Device,
            BufferUsageFlags.ComputeStorageRead,
            SHAPE_DATA_AMOUNT
        );

        _shapeDataBufferTwo = Buffer.Create<ColliderShapeData>
        (
            Device,
            BufferUsageFlags.ComputeStorageRead,
            SHAPE_DATA_AMOUNT
        );

        _collisionResultsTransferUploadBuffer = TransferBuffer.Create<int>(
            Device,
            TransferBufferUsage.Upload,
            COLLISION_RESULT_AMOUNT
        );

        _collisionResultsTransferDownloadBuffer = TransferBuffer.Create<int>(
            Device,
            TransferBufferUsage.Download,
            COLLISION_RESULT_AMOUNT
        );

        _collisionResultsBuffer = Buffer.Create<int>
        (
            Device,
            BufferUsageFlags.ComputeStorageWrite,
            COLLISION_RESULT_AMOUNT
        );

        var transferUploadSpan = _collisionResultsTransferUploadBuffer.Map<int>(false);
        for (int i = 0; i < COLLISION_RESULT_AMOUNT; i += 1)
        {
            transferUploadSpan[i] = -1;
        }
        _collisionResultsTransferUploadBuffer.Unmap();

        _collisionComputeUniforms.ColliderShapeResultBufferLength = COLLIDER_SHAPE_CONTAINER_AMOUNT;
    }

    public IEnumerable<(IHasColliderShapes, IHasColliderShapes)> HandleCollisions(IList<IHasColliderShapes> colliderShapesGroupOne, IList<IHasColliderShapes> colliderShapesGroupTwo)
    {
        CommandBuffer commandBuffer = Device.AcquireCommandBuffer();


        int MapGroupToBuffer(IEnumerable<IHasColliderShapes> colliderGroup, TransferBuffer dataTransferBuffer)
        {
            var dataUploadSpan = dataTransferBuffer.Map<ColliderShapeData>(true);

            var colliderIndex = 0;
            var shapeDataIndex = 0;

            foreach (var collider in colliderGroup)
            {
                foreach (var shape in collider.Shapes)
                {
                    dataUploadSpan[shapeDataIndex].ColliderIndex = colliderIndex;
                    dataUploadSpan[shapeDataIndex].ShapeType = shape.ShapeType;
                    dataUploadSpan[shapeDataIndex].Center = shape.ShapeCenter + collider.ShapeOffset;
                    dataUploadSpan[shapeDataIndex].DecimalFields = shape.ShapeDecimalFields;
                    dataUploadSpan[shapeDataIndex].IntegerFields = shape.ShapeIntegerFields;
                    shapeDataIndex++;
                }

                colliderIndex++;
            }

            dataTransferBuffer.Unmap();

            return shapeDataIndex;
        }

        var collisionResultCopyPass = commandBuffer.BeginCopyPass();
        collisionResultCopyPass.UploadToBuffer(_collisionResultsTransferUploadBuffer, _collisionResultsBuffer, true);
        commandBuffer.EndCopyPass(collisionResultCopyPass);

        _collisionComputeUniforms.ShapeDataBufferOneLength = MapGroupToBuffer(colliderShapesGroupOne, _shapeDataTransferBufferOne);
        _collisionComputeUniforms.ShapeDataBufferTwoLength = MapGroupToBuffer(colliderShapesGroupTwo, _shapeDataTransferBufferTwo);

        var copyPass = commandBuffer.BeginCopyPass();
        copyPass.UploadToBuffer(_shapeDataTransferBufferOne, _shapeDataBufferOne, true);
        copyPass.UploadToBuffer(_shapeDataTransferBufferTwo, _shapeDataBufferTwo, true);
        commandBuffer.EndCopyPass(copyPass);


        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(_collisionResultsBuffer, false)
        );
        computePass.BindComputePipeline(_computePipeline);
        computePass.BindStorageBuffers(_shapeDataBufferOne, _shapeDataBufferTwo);
        commandBuffer.PushComputeUniformData(_collisionComputeUniforms);
        computePass.Dispatch(((uint)_collisionComputeUniforms.ShapeDataBufferOneLength + 15) / 16, ((uint)_collisionComputeUniforms.ShapeDataBufferTwoLength + 15) / 16, 1);
        commandBuffer.EndComputePass(computePass);

        copyPass = commandBuffer.BeginCopyPass();
        copyPass.DownloadFromBuffer(_collisionResultsBuffer, _collisionResultsTransferDownloadBuffer);
        commandBuffer.EndCopyPass(copyPass);

        var fence = Device.SubmitAndAcquireFence(commandBuffer);
        Device.WaitForFence(fence);
        Device.ReleaseFence(fence);


        var transferDownloadSpan = _collisionResultsTransferDownloadBuffer.Map<int>(true);

        List<(IHasColliderShapes, IHasColliderShapes)> resultList = [];
        List<(int, int)> resultIndexList = [];
        for (int i = 0; i < transferDownloadSpan.Length; i++)
        {
            int collisionAmount = transferDownloadSpan[i];
            int indexOne = i % COLLIDER_SHAPE_CONTAINER_AMOUNT;
            int indexTwo = i / COLLIDER_SHAPE_CONTAINER_AMOUNT;

            bool resultIsUnique = !resultIndexList.Contains((indexOne, indexTwo));
            bool collidersHaveCollided = collisionAmount != -1;

            if (resultIsUnique && collidersHaveCollided)
            {
                resultList.Add((colliderShapesGroupOne[indexOne], colliderShapesGroupTwo[indexTwo]));
                resultIndexList.Add((indexOne, indexTwo));
            }
        }

        _collisionResultsTransferDownloadBuffer.Unmap();

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
                _computePipeline.Dispose();
                _shapeDataTransferBufferOne.Dispose();
                _shapeDataTransferBufferOne.Dispose();
                _shapeDataBufferOne.Dispose();
                _shapeDataBufferTwo.Dispose();
                _collisionResultsTransferUploadBuffer.Dispose();
                _collisionResultsTransferDownloadBuffer.Dispose();
                _collisionResultsBuffer.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
