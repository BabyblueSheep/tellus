using MoonWorks;
using MoonWorks.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed class CollisionHandler : GraphicsResource
{
    [StructLayout(LayoutKind.Explicit, Size = 28)]
    private struct ColliderShapeData
    {
        [FieldOffset(0)]
        public int ColliderIndex;

        [FieldOffset(4)]
        public uint Type;

        [FieldOffset(8)]
        public Vector2 Center;

        [FieldOffset(16)]
        public Vector3 Fields;
    }

    private readonly ComputePipeline _computePipeline;

    private readonly TransferBuffer _colliderShapesTransferBuffer;
    private readonly Buffer _colliderShapesBufferOne;
    private readonly Buffer _colliderShapesBufferTwo;

    private readonly TransferBuffer _collisionResultsTransferUploadBuffer;
    private readonly TransferBuffer _collisionResultsTransferDownloadBuffer;
    private readonly Buffer _collisionResultsBufferOne;
    private readonly Buffer _collisionResultsBufferTwo;
    private readonly int[] _defaultCollisionResults;
    private readonly int[] _collisionResultTransition;

    record struct CollisionComputeUniforms(int ColliderShapeBufferOneLength, int ColliderShapeBufferTwoLength);
    private CollisionComputeUniforms _collisionComputeUniforms;

    private const int COLLIDER_SHAPE_AMOUNT = 1024;
    private const int COLLIDER_SHAPE_CONTAINER_AMOUNT = 100;
    private const int COLLISION_RESULT_AMOUNT = COLLIDER_SHAPE_CONTAINER_AMOUNT * COLLIDER_SHAPE_CONTAINER_AMOUNT;

    public CollisionHandler(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        Utils.LoadShaderFromManifest(Device, "Assets.ComputeCollisions.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 2,
            NumReadWriteStorageBuffers = 2,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 16,
            ThreadCountZ = 1
        }, out _computePipeline);

        _colliderShapesTransferBuffer = TransferBuffer.Create<ColliderShapeData>(
            Device,
            TransferBufferUsage.Upload,
            COLLIDER_SHAPE_AMOUNT
        );

        _colliderShapesBufferOne = Buffer.Create<ColliderShapeData>
        (
            Device,
            BufferUsageFlags.ComputeStorageRead,
            COLLIDER_SHAPE_AMOUNT
        );

        _colliderShapesBufferTwo = Buffer.Create<ColliderShapeData>
        (
            Device,
            BufferUsageFlags.ComputeStorageRead,
            COLLIDER_SHAPE_AMOUNT
        );

        _collisionResultsBufferOne = Buffer.Create<int>
        (
            Device,
            BufferUsageFlags.ComputeStorageWrite,
            COLLISION_RESULT_AMOUNT
        );

        _collisionResultsBufferTwo = Buffer.Create<int>
        (
            Device,
            BufferUsageFlags.ComputeStorageWrite,
            COLLISION_RESULT_AMOUNT
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

        _defaultCollisionResults = Enumerable.Repeat(-1, COLLISION_RESULT_AMOUNT).ToArray();
        _collisionResultTransition = new int[COLLISION_RESULT_AMOUNT];
    }

    
    public List<(IHasColliderShapes, IHasColliderShapes)> HandleCircleCircleCollision(List<IHasColliderShapes> colliderShapesGroupOne, List<IHasColliderShapes> colliderShapesGroupTwo)
    {
        CommandBuffer commandBuffer = Device.AcquireCommandBuffer();

        int MapGroupToBuffer(List<IHasColliderShapes> group)
        {
            var uploadColliderShapes = _colliderShapesTransferBuffer.Map<ColliderShapeData>(false);
            int index = 0;
            for (int i = 0; i < group.Count; i++)
            {
                foreach (var colliderShape in group[i].GetColliderShapes())
                {
                    uploadColliderShapes[index].ColliderIndex = i;
                    if (colliderShape is CircleCollider circleColliderShape)
                    {
                        uploadColliderShapes[index].Type = 0;
                        uploadColliderShapes[index].Center = circleColliderShape.Center;
                        uploadColliderShapes[index].Fields = new Vector3(circleColliderShape.Radius, 0, 0);
                    }
                    else if (colliderShape is RectangleCollider rectangleCollider)
                    {
                        uploadColliderShapes[index].Type = 1;
                        uploadColliderShapes[index].Center = rectangleCollider.Center;
                        uploadColliderShapes[index].Fields = new Vector3(rectangleCollider.Angle, rectangleCollider.SideHalfLength, rectangleCollider.SideHalfWidth);
                    }

                    index++;
                }
            }
            _colliderShapesTransferBuffer.Unmap();

            return index;
        }

        
        var transferUploadSpan = _collisionResultsTransferUploadBuffer.Map<int>(false);
        _defaultCollisionResults.CopyTo(transferUploadSpan);
        _collisionResultsTransferUploadBuffer.Unmap();

        var collisionResultCopyPass = commandBuffer.BeginCopyPass();
        collisionResultCopyPass.UploadToBuffer(_collisionResultsTransferUploadBuffer, _collisionResultsBufferOne, true);
        collisionResultCopyPass.UploadToBuffer(_collisionResultsTransferUploadBuffer, _collisionResultsBufferTwo, true);
        commandBuffer.EndCopyPass(collisionResultCopyPass);

        _collisionComputeUniforms.ColliderShapeBufferOneLength = MapGroupToBuffer(colliderShapesGroupOne);

        var bufferOneCopyPass = commandBuffer.BeginCopyPass();
        bufferOneCopyPass.UploadToBuffer(_colliderShapesTransferBuffer, _colliderShapesBufferOne, true);
        commandBuffer.EndCopyPass(bufferOneCopyPass);

        _collisionComputeUniforms.ColliderShapeBufferTwoLength = MapGroupToBuffer(colliderShapesGroupTwo);

        var bufferTwoCopyPass = commandBuffer.BeginCopyPass();
        bufferTwoCopyPass.UploadToBuffer(_colliderShapesTransferBuffer, _colliderShapesBufferTwo, true);
        commandBuffer.EndCopyPass(bufferTwoCopyPass);

        var computePass = commandBuffer.BeginComputePass
        (
            [], 
            [
                new StorageBufferReadWriteBinding(_collisionResultsBufferOne, true),
                new StorageBufferReadWriteBinding(_collisionResultsBufferTwo, true)
            ]
        );
        computePass.BindComputePipeline(_computePipeline);
        computePass.BindStorageBuffers(_colliderShapesBufferOne, _colliderShapesBufferTwo);
        commandBuffer.PushComputeUniformData(_collisionComputeUniforms);
        computePass.Dispatch(((uint)colliderShapesGroupOne.Count + 15) / 16, ((uint)colliderShapesGroupTwo.Count + 15) / 16, 1);
        commandBuffer.EndComputePass(computePass);

        int[] CopyBufferToArray(Buffer buffer)
        {
            CommandBuffer copyCommandBuffer = Device.AcquireCommandBuffer();

            var copyPass = copyCommandBuffer.BeginCopyPass();
            copyPass.DownloadFromBuffer(buffer, _collisionResultsTransferDownloadBuffer);
            copyCommandBuffer.EndCopyPass(copyPass);

            var fence = Device.SubmitAndAcquireFence(copyCommandBuffer);
            Device.WaitForFence(fence);
            Device.ReleaseFence(fence);

            var transferDownloadSpan = _collisionResultsTransferDownloadBuffer.Map<int>(false);
            transferDownloadSpan.CopyTo(_collisionResultTransition);
            _collisionResultsTransferDownloadBuffer.Unmap();

            return _collisionResultTransition;
        }

        int[] collisionResultsOne = CopyBufferToArray(_collisionResultsBufferOne);
        int[] collisionResultsTwo = CopyBufferToArray(_collisionResultsBufferTwo);
        
        List<(IHasColliderShapes, IHasColliderShapes)> resultList = [];
        List<(int, int)> resultIndexList = [];
        for (int i = 0; i < collisionResultsOne.Length; i++)
        {
            int indexOne = collisionResultsOne[i];
            int indexTwo = collisionResultsTwo[i];

            bool resultIsUnique = !resultIndexList.Contains((indexOne, indexTwo));
            bool resultIsValid = indexOne != -1 && indexTwo != -1;

            if (resultIsUnique && resultIsValid)
            {
                resultList.Add((colliderShapesGroupOne[indexOne], colliderShapesGroupTwo[indexTwo]));
                resultIndexList.Add((indexOne, indexTwo));
            }
        }

        _collisionResultsTransferDownloadBuffer.Unmap();

        return resultList;
    }
    

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                _computePipeline.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
