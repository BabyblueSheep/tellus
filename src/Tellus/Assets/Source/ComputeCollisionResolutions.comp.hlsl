// https://dyn4j.org/2010/01/sat/
// https://www.metanetsoftware.com/technique/tutorialA.html

#include "../ComputeCollisionCommon.comp.hlsl"

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
    float start = max(projectionOne.x, projectionTwo.x);
    float end = min(projectionOne.y, projectionTwo.y);
    
    float result = end - start;
    float direction = projectionOne.x > projectionTwo.x ? 1 : -1;
    return result * direction;
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
        if (abs(minimumTransitionVectorLength) > abs(currentMtvLength))
        {
            minimumTransitionVectorLength = currentMtvLength;
            minimumTransitionVectorDirection = axis;
        }
    }
    
    for (int i = 0; i < shapeVerticesTwoAmount; i++)
    {
        int j = (i == (shapeVerticesTwoAmount - 1)) ? 0 : (i + 1);
        float2 vertexOne = shapeVerticesTwo[i];
        float2 vertexTwo = shapeVerticesTwo[j];
        
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
        if (abs(minimumTransitionVectorLength) > abs(currentMtvLength))
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
    bool hasCollidedWithAnything = false;
    
    for (int iteration = 0; iteration < 16; iteration++)
    {
        bool hasCollidedWithAnythingThisIteration = false;
        
        for (int i = 0; i < collisionBodyDataMovable.BodyPartIndexLength; i++)
        {
            float smallestCurrentMinimumTransitionVectorLength = 999999.9;
            float2 smallestCurrentMinimumTransitionVector = float2(0.0, 0.0);
            
            bool hasCollidedWithAnythingThisBodyPart = false;
            
            CollisionBodyPartData collisionBodyPartDataMovable = BodyPartDataBufferMovable[i + collisionBodyDataMovable.BodyPartIndexStart];
            
            constructVertexPositions(collisionBodyPartDataMovable, collisionBodyDataMovable, bodyPartVerticesMovable, bodyPartVerticeLengthsMovable);
            
            for (int j = 0; j < StoredBodyCountImmovable; j++)
            {
                CollisionBodyData collisionBodyDataImmovable = BodyDataBufferImmovable[j];
                
                for (int k = 0; k < collisionBodyDataImmovable.BodyPartIndexLength; k++)
                {
                    CollisionBodyPartData collisionBodyPartDataImmovable = BodyPartDataBufferImmovable[k + collisionBodyDataImmovable.BodyPartIndexStart];
            
                    constructVertexPositions(collisionBodyPartDataImmovable, collisionBodyDataImmovable, bodyPartVerticesImmovable, bodyPartVerticeLengthsImmovable);
                    
                    float4 overlapInfo = doBodyPartsOverlap(bodyPartVerticesMovable, bodyPartVerticeLengthsMovable, bodyPartVerticesImmovable, bodyPartVerticeLengthsImmovable);
            
                    bool doBodyPartsCollide = overlapInfo.x != 0.0;
                    float minimumTransitionVectorLength = overlapInfo.y;
                    float2 minimumTransitionVectorDirection = float2(overlapInfo.z, overlapInfo.w);
                    
                    if (!doBodyPartsCollide)
                        continue;
                    
                    hasCollidedWithAnything = true;
                    hasCollidedWithAnythingThisIteration = true;
                    hasCollidedWithAnythingThisBodyPart = true;
                    
                    if (abs(smallestCurrentMinimumTransitionVectorLength) > abs(minimumTransitionVectorLength))
                    {
                        float2 minimumTransitionVector = minimumTransitionVectorDirection * minimumTransitionVectorLength;
                        
                        smallestCurrentMinimumTransitionVectorLength = minimumTransitionVectorLength;
                        smallestCurrentMinimumTransitionVector = minimumTransitionVector;
                    }
                }
            }
            
            if (hasCollidedWithAnythingThisBodyPart)
            {
                collisionBodyDataMovable.Offset += smallestCurrentMinimumTransitionVector;
                totalMinimumTransitionVector += smallestCurrentMinimumTransitionVector;
                
                break;
            }
        }
        
        if (!hasCollidedWithAnythingThisIteration)
        {
            break;
        }
    }
    
    if (hasCollidedWithAnything)
    {
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
}