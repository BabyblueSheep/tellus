// https://dyn4j.org/2010/01/sat/
// https://www.metanetsoftware.com/technique/tutorialA.html

#include "../ComputeCollisionCommon.comp.hlsl"

StructuredBuffer<CollisionBodyPartData> BodyPartDataBuffer : register(t0, space0);
StructuredBuffer<CollisionBodyData> BodyDataBuffer : register(t1, space0);
StructuredBuffer<RayCasterData> RayCasterDataBuffer : register(t2, space0);
StructuredBuffer<RayData> RayDataBuffer : register(t3, space0);

RWByteAddressBuffer CollisionResultBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    uint StoredBodyCount;
    uint StoredRayCasterCount;
    uint ColliderShapeResultBufferLength;
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
    
    RayData lineInfo;
    float2 _;
    
    for (int i = 0; i < rayCasterData.RayIndexLength; i++)
    {
        RayData rayData = RayDataBuffer[i + rayCasterData.RayIndexStart];
        rayData.Origin += rayCasterData.Offset;
        
        for (int j = 0; j < collisionBodyData.BodyPartIndexLength; j++)
        {
            CollisionBodyPartData collisionBodyPartData = BodyPartDataBuffer[i + collisionBodyData.BodyPartIndexStart];
        
            constructVertexPositions(collisionBodyPartData, collisionBodyData, bodyPartVertices, bodyPartVerticeLengths);
            
            for (int m = 0; m < bodyPartVerticeLengths; m++)
            {
                int n = (m == (bodyPartVerticeLengths - 1)) ? 0 : (m + 1);
            
                lineInfo.Origin = bodyPartVertices[m];
                lineInfo.Direction = normalize(bodyPartVertices[n] - bodyPartVertices[m]);
                lineInfo.Length = length(bodyPartVertices[n] - bodyPartVertices[m]);
            
                bool didIntersect = getLineLineIntersection(lineInfo, rayData, _);
                if (didIntersect)
                {
                    int collisionAmount;
                    CollisionResultBuffer.InterlockedAdd(0, 1, collisionAmount);
    
                    if (collisionAmount < ColliderShapeResultBufferLength)
                    {
                        CollisionResultBuffer.Store(8 + collisionAmount * 8 + 0, x);
                        CollisionResultBuffer.Store(8 + collisionAmount * 8 + 4, y);
                        CollisionResultBuffer.Store(8 + collisionAmount * 8 + 8, 0);
                    }
                    
                    return;
                }
            }
        }
    }
}