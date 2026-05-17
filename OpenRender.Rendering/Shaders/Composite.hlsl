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

Texture2D sceneColor : register(t0);
Texture2D ssaoColor  : register(t1);
Texture2D bloomBlur  : register(t2);
SamplerState gSampler : register(s0);

float4 PSMain(VSOutput input) : SV_TARGET {
    float3 hdrColor = sceneColor.Sample(gSampler, input.TexCoord).rgb;
    float ssao = ssaoColor.Sample(gSampler, input.TexCoord).r;
    float3 bloom = bloomBlur.Sample(gSampler, input.TexCoord).rgb;

    // Apply SSAO to ambient term (simplified here as global multiplier)
    hdrColor *= ssao;
    
    // Add Bloom
    hdrColor += bloom;

    // --- ACES Tonemapping ---
    const float a = 2.51f;
    const float b = 0.03f;
    const float c = 2.43f;
    const float d = 0.59f;
    const float e = 0.14f;
    float3 color = clamp((hdrColor * (a * hdrColor + b)) / (hdrColor * (c * hdrColor + d) + e), 0.0, 1.0);

    // Gamma correction
    color = pow(color, float3(1.0/2.2, 1.0/2.2, 1.0/2.2));

    return float4(color, 1.0);
}
