using MoonWorks.Graphics;
using System.Numerics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    private readonly ComputePipeline _hitComputePipeline;
    private readonly ComputePipeline _resolutionComputePipeline;

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
        }, out _hitComputePipeline);

        Utils.LoadShaderFromManifest(Device, "Assets.ComputeCollisionResolutions.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 4,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 1,
            ThreadCountZ = 1
        }, out _resolutionComputePipeline);
    }

    public void ComputeCollisionHits(CommandBuffer commandBuffer, BodyStorageBuffer bodyListOneBuffer, BodyStorageBuffer bodyListTwoBuffer, HitResultStorageBuffer resultBuffer)
    {
        var uniforms = new CollisionComputeUniforms
        {
            StoredBodyCountOne = bodyListOneBuffer.ValidBodyCount,
            StoredBodyCountTwo = bodyListTwoBuffer.ValidBodyCount,
            ColliderShapeResultBufferLength = resultBuffer.CollisionResultAmount
        };

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(resultBuffer.Buffer, false)
        );
        computePass.BindComputePipeline(_hitComputePipeline);
        computePass.BindStorageBuffers(bodyListOneBuffer.BodyPartDataBuffer, bodyListTwoBuffer.BodyPartDataBuffer, bodyListOneBuffer.BodyDataBuffer, bodyListTwoBuffer.BodyDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch((bodyListOneBuffer.ValidBodyCount + 15) / 16, (bodyListTwoBuffer.ValidBodyCount + 15) / 16, 1);
        commandBuffer.EndComputePass(computePass);
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                _hitComputePipeline.Dispose();
                _resolutionComputePipeline.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    public void ComputeCollisionResolutions(CommandBuffer commandBuffer, BodyStorageBuffer bodyListMovableBuffer, BodyStorageBuffer bodyListTwoBuffer, ResolutionResultStorageBuffer resultBuffer)
    {

    }

        /*
        public IEnumerable<(ICollisionBody, Vector2)> ComputeCollisionResolutions(bool isGroupTwoImmovable)
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
            _collisionComputeUniforms.StoredBodyCountOne = _storedBodyCountOne;
            _collisionComputeUniforms.StoredBodyCountTwo = _storedBodyCountTwo;

            CommandBuffer commandBuffer = Device.AcquireCommandBuffer();

            var collisionResultCopyPass = commandBuffer.BeginCopyPass();
            collisionResultCopyPass.UploadToBuffer(_storageBuffer.CollisionResultsTransferUploadBuffer, _storageBuffer.CollisionResultsBuffer, true);
            commandBuffer.EndCopyPass(collisionResultCopyPass);

            var computePass = commandBuffer.BeginComputePass
            (
                new StorageBufferReadWriteBinding(_storageBuffer.CollisionResultsBuffer, false)
            );
            computePass.BindComputePipeline(_resolutionComputePipeline);
            if (isGroupTwoImmovable)
            {
                computePass.BindStorageBuffers(_storageBuffer.BodyPartDataBufferOne, _storageBuffer.BodyPartDataBufferTwo, _storageBuffer.BodyDataBufferOne, _storageBuffer.BodyDataBufferTwo);
            }
            else
            {
                computePass.BindStorageBuffers(_storageBuffer.BodyPartDataBufferTwo, _storageBuffer.BodyPartDataBufferOne, _storageBuffer.BodyDataBufferTwo, _storageBuffer.BodyDataBufferOne);
            }
            commandBuffer.PushComputeUniformData(_collisionComputeUniforms);
            computePass.Dispatch((_storedBodyCountOne + 15) / 16, 1, 1);
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

            var transferDownloadSpan = _storageBuffer.CollisionResultsTransferDownloadBuffer.Map<CollisionResolutionData>(true, 12);

            List<(ICollisionBody, Vector2)> resultList = [];
            for (int i = 0; i < collisionResultAmount; i++)
            {
                CollisionResolutionData resultData = transferDownloadSpan[i];
                int index = resultData.CollisionBodyIndex;
                resultList.Add((isGroupTwoImmovable ? _storedBodyGroupOne[index] : _storedBodyGroupTwo[index], resultData.TotalMinimumTransitionVector));
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
                    _hitComputePipeline.Dispose();
                    _storageBuffer?.Dispose();
                }
            }
            base.Dispose(disposing);
        }*/
    }
