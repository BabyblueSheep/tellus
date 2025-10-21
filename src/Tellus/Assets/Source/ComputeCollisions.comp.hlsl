struct ColliderShapeData
{
	int ColliderIndex;
    uint ShapeType;
    float2 ShapeFieldOne;
    float2 ShapeFieldTwo;
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

bool twoCirclesIntersect(float2 positionOne, float radiusOne, float2 positionTwo, float radiusTwo)
{
    float distanceX = positionOne.x - positionTwo.x;
    float distanceY = positionOne.y - positionTwo.y;
    float distance = sqrt(distanceX * distanceX + distanceY * distanceY);
    return distance < (radiusOne + radiusTwo);
}

[numthreads(16, 16, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint x = GlobalInvocationID.x;
    uint y = GlobalInvocationID.y;
    
    ColliderShapeData colliderShapeDataOne = ColliderShapeBufferOne[x];
    ColliderShapeData colliderShapeDataTwo = ColliderShapeBufferTwo[y];
        
    if (colliderShapeDataOne.ShapeType == CIRCLE_TYPE && colliderShapeDataTwo.ShapeType == CIRCLE_TYPE)
    {
        float2 circlePositionOne = colliderShapeDataOne.ShapeFieldOne;
        float circleRadiusOne = colliderShapeDataOne.ShapeFieldTwo.x;
        float2 circlePositionTwo = colliderShapeDataTwo.ShapeFieldOne;
        float circleRadiusTwo = colliderShapeDataTwo.ShapeFieldTwo.x;
        float result = twoCirclesIntersect(circlePositionOne, circleRadiusOne, circlePositionTwo, circleRadiusTwo);
        if (result)
        {
            CollisionResultData resultData;
            resultData.ColliderIndexOne = colliderShapeDataOne.ColliderIndex;
            resultData.ColliderIndexTwo = colliderShapeDataTwo.ColliderIndex;
            resultData.CollisionResultInformation = float2(0, 0);
            //CollisionResultBuffer.Append(resultData);
        }
    }
    else if (colliderShapeDataOne.ShapeType == LINE_TYPE && colliderShapeDataTwo.ShapeType == LINE_TYPE)
    {
        // TODO
    }
    else if ((colliderShapeDataOne.ShapeType == CIRCLE_TYPE && colliderShapeDataTwo.ShapeType == LINE_TYPE) ||
        (colliderShapeDataOne.ShapeType == LINE_TYPE && colliderShapeDataTwo.ShapeType == CIRCLE_TYPE))
    {
        // TODO
    }

}