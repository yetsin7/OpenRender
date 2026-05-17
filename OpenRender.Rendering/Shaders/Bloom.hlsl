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
SamplerState gSampler : register(s0);

float4 PSMain(VSOutput input) : SV_TARGET {
    float3 color = sceneColor.Sample(gSampler, input.TexCoord).rgb;
    
    // Extract brightness
    float brightness = dot(color, float3(0.2126, 0.7152, 0.0722));
    if(brightness > 1.0)
        return float4(color, 1.0);
    else
        return float4(0.0, 0.0, 0.0, 1.0);
}
