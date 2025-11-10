#include "../ComputeCollisionCommon.comp.hlsl"

StructuredBuffer<RayData> RayDataBuffer : register(t0, space0);
RWStructuredBuffer<RayCasterData> RayCasterDataBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    uint StoredRayCasterCount;
};

[numthreads(16, 1, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    
    if (x >= StoredRayCasterCount)
    {
        return;
    }
    
    RayData ray = RayDataBuffer[RayCasterDataBuffer[x].RayVelocityIndex];
    RayCasterDataBuffer[x].Offset += ray.Direction * ray.Length;
}