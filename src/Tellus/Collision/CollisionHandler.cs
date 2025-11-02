using MoonWorks.Graphics;
using System.Numerics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    private readonly ComputePipeline _computePipeline;
    private StorageBuffer? _storageBuffer;
    private IList<ICollisionBody>? _storedBodyGroupOne;
    private IList<ICollisionBody>? _storedBodyGroupTwo;
    private CollisionComputeUniforms _collisionComputeUniforms;

    public CollisionHandler(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        Utils.LoadShaderFromManifest(Device, "Assets.ComputeCollisionHits.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 4,
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

    private uint TransferDataToBuffer(IList<ICollisionBody> bodyGroup, Buffer bodyDataBuffer, Buffer bodyPartDataBuffer, TransferBuffer bodyDataTransferBuffer, TransferBuffer bodyPartDataTransferBuffer)
    {
        if (_storageBuffer == null)
        {
            throw new NullReferenceException($"{nameof(_storageBuffer)} is unbound!");
        }

        CommandBuffer commandBuffer = Device.AcquireCommandBuffer();

        var bodyDataUploadSpan = bodyDataTransferBuffer.Map<CollisionBodyData>(true);
        var bodyPartDataUploadSpan = bodyPartDataTransferBuffer.Map<CollisionBodyPartData>(true);

        int bodyDataIndex = 0;
        int bodyPartDataIndex = 0;

        foreach (var body in bodyGroup)
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
            }

            bodyDataUploadSpan[bodyDataIndex].BodyPartIndexLength = bodyPartDataIndex - bodyDataUploadSpan[bodyDataIndex].BodyPartIndexStart;
            bodyDataUploadSpan[bodyDataIndex].Offset = body.BodyOffset;
            bodyDataUploadSpan[bodyDataIndex].Flags = 0;
            bodyDataUploadSpan[bodyDataIndex].Flags |= body.IsStatic ? 1 : 0;

            bodyDataIndex++;
        }

        bodyDataTransferBuffer.Unmap();
        bodyPartDataTransferBuffer.Unmap();

        var copyPass = commandBuffer.BeginCopyPass();
        copyPass.UploadToBuffer(bodyDataTransferBuffer, bodyDataBuffer, true);
        copyPass.UploadToBuffer(bodyPartDataTransferBuffer, bodyPartDataBuffer, true);
        commandBuffer.EndCopyPass(copyPass);

        Device.Submit(commandBuffer);

        return (uint)bodyPartDataIndex;
    }

    public void TransferDataToBuffersOne(IList<ICollisionBody> bodyGroup)
    {
        if (_storageBuffer == null)
        {
            throw new NullReferenceException($"{nameof(_storageBuffer)} is unbound!");
        }

        _collisionComputeUniforms.ShapeDataBufferOneLength = TransferDataToBuffer
        (
            bodyGroup, 
            _storageBuffer.BodyDataBufferOne, 
            _storageBuffer.BodyPartDataBufferOne, 
            _storageBuffer.BodyDataTransferBufferOne, 
            _storageBuffer.BodyPartDataTransferBufferOne
        );
        _storedBodyGroupOne = bodyGroup;
    }

    public void TransferDataToBuffersTwo(IList<ICollisionBody> bodyGroup)
    {
        if (_storageBuffer == null)
        {
            throw new NullReferenceException($"{nameof(_storageBuffer)} is unbound!");
        }

        _collisionComputeUniforms.ShapeDataBufferTwoLength = TransferDataToBuffer
        (
            bodyGroup,
            _storageBuffer.BodyDataBufferTwo,
            _storageBuffer.BodyPartDataBufferTwo,
            _storageBuffer.BodyDataTransferBufferTwo,
            _storageBuffer.BodyPartDataTransferBufferTwo
        );
        _storedBodyGroupTwo = bodyGroup;
    }

    public IEnumerable<(ICollisionBody, ICollisionBody)> ComputeCollisionHits()
    {
        if (_storageBuffer == null)
        {
            throw new NullReferenceException($"{nameof(_storageBuffer)} is unbound!");
        }

        if (_storedBodyGroupOne == null)
        {
            throw new NullReferenceException($"{nameof(_storedBodyGroupOne)} isn't set, so buffers don't have data!");
        }
        if (_storedBodyGroupTwo == null)
        {
            throw new NullReferenceException($"{nameof(_storedBodyGroupTwo)} isn't set, so buffers don't have data!");
        }

        _collisionComputeUniforms.ColliderShapeResultBufferLength = _storageBuffer.CollisionResultAmount;

        CommandBuffer commandBuffer = Device.AcquireCommandBuffer();

        var collisionResultCopyPass = commandBuffer.BeginCopyPass();
        collisionResultCopyPass.UploadToBuffer(_storageBuffer.CollisionResultsTransferUploadBuffer, _storageBuffer.CollisionResultsBuffer, true);
        commandBuffer.EndCopyPass(collisionResultCopyPass);

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(_storageBuffer.CollisionResultsBuffer, false)
        );
        computePass.BindComputePipeline(_computePipeline);
        computePass.BindStorageBuffers(_storageBuffer.BodyPartDataBufferOne, _storageBuffer.BodyPartDataBufferTwo, _storageBuffer.BodyDataBufferOne, _storageBuffer.BodyDataBufferTwo);
        commandBuffer.PushComputeUniformData(_collisionComputeUniforms);
        computePass.Dispatch((_collisionComputeUniforms.ShapeDataBufferOneLength + 15) / 16, (_collisionComputeUniforms.ShapeDataBufferTwoLength + 15) / 16, 1);
        commandBuffer.EndComputePass(computePass);

        var copyPass = commandBuffer.BeginCopyPass();
        copyPass.DownloadFromBuffer(_storageBuffer.CollisionResultsBuffer, _storageBuffer.CollisionResultsTransferDownloadBuffer);
        commandBuffer.EndCopyPass(copyPass);

        var fence = Device.SubmitAndAcquireFence(commandBuffer);
        Device.WaitForFence(fence);
        Device.ReleaseFence(fence);

        var tempTransferDownloadSpan = _storageBuffer.CollisionResultsTransferDownloadBuffer.Map<int>(false, 0);
        int collisionResultAmount = tempTransferDownloadSpan[0];
        _storageBuffer.CollisionResultsTransferDownloadBuffer.Unmap();

        var transferDownloadSpan = _storageBuffer.CollisionResultsTransferDownloadBuffer.Map<CollisionResultData>(true, 8);

        List<(ICollisionBody, ICollisionBody)> resultList = [];
        List<(int, int)> resultIndexList = [];
        for (int i = 0; i < collisionResultAmount; i++)
        {
            CollisionResultData resultData = transferDownloadSpan[i];
            int indexOne = resultData.CollisionBodyIndexOne;
            int indexTwo = resultData.CollisionBodyIndexTwo;

            bool resultIsUnique = !resultIndexList.Contains((indexOne, indexTwo));

            if (resultIsUnique)
            {
                resultList.Add((_storedBodyGroupOne[indexOne], _storedBodyGroupTwo[indexTwo]));
                resultIndexList.Add((indexOne, indexTwo));
            }
        }

        _storageBuffer.CollisionResultsTransferDownloadBuffer.Unmap();

        foreach (var result in resultList)
        {
            yield return result;
        }
    }

    public IEnumerable<(ICollisionBody, Vector2)> ComputeCollisionResolutions(IList<ICollisionBody> bodyGroupOne, IList<ICollisionBody> bodyGroupTwo)
    {
        List<(ICollisionBody, Vector2)> resultList = [];

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
