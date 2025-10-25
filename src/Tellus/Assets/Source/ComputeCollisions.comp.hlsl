struct ColliderShapeData
{
	uint ColliderIndex;
    uint Type;
    float2 Center;
    float3 Fields;
};

StructuredBuffer<ColliderShapeData> ColliderShapeBufferOne : register(t0, space0);
StructuredBuffer<ColliderShapeData> ColliderShapeBufferTwo : register(t1, space0);
RWByteAddressBuffer CollisionResultBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    uint ColliderShapeBufferOneLength;
    uint ColliderShapeBufferTwoLength;
};

#define CIRCLE_TYPE 1
#define RECTANGLE_TYPE 2

// https://iquilezles.org/articles/distfunctions2d/
// Rewritten for readability (I'm not sure if that's gonna help, though...)
float sdfCircle(float2 samplePoint, float radius)
{
    return length(samplePoint) - radius;
}

float sdfOrientedBox(float2 samplePoint, float2 spineStartPoint, float2 spineEndPoint, float thickness)
{
    float spineLength = length(spineEndPoint - spineStartPoint);
    float2 normalizedSpine = (spineEndPoint - spineStartPoint) / spineLength;
    float2 localizedSamplePoint = (samplePoint - (spineStartPoint + spineEndPoint) * 0.5);
    float2 axisAlignedSamplePoint = mul(float2x2(normalizedSpine.x, -normalizedSpine.y, normalizedSpine.y, normalizedSpine.x), localizedSamplePoint); // Rotated to always be in one quadrant
    float2 distanceFromBox = abs(axisAlignedSamplePoint) - float2(spineLength, thickness) * 0.5;
    return length(max(distanceFromBox, 0.0)) + min(max(distanceFromBox.x, distanceFromBox.y), 0.0);
}

float distanceFromShapeData(ColliderShapeData shapeData, float2 samplePoint)
{
    float2 center = shapeData.Center;
    float2 relativeSamplePoint = samplePoint - center;
    
    if (shapeData.Type == CIRCLE_TYPE)
    {
        float radius = shapeData.Fields.x;
        
        return sdfCircle(relativeSamplePoint, radius);
    }
    
    else if (shapeData.Type == RECTANGLE_TYPE)
    {
        float angle = shapeData.Fields.x;
        float halfLength = shapeData.Fields.y;
        float halfWidth = shapeData.Fields.z;
        float2 spineOffset = float2(cos(angle), sin(angle));
        return sdfOrientedBox(relativeSamplePoint, spineOffset * halfLength, -spineOffset * halfLength, halfWidth);
    }
    
    return 0.0;
}

[numthreads(16, 16, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    uint y = GlobalInvocationID.y;
    
    if (x >= ColliderShapeBufferOneLength || y >= ColliderShapeBufferTwoLength)
    {
        return;
    }
    
    ColliderShapeData colliderShapeDataOne = ColliderShapeBufferOne[x];
    ColliderShapeData colliderShapeDataTwo = ColliderShapeBufferTwo[y];
     
    float2 scanPoint = colliderShapeDataOne.Center;
    float2 scanDirection = normalize(colliderShapeDataTwo.Center - colliderShapeDataOne.Center);
    
    int stepAmount = 0;
    
    while (stepAmount < 32)
    {
        float distanceToShapeOne = distanceFromShapeData(colliderShapeDataOne, scanPoint);
        float distanceToShapeTwo = distanceFromShapeData(colliderShapeDataTwo, scanPoint);
        
        if (distanceToShapeOne > 0.001)
        {
            break;
        }
        if (distanceToShapeTwo < -0.001)
        {
            uint index = colliderShapeDataTwo.ColliderIndex * 100 + colliderShapeDataOne.ColliderIndex;
            uint _ = 0;
            CollisionResultBuffer.InterlockedAdd(index * 4, 1, _);
            return;
        }
    
        scanPoint += scanDirection * distanceToShapeTwo;
        stepAmount++;
    }
    
    scanPoint = colliderShapeDataTwo.Center;
    scanDirection = normalize(colliderShapeDataOne.Center - colliderShapeDataTwo.Center);
    stepAmount = 0;
    
    while (stepAmount < 32)
    {
        float distanceToShapeOne = distanceFromShapeData(colliderShapeDataOne, scanPoint);
        float distanceToShapeTwo = distanceFromShapeData(colliderShapeDataTwo, scanPoint);
        
        if (distanceToShapeTwo > 0.001)
        {
            break;
        }
        if (distanceToShapeOne < -0.001)
        {
            uint index = colliderShapeDataTwo.ColliderIndex * 100 + colliderShapeDataOne.ColliderIndex;
            uint _ = 0;
            CollisionResultBuffer.InterlockedAdd(index * 4, 1, _);
            return;
        }
    
        scanPoint += scanDirection * distanceToShapeOne;
        stepAmount++;
    }
}