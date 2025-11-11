#include "../ComputeCollisionCommon.comp.hlsl"

struct CollisionBodyRayCasterPair
{
    int BodyIndex;
    int RayCasterIndex;
};

StructuredBuffer<CollisionBodyRayCasterPair> PairDataBuffer : register(t0, space0);
StructuredBuffer<RayData> RayDataBuffer : register(t1, space0);
RWStructuredBuffer<RayCasterData> RayCasterDataBuffer : register(u0, space1);
RWStructuredBuffer<CollisionBodyData> BodyDataBuffer : register(u1, space1);

cbuffer UniformBlock : register(b0, space2)
{
    int PairDataBufferStartIndex;
    int PairDataBufferLength;
};

[numthreads(16, 1, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    
    if (x >= PairDataBufferLength)
    {
        return;
    }
    
    int bodyIndex = PairDataBuffer[x + PairDataBufferStartIndex].BodyIndex;
    int rayCasterIndex = PairDataBuffer[x + PairDataBufferStartIndex].RayCasterIndex;
    
    RayData ray = RayDataBuffer[RayCasterDataBuffer[rayCasterIndex].RayVelocityIndex];
    RayCasterDataBuffer[rayCasterIndex].Offset += ray.Direction * ray.Length;
    BodyDataBuffer[bodyIndex].Offset += ray.Direction * ray.Length;
}