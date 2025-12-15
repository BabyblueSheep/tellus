#include "../ComputeCollisionCommon.comp.hlsl"

struct CollisionBodyLineCollectionPair
{
    int BodyIndex;
    int LineCollectionIndex;
};

StructuredBuffer<CollisionBodyLineCollectionPair> PairDataBuffer : register(t0, space0);
StructuredBuffer<LineData> LineDataBuffer : register(t1, space0);
RWStructuredBuffer<LineCollectionData> LineCollectionCasterDataBuffer : register(u0, space1);
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
    int lineCollectionIndex = PairDataBuffer[x + PairDataBufferStartIndex].LineCollectionIndex;
    
    LineData lineData = LineDataBuffer[LineCollectionCasterDataBuffer[lineCollectionIndex].LineVelocityIndex];
    if ((lineData.Flags & 2) == 2)
    {
        LineCollectionCasterDataBuffer[lineCollectionIndex].Offset += normalize(lineData.Vector - lineData.Origin) * lineData.Length;
        BodyDataBuffer[bodyIndex].Offset += normalize(lineData.Vector - lineData.Origin) * lineData.Length;

    }
    else
    {
        LineCollectionCasterDataBuffer[lineCollectionIndex].Offset += lineData.Vector * lineData.Length;
        BodyDataBuffer[bodyIndex].Offset += lineData.Vector * lineData.Length;
    }
}