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

public sealed partial class CollisionHandler : GraphicsResource
{
    private readonly ComputePipeline _computePipeline;
    private StorageBuffer? _storageBuffer;
    private CollisionComputeUniforms _collisionComputeUniforms;

    private const int SHAPE_DATA_AMOUNT = 2048;
    private const int COLLISION_RESULT_AMOUNT = 2048;

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

    public IEnumerable<(ICollisionBody, ICollisionBody)> HandleCollisions(IList<ICollisionBody> colliderShapesGroupOne, IList<ICollisionBody> colliderShapesGroupTwo)
    {
        if (_storageBuffer == null)
        {
            throw new NullReferenceException($"{nameof(_storageBuffer)} is unbound!");
        }   
        
        CommandBuffer commandBuffer = Device.AcquireCommandBuffer();


        uint MapGroupToBuffer(IEnumerable<ICollisionBody> colliderGroup, TransferBuffer dataTransferBuffer)
        {
            var dataUploadSpan = dataTransferBuffer.Map<CollisionBodyPartData>(true);

            int colliderIndex = 0;
            int shapeDataIndex = 0;

            foreach (var collider in colliderGroup)
            {
                foreach (var shape in collider.BodyParts)
                {
                    dataUploadSpan[shapeDataIndex].CollisionBodyIndex = colliderIndex;
                    dataUploadSpan[shapeDataIndex].ShapeType = shape.ShapeType;
                    dataUploadSpan[shapeDataIndex].Center = shape.BodyPartCenter + collider.BodyOffset;
                    dataUploadSpan[shapeDataIndex].DecimalFields = shape.DecimalFields;
                    dataUploadSpan[shapeDataIndex].IntegerFields = shape.IntegerFields;
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

        _collisionComputeUniforms.ShapeDataBufferOneLength = MapGroupToBuffer(colliderShapesGroupOne, _storageBuffer.BodyPartDataTransferBufferOne);
        _collisionComputeUniforms.ShapeDataBufferTwoLength = MapGroupToBuffer(colliderShapesGroupTwo, _storageBuffer.BodyPartDataTransferBufferTwo);

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


        var transferDownloadSpan = _storageBuffer.CollisionResultsTransferDownloadBuffer.Map<int>(true, 8);

        List<(ICollisionBody, ICollisionBody)> resultList = [];
        List<(int, int)> resultIndexList = [];
        for (int i = 0; i < transferDownloadSpan.Length; i++)
        {
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
