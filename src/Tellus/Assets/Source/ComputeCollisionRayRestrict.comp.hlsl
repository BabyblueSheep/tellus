#include "../ComputeCollisionCommon.comp.hlsl"

StructuredBuffer<CollisionBodyPartData> BodyPartDataBuffer : register(t0, space0);
StructuredBuffer<CollisionBodyData> BodyDataBuffer : register(t1, space0);

RWStructuredBuffer<RayData> RayBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    uint StoredBodyCount;
    uint StoredRayCount;
};

[numthreads(16, 16, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    uint y = GlobalInvocationID.y;
    
    if (x >= StoredBodyCount || y >= StoredRayCount)
    {
        return;
    }
    
    CollisionBodyData collisionBodyData = BodyDataBuffer[x];
    RayData rayData = RayBuffer[y];

    float2 bodyPartVertices[16];
    int bodyPartVerticeLengths;
    
    RayData lineInfo;
    
    float2 intersectionPoint;
    float smallestNewLength = rayData.Length;
    
    for (int i = 0; i < collisionBodyData.BodyPartIndexLength; i++)
    {
        CollisionBodyPartData collisionBodyPartData = BodyPartDataBuffer[i + collisionBodyData.BodyPartIndexStart];
        
        constructVertexPositions(collisionBodyPartData, collisionBodyData, bodyPartVertices, bodyPartVerticeLengths);
        
        for (int i = 0; i < bodyPartVerticeLengths; i++)
        {
            int j = (i == (bodyPartVerticeLengths - 1)) ? 0 : (i + 1);
            
            lineInfo.Origin = bodyPartVertices[i];
            lineInfo.Direction = normalize(bodyPartVertices[j] - bodyPartVertices[i]);
            lineInfo.Length = length(bodyPartVertices[j] - bodyPartVertices[i]);
            
            bool didIntersect = getLineLineIntersection(lineInfo, rayData, intersectionPoint);
            if (didIntersect)
            {
                float newLength = length(intersectionPoint - bodyPartVertices[i]);
                if (newLength < smallestNewLength)
                {
                    smallestNewLength = newLength;
                }
            }
        }
    }
    
    RayBuffer[y].Length = smallestNewLength;
}