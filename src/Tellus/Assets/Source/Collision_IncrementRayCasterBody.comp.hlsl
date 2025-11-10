#include "../ComputeCollisionCommon.comp.hlsl"

Buffer<int2> PairDataBuffer : register(t0, space0);
StructuredBuffer<RayData> RayDataBuffer : register(t1, space0);
RWStructuredBuffer<RayCasterData> RayCasterDataBuffer : register(u0, space1);
RWStructuredBuffer<CollisionBodyData> BodyDataBuffer : register(u1, space1);

cbuffer UniformBlock : register(b0, space2)
{
    uint StoredPairCount;
};

[numthreads(16, 1, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    
    if (x >= StoredPairCount)
    {
        return;
    }
    
    int bodyIndex = PairDataBuffer[x].x;
    int rayCasterIndex = PairDataBuffer[x].y;
    
    RayData ray = RayDataBuffer[RayCasterDataBuffer[x].RayVelocityIndex];
    RayCasterDataBuffer[rayCasterIndex].Offset += ray.Direction * ray.Length;
    BodyDataBuffer[bodyIndex].Offset += ray.Direction * ray.Length;
}