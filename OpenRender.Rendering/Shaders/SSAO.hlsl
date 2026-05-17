struct VSOutput {
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

VSOutput VSMain(float2 Position : POSITION, float2 TexCoord : TEXCOORD0) {
    VSOutput output;
    output.Position = float4(Position, 0.0f, 1.0f);
    output.TexCoord = TexCoord;
    return output;
}

Texture2D gPosition : register(t0);
Texture2D gNormal   : register(t1);
Texture2D texNoise  : register(t2);
SamplerState gSampler : register(s0);

cbuffer SSAOParams : register(b0) {
    float4x4 projection;
    float3 samples[64];
    float radius;
    float bias;
    float2 noiseScale;
};

float4 PSMain(VSOutput input) : SV_TARGET {
    float3 fragPos = gPosition.Sample(gSampler, input.TexCoord).xyz;
    float3 normal  = normalize(gNormal.Sample(gSampler, input.TexCoord).rgb);
    float3 randomVec = normalize(texNoise.Sample(gSampler, input.TexCoord * noiseScale).xyz);

    // Create TBN matrix (Tangent, Bitangent, Normal)
    float3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    float3 bitangent = cross(normal, tangent);
    float3x3 TBN = float3x3(tangent, bitangent, normal);

    float occlusion = 0.0;
    for(int i = 0; i < 64; ++i) {
        // From tangent to view-space
        float3 samplePos = mul(TBN, samples[i]); 
        samplePos = fragPos + samplePos * radius; 
        
        // Project sample position to find corresponding texel
        float4 offset = float4(samplePos, 1.0);
        offset = mul(projection, offset); 
        offset.xyz /= offset.w; 
        offset.xyz = offset.xyz * 0.5 + 0.5; 
        
        // Get sample depth
        float sampleDepth = gPosition.Sample(gSampler, offset.xy).z; 
        
        // Range check & accumulate
        float rangeCheck = smoothstep(0.0, 1.0, radius / abs(fragPos.z - sampleDepth));
        occlusion += (sampleDepth >= samplePos.z + bias ? 1.0 : 0.0) * rangeCheck;           
    }
    
    occlusion = 1.0 - (occlusion / 64.0);
    return float4(occlusion, occlusion, occlusion, 1.0);
}
