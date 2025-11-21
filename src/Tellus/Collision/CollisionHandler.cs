using MoonWorks.Graphics;
using System.Numerics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Tellus.Collision;

/// <summary>
/// Provides various functions that deal with collision detection and resolution.
/// </summary>
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

    /// <summary>
    /// Computes body-body hits given two groups of bodies.
    /// </summary>
    /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
    /// <param name="bodyListOneBuffer">A buffer bundle with the first group of bodies.</param>
    /// <param name="bodyListOneRange">The range of the first buffer bundle.</param>
    /// <param name="bodyListTwoBuffer">A buffer bundle with the second group of bodies.</param>
    /// <param name="bodyListTwoRange">The range of the second buffer bundle.</param>
    /// <param name="resultBuffer">A buffer to store hit results in.</param>
    public void ComputeBodyBodyHits(CommandBuffer commandBuffer, BodyStorageBufferBundle bodyListOneBuffer, (int, int) bodyListOneRange, BodyStorageBufferBundle bodyListTwoBuffer, (int, int) bodyListTwoRange, HitResultStorageBufferBundle resultBuffer)
    {
        var uniforms = new ComputeBodyBodyHitsUniforms
        {
            BodyDataBufferOneStartIndex = bodyListOneRange.Item1,
            BodyDataBufferOneLength = bodyListOneRange.Item2,
            BodyDataBufferTwoStartIndex = bodyListTwoRange.Item1,
            BodyDataBufferTwoLength = bodyListTwoRange.Item2,
            ColliderShapeResultBufferLength = resultBuffer.HitResultAmount,
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

    /// <summary>
    /// Resolves body-body collisions and provides vectors to push bodies out.
    /// </summary>
    /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
    /// <param name="bodyListMovableBuffer">A buffer bundle with the group of bodies to push out.</param>
    /// <param name="bodyListMovableRange">The range of the movable bodies buffer bundle.</param>
    /// <param name="bodyListImmovableBuffer">A buffer bundle with the group of static bodies.</param>
    /// <param name="bodyListImmovableRange">The range of the static bodies buffer bundle.</param>
    /// <param name="resultBuffer">A buffer to store resolution vectors in.</param>
    public void ResolveBodyBodyCollisions(CommandBuffer commandBuffer, BodyStorageBufferBundle bodyListMovableBuffer, (int, int) bodyListMovableRange, BodyStorageBufferBundle bodyListImmovableBuffer, (int, int) bodyListImmovableRange, ResolutionResultStorageBufferBundle resultBuffer)
    {
        var uniforms = new ResolveBodyBodyCollisionsUniforms
        {
            BodyDataBufferOneStartIndex = bodyListMovableRange.Item1,
            BodyDataBufferOneLength = bodyListMovableRange.Item2,
            BodyDataBufferTwoStartIndex = bodyListImmovableRange.Item1,
            BodyDataBufferTwoLength = bodyListImmovableRange.Item2,
            ColliderShapeResultBufferLength = resultBuffer.ResolutionAmount,
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

    /// <summary>
    /// Computes body-line hits given a group of bodies and a group of line collections.
    /// </summary>
    /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
    /// <param name="bodyListBuffer">A buffer bundle with the group of bodies.</param>
    /// <param name="bodyListRange">The range of the bodies buffer bundle.</param>
    /// <param name="lineListBuffer">A buffer bundle with the group of line collections.</param>
    /// <param name="lineListRange">The range of the line collections buffer bundle.</param>
    /// <param name="resultBuffer">A buffer to store hit results in.</param>
    public void ComputeLineBodyHits(CommandBuffer commandBuffer, BodyStorageBufferBundle bodyListBuffer, (int, int) bodyListRange, LineCollectionStorageBufferBundle lineListBuffer, (int, int) lineListRange, HitResultStorageBufferBundle resultBuffer)
    {
        var uniforms = new ComputeLineBodyHitsUniforms
        {
            BodyDataBufferStartIndex = bodyListRange.Item1,
            BodyDataBufferLength = bodyListRange.Item2,
            LineCollectionDataBufferStartIndex = lineListRange.Item1,
            LineCollectionDataBufferLength = lineListRange.Item2,
            ColliderShapeResultBufferLength = resultBuffer.HitResultAmount,
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

    /// <summary>
    /// Restricts lines so that they don't overlap with any specified bodies.
    /// </summary>
    /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
    /// <param name="bodyListBuffer">A buffer bundle with the group of bodies.</param>
    /// <param name="bodyListRange">The range of the bodies buffer bundle.</param>
    /// <param name="lineListBuffer">A buffer bundle with the group of line collections.</param>
    /// <param name="lineListRange">The range of the line collections buffer bundle.</param>
    public void RestrictLines(CommandBuffer commandBuffer, BodyStorageBufferBundle bodyListBuffer, (int, int) bodyListRange, LineCollectionStorageBufferBundle lineListBuffer, (int, int) lineListRange)
    {
        var uniforms = new RestrictLinesUniforms
        {
            BodyDataBufferStartIndex = bodyListRange.Item1,
            BodyDataBufferLength = bodyListRange.Item2,
            LineCollectionDataBufferStartIndex = lineListRange.Item1,
            LineCollectionDataBufferLength = lineListRange.Item2,
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

    /// <summary>
    /// Increments the offsets of line collections with a velocity line.
    /// </summary>
    /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
    /// <param name="lineListBuffer">A buffer bundle with the group of line collections.</param>
    /// <param name="lineCollectionRange">A range of the line collections buffer bundle.</param>
    public void IncrementLineCollectionOffsets(CommandBuffer commandBuffer, LineCollectionStorageBufferBundle lineListBuffer, (int, int) lineCollectionRange)
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

    /// <summary>
    /// Increments the offsets of line collections and bodies with a velocity line.
    /// </summary>
    /// <param name="commandBuffer">The <see cref="CommandBuffer"/> to attach commands to.</param>
    /// <param name="bodyBufer">A buffer bundle with the group of bodies.</param>
    /// <param name="lineListBuffer">A buffer bundle with the group of line collections.</param>
    /// <param name="pairBuffer">A buffer bundle with pairs of body and line collection buffer indices.</param>
    /// <param name="pairRange">The range of the pair buffer bundle.</param>
    public void IncrementLineCollectionBodiesOffsets(CommandBuffer commandBuffer, BodyStorageBufferBundle bodyBufer, LineCollectionStorageBufferBundle lineListBuffer, BodyLineCollectionPairStorageBufferBundle pairBuffer, (int, int) pairRange)
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
