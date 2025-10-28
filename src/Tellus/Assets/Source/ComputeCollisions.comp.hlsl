// https://dyn4j.org/2010/01/sat/
// https://www.metanetsoftware.com/technique/tutorialA.html

struct ColliderShapeData
{
	int ColliderIndex;
    int ShapeType;
    float2 Center;
    float4 Fields;
};

StructuredBuffer<ColliderShapeData> ShapeDataBufferOne : register(t0, space0);
StructuredBuffer<ColliderShapeData> ShapeDataBufferTwo : register(t1, space0);

RWByteAddressBuffer CollisionResultBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    int ColliderShapeBufferOneLength;
    int ColliderShapeBufferTwoLength;
    int ColliderShapeResultBufferLength;
};

#define TAU 6.28318530718

#define CIRCLE_TYPE 0

float2 projectVerticesOnAxis(float2 vertexPositions[16], int vertexAmount, float2 axis)
{
    float minProjectionPosition = dot(vertexPositions[0], axis);
    float maxProjectionPosition = minProjectionPosition;
    
    for (int i = 1; i < vertexAmount; i++)
    {
        float currentProjectionPosition = dot(vertexPositions[i], axis);
        minProjectionPosition = min(minProjectionPosition, currentProjectionPosition);
        maxProjectionPosition = max(maxProjectionPosition, currentProjectionPosition);
    }

    return float2(minProjectionPosition, maxProjectionPosition);
}

bool doProjectionsOverlap(float2 projectionOne, float2 projectionTwo)
{
    return projectionOne.x <= projectionTwo.y && projectionOne.y >= projectionTwo.x;
}

void constructVertexPositions(ColliderShapeData shapeData, out float2 vertexPositions[16], out int vertexAmount)
{
    vertexAmount = 1;
    for (int j = 0; j < 16; j++)
    {
        vertexPositions[j] = float2(0, 0);
    }
    
    if (shapeData.ShapeType == CIRCLE_TYPE)
    {
        vertexAmount = 12;
        float radius = shapeData.Fields.x;
        for (int i = 0; i < vertexAmount; i++)
        {
            vertexPositions[i] = shapeData.Center + float2(cos(TAU * i / vertexAmount), sin(TAU * i / vertexAmount)) * radius;
        }
    }
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
    
    ColliderShapeData colliderShapeDataOne = ShapeDataBufferOne[x];
    ColliderShapeData colliderShapeDataTwo = ShapeDataBufferTwo[y];
    
    float2 shapeVerticesOne[16], shapeVerticesTwo[16];
    int shapeVertexAmountOne, shapeVertexAmountTwo;
    constructVertexPositions(colliderShapeDataOne, shapeVerticesOne, shapeVertexAmountOne);
    constructVertexPositions(colliderShapeDataTwo, shapeVerticesTwo, shapeVertexAmountTwo);
    
    for (int i = 0; i < shapeVertexAmountOne; i++)
    {
        int j = (i == (shapeVertexAmountOne - 1)) ? 0 : (i + 1);
        float2 vertexOne = shapeVerticesOne[i];
        float2 vertexTwo = shapeVerticesOne[j];
        
        float2 edge = vertexTwo - vertexOne;
        float2 normal = float2(-edge.y, edge.x);
        float2 axis = normalize(normal);
        
        float2 shapeOneProjection = projectVerticesOnAxis(shapeVerticesOne, shapeVertexAmountOne, axis);
        float2 shapeTwoProjection = projectVerticesOnAxis(shapeVerticesTwo, shapeVertexAmountTwo, axis);
        
        if (!doProjectionsOverlap(shapeOneProjection, shapeTwoProjection))
        {
            return;
        }
    }
    
    for (int m = 0; m < shapeVertexAmountTwo; m++)
    {
        int n = (m == (shapeVertexAmountTwo - 1)) ? 0 : (m + 1);
        float2 vertexOne = shapeVerticesTwo[m];
        float2 vertexTwo = shapeVerticesTwo[n];
        
        float2 edge = vertexTwo - vertexOne;
        float2 normal = float2(-edge.y, edge.x);
        float2 axis = normalize(normal);
        
        float2 shapeOneProjection = projectVerticesOnAxis(shapeVerticesOne, shapeVertexAmountOne, axis);
        float2 shapeTwoProjection = projectVerticesOnAxis(shapeVerticesTwo, shapeVertexAmountTwo, axis);
        
        if (!doProjectionsOverlap(shapeOneProjection, shapeTwoProjection))
        {
            return;
        }
    }
    
    uint resultIndex = colliderShapeDataTwo.ColliderIndex * ColliderShapeResultBufferLength + colliderShapeDataOne.ColliderIndex;
    
    int _;
    CollisionResultBuffer.InterlockedAdd(resultIndex * 4, 1, _);
}