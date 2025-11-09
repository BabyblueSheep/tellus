using MoonWorks.Graphics;
using System.Numerics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    private readonly ComputePipeline _hitComputePipeline;
    private readonly ComputePipeline _resolutionComputePipeline;
    private readonly ComputePipeline _rayRestrictComputePipeline;

    public CollisionHandler(GraphicsDevice graphicsDevice) : base(graphicsDevice)
    {
        Utils.LoadShaderFromManifest(Device, "ComputeCollisionHits.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 4,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 16,
            ThreadCountZ = 1
        }, out _hitComputePipeline);

        Utils.LoadShaderFromManifest(Device, "ComputeCollisionResolutions.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 4,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 1,
            ThreadCountZ = 1
        }, out _resolutionComputePipeline);

        Utils.LoadShaderFromManifest(Device, "ComputeCollisionRayRestrict.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 3,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 16,
            ThreadCountZ = 1
        }, out _rayRestrictComputePipeline);
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

    public void ComputeCollisionResolutions(CommandBuffer commandBuffer, BodyStorageBuffer bodyListMovableBuffer, BodyStorageBuffer bodyListImmovableBuffer, ResolutionResultStorageBuffer resultBuffer)
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

    public void ComputeRayRestrictions(CommandBuffer commandBuffer, BodyStorageBuffer bodyListBuffer, RayCasterStorageBuffer rayListBuffer)
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
        computePass.Dispatch((bodyListBuffer.ValidBodyCount + 15) / 16, (rayListBuffer.ValidRayCasterCount + 15) / 16, 1);
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
