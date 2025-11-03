// https://dyn4j.org/2010/01/sat/
// https://www.metanetsoftware.com/technique/tutorialA.html

#include "./ComputeCollisionCommon.comp.hlsl"

StructuredBuffer<CollisionBodyPartData> BodyPartDataBufferOne : register(t0, space0);
StructuredBuffer<CollisionBodyPartData> BodyPartDataBufferTwo : register(t1, space0);
StructuredBuffer<CollisionBodyData> BodyDataBufferOne : register(t2, space0);
StructuredBuffer<CollisionBodyData> BodyDataBufferTwo : register(t3, space0);

RWByteAddressBuffer CollisionResultBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    uint StoredBodyCountOne;
    uint StoredBodyCountTwo;
    uint ColliderShapeResultBufferLength;
};

bool doBodyPartsOverlap(float2 shapeVerticesMain[16], int shapeVerticesMainAmount, float2 shapeVerticesSub[16], int shapeVerticesSubAmount)
{
    for (int i = 0; i < shapeVerticesMainAmount; i++)
    {
        int j = (i == (shapeVerticesMainAmount - 1)) ? 0 : (i + 1);
        float2 vertexOne = shapeVerticesMain[i];
        float2 vertexTwo = shapeVerticesMain[j];
        
        float2 edge = vertexTwo - vertexOne;
        float2 normal = float2(-edge.y, edge.x);
        float2 axis = normalize(normal);
        
        float2 shapeOneProjection = projectVerticesOnAxis(shapeVerticesMain, shapeVerticesMainAmount, axis);
        float2 shapeTwoProjection = projectVerticesOnAxis(shapeVerticesSub, shapeVerticesSubAmount, axis);
        
        if (!doProjectionsOverlap(shapeOneProjection, shapeTwoProjection))
        {
            return false;
        }
    }
    
    return true;
}

[numthreads(16, 16, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    uint y = GlobalInvocationID.y;
    
    if (x >= StoredBodyCountOne || y >= StoredBodyCountTwo)
    {
        return;
    }
    
    CollisionBodyData collisionBodyDataOne = BodyDataBufferOne[x];
    CollisionBodyData collisionBodyDataTwo = BodyDataBufferTwo[y];
    
    float2 bodyPartVerticesOne[16];
    float2 bodyPartCenterVertexOne;
    int bodyPartVerticeLengthsOne;
    float2 bodyPartVerticesTwo[16];
    float2 bodyPartCenterVertexTwo;
    int bodyPartVerticeLengthsTwo;
    
    for (int i = 0; i < collisionBodyDataOne.BodyPartIndexLength; i++)
    {
        CollisionBodyPartData collisionBodyPartDataOne = BodyPartDataBufferOne[i + collisionBodyDataOne.BodyPartIndexStart];
        
        constructVertexPositions(collisionBodyPartDataOne, collisionBodyDataOne, bodyPartVerticesOne, bodyPartCenterVertexOne, bodyPartVerticeLengthsOne);
        
        for (int j = 0; j < collisionBodyDataTwo.BodyPartIndexLength; j++)
        {
            CollisionBodyPartData collisionBodyPartDataTwo = BodyPartDataBufferTwo[j + collisionBodyDataTwo.BodyPartIndexStart];
            
            constructVertexPositions(collisionBodyPartDataTwo, collisionBodyDataTwo, bodyPartVerticesTwo, bodyPartCenterVertexTwo, bodyPartVerticeLengthsTwo);
            
            bool doBodyPartsCollide = doBodyPartsOverlap(bodyPartVerticesOne, bodyPartVerticeLengthsOne, bodyPartVerticesTwo, bodyPartVerticeLengthsTwo);
            if (!doBodyPartsCollide)
                continue;
            
            doBodyPartsCollide = doBodyPartsOverlap(bodyPartVerticesTwo, bodyPartVerticeLengthsTwo, bodyPartVerticesOne, bodyPartVerticeLengthsOne);
            if (!doBodyPartsCollide)
                continue;
            
            int collisionAmount;
            int _;
            CollisionResultBuffer.InterlockedAdd(0, 1, collisionAmount);
    
            if (collisionAmount < ColliderShapeResultBufferLength)
            {
                CollisionResultBuffer.Store(12 + collisionAmount * 12 + 0, x);
                CollisionResultBuffer.Store(12 + collisionAmount * 12 + 4, y);
                CollisionResultBuffer.Store(12 + collisionAmount * 12 + 8, 0);
            }
            
            return;
        }
    }
}