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

struct LineData
{
    float2 Origin;
    float2 Vector;
    float Length;
    int Flags;
};

struct LineCollectionData
{
    int LineIndexStart;
    int LineIndexLength;
    float2 Offset;
    int LineVelocityIndex;
    int Padding;
};

#define TAU 6.28318530718
#define EPSILON 0.001

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
// https://theswissbay.ch/pdf/Gentoomen%20Library/Game%20Development/Programming/Graphics%20Gems%203.pdf
bool getLineLineIntersection(float4 lineOne, float4 lineTwo, out float2 intersectionPoint)
{
    intersectionPoint = 0;

    float2 lineOneDirection = lineOne.zw - lineOne.xy;
    float2 lineTwoDirection = lineTwo.zw - lineTwo.xy;
    float2 originDifference = lineTwo.xy - lineOne.xy;
    
    float lineOneNominator = cross(originDifference, lineTwoDirection);
    float lineTwoNominator = cross(originDifference, lineOneDirection);
    float denominator = cross(lineOneDirection, lineTwoDirection);
    
    
    bool denominatorIsZero = abs(denominator) < EPSILON;
    bool nominatorTwoIsZero = abs(lineTwoNominator) < EPSILON;
    
    bool areLinesCollinear = denominatorIsZero && nominatorTwoIsZero;
    bool areLinesParallel = denominatorIsZero && !nominatorTwoIsZero;
    bool areLinesIntersecting = !denominatorIsZero;
    
    if (areLinesCollinear)
    {
        return false; // TODO: figure out what to do here
    }
    else if (areLinesParallel)
    {
        return false;
    }
    else if (areLinesIntersecting)
    {
        float lineProgressOne = lineOneNominator / denominator;
        float lineProgressTwo = lineTwoNominator / denominator;
        
        if ((lineProgressOne >= 0.0 && lineProgressOne <= 1.0) && (lineProgressTwo >= 0.0 && lineProgressTwo <= 1.0))
        {
            intersectionPoint = lerp(lineTwo.xy, lineTwo.zw, lineProgressTwo);
            return true;
        }
        return false;
    }
    else
    {
        return false;
    }
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
        
        float angle = bodyPartData.DecimalFields.z;
        float sine = sin(angle);
        float cosine = cos(angle);
        
        float sideA = bodyPartData.DecimalFields.x;
        float sideB = bodyPartData.DecimalFields.y;
        
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