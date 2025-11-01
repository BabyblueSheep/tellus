using MoonWorks.Graphics;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    private readonly ComputePipeline _computePipeline;
    private StorageBuffer? _storageBuffer;
    private CollisionComputeUniforms _collisionComputeUniforms;

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

        _collisionComputeUniforms = new CollisionComputeUniforms();
        _storageBuffer = null;
    }

    public void BindStorageBuffer(StorageBuffer buffer)
    {
        _storageBuffer = buffer;
        _collisionComputeUniforms.ColliderShapeResultBufferLength = _storageBuffer.CollisionResultAmount;
    }

    public IEnumerable<(ICollisionBody, ICollisionBody)> ComputeCollisionResults(IList<ICollisionBody> bodyGroupOne, IList<ICollisionBody> bodyGroupTwo)
    {
        if (_storageBuffer == null)
        {
            throw new NullReferenceException($"{nameof(_storageBuffer)} is unbound!");
        }   
        
        CommandBuffer commandBuffer = Device.AcquireCommandBuffer();


        uint MapGroupToBuffer(IEnumerable<ICollisionBody> bodyGroup, TransferBuffer dataTransferBuffer)
        {
            var dataUploadSpan = dataTransferBuffer.Map<CollisionBodyPartData>(true);

            int colliderIndex = 0;
            int shapeDataIndex = 0;

            foreach (var body in bodyGroup)
            {
                foreach (var bodyPart in body.BodyParts)
                {
                    dataUploadSpan[shapeDataIndex].CollisionBodyIndex = colliderIndex;
                    dataUploadSpan[shapeDataIndex].ShapeType = bodyPart.ShapeType;
                    dataUploadSpan[shapeDataIndex].Center = bodyPart.BodyPartCenter + body.BodyOffset;
                    dataUploadSpan[shapeDataIndex].DecimalFields = bodyPart.DecimalFields;
                    dataUploadSpan[shapeDataIndex].IntegerFields = bodyPart.IntegerFields;

                    dataUploadSpan[shapeDataIndex].Flags = 0;
                    dataUploadSpan[shapeDataIndex].Flags |= body.IsStatic ? 1 : 0;

                    shapeDataIndex++;
                }

                colliderIndex++;
            }

            dataTransferBuffer.Unmap();

            return (uint)shapeDataIndex;
        }

        var collisionResultCopyPass = commandBuffer.BeginCopyPass();
        collisionResultCopyPass.UploadToBuffer(_storageBuffer.CollisionResultsTransferUploadBuffer, _storageBuffer.CollisionResultsBuffer, true);
        commandBuffer.EndCopyPass(collisionResultCopyPass);

        _collisionComputeUniforms.ShapeDataBufferOneLength = MapGroupToBuffer(bodyGroupOne, _storageBuffer.BodyPartDataTransferBufferOne);
        _collisionComputeUniforms.ShapeDataBufferTwoLength = MapGroupToBuffer(bodyGroupTwo, _storageBuffer.BodyPartDataTransferBufferTwo);

        var copyPass = commandBuffer.BeginCopyPass();
        copyPass.UploadToBuffer(_storageBuffer.BodyPartDataTransferBufferOne, _storageBuffer.BodyPartDataBufferOne, true);
        copyPass.UploadToBuffer(_storageBuffer.BodyPartDataTransferBufferTwo, _storageBuffer.BodyPartDataBufferTwo, true);
        commandBuffer.EndCopyPass(copyPass);


        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(_storageBuffer.CollisionResultsBuffer, false)
        );
        computePass.BindComputePipeline(_computePipeline);
        computePass.BindStorageBuffers(_storageBuffer.BodyPartDataBufferOne, _storageBuffer.BodyPartDataBufferTwo);
        commandBuffer.PushComputeUniformData(_collisionComputeUniforms);
        computePass.Dispatch(((uint)_collisionComputeUniforms.ShapeDataBufferOneLength + 15) / 16, ((uint)_collisionComputeUniforms.ShapeDataBufferTwoLength + 15) / 16, 1);
        commandBuffer.EndComputePass(computePass);

        copyPass = commandBuffer.BeginCopyPass();
        copyPass.DownloadFromBuffer(_storageBuffer.CollisionResultsBuffer, _storageBuffer.CollisionResultsTransferDownloadBuffer);
        commandBuffer.EndCopyPass(copyPass);

        var fence = Device.SubmitAndAcquireFence(commandBuffer);
        Device.WaitForFence(fence);
        Device.ReleaseFence(fence);


        var transferDownloadSpan = _storageBuffer.CollisionResultsTransferDownloadBuffer.Map<CollisionResultData>(true, 8);

        List<(ICollisionBody, ICollisionBody)> resultList = [];
        List<(int, int)> resultIndexList = [];
        for (int i = 0; i < transferDownloadSpan.Length; i++)
        {
            CollisionResultData resultData = transferDownloadSpan[i];
            int indexOne = resultData.CollisionBodyIndexOne;
            int indexTwo = resultData.CollisionBodyIndexTwo;


            /*int collisionAmount = transferDownloadSpan[i];
            int indexOne = i % COLLIDER_SHAPE_CONTAINER_AMOUNT;
            int indexTwo = i / COLLIDER_SHAPE_CONTAINER_AMOUNT;

            bool resultIsUnique = !resultIndexList.Contains((indexOne, indexTwo));
            bool collidersHaveCollided = collisionAmount != -1;

            if (resultIsUnique && collidersHaveCollided)
            {
                resultList.Add((colliderShapesGroupOne[indexOne], colliderShapesGroupTwo[indexTwo]));
                resultIndexList.Add((indexOne, indexTwo));
            }*/
        }

        _storageBuffer.CollisionResultsTransferDownloadBuffer.Unmap();

        foreach (var result in resultList)
        {
            yield return result;
        }
    }

    public IEnumerable<(ICollisionBody, ICollisionBody)> ComputeCollisionResolutions(IList<ICollisionBody> bodyGroupOne, IList<ICollisionBody> bodyGroupTwo)
    {
        List<(ICollisionBody, ICollisionBody)> resultList = [];

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
                _storageBuffer?.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
