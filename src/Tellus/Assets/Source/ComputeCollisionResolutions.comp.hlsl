// https://dyn4j.org/2010/01/sat/
// https://www.metanetsoftware.com/technique/tutorialA.html

#include "./ComputeCollisionCommon.comp.hlsl"

StructuredBuffer<CollisionBodyPartData> BodyPartDataBufferMovable : register(t0, space0);
StructuredBuffer<CollisionBodyPartData> BodyPartDataBufferImmovable : register(t1, space0);
StructuredBuffer<CollisionBodyData> BodyDataBufferMovable : register(t2, space0);
StructuredBuffer<CollisionBodyData> BodyDataBufferImmovable : register(t3, space0);

RWByteAddressBuffer CollisionResultBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    uint StoredBodyCountMovable;
    uint StoredBodyCountImmovable;
    uint ColliderShapeResultBufferLength;
};

float getProjectionOverlap(float2 projectionOne, float2 projectionTwo)
{
    float start = projectionOne.x > projectionTwo.x ? projectionOne.x : projectionTwo.x;
    float end = projectionOne.y < projectionTwo.y ? projectionOne.y : projectionTwo.y;
    return end - start;
}

float4 doBodyPartsOverlap(float2 shapeVerticesOne[16], int shapeVerticesOneAmount, float2 shapeVerticesTwo[16], int shapeVerticesTwoAmount)
{
    float minimumTransitionVectorLength = 999999.9;
    float2 minimumTransitionVectorDirection = float2(0, 0);
    
    for (int i = 0; i < shapeVerticesOneAmount; i++)
    {
        int j = (i == (shapeVerticesOneAmount - 1)) ? 0 : (i + 1);
        float2 vertexOne = shapeVerticesOne[i];
        float2 vertexTwo = shapeVerticesOne[j];
        
        float2 edge = vertexTwo - vertexOne;
        float2 normal = float2(-edge.y, edge.x);
        float2 axis = normalize(normal);
        
        float2 shapeOneProjection = projectVerticesOnAxis(shapeVerticesOne, shapeVerticesOneAmount, axis);
        float2 shapeTwoProjection = projectVerticesOnAxis(shapeVerticesTwo, shapeVerticesTwoAmount, axis);
        
        if (!doProjectionsOverlap(shapeOneProjection, shapeTwoProjection))
        {
            return float4(0.0, 0.0, 0.0, 0.0);
        }
        
        float currentMtvLength = getProjectionOverlap(shapeOneProjection, shapeTwoProjection);
        if (minimumTransitionVectorLength > currentMtvLength)
        {
            minimumTransitionVectorLength = currentMtvLength;
            minimumTransitionVectorDirection = axis;
        }
    }
    return float4(1.0, minimumTransitionVectorLength, minimumTransitionVectorDirection.x, minimumTransitionVectorDirection.y);
}

[numthreads(16, 1, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    
    if (x >= StoredBodyCountMovable)
    {
        return;
    }
    
    CollisionBodyData collisionBodyDataMovable = BodyDataBufferMovable[x];
    
    float2 bodyPartVerticesMovable[16];
    int bodyPartVerticeLengthsMovable;
    float2 bodyPartVerticesImmovable[16];
    int bodyPartVerticeLengthsImmovable;
    
    float2 totalMinimumTransitionVector = float2(0.0, 0.0);
    
    while (true)
    {
        bool shouldBreak = true;
        
        for (int i = 0; i < StoredBodyCountImmovable; i++)
        {
            if (!shouldBreak)
                break;
            
            CollisionBodyData collisionBodyDataImmovable = BodyDataBufferImmovable[i];
        
            for (int j = 0; j < collisionBodyDataMovable.BodyPartIndexLength; j++)
            {
                if (!shouldBreak)
                    break;
                
                CollisionBodyPartData collisionBodyPartDataMovable = BodyPartDataBufferMovable[j + collisionBodyDataMovable.BodyPartIndexStart];
            
                constructVertexPositions(collisionBodyPartDataMovable, collisionBodyDataMovable, bodyPartVerticesMovable, bodyPartVerticeLengthsMovable);
            
                for (int k = 0; k < collisionBodyDataImmovable.BodyPartIndexLength; k++)
                {
                    CollisionBodyPartData collisionBodyPartDataImmovable = BodyPartDataBufferImmovable[k + collisionBodyDataImmovable.BodyPartIndexStart];
            
                    constructVertexPositions(collisionBodyPartDataImmovable, collisionBodyDataImmovable, bodyPartVerticesImmovable, bodyPartVerticeLengthsImmovable);
                
                    float4 overlapInfoOne = doBodyPartsOverlap(bodyPartVerticesMovable, bodyPartVerticeLengthsMovable, bodyPartVerticesImmovable, bodyPartVerticeLengthsImmovable);
            
                    bool doBodyPartsCollideOne = overlapInfoOne.x != 0.0;
                    float minimumTransitionVectorLengthOne = overlapInfoOne.y;
                    float2 minimumTransitionVectorDirectionOne = float2(overlapInfoOne.z, overlapInfoOne.w);
                    
                    if (!doBodyPartsCollideOne)
                        continue;
           
                    float4 overlapInfoTwo = doBodyPartsOverlap(bodyPartVerticesImmovable, bodyPartVerticeLengthsImmovable, bodyPartVerticesMovable, bodyPartVerticeLengthsMovable);
            
                    bool doBodyPartsCollideTwo = overlapInfoTwo.x != 0.0;
                    float minimumTransitionVectorLengthTwo = overlapInfoTwo.y;
                    float2 minimumTransitionVectorDirectionTwo = float2(overlapInfoTwo.z, overlapInfoTwo.w);
            
                    if (!doBodyPartsCollideTwo)
                        continue;
                    
                    float2 minimumTransitionVector = float2(0.0, 0.0);
                    if (minimumTransitionVectorLengthOne < minimumTransitionVectorLengthTwo)
                    {
                        minimumTransitionVector = minimumTransitionVectorDirectionOne * minimumTransitionVectorLengthOne;
                    }
                    else
                    {
                        minimumTransitionVector = minimumTransitionVectorDirectionTwo * minimumTransitionVectorLengthTwo;
                    }
            
                    totalMinimumTransitionVector += minimumTransitionVector;
                    collisionBodyDataMovable.Offset += minimumTransitionVector;
                    shouldBreak = false;
                    break;
                }
            }
        }
        
        if (shouldBreak)
            break;
    }
    
    int collisionAmount;
    int _;
    CollisionResultBuffer.InterlockedAdd(0, 1, collisionAmount);
    
    if (collisionAmount < ColliderShapeResultBufferLength)
    {
        CollisionResultBuffer.Store(12 + collisionAmount * 12 + 0, x);
        uint totalMtvX = asuint(totalMinimumTransitionVector.x);
        CollisionResultBuffer.Store(12 + collisionAmount * 12 + 4, totalMtvX);
        uint totalMtvY = asuint(totalMinimumTransitionVector.y);
        CollisionResultBuffer.Store(12 + collisionAmount * 12 + 8, totalMtvY);
    }
}