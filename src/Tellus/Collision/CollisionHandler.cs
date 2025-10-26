using MoonWorks;
using MoonWorks.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed class CollisionHandler : GraphicsResource
{
    [StructLayout(LayoutKind.Explicit, Size = 12)]
    private struct ColliderShapeData
    {
        [FieldOffset(0)]
        public int ColliderIndex;

        [FieldOffset(4)]
        public int ShapeIndexRangeStart;

        [FieldOffset(8)]
        public int ShapeIndexRangeRangeLength;
    }

    private readonly ComputePipeline _computePipeline;

    private readonly TransferBuffer _shapeVertexTransferBufferOne;
    private readonly TransferBuffer _shapeVertexTransferBufferTwo;
    private readonly Buffer _shapeVertexBufferOne;
    private readonly Buffer _shapeVertexBufferTwo;

    private readonly TransferBuffer _shapeIndexRangeTransferBufferOne;
    private readonly TransferBuffer _shapeIndexRangeTransferBufferTwo;
    private readonly Buffer _shapeIndexRangeBufferOne;
    private readonly Buffer _shapeIndexRangeBufferTwo;

    private readonly TransferBuffer _collisionResultsTransferUploadBuffer;
    private readonly TransferBuffer _collisionResultsTransferDownloadBuffer;
    private readonly Buffer _collisionResultsBuffer;

    record struct CollisionComputeUniforms(int ShapeIndexRangeBufferOneLength, int ShapeIndexRangeBufferTwoLength, int ColliderShapeResultBufferLength);
    private CollisionComputeUniforms _collisionComputeUniforms;

    private const int SHAPE_VERTEX_AMOUNT = 2048;
    private const int SHAPE_INDEX_RANGE_AMOUNT = 1024;
    private const int COLLIDER_SHAPE_CONTAINER_AMOUNT = 100;
    private const int COLLISION_RESULT_AMOUNT = COLLIDER_SHAPE_CONTAINER_AMOUNT * COLLIDER_SHAPE_CONTAINER_AMOUNT;

    public CollisionHandler(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        Utils.LoadShaderFromManifest(Device, "Assets.ComputeCollisions.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 4,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 16,
            ThreadCountZ = 1
        }, out _computePipeline);

        _shapeVertexTransferBufferOne = TransferBuffer.Create<Vector2>(
            Device,
            TransferBufferUsage.Upload,
            SHAPE_VERTEX_AMOUNT
        );

        _shapeVertexTransferBufferTwo = TransferBuffer.Create<Vector2>(
            Device,
            TransferBufferUsage.Upload,
            SHAPE_VERTEX_AMOUNT
        );

        _shapeVertexBufferOne = Buffer.Create<Vector2>
        (
            Device,
            BufferUsageFlags.ComputeStorageRead,
            SHAPE_VERTEX_AMOUNT
        );

        _shapeVertexBufferTwo = Buffer.Create<Vector2>
        (
            Device,
            BufferUsageFlags.ComputeStorageRead,
            SHAPE_VERTEX_AMOUNT
        );

        _shapeIndexRangeTransferBufferOne = TransferBuffer.Create<ColliderShapeData>(
            Device,
            TransferBufferUsage.Upload,
            SHAPE_INDEX_RANGE_AMOUNT
        );

        _shapeIndexRangeTransferBufferTwo = TransferBuffer.Create<ColliderShapeData>(
            Device,
            TransferBufferUsage.Upload,
            SHAPE_INDEX_RANGE_AMOUNT
        );

        _shapeIndexRangeBufferOne = Buffer.Create<ColliderShapeData>
        (
            Device,
            BufferUsageFlags.ComputeStorageRead,
            SHAPE_INDEX_RANGE_AMOUNT
        );

        _shapeIndexRangeBufferTwo = Buffer.Create<ColliderShapeData>
        (
            Device,
            BufferUsageFlags.ComputeStorageRead,
            SHAPE_INDEX_RANGE_AMOUNT
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
            transferUploadSpan[i] = 0;
        }
        _collisionResultsTransferUploadBuffer.Unmap();

        _collisionComputeUniforms.ColliderShapeResultBufferLength = COLLISION_RESULT_AMOUNT;
    }

    public IEnumerable<(IHasColliderShapes, IHasColliderShapes)> HandleCollisions(IList<IHasColliderShapes> colliderShapesGroupOne, IList<IHasColliderShapes> colliderShapesGroupTwo)
    {
        CommandBuffer commandBuffer = Device.AcquireCommandBuffer();


        int MapGroupToBuffer(IEnumerable<IHasColliderShapes> colliderGroup, TransferBuffer vertexTransferBuffer, TransferBuffer indexRangeTransferBuffer)
        {
            var vertexUploadSpan = vertexTransferBuffer.Map<Vector2>(true);
            var indexRangeUploadSpan = indexRangeTransferBuffer.Map<ColliderShapeData>(true);

            var colliderIndex = 0;
            var vertexIndex = 0;
            var indexRangeIndex = 0;

            foreach (var collider in colliderGroup)
            {
                foreach (var vertex in collider.ShapeVertices)
                {
                    vertexUploadSpan[vertexIndex] = vertex + collider.ShapeOffset;
                    vertexIndex++;
                }
                foreach (var indexPair in collider.ShapeIndexRanges)
                {
                    indexRangeUploadSpan[indexRangeIndex].ColliderIndex = colliderIndex;
                    indexRangeUploadSpan[indexRangeIndex].ShapeIndexRangeStart = indexPair.Item1 + vertexIndex;
                    indexRangeUploadSpan[indexRangeIndex].ShapeIndexRangeRangeLength = indexPair.Item2;
                    indexRangeIndex++;
                }
                colliderIndex++;
            }

            vertexTransferBuffer.Unmap();
            indexRangeTransferBuffer.Unmap();

            return indexRangeIndex;
        }

        var collisionResultCopyPass = commandBuffer.BeginCopyPass();
        collisionResultCopyPass.UploadToBuffer(_collisionResultsTransferUploadBuffer, _collisionResultsBuffer, true);
        commandBuffer.EndCopyPass(collisionResultCopyPass);

        _collisionComputeUniforms.ShapeIndexRangeBufferOneLength = MapGroupToBuffer(colliderShapesGroupOne, _shapeVertexTransferBufferOne, _shapeIndexRangeTransferBufferOne);
        _collisionComputeUniforms.ShapeIndexRangeBufferTwoLength = MapGroupToBuffer(colliderShapesGroupTwo, _shapeVertexTransferBufferTwo, _shapeIndexRangeTransferBufferTwo);

        var copyPass = commandBuffer.BeginCopyPass();
        copyPass.UploadToBuffer(_shapeVertexTransferBufferOne, _shapeVertexBufferOne, true);
        copyPass.UploadToBuffer(_shapeIndexRangeTransferBufferOne, _shapeIndexRangeBufferOne, true);
        copyPass.UploadToBuffer(_shapeVertexTransferBufferTwo, _shapeVertexBufferTwo, true);
        copyPass.UploadToBuffer(_shapeIndexRangeTransferBufferTwo, _shapeIndexRangeBufferTwo, true);
        commandBuffer.EndCopyPass(copyPass);


        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(_collisionResultsBuffer, true)
        );
        computePass.BindComputePipeline(_computePipeline);
        computePass.BindStorageBuffers(_shapeVertexBufferOne, _shapeVertexBufferTwo, _shapeIndexRangeBufferOne, _shapeIndexRangeBufferTwo);
        commandBuffer.PushComputeUniformData(_collisionComputeUniforms);
        computePass.Dispatch(((uint)_collisionComputeUniforms.ShapeIndexRangeBufferOneLength + 15) / 16, ((uint)_collisionComputeUniforms.ShapeIndexRangeBufferTwoLength + 15) / 16, 1);
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
            int indexTwo = (int)((float)i / COLLIDER_SHAPE_CONTAINER_AMOUNT);

            bool resultIsUnique = !resultIndexList.Contains((indexOne, indexTwo));
            bool collidersHaveCollided = collisionAmount != 0;

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
                _shapeVertexTransferBufferOne.Dispose();
                _shapeVertexTransferBufferTwo.Dispose();
                _shapeVertexBufferOne.Dispose();
                _shapeVertexBufferTwo.Dispose();
                _shapeIndexRangeTransferBufferOne.Dispose();
                _shapeIndexRangeTransferBufferTwo.Dispose();
                _shapeIndexRangeBufferOne.Dispose();
                _shapeIndexRangeBufferTwo.Dispose();
                _collisionResultsTransferUploadBuffer.Dispose();
                _collisionResultsTransferDownloadBuffer.Dispose();
                _collisionResultsBuffer.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
