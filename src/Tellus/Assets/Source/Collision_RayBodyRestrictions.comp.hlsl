#include "../ComputeCollisionCommon.comp.hlsl"

StructuredBuffer<CollisionBodyPartData> BodyPartDataBuffer : register(t0, space0);
StructuredBuffer<CollisionBodyData> BodyDataBuffer : register(t1, space0);
StructuredBuffer<RayCasterData> RayCasterDataBuffer : register(t2, space0);

RWStructuredBuffer<RayData> RayDataBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    int BodyDataBufferStartIndex;
    int BodyDataBufferLength;
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
    
    RayCasterData rayCasterData = RayCasterDataBuffer[x + RayCasterDataBufferStartIndex];

    float2 bodyPartVertices[16];
    int bodyPartVerticeLengths;
    
    RayData lineInfo;
    
    float2 intersectionPoint;
    
    for (int i = 0; i < rayCasterData.RayIndexLength; i++)
    {
        RayData rayData = RayDataBuffer[i + rayCasterData.RayIndexStart];
        rayData.Origin += rayCasterData.Offset;
        
        if ((rayData.Flags & 1) == 0)
            continue;
        
        float smallestNewLength = rayData.Length;
        
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
            
                    lineInfo.Origin = bodyPartVertices[m];
                    lineInfo.Direction = normalize(bodyPartVertices[n] - bodyPartVertices[m]);
                    lineInfo.Length = length(bodyPartVertices[n] - bodyPartVertices[m]);
            
                    bool didIntersect = getLineLineIntersection(lineInfo, rayData, intersectionPoint);
                    if (didIntersect)
                    {
                        float newLength = length(intersectionPoint - rayData.Origin);
                        if (newLength < smallestNewLength)
                        {
                            smallestNewLength = newLength;
                        }
                    }
                }
            }
        }
        
        RayDataBuffer[i + rayCasterData.RayIndexStart].Length = smallestNewLength;
    }
}