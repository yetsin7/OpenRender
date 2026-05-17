struct VSInput {
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD0;
    float3 Tangent : TANGENT;
    // Instance Data
    float4 InstanceRow1 : INSTANCE_M1;
    float4 InstanceRow2 : INSTANCE_M2;
    float4 InstanceRow3 : INSTANCE_M3;
    float4 InstanceRow4 : INSTANCE_M4;
};

struct VSOutput {
    float4 Position : SV_POSITION;
    float3 WorldPos : POSITION;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD0;
    float3 Tangent : TANGENT;
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
    
    // Construct Instance Matrix
    float4x4 instanceMatrix = float4x4(input.InstanceRow1, input.InstanceRow2, input.InstanceRow3, input.InstanceRow4);
    
    float3 localPos = input.Position;
    
    // Simple Wind Animation (Procedural)
    // We use the world Y and X to vary wind, and Time to animate.
    float windStrength = 0.2;
    float windSpeed = 2.0;
    if (localPos.y > 0.1) { // Only sway tops
        float sway = sin(Time * windSpeed + instanceMatrix[3][0] + instanceMatrix[3][2]) * windStrength * localPos.y;
        localPos.x += sway;
        localPos.z += sway * 0.5;
    }

    float4 worldPos = mul(instanceMatrix, float4(localPos, 1.0f));
    output.WorldPos = worldPos.xyz;
    output.Position = mul(ViewProjection, worldPos);
    
    // Transform normal with instance matrix
    output.Normal = normalize(mul((float3x3)instanceMatrix, input.Normal));
    output.TexCoord = input.TexCoord;
    output.Tangent = normalize(mul((float3x3)instanceMatrix, input.Tangent));
    
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
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;
    float num = NdotV;
    float denom = NdotV * (1.0 - k) + k;
    return num / denom;
}

float GeometrySmith(float3 N, float3 V, float3 L, float roughness) {
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);
    return ggx1 * ggx2;
}

float3 fresnelSchlick(float cosTheta, float3 F0) {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}
struct PSOutput {
    float4 Color    : SV_Target0;
    float4 Position : SV_Target1;
    float4 Normal   : SV_Target2;
};

PSOutput PSMain(VSOutput input) : SV_TARGET {
    float3 albedo = float3(0.8f, 0.8f, 0.8f);
    float roughness = 0.5f;
    float metallic = 0.0f;

    float3 N = normalize(input.Normal);
    float3 V = normalize(CameraPos - input.WorldPos);

    float3 F0 = float3(0.04, 0.04, 0.04); 
    F0 = lerp(F0, albedo, metallic);

    float3 L = normalize(-LightDir);
    float3 H = normalize(V + L);
    float3 radiance = LightColor * LightIntensity;

    float NDF = DistributionGGX(N, H, roughness);   
    float G = GeometrySmith(N, V, L, roughness);      
    float3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);           

    float3 numerator = NDF * G * F; 
    float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001; 
    float3 specular = numerator / denominator;

    float3 kS = F;
    float3 kD = float3(1.0, 1.0, 1.0) - kS;
    kD *= 1.0 - metallic;	  

    float NdotL = max(dot(N, L), 0.0);        

    float3 lo = (kD * albedo / PI + specular) * radiance * NdotL;
    float3 ambient = float3(0.03, 0.03, 0.03) * albedo;

    float3 color = ambient + lo;

    // HDR Tonemapping (ACES approximation)
    color = color / (color + float3(1.0, 1.0, 1.0));
    color = pow(color, float3(1.0/2.2, 1.0/2.2, 1.0/2.2)); 

    PSOutput output;
    output.Color = float4(color, 1.0);
    output.Position = float4(input.WorldPos, 1.0);
    output.Normal = float4(N, 1.0);
    return output;
}

