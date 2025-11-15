using MoonWorks.Graphics;
using System.Numerics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

public sealed partial class CollisionHandler : GraphicsResource
{
    private readonly ComputePipeline _hitComputePipeline;
    private readonly ComputePipeline _resolutionComputePipeline;

    private readonly ComputePipeline _lineHitComputePipeline;
    private readonly ComputePipeline _lineRestrictComputePipeline;

    private readonly ComputePipeline _incrementLineCollectionPipeline;
    private readonly ComputePipeline _incrementLineCollectionBodyPipeline;

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

        Utils.LoadShaderFromManifest(Device, "Collision_LineBodyHits.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 4,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 16,
            ThreadCountZ = 1
        }, out _lineHitComputePipeline);

        Utils.LoadShaderFromManifest(Device, "Collision_LineBodyRestrictions.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 3,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 1,
            ThreadCountZ = 1
        }, out _lineRestrictComputePipeline);

        Utils.LoadShaderFromManifest(Device, "Collision_IncrementLineCollection.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 1,
            NumReadWriteStorageBuffers = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 1,
            ThreadCountZ = 1
        }, out _incrementLineCollectionPipeline);

        Utils.LoadShaderFromManifest(Device, "Collision_IncrementLineCollectionBody.comp", new ComputePipelineCreateInfo()
        {
            NumReadonlyStorageBuffers = 2,
            NumReadWriteStorageBuffers = 2,
            NumUniformBuffers = 1,
            ThreadCountX = 16,
            ThreadCountY = 1,
            ThreadCountZ = 1
        }, out _incrementLineCollectionBodyPipeline);
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

    public void ComputeLineBodyHits(CommandBuffer commandBuffer, BodyBufferStorage bodyListBuffer, (int, int) bodyListRange, LineCollectionBufferStorage lineListBuffer, (int, int) lineCollectionRange, HitResultBufferStorage resultBuffer)
    {
        var uniforms = new ComputeLineBodyHitsUniforms
        {
            BodyDataBufferStartIndex = bodyListRange.Item1,
            BodyDataBufferLength = bodyListRange.Item2,
            LineCollectionDataBufferStartIndex = lineCollectionRange.Item1,
            LineCollectionDataBufferLength = lineCollectionRange.Item2,
            ColliderShapeResultBufferLength = resultBuffer.CollisionResultAmount,
        };

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(resultBuffer.Buffer, false)
        );
        computePass.BindComputePipeline(_lineHitComputePipeline);
        computePass.BindStorageBuffers(bodyListBuffer.BodyPartDataBuffer, bodyListBuffer.BodyDataBuffer, lineListBuffer.LineCollectionDataBuffer, lineListBuffer.LineDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch(((uint)uniforms.BodyDataBufferLength + 15) / 16, ((uint)uniforms.LineCollectionDataBufferLength + 15) / 16, 1);
        commandBuffer.EndComputePass(computePass);
    }

    public void RestrictLines(CommandBuffer commandBuffer, BodyBufferStorage bodyListBuffer, (int, int) bodyListRange, LineCollectionBufferStorage lineListBuffer, (int, int) listCollectionRange)
    {
        var uniforms = new RestrictLinesUniforms
        {
            BodyDataBufferStartIndex = bodyListRange.Item1,
            BodyDataBufferLength = bodyListRange.Item2,
            LineCollectionDataBufferStartIndex = listCollectionRange.Item1,
            LineCollectionDataBufferLength = listCollectionRange.Item2,
        };

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(lineListBuffer.LineDataBuffer, false)
        );
        computePass.BindComputePipeline(_lineRestrictComputePipeline);
        computePass.BindStorageBuffers(bodyListBuffer.BodyPartDataBuffer, bodyListBuffer.BodyDataBuffer, lineListBuffer.LineCollectionDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch(((uint)uniforms.LineCollectionDataBufferLength + 15) / 16, 1, 1);
        commandBuffer.EndComputePass(computePass);
    }

    public void IncrementLineCollectionOffsets(CommandBuffer commandBuffer, LineCollectionBufferStorage lineListBuffer, (int, int) lineCollectionRange)
    {
        var uniforms = new IncrementLineCollectionOffsetsUniforms
        {
            LineCollectionDataBufferStartIndex = lineCollectionRange.Item1,
            LineCollectionDataBufferLength = lineCollectionRange.Item2
        };

        var computePass = commandBuffer.BeginComputePass
        (
            new StorageBufferReadWriteBinding(lineListBuffer.LineCollectionDataBuffer, false)
        );
        computePass.BindComputePipeline(_incrementLineCollectionPipeline);
        computePass.BindStorageBuffers(lineListBuffer.LineDataBuffer);
        commandBuffer.PushComputeUniformData(uniforms);
        computePass.Dispatch(((uint)uniforms.LineCollectionDataBufferLength + 15) / 16, 1, 1);
        commandBuffer.EndComputePass(computePass);
    }

    public void IncrementLineCollectionBodiesOffsets(CommandBuffer commandBuffer, BodyBufferStorage bodyBufer, LineCollectionBufferStorage lineListBuffer, BodyLineCollectionPairBufferStorage pairBuffer, (int, int) pairRange)
    {
        var uniforms = new IncrementLineCollectionBodiesOffsetsUniforms
        {
            PairDataBufferStartIndex = pairRange.Item1,
            PairDataBufferLength = pairRange.Item2
        };

        var computePass = commandBuffer.BeginComputePass
        (
            [],
            [
                new StorageBufferReadWriteBinding(lineListBuffer.LineCollectionDataBuffer, false),
                new StorageBufferReadWriteBinding(bodyBufer.BodyDataBuffer, false)
            ]
        );
        computePass.BindComputePipeline(_incrementLineCollectionBodyPipeline);
        computePass.BindStorageBuffers(pairBuffer.PairDataBuffer, lineListBuffer.LineDataBuffer);
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
                _lineHitComputePipeline.Dispose();
                _resolutionComputePipeline.Dispose();
                _lineRestrictComputePipeline.Dispose();
                _incrementLineCollectionPipeline.Dispose();
                _incrementLineCollectionBodyPipeline.Dispose();
            }
        }
        base.Dispose(disposing);
    }
    }
