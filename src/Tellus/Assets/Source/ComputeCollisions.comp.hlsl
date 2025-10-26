// https://dyn4j.org/2010/01/sat/
// https://www.metanetsoftware.com/technique/tutorialA.html

struct ColliderShapeData
{
	int ColliderIndex;
    int ShapeIndexRangeStart;
    int ShapeIndexRangeRangeLength;
    int Padding;
};

Buffer<float2> ShapeVertexBufferOne : register(t0, space0);
Buffer<float2> ShapeVertexBufferTwo : register(t1, space0);
StructuredBuffer<ColliderShapeData> ShapeIndexRangeBufferOne : register(t2, space0);
StructuredBuffer<ColliderShapeData> ShapeIndexRangeBufferTwo : register(t3, space0);

RWByteAddressBuffer CollisionResultBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    int ColliderShapeBufferOneLength;
    int ColliderShapeBufferTwoLength;
    int ColliderShapeResultBufferLength;
};

float2 projectVerticesOnAxis(Buffer<float2> vertexBuffer, int indexStart, int indexEnd, float2 axis)
{
    float minProjectionPosition = dot(vertexBuffer[indexStart], axis);
    float maxProjectionPosition = minProjectionPosition;
    
    for (int i = indexStart + 1; i <= indexEnd; i++)
    {
        float currentProjectionPosition = dot(vertexBuffer[i], axis);
        minProjectionPosition = min(minProjectionPosition, currentProjectionPosition);
        maxProjectionPosition = max(maxProjectionPosition, currentProjectionPosition);
    }

    return float2(minProjectionPosition, maxProjectionPosition);
}

bool doProjectionsOverlap(float2 projectionOne, float2 projectionTwo)
{
    return projectionOne.x <= projectionTwo.y && projectionOne.y >= projectionTwo.x;
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
    
    ColliderShapeData colliderShapeDataOne = ShapeIndexRangeBufferOne[x];
    ColliderShapeData colliderShapeDataTwo = ShapeIndexRangeBufferTwo[y];
    
    int indexRangeOneStart = colliderShapeDataOne.ShapeIndexRangeStart;
    int indexRangeOneEnd = indexRangeOneStart + colliderShapeDataOne.ShapeIndexRangeRangeLength - 1;
    int indexRangeTwoStart = colliderShapeDataTwo.ShapeIndexRangeStart;
    int indexRangeTwoEnd = indexRangeTwoStart + colliderShapeDataTwo.ShapeIndexRangeRangeLength - 1;
    
    uint resultIndex = colliderShapeDataTwo.ColliderIndex * ColliderShapeResultBufferLength + colliderShapeDataOne.ColliderIndex;
    
    for (int i = indexRangeOneStart; i <= indexRangeOneEnd; i++)
    {
        int j = i == indexRangeOneEnd ? indexRangeOneStart : (i + 1);
        float2 vertexOne = ShapeVertexBufferOne[i];
        float2 vertexTwo = ShapeVertexBufferOne[j];
        
        float2 edge = vertexTwo - vertexOne;
        float2 normal = float2(-edge.y, edge.x);
        float2 axis = normalize(normal);
        
        float2 shapeOneProjection = projectVerticesOnAxis(ShapeVertexBufferOne, indexRangeOneStart, indexRangeOneEnd, axis);
        float2 shapeTwoProjection = projectVerticesOnAxis(ShapeVertexBufferTwo, indexRangeTwoStart, indexRangeTwoEnd, axis);
        
        if (!doProjectionsOverlap(shapeOneProjection, shapeTwoProjection))
        {
            CollisionResultBuffer.Store(resultIndex * 4, 2);
            return;
        }
    }

    for (int i = indexRangeTwoStart; i <= indexRangeTwoEnd; i++)
    {
        int j = i == indexRangeTwoEnd ? indexRangeTwoStart : (i + 1);
        float2 vertexOne = ShapeVertexBufferTwo[i];
        float2 vertexTwo = ShapeVertexBufferTwo[j];
        
        float2 edge = vertexTwo - vertexOne;
        float2 normal = float2(-edge.y, edge.x);
        float2 axis = normalize(normal);
        
        float2 shapeOneProjection = projectVerticesOnAxis(ShapeVertexBufferOne, indexRangeOneStart, indexRangeOneEnd, axis);
        float2 shapeTwoProjection = projectVerticesOnAxis(ShapeVertexBufferTwo, indexRangeTwoStart, indexRangeTwoEnd, axis);
        
        if (!doProjectionsOverlap(shapeOneProjection, shapeTwoProjection))
        {
            CollisionResultBuffer.Store(resultIndex * 4, 3);
            return;
        }
    }
    
    CollisionResultBuffer.Store(resultIndex * 4, 5);
}