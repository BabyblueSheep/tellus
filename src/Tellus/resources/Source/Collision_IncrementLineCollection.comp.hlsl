#include "../ComputeCollisionCommon.comp.hlsl"

StructuredBuffer<LineData> LineDataBuffer : register(t0, space0);
RWStructuredBuffer<LineCollectionData> LineCollectionCasterDataBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    int LineCollectionDataBufferStartIndex;
    int LineCollectionDataBufferLength;
};

[numthreads(16, 1, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    
    if (x >= LineCollectionDataBufferLength)
    {
        return;
    }
    
    LineData lineData = LineDataBuffer[LineCollectionCasterDataBuffer[x + LineCollectionDataBufferStartIndex].LineVelocityIndex];
    if ((lineData.Flags & 2) == 2)
    {
        LineCollectionCasterDataBuffer[x + LineCollectionDataBufferStartIndex].Offset += normalize(lineData.Vector - lineData.Origin) * lineData.Length;
    }
    else
    {
        LineCollectionCasterDataBuffer[x + LineCollectionDataBufferStartIndex].Offset += lineData.Vector * lineData.Length;
    }
}