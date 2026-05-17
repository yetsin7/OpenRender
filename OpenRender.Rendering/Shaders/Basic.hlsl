struct VSInput {
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD0;
    float3 Tangent : TANGENT;
};

struct VSOutput {
    float4 Position : SV_POSITION;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD0;
};

cbuffer SceneBuffer : register(b0) {
    float4x4 ViewProjection;
};

cbuffer ModelBuffer : register(b1) {
    float4x4 ModelMatrix;
};

VSOutput VSMain(VSInput input) {
    VSOutput output;
    float4 worldPos = mul(ModelMatrix, float4(input.Position, 1.0f));
    output.Position = mul(ViewProjection, worldPos);
    output.Normal = mul((float3x3)ModelMatrix, input.Normal);
    output.TexCoord = input.TexCoord;
    return output;
}

float4 PSMain(VSOutput input) : SV_TARGET {
    float3 lightDir = normalize(float3(1.0f, 1.0f, 1.0f));
    float diff = max(dot(normalize(input.Normal), lightDir), 0.2f);
    return float4(float3(0.8f, 0.8f, 0.8f) * diff, 1.0f);
}
