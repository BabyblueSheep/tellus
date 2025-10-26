struct ColliderShapeData
{
	int ColliderIndex;
    int ShapeIndexRangeStart;
    int ShapeIndexRangeRangeLength;
};

Buffer<float2> ShapeVertexBufferOne : register(t0, space0);
Buffer<float2> ShapeVertexBufferTwo : register(t1, space0);
StructuredBuffer<ColliderShapeData> ShapeIndexRangeBufferOne : register(t2, space0);
StructuredBuffer<ColliderShapeData> ShapeIndexRangeBufferTwo : register(t3, space0);
RWByteAddressBuffer CollisionResultBuffer : register(u0, space1);

cbuffer UniformBlock : register(b0, space2)
{
    uint ColliderShapeBufferOneLength;
    uint ColliderShapeBufferTwoLength;
};

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
    
    
}