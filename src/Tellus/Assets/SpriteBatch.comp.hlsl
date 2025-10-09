struct SpriteInstanceData
{
    float3 Position;
    float Rotation;
    float2 Scale;
    float4 Color;
};

struct SpriteVertexData
{
    float4 Position;
    float2 TextureCoordinate;
    float4 Color;
};

StructuredBuffer<SpriteInstanceData> InstanceBuffer : register(t0, space0);
RWStructuredBuffer<SpriteVertexData> VertexBuffer : register(u0, space1);

[numthreads(64, 1, 1)]
void main(uint3 GlobalInvocationID : SV_DispatchThreadID)
{
    uint n = GlobalInvocationID.x;

	SpriteInstanceData currentSpriteData = InstanceBuffer[n];

    float4x4 Scale = float4x4(
        float4(currentSpriteData.Scale.x, 0.0f, 0.0f, 0.0f),
        float4(0.0f, currentSpriteData.Scale.y, 0.0f, 0.0f),
        float4(0.0f, 0.0f, 1.0f, 0.0f),
        float4(0.0f, 0.0f, 0.0f, 1.0f)
    );

    float c = cos(currentSpriteData.Rotation);
    float s = sin(currentSpriteData.Rotation);

    float4x4 Rotation = float4x4(
        float4(   c,    s, 0.0f, 0.0f),
        float4(  -s,    c, 0.0f, 0.0f),
        float4(0.0f, 0.0f, 1.0f, 0.0f),
        float4(0.0f, 0.0f, 0.0f, 1.0f)
    );

    float4x4 Translation = float4x4(
        float4(1.0f, 0.0f, 0.0f, 0.0f),
        float4(0.0f, 1.0f, 0.0f, 0.0f),
        float4(0.0f, 0.0f, 1.0f, 0.0f),
        float4(currentSpriteData.Position.x, currentSpriteData.Position.y, currentSpriteData.Position.z, 1.0f)
    );

    float4x4 Model = mul(Scale, mul(Rotation, Translation));

    float4 topLeft = float4(0.0f, 0.0f, 0.0f, 1.0f);
    float4 topRight = float4(1.0f, 0.0f, 0.0f, 1.0f);
    float4 bottomLeft = float4(0.0f, 1.0f, 0.0f, 1.0f);
    float4 bottomRight = float4(1.0f, 1.0f, 0.0f, 1.0f);

	VertexBuffer[n * 4u].Position = mul(topLeft, Model);
	VertexBuffer[n * 4u + 1].Position = mul(topRight, Model);
	VertexBuffer[n * 4u + 2].Position = mul(bottomLeft, Model);
	VertexBuffer[n * 4u + 3].Position = mul(bottomRight, Model);

	VertexBuffer[n * 4u].TextureCoordinate = float2(0.0f, 0.0f);
	VertexBuffer[n * 4u + 1].TextureCoordinate = float2(1.0f, 0.0f);
	VertexBuffer[n * 4u + 2].TextureCoordinate = float2(0.0f, 1.0f);
	VertexBuffer[n * 4u + 3].TextureCoordinate = float2(1.0f, 1.0f);

    VertexBuffer[n * 4u]    .Color = currentSpriteData.Color;
    VertexBuffer[n * 4u + 1].Color = currentSpriteData.Color;
    VertexBuffer[n * 4u + 2].Color = currentSpriteData.Color;
    VertexBuffer[n * 4u + 3].Color = currentSpriteData.Color;
}