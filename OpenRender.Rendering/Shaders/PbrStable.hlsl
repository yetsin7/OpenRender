struct VSInput {
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD0;
    float3 Tangent : TANGENT;
    float4 InstanceRow1 : INSTANCE_M1;
    float4 InstanceRow2 : INSTANCE_M2;
    float4 InstanceRow3 : INSTANCE_M3;
    float4 InstanceRow4 : INSTANCE_M4;
};

struct VSOutput {
    float4 Position : SV_POSITION;
    float3 WorldPos : POSITION;
    float3 Normal : NORMAL;
};

cbuffer SceneBuffer : register(b0) {
    float4x4 ViewProjection;
    float3 CameraPos;
    float _pad0;
    float3 LightDir;
    float _pad1;
    float3 LightColor;
    float LightIntensity;
    float Time;
};

VSOutput VSMain(VSInput input) {
    VSOutput output;
    float4x4 instanceMatrix = float4x4(input.InstanceRow1, input.InstanceRow2, input.InstanceRow3, input.InstanceRow4);
    float4 worldPos = mul(instanceMatrix, float4(input.Position, 1.0f));
    output.WorldPos = worldPos.xyz;
    output.Position = mul(ViewProjection, worldPos);
    output.Normal = normalize(mul((float3x3)instanceMatrix, input.Normal));
    return output;
}

static const float PI = 3.14159265359;

float DistributionGGX(float3 N, float3 H, float roughness) {
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;
    return num / denom;
}

float GeometrySchlickGGX(float NdotV, float roughness) {
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;
    float num = NdotV;
    float denom = NdotV * (1.0 - k) + k;
    return num / denom;
}

float GeometrySmith(float3 N, float3 V, float3 L, float roughness) {
    return GeometrySchlickGGX(max(dot(N, V), 0.0), roughness) * GeometrySchlickGGX(max(dot(N, L), 0.0), roughness);
}

float3 FresnelSchlick(float cosTheta, float3 F0) {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

float4 PSMain(VSOutput input) : SV_TARGET {
    float3 albedo = float3(0.83f, 0.84f, 0.86f);
    float roughness = 0.58f;
    float metallic = 0.02f;

    float3 N = normalize(input.Normal);
    float3 V = normalize(CameraPos - input.WorldPos);
    float3 L = normalize(-LightDir);
    float3 H = normalize(V + L);

    float3 F0 = lerp(float3(0.04f, 0.04f, 0.04f), albedo, metallic);
    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    float3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);

    float3 specular = (NDF * G * F) / max(4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0), 0.0001);
    float3 kS = F;
    float3 kD = (1.0 - kS) * (1.0 - metallic);
    float3 diffuse = kD * albedo / PI;
    float3 ambient = albedo * 0.10f;
    float3 direct = (diffuse + specular) * LightColor * LightIntensity * max(dot(N, L), 0.0);
    float3 color = ambient + direct;

    color = color / (color + 1.0);
    color = pow(color, float3(1.0 / 2.2, 1.0 / 2.2, 1.0 / 2.2));
    return float4(color, 1.0);
}
