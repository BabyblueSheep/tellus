// https://dyn4j.org/2010/01/sat/
// https://www.metanetsoftware.com/technique/tutorialA.html

struct CollisionBodyPartData
{
    int CollisionBodyIndex;
    int ShapeType;
    float2 Center;
    float4 DecimalFields;
    int2 IntegerFields;
    
    int Padding1;
    int Padding2;
};

struct CollisionBodyData
{
    int BodyPartIndexStart;
    int BodyPartIndexLength;
    float2 Offset;
};

struct RayData
{
    float2 Origin;
    float2 Direction;
    float Length;
};

#define TAU 6.28318530718
#define EPSILON 1e-6

#define CIRCLE_TYPE 0
#define RECTANGLE_TYPE 1
#define TRIANGLE_TYPE 2

float cross(float2 x, float2 y)
{
    return x.x * y.y - x.y * y.x;
}

// https://stackoverflow.com/questions/563198/how-do-you-detect-where-two-line-segments-intersect/565282#565282
// https://github.com/pgkelley4/line-segments-intersect/blob/master/js/line-segments-intersect.js

// https://en.wikipedia.org/wiki/Line%E2%80%93line_intersection
// https://mathworld.wolfram.com/Line-LineIntersection.html
bool getLineLineIntersection(RayData lineInfo, RayData rayInfo, out float2 intersectionPoint)
{
    intersectionPoint = 0;
    
    float2 lineFullDirection = lineInfo.Direction * lineInfo.Length;
    float2 rayFullDirection = rayInfo.Direction * rayInfo.Length;
    
    float numerator = cross(rayInfo.Origin - lineInfo.Origin, lineFullDirection);
    float denominator = cross(lineFullDirection, rayFullDirection);
    
    if (numerator == 0 && denominator == 0) // Collinear
    {
        float t0 = dot(rayInfo.Origin - lineInfo.Origin, lineFullDirection) / dot(lineFullDirection, lineFullDirection);
        float t1 = t0 + dot(rayFullDirection, lineFullDirection) / dot(lineFullDirection, lineFullDirection);
        
        float start = (dot(lineFullDirection, rayFullDirection) < 0) ? t1 : t0;
        float end = (dot(lineFullDirection, rayFullDirection) < 0) ? t0 : t1;
        
        if (start <= 1 && end >= 0)
        {
            intersectionPoint = (dot(lineFullDirection, rayFullDirection) < 0) ? lineInfo.Origin : rayInfo.Origin;
            return true;
        }
        return false;
    }
    else if (denominator == 0 && numerator != 0) // Parralel
    {
        return false;
    }
    else if (denominator != 0) // Intersecting
    {
        float u = numerator / denominator;
        float t = cross(rayInfo.Origin - lineInfo.Origin, rayFullDirection) / denominator;
        if (u >= 0 && u <= 1 && t >= 0 && t <= 1)
        {
            intersectionPoint = rayInfo.Origin + rayFullDirection * u;
            return true;
        }
        return false;
    }
    return false;
}

bool doProjectionsOverlap(float2 projectionOne, float2 projectionTwo)
{
    return projectionOne.x <= projectionTwo.y && projectionOne.y >= projectionTwo.x;
}

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

void constructVertexPositions(CollisionBodyPartData bodyPartData, CollisionBodyData bodyData, out float2 vertexPositions[16], out int vertexAmount)
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
    
    for (int k = 0; k < 16; k++)
    {
        vertexPositions[k] += bodyData.Offset;
    }
}