// https://dyn4j.org/2010/01/sat/
// https://www.metanetsoftware.com/technique/tutorialA.html

#include "../ComputeCollisionCommon.comp.hlsl"

StructuredBuffer<CollisionBodyPartData> BodyPartDataBuffer : register(t0, space0);
StructuredBuffer<CollisionBodyData> BodyDataBuffer : register(t1, space0);
StructuredBuffer<LineCollectionData> LineCollectionDataBuffer : register(t2, space0);
StructuredBuffer<LineData> LineDataBuffer : register(t3, space0);

RWByteAddressBuffer CollisionResultBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    int BodyDataBufferStartIndex;
    int BodyDataBufferLength;
    int LineCollectionDataBufferStartIndex;
    int LineCollectionDataBufferLength;
    int ColliderShapeResultBufferLength;
};

[numthreads(16, 16, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    uint y = GlobalInvocationID.y;
    
    if (x >= BodyDataBufferLength || y >= LineCollectionDataBufferLength)
    {
        return;
    }
    
    CollisionBodyData collisionBodyData = BodyDataBuffer[x + BodyDataBufferStartIndex];
    LineCollectionData lineCollectionData = LineCollectionDataBuffer[y + LineCollectionDataBufferStartIndex];
    
    float2 bodyPartVertices[16];
    int bodyPartVerticeLengths;
    
    float2 _;
    
    for (int i = 0; i < lineCollectionData.LineIndexLength; i++)
    {
        LineData lineData = LineDataBuffer[i + lineCollectionData.LineIndexLength];
        lineData.Origin += lineCollectionData.Offset;
        float2 lineStart = lineData.Origin;
        float2 lineEnd;
        if ((lineData.Flags & 2) == 2)
        {
            lineEnd = lineData.Origin + normalize(lineData.Vector - lineData.Origin) * lineData.Length;
        }
        else
        {
            lineEnd = lineData.Origin + lineData.Vector * lineData.Length;
        }
        
        for (int j = 0; j < collisionBodyData.BodyPartIndexLength; j++)
        {
            CollisionBodyPartData collisionBodyPartData = BodyPartDataBuffer[j + collisionBodyData.BodyPartIndexStart];
        
            constructVertexPositions(collisionBodyPartData, collisionBodyData, bodyPartVertices, bodyPartVerticeLengths);
            
            for (int m = 0; m < bodyPartVerticeLengths; m++)
            {
                int n = (m == (bodyPartVerticeLengths - 1)) ? 0 : (m + 1);
            
                float2 bodyPartLineStart = bodyPartVertices[m];
                float2 bodyPartLineEnd = bodyPartVertices[n];
                
            
                bool didIntersect = getLineLineIntersection(float4(bodyPartLineStart, bodyPartLineEnd), float4(lineStart, lineEnd), _);
                if (didIntersect)
                {
                    int collisionAmount;
                    CollisionResultBuffer.InterlockedAdd(0, 1, collisionAmount);
    
                    if (collisionAmount < ColliderShapeResultBufferLength)
                    {
                        CollisionResultBuffer.Store(8 + collisionAmount * 8 + 0, x);
                        CollisionResultBuffer.Store(8 + collisionAmount * 8 + 4, y);
                    }
                    
                    return;
                }
            }
        }
    }
}