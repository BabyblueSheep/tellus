using MoonWorks.Graphics;
using System.Numerics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    private readonly ComputePipeline _hitComputePipeline;
    private readonly ComputePipeline _resolutionComputePipeline;

    private readonly ComputePipeline _rayHitComputePipeline;
    private readonly ComputePipeline _rayRestrictComputePipeline;

    private readonly ComputePipeline _incrementRayCasterPipeline;
    private readonly ComputePipeline _incrementRayCasterBodyPipeline;

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
            NumReadonlyStorageBuffers = 3,
            NumReadWriteStorageBuffers = 2,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 1,
            ThreadCountZ = 1
        }, out _resolutionComputePipeline);

        Utils.LoadShaderFromManifest(Device, "Collision_RayBodyHits.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 4,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 16,
            ThreadCountZ = 1
        }, out _rayHitComputePipeline);

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

        Utils.LoadShaderFromManifest(Device, "Collision_IncrementRayCasterBody.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 2,
            NumReadWriteStorageBuffers = 2,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 1,
            ThreadCountZ = 1
        }, out _incrementRayCasterBodyPipeline);
    }

    public void ComputeBodyBodyHits(CommandBuffer commandBuffer, BodyBufferStorage bodyListOneBuffer, (int, int) bodyListOneRange, BodyBufferStorage bodyListTwoBuffer, (int, int) bodyListTwoRange, HitResultBufferStorage resultBuffer)
    {
        var uniforms = new ComputeBodyBodyHitsUniforms
        {
            BodyDataBufferOneStartIndex = bodyListOneRange.Item1,
            BodyDataBufferOneLength = bodyListOneRange.Item2,
            BodyDataBufferTwoStartIndex = bodyListTwoRange.Item1,
            BodyDataBufferTwoLength = bodyListTwoRange.Item2,
            ColliderShapeResultBufferLength = resultBuffer.CollisionResultAmount,
        };

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(resultBuffer.Buffer, false)
        );
        computePass.BindComputePipeline(_hitComputePipeline);
        computePass.BindStorageBuffers(bodyListOneBuffer.BodyPartDataBuffer, bodyListTwoBuffer.BodyPartDataBuffer, bodyListOneBuffer.BodyDataBuffer, bodyListTwoBuffer.BodyDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch(((uint)uniforms.BodyDataBufferOneLength + 15) / 16, ((uint)uniforms.BodyDataBufferTwoLength + 15) / 16, 1);
        commandBuffer.EndComputePass(computePass);
    }

    public void ResolveBodyBodyCollisions(CommandBuffer commandBuffer, BodyBufferStorage bodyListMovableBuffer, (int, int) bodyListMovableRange, BodyBufferStorage bodyListImmovableBuffer, (int, int) bodyListImmovableRange, ResolutionResultBufferStorage resultBuffer)
    {
        var uniforms = new ResolveBodyBodyCollisionsUniforms
        {
            BodyDataBufferOneStartIndex = bodyListMovableRange.Item1,
            BodyDataBufferOneLength = bodyListMovableRange.Item2,
            BodyDataBufferTwoStartIndex = bodyListImmovableRange.Item1,
            BodyDataBufferTwoLength = bodyListImmovableRange.Item2,
            ColliderShapeResultBufferLength = resultBuffer.CollisionResultAmount,
        };

        var computePass = commandBuffer.BeginComputePass
        (
            [],
            [
                new StorageBufferReadWriteBinding(resultBuffer.Buffer, false),
                new StorageBufferReadWriteBinding(bodyListMovableBuffer.BodyDataBuffer, false)
            ]
        );
        computePass.BindComputePipeline(_resolutionComputePipeline);
        computePass.BindStorageBuffers(bodyListMovableBuffer.BodyPartDataBuffer, bodyListImmovableBuffer.BodyPartDataBuffer, bodyListImmovableBuffer.BodyDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch(((uint)uniforms.BodyDataBufferOneLength + 15) / 16, 1, 1);
        commandBuffer.EndComputePass(computePass);
    }

    public void ComputeRayBodyHits(CommandBuffer commandBuffer, BodyBufferStorage bodyListBuffer, (int, int) bodyListRange, RayCasterBufferStorage rayListBuffer, (int, int) rayCasterRange, HitResultBufferStorage resultBuffer)
    {
        var uniforms = new ComputeRayBodyHitsUniforms
        {
            BodyDataBufferStartIndex = bodyListRange.Item1,
            BodyDataBufferLength = bodyListRange.Item2,
            RayCasterDataBufferStartIndex = rayCasterRange.Item1,
            RayCasterDataBufferLength = rayCasterRange.Item2,
            ColliderShapeResultBufferLength = resultBuffer.CollisionResultAmount,
        };

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(resultBuffer.Buffer, false)
        );
        computePass.BindComputePipeline(_rayHitComputePipeline);
        computePass.BindStorageBuffers(bodyListBuffer.BodyPartDataBuffer, bodyListBuffer.BodyDataBuffer, rayListBuffer.RayCasterDataBuffer, rayListBuffer.RayDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch(((uint)uniforms.BodyDataBufferLength + 15) / 16, ((uint)uniforms.RayCasterDataBufferLength + 15) / 16, 1);
        commandBuffer.EndComputePass(computePass);
    }

    public void RestrictRays(CommandBuffer commandBuffer, BodyBufferStorage bodyListBuffer, (int, int) bodyListRange, RayCasterBufferStorage rayListBuffer, (int, int) rayCasterRange)
    {
        var uniforms = new RestrictRaysUniforms
        {
            BodyDataBufferStartIndex = bodyListRange.Item1,
            BodyDataBufferLength = bodyListRange.Item2,
            RayCasterDataBufferStartIndex = rayCasterRange.Item1,
            RayCasterDataBufferLength = rayCasterRange.Item2,
        };

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(rayListBuffer.RayDataBuffer, false)
        );
        computePass.BindComputePipeline(_rayRestrictComputePipeline);
        computePass.BindStorageBuffers(bodyListBuffer.BodyPartDataBuffer, bodyListBuffer.BodyDataBuffer, rayListBuffer.RayCasterDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch(((uint)uniforms.RayCasterDataBufferLength + 15) / 16, 1, 1);
        commandBuffer.EndComputePass(computePass);
    }

    public void IncrementRayCasterOffsets(CommandBuffer commandBuffer, RayCasterBufferStorage rayListBuffer, (int, int) rayCasterRange)
    {
        var uniforms = new IncrementRayCasterOffsetsUniforms
        {
            RayCasterDataBufferStartIndex = rayCasterRange.Item1,
            RayCasterDataBufferLength = rayCasterRange.Item2
        };

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(rayListBuffer.RayCasterDataBuffer, false)
        );
        computePass.BindComputePipeline(_incrementRayCasterPipeline);
        computePass.BindStorageBuffers(rayListBuffer.RayDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch(((uint)uniforms.RayCasterDataBufferLength + 15) / 16, 1, 1);
        commandBuffer.EndComputePass(computePass);
    }

    public void IncrementRayCasterBodiesOffsets(CommandBuffer commandBuffer, BodyBufferStorage bodyBufer, RayCasterBufferStorage rayListBuffer, BodyRayCasterPairBufferStorage pairBuffer, (int, int) pairRange)
    {
        var uniforms = new IncrementRayCasterBodiesOffsetsUniforms
        {
            PairDataBufferStartIndex = pairRange.Item1,
            PairDataBufferLength = pairRange.Item2
        };

        var computePass = commandBuffer.BeginComputePass
        (
            [],
            [
            new StorageBufferReadWriteBinding(rayListBuffer.RayCasterDataBuffer, false),
            new StorageBufferReadWriteBinding(bodyBufer.BodyDataBuffer, false)
            ]
        );
        computePass.BindComputePipeline(_incrementRayCasterBodyPipeline);
        computePass.BindStorageBuffers(pairBuffer.PairDataBuffer, rayListBuffer.RayDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch(((uint)uniforms.PairDataBufferLength + 15) / 16, 1, 1);
        commandBuffer.EndComputePass(computePass);
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                _hitComputePipeline.Dispose();
                _rayHitComputePipeline.Dispose();
                _resolutionComputePipeline.Dispose();
                _rayRestrictComputePipeline.Dispose();
                _incrementRayCasterPipeline.Dispose();
                _incrementRayCasterBodyPipeline.Dispose();
            }
        }
        base.Dispose(disposing);
    }
    }
