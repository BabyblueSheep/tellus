struct ColliderShapeData
{
	int ColliderIndex;
    uint Type;
    float2 Center;
    float2 FieldOne;
};

struct CollisionResultData
{
    int ColliderIndexOne;
    int ColliderIndexTwo;
    float2 CollisionResultInformation;
};

StructuredBuffer<ColliderShapeData> ColliderShapeBufferOne : register(t0, space0);
StructuredBuffer<ColliderShapeData> ColliderShapeBufferTwo : register(t1, space0);
AppendStructuredBuffer<CollisionResultData> CollisionResultBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    int CollderShapeBufferOneLength : packoffset(c0);
    int CollderShapeBufferTwoLength : packoffset(c1);
};

#define CIRCLE_TYPE 0
#define LINE_TYPE 1

// https://iquilezles.org/articles/distfunctions2d/
float sdfCircle(float2 samplePoint, float radius)
{
    return length(samplePoint) - radius;
}

float sdfSegment(float2 samplePoint, float2 startPoint, float2 endPoint)
{
    float2 directionToOrigin = samplePoint - startPoint;
    float2 directionToEnd = endPoint - startPoint;
    float h = saturate(dot(directionToOrigin, directionToEnd) / dot(directionToEnd, directionToEnd));
    return length(directionToOrigin - directionToEnd * h);
}

[numthreads(16, 16, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    uint y = GlobalInvocationID.y;
    
    if (x >= CollderShapeBufferOneLength || y >= CollderShapeBufferTwoLength)
    {
        return;
    }
    
    ColliderShapeData colliderShapeDataOne = ColliderShapeBufferOne[x];
    ColliderShapeData colliderShapeDataTwo = ColliderShapeBufferTwo[y];
        
   

}