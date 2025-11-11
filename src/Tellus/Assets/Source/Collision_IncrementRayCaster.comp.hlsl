#include "../ComputeCollisionCommon.comp.hlsl"

StructuredBuffer<RayData> RayDataBuffer : register(t0, space0);
RWStructuredBuffer<RayCasterData> RayCasterDataBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    int RayCasterDataBufferStartIndex;
    int RayCasterDataBufferLength;
};

[numthreads(16, 1, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    
    if (x >= RayCasterDataBufferLength)
    {
        return;
    }
    
    RayData ray = RayDataBuffer[RayCasterDataBuffer[x + RayCasterDataBufferStartIndex].RayVelocityIndex];
    RayCasterDataBuffer[x + RayCasterDataBufferStartIndex].Offset += ray.Direction * ray.Length;
}