using MoonWorks.Graphics;
using System.Numerics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    private readonly ComputePipeline _hitComputePipeline;
    private readonly ComputePipeline _resolutionComputePipeline;

    private readonly ComputePipeline _rayRestrictComputePipeline;

    private readonly ComputePipeline _incrementRayCasterPipeline;

    public CollisionHandler(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        Utils.LoadShaderFromManifest(Device, "Collision_BodyBodyHits.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 4,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 16,
            ThreadCountZ = 1
        }, out _hitComputePipeline);

        Utils.LoadShaderFromManifest(Device, "Collision_BodyBodyRestrictions.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 4,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 1,
            ThreadCountZ = 1
        }, out _resolutionComputePipeline);

        Utils.LoadShaderFromManifest(Device, "Collision_RayBodyRestrictions.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 3,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 1,
            ThreadCountZ = 1
        }, out _rayRestrictComputePipeline);

        Utils.LoadShaderFromManifest(Device, "Collision_IncrementRayCaster.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 1,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 1,
            ThreadCountZ = 1
        }, out _incrementRayCasterPipeline);
    }

    public void ComputeBodyBodyHits(CommandBuffer commandBuffer, BodyBufferStorage bodyListOneBuffer, BodyBufferStorage bodyListTwoBuffer, HitResultBufferStorage resultBuffer)
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

    public void ResolveBodyBodyCollisions(CommandBuffer commandBuffer, BodyBufferStorage bodyListMovableBuffer, BodyBufferStorage bodyListImmovableBuffer, ResolutionResultBufferStorage resultBuffer)
    {
        var uniforms = new CollisionComputeUniforms
        {
            StoredBodyCountOne = bodyListMovableBuffer.ValidBodyCount,
            StoredBodyCountTwo = bodyListImmovableBuffer.ValidBodyCount,
            ColliderShapeResultBufferLength = resultBuffer.CollisionResultAmount
        };

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(resultBuffer.Buffer, false)
        );
        computePass.BindComputePipeline(_resolutionComputePipeline);
        computePass.BindStorageBuffers(bodyListMovableBuffer.BodyPartDataBuffer, bodyListImmovableBuffer.BodyPartDataBuffer, bodyListMovableBuffer.BodyDataBuffer, bodyListImmovableBuffer.BodyDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch((bodyListMovableBuffer.ValidBodyCount + 15) / 16, 1, 1);
        commandBuffer.EndComputePass(computePass);
    }

    public void RestrictRays(CommandBuffer commandBuffer, BodyBufferStorage bodyListBuffer, RayCasterBufferStorage rayListBuffer)
    {
        var uniforms = new RayComputeUniforms
        {
            StoredBodyCount = bodyListBuffer.ValidBodyCount,
            StoredRayCasterCount = rayListBuffer.ValidRayCasterCount,
        };

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(rayListBuffer.RayDataBuffer, false)
        );
        computePass.BindComputePipeline(_rayRestrictComputePipeline);
        computePass.BindStorageBuffers(bodyListBuffer.BodyPartDataBuffer, bodyListBuffer.BodyDataBuffer, rayListBuffer.RayCasterDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch((rayListBuffer.ValidRayCasterCount + 15) / 16, 1, 1);
        commandBuffer.EndComputePass(computePass);
    }

    public void IncrementRayCasterOffsets(CommandBuffer commandBuffer, RayCasterBufferStorage rayListBuffer)
    {
        var uniforms = new IncrementRaysUniforms
        {
            StoredRayCasterCount = rayListBuffer.ValidRayCasterCount,
        };

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(rayListBuffer.RayCasterDataBuffer, false)
        );
        computePass.BindComputePipeline(_rayRestrictComputePipeline);
        computePass.BindStorageBuffers(rayListBuffer.RayDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch((rayListBuffer.ValidRayCasterCount + 15) / 16, 1, 1);
        commandBuffer.EndComputePass(computePass);
    }

    public void IncrementRayCasterBodiesOffsets(CommandBuffer commandBuffer, BodyBufferStorage bodyBufer, RayCasterBufferStorage rayListBuffer, BodyRayCasterPairBufferStorage pairBuffer)
    {
        var uniforms = new IncrementPairsUniforms
        {
            StoredPairCount = pairBuffer.ValidPairCount,
        };

        var computePass = commandBuffer.BeginComputePass
        (
            [],
            [
                new StorageBufferReadWriteBinding(rayListBuffer.RayCasterDataBuffer, false),
                new StorageBufferReadWriteBinding(bodyBufer.BodyDataBuffer, false)
            ]
        );
        computePass.BindComputePipeline(_rayRestrictComputePipeline);
        computePass.BindStorageBuffers(pairBuffer.PairDataBuffer, rayListBuffer.RayDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch((rayListBuffer.ValidRayCasterCount + 15) / 16, 1, 1);
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
                _rayRestrictComputePipeline.Dispose();
            }
        }
        base.Dispose(disposing);
    }
    }
