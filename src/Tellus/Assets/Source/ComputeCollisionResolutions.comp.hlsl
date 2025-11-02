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

bool doProjectionsOverlap(float2 projectionOne, float2 projectionTwo)
{
    return projectionOne.x <= projectionTwo.y && projectionOne.y >= projectionTwo.x;
}

float getProjectionOverlap(float2 projectionOne, float2 projectionTwo)
{
    float start = projectionOne.x > projectionTwo.x ? projectionOne.x : projectionTwo.x;
    float end = projectionOne.y < projectionTwo.y ? projectionOne.y : projectionTwo.y;
    return end - start;
}

float2 doBodyPartsOverlap(float2 shapeVerticesMain[16], int shapeVerticesMainAmount, float2 shapeVerticesSub[16], int shapeVerticesSubAmount, out float2 minimumTransitionVector)
{
    float smallestMtvLength = 999999.9;
    float2 smallestMtvUnit = float2(0, 0);
    
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
        
        float currentMtvLength = getProjectionOverlap(shapeOneProjection, shapeTwoProjection);
        if (smallestMtvUnit > currentMtvLength)
        {
            smallestMtvLength = currentMtvLength;
            smallestMtvUnit = axis;
        }
    }
    
    minimumTransitionVector = smallestMtvUnit * smallestMtvLength;
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
    
    float2 bodyPartVerticesOne[16][16];
    float bodyPartVerticeLengthsOne[16];
    float2 bodyPartVerticesTwo[16][16];
    float bodyPartVerticeLengthsTwo[16];
    
    for (int i = collisionBodyDataOne.BodyPartIndexStart; i < collisionBodyDataOne.BodyPartIndexStart + collisionBodyDataOne.BodyPartIndexLength; i++)
    {
        CollisionBodyPartData collisionBodyPartDataOne = BodyPartDataBufferOne[i];
        constructVertexPositions(collisionBodyPartDataOne, collisionBodyDataOne, bodyPartVerticesOne[i], bodyPartVerticeLengthsOne[i]);
    }
    
    for (int i = collisionBodyDataTwo.BodyPartIndexStart; i < collisionBodyDataTwo.BodyPartIndexStart + collisionBodyDataTwo.BodyPartIndexLength; i++)
    {
        CollisionBodyPartData collisionBodyPartDataTwo = BodyPartDataBufferTwo[i];
        constructVertexPositions(collisionBodyPartDataTwo, collisionBodyDataTwo, bodyPartVerticesTwo[i], bodyPartVerticeLengthsTwo[i]);
    }
    
    for (int i = collisionBodyDataOne.BodyPartIndexStart; i < collisionBodyDataOne.BodyPartIndexStart + collisionBodyDataOne.BodyPartIndexLength; i++)
    {
        for (int j = collisionBodyDataTwo.BodyPartIndexStart; j < collisionBodyDataTwo.BodyPartIndexStart + collisionBodyDataTwo.BodyPartIndexLength; j++)
        {
            CollisionBodyPartData collisionBodyPartDataOne = BodyPartDataBufferOne[i];
            CollisionBodyPartData collisionBodyPartDataTwo = BodyPartDataBufferTwo[j];
            
            float2 shapeVerticesOne[16], shapeVerticesTwo[16];
            int shapeVertexAmountOne, shapeVertexAmountTwo;
            constructVertexPositions(collisionBodyPartDataOne, BodyDataBufferOne[collisionBodyPartDataOne.CollisionBodyIndex], shapeVerticesOne, shapeVertexAmountOne);
            constructVertexPositions(collisionBodyPartDataTwo, BodyDataBufferTwo[collisionBodyPartDataTwo.CollisionBodyIndex], shapeVerticesTwo, shapeVertexAmountTwo);
            
            bool doBodyPartsCollide = doBodyPartsOverlap(shapeVerticesOne, shapeVertexAmountOne, shapeVerticesTwo, shapeVertexAmountTwo);
            if (!doBodyPartsCollide)
                continue;
            
            doBodyPartsCollide = doBodyPartsOverlap(shapeVerticesTwo, shapeVertexAmountTwo, shapeVerticesOne, shapeVertexAmountOne);
            if (!doBodyPartsCollide)
                continue;
            
            int collisionAmount;
            int _;
            CollisionResultBuffer.InterlockedAdd(0, 1, collisionAmount);
    
            if (collisionAmount < ColliderShapeResultBufferLength)
            {
                CollisionResultBuffer.Store(8 + collisionAmount * 8 + 0, collisionBodyPartDataOne.CollisionBodyIndex);
                CollisionResultBuffer.Store(8 + collisionAmount * 8 + 4, collisionBodyPartDataTwo.CollisionBodyIndex);
            }
            
            return;
        }
    }
}