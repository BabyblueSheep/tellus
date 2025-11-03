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

#define TAU 6.28318530718

#define CIRCLE_TYPE 0
#define RECTANGLE_TYPE 1
#define TRIANGLE_TYPE 2
#define LINE_TYPE 3

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

void constructVertexPositions(CollisionBodyPartData bodyPartData, CollisionBodyData bodyData, out float2 vertexPositions[16], float2 shapeCenterVertex, out int vertexAmount)
{
    vertexAmount = 1;
    for (int j = 0; j < 16; j++)
    {
        vertexPositions[j] = float2(0, 0);
    }
    shapeCenterVertex = float2(0, 0);
    
    if (bodyPartData.ShapeType == CIRCLE_TYPE)
    {
        vertexAmount = bodyPartData.IntegerFields.x;
        float radius = bodyPartData.DecimalFields.x;
        for (int i = 0; i < vertexAmount; i++)
        {
            vertexPositions[i] = bodyPartData.Center + float2(cos(TAU * i / vertexAmount), sin(TAU * i / vertexAmount)) * radius;
        }
        shapeCenterVertex = bodyPartData.Center;
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
        shapeCenterVertex = bodyPartData.Center;
    }
    else if (bodyPartData.ShapeType == TRIANGLE_TYPE)
    {
        vertexAmount = 3;
        
        vertexPositions[0] = bodyPartData.Center;
        vertexPositions[1] = float2(bodyPartData.DecimalFields.x, bodyPartData.DecimalFields.y);
        vertexPositions[2] = float2(bodyPartData.DecimalFields.z, bodyPartData.DecimalFields.w);
        shapeCenterVertex = (vertexPositions[0] + vertexPositions[1] + vertexPositions[2]) / 3.0;

    }
    else if (bodyPartData.ShapeType == LINE_TYPE)
    {
        vertexAmount = 2;
        vertexPositions[0] = bodyPartData.Center;
        vertexPositions[1] = float2(bodyPartData.DecimalFields.x, bodyPartData.DecimalFields.y);
        shapeCenterVertex = (vertexPositions[0] + vertexPositions[1]) / 2.0;
    }
    
    for (int k = 0; k < 16; k++)
    {
        vertexPositions[k] += bodyData.Offset;
    }
    shapeCenterVertex += bodyData.Offset;
}