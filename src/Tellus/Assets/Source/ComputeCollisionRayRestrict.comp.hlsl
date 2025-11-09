#include "../ComputeCollisionCommon.comp.hlsl"

StructuredBuffer<CollisionBodyPartData> BodyPartDataBuffer : register(t0, space0);
StructuredBuffer<CollisionBodyData> BodyDataBuffer : register(t1, space0);
StructuredBuffer<RayCasterData> RayCasterDataBuffer : register(t2, space0);

RWByteAddressBuffer RayDataBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    uint StoredBodyCount;
    uint StoredRayCasterCount;
};

[numthreads(16, 16, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    uint y = GlobalInvocationID.y;
    
    if (x >= StoredBodyCount || y >= StoredRayCasterCount)
    {
        return;
    }
    
    CollisionBodyData collisionBodyData = BodyDataBuffer[x];
    RayCasterData rayCasterData = RayCasterDataBuffer[y];

    float2 bodyPartVertices[16];
    int bodyPartVerticeLengths;
    
    RayData rayData;
    RayData lineInfo;
    
    float2 intersectionPoint;
    
    for (int i = 0; i < rayCasterData.RayIndexLength; i++)
    {
        int rayIndex = i + rayCasterData.RayIndexStart;
        rayData.Origin.x = asfloat(RayDataBuffer.Load(rayIndex * 20));
        rayData.Origin.y = asfloat(RayDataBuffer.Load(rayIndex * 20 + 4));
        rayData.Direction.x = asfloat(RayDataBuffer.Load(rayIndex * 20 + 8));
        rayData.Direction.y = asfloat(RayDataBuffer.Load(rayIndex * 20 + 12));
        rayData.Length = asfloat(RayDataBuffer.Load(rayIndex * 20 + 16));

        rayData.Origin += rayCasterData.Offset;
        
        float smallestNewLength = rayData.Length;
        
        for (int j = 0; j < collisionBodyData.BodyPartIndexLength; j++)
        {
            CollisionBodyPartData collisionBodyPartData = BodyPartDataBuffer[j + collisionBodyData.BodyPartIndexStart];
        
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
        
        //RayDataBuffer[i + rayCasterData.RayIndexStart].Length = smallestNewLength;
        RayDataBuffer.Store(rayIndex * 20 + 16, asuint(smallestNewLength));
    }
}