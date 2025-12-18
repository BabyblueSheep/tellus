Texture2D<float4> Texture : register(t0, space2);
SamplerState Sampler : register(s0, space2);

struct Input
{
    float2 TexCoord : TEXCOORD0;
    float4 TintColor : TEXCOORD1;
    float4 OffsetColor : TEXCOORD2;
};

float4 main(Input input) : SV_Target0
{
    float4 color = input.TintColor * Texture.Sample(Sampler, input.TexCoord);
    return float4(color.rgb + input.OffsetColor.rgb * input.OffsetColor.a, color.a);
}