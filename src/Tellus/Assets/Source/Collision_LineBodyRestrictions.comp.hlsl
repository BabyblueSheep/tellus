#include "../ComputeCollisionCommon.comp.hlsl"

StructuredBuffer<CollisionBodyPartData> BodyPartDataBuffer : register(t0, space0);
StructuredBuffer<CollisionBodyData> BodyDataBuffer : register(t1, space0);
StructuredBuffer<LineCollectionData> LineCollectionDataBuffer : register(t2, space0);

RWStructuredBuffer<LineData> LineDataBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    int BodyDataBufferStartIndex;
    int BodyDataBufferLength;
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
    
    LineCollectionData lineCollectionData = LineCollectionDataBuffer[x + LineCollectionDataBufferStartIndex];

    float2 bodyPartVertices[16];
    int bodyPartVerticeLengths;
    
    float2 intersectionPoint;
    
    for (int i = 0; i < lineCollectionData.LineIndexLength; i++)
    {
        LineData lineData = LineDataBuffer[i + lineCollectionData.LineIndexLength];
        if ((lineData.Flags & 1) == 0)
            continue;
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
        
        
        float smallestNewLength = lineData.Length;
        
        for (int j = 0; j < BodyDataBufferLength; j++)
        {
            CollisionBodyData collisionBodyData = BodyDataBuffer[j + BodyDataBufferStartIndex];
            
            for (int k = 0; k < collisionBodyData.BodyPartIndexLength; k++)
            {
                CollisionBodyPartData collisionBodyPartData = BodyPartDataBuffer[k + collisionBodyData.BodyPartIndexStart];
        
                constructVertexPositions(collisionBodyPartData, collisionBodyData, bodyPartVertices, bodyPartVerticeLengths);
        
                for (int m = 0; m < bodyPartVerticeLengths; m++)
                {
                    int n = (m == (bodyPartVerticeLengths - 1)) ? 0 : (m + 1);
            
                    float2 bodyPartLineStart = bodyPartVertices[m];
                    float2 bodyPartLineEnd = bodyPartVertices[n];
            
                    bool didIntersect = getLineLineIntersection(float4(bodyPartLineStart, bodyPartLineEnd), float4(lineStart, lineEnd), intersectionPoint);
                    if (didIntersect)
                    {
                        float newLength = length(intersectionPoint - lineStart);
                        if (newLength < smallestNewLength)
                        {
                            smallestNewLength = newLength;
                        }
                    }
                }
            }
        }
        
        LineDataBuffer[i + lineCollectionData.LineIndexLength].Length = smallestNewLength;
    }
}