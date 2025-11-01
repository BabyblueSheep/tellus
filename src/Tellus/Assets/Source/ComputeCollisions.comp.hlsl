// https://dyn4j.org/2010/01/sat/
// https://www.metanetsoftware.com/technique/tutorialA.html

struct CollisionBodyPartData
{
	int ColliderIndex;
    int ShapeType;
    float2 Center;
    float4 DecimalFields;
    int2 IntegerFields;
    
    int Padding1;
    int Padding2;
};

StructuredBuffer<CollisionBodyPartData> ShapeDataBufferOne : register(t0, space0);
StructuredBuffer<CollisionBodyPartData> ShapeDataBufferTwo : register(t1, space0);

RWByteAddressBuffer CollisionResultBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    int ColliderShapeBufferOneLength;
    int ColliderShapeBufferTwoLength;
    int ColliderShapeResultBufferLength;
};

#define TAU 6.28318530718

#define CIRCLE_TYPE 0
#define RECTANGLE_TYPE 1
#define TRIANGLE_TYPE 2
#define LINE_TYPE 3

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

void constructVertexPositions(CollisionBodyPartData bodyPartData, out float2 vertexPositions[16], out int vertexAmount)
{
    vertexAmount = 1;
    for (int j = 0; j < 16; j++)
    {
        vertexPositions[j] = float2(0, 0);
    }
    
    if (bodyPartData.ShapeType == CIRCLE_TYPE)
    {
        vertexAmount = bodyPartData.IntegerFields.x;
        float radius = bodyPartData.DecimalFields.x;
        for (int i = 0; i < vertexAmount; i++)
        {
            vertexPositions[i] = bodyPartData.Center + float2(cos(TAU * i / vertexAmount), sin(TAU * i / vertexAmount)) * radius;
        }
    }
    else if (bodyPartData.ShapeType == RECTANGLE_TYPE)
    {
        vertexAmount = 4;
        
        float angle = bodyPartData.DecimalFields.x;
        float sine = sin(angle);
        float cosine = cos(angle);
        
        float sideA = bodyPartData.DecimalFields.y;
        float sideB = bodyPartData.DecimalFields.z;
        
        vertexPositions[0] = float2(-sideA * 0.5, -sideB * 0.5);
        vertexPositions[1] = float2(sideA * 0.5, -sideB * 0.5);
        vertexPositions[2] = float2(sideA * 0.5, sideB * 0.5);
        vertexPositions[3] = float2(-sideA * 0.5, sideB * 0.5);
        for (int i = 0; i < 4; i++)
        {
            float newX = (vertexPositions[i].x * cosine) + (vertexPositions[i].y * (-sine));
            float newY = (vertexPositions[i].x * sine) + (vertexPositions[i].y * cosine);
            vertexPositions[i].x = newX;
            vertexPositions[i].y = newY;
            
            vertexPositions[i] += bodyPartData.Center;
        }
    }
    else if (bodyPartData.ShapeType == TRIANGLE_TYPE)
    {
        vertexAmount = 3;
        
        vertexPositions[0] = bodyPartData.Center;
        vertexPositions[1] = float2(bodyPartData.DecimalFields.x, bodyPartData.DecimalFields.y);
        vertexPositions[2] = float2(bodyPartData.DecimalFields.z, bodyPartData.DecimalFields.w);
    }
    else if (bodyPartData.ShapeType == LINE_TYPE)
    {
        vertexAmount = 2;
        vertexPositions[0] = bodyPartData.Center;
        vertexPositions[1] = float2(bodyPartData.DecimalFields.x, bodyPartData.DecimalFields.y);
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
    
    CollisionBodyPartData collisionBodyPartDataOne = ShapeDataBufferOne[x];
    CollisionBodyPartData collisionBodyPartDataTwo = ShapeDataBufferTwo[y];
    
    float2 shapeVerticesOne[16], shapeVerticesTwo[16];
    int shapeVertexAmountOne, shapeVertexAmountTwo;
    constructVertexPositions(collisionBodyPartDataOne, shapeVerticesOne, shapeVertexAmountOne);
    constructVertexPositions(collisionBodyPartDataTwo, shapeVerticesTwo, shapeVertexAmountTwo);
    
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
    
    int collisionAmount;
    int _;
    CollisionResultBuffer.InterlockedAdd(0, 1, collisionAmount);
    if (collisionAmount < ColliderShapeResultBufferLength)
    {
        CollisionResultBuffer.InterlockedAdd(4 + collisionAmount * 4, 1, _);
    }
}