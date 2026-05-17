namespace OpenRender.Rendering.Shaders;

/// <summary>
/// Contains GLSL shader source code for the PBR rendering pipeline.
/// </summary>
public static class ShaderSources
{
    public const string VertexShader = @"#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
uniform mat3 uNormalMatrix;

out vec3 FragPos;
out vec3 Normal;
out vec2 TexCoord;

void main()
{
    vec4 worldPos = uModel * vec4(aPos, 1.0);
    FragPos = worldPos.xyz;
    Normal = uNormalMatrix * aNormal;
    TexCoord = aTexCoord;
    gl_Position = uProjection * uView * worldPos;
}
";

    public const string FragmentShader = @"#version 330 core
in vec3 FragPos;
in vec3 Normal;
in vec2 TexCoord;

// Material properties
uniform vec3 uAlbedo;
uniform float uMetallic;
uniform float uRoughness;
uniform float uAmbientOcclusion;
uniform float uOpacity;
uniform float uNormalStrength;
uniform float uUvScale;

uniform sampler2D uAlbedoMap;
uniform sampler2D uNormalMap;
uniform sampler2D uRoughnessMap;
uniform sampler2D uAoMap;

uniform int uHasAlbedoMap;
uniform int uHasNormalMap;
uniform int uHasRoughnessMap;
uniform int uHasAoMap;

// Lighting
uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform float uLightIntensity;
uniform vec3 uAmbientColor;
uniform float uAmbientIntensity;

// Camera
uniform vec3 uViewPos;

// Tone mapping
uniform float uExposure;
uniform float uGamma;
uniform float uContrast;
uniform float uWhiteBalance;

out vec4 FragColor;

vec3 ResolveNormal(vec2 uv)
{
    vec3 baseNormal = normalize(Normal);
    if (uHasNormalMap == 0)
        return baseNormal;

    vec3 tangentNormal = texture(uNormalMap, uv).xyz * 2.0 - 1.0;
    tangentNormal.xy *= uNormalStrength;
    tangentNormal = normalize(tangentNormal);

    vec3 dp1 = dFdx(FragPos);
    vec3 dp2 = dFdy(FragPos);
    vec2 duv1 = dFdx(uv);
    vec2 duv2 = dFdy(uv);

    float determinant = duv1.x * duv2.y - duv1.y * duv2.x;
    if (abs(determinant) < 0.00001)
        return baseNormal;

    vec3 tangent = normalize((dp1 * duv2.y - dp2 * duv1.y) / determinant);
    tangent = normalize(tangent - baseNormal * dot(baseNormal, tangent));

    vec3 bitangent = normalize((-dp1 * duv2.x + dp2 * duv1.x) / determinant);
    bitangent = normalize(bitangent - baseNormal * dot(baseNormal, bitangent));

    mat3 tbn = mat3(tangent, bitangent, baseNormal);
    return normalize(tbn * tangentNormal);
}

vec3 ApplyWhiteBalance(vec3 color, float whiteBalance)
{
    float warm = max(whiteBalance, 0.0);
    float cool = max(-whiteBalance, 0.0);

    color.r *= 1.0 + warm * 0.16 - cool * 0.04;
    color.g *= 1.0 + warm * 0.03 - cool * 0.01;
    color.b *= 1.0 - warm * 0.12 + cool * 0.18;

    return max(color, vec3(0.0));
}

void main()
{
    vec2 uv = TexCoord * uUvScale;
    vec3 baseColor = uAlbedo;
    if (uHasAlbedoMap == 1)
    {
        vec3 sampledAlbedo = pow(texture(uAlbedoMap, uv).rgb, vec3(2.2));
        baseColor *= sampledAlbedo;
    }

    float roughness = clamp(uRoughness, 0.04, 1.0);
    if (uHasRoughnessMap == 1)
        roughness = clamp(uRoughness * texture(uRoughnessMap, uv).r, 0.04, 1.0);

    float ambientOcclusion = clamp(uAmbientOcclusion, 0.0, 1.0);
    if (uHasAoMap == 1)
        ambientOcclusion *= texture(uAoMap, uv).r;

    vec3 norm = ResolveNormal(uv);
    vec3 lightDir = normalize(-uLightDir);
    vec3 viewDir = normalize(uViewPos - FragPos);
    vec3 halfDir = normalize(lightDir + viewDir);

    // Ambient
    vec3 ambient = uAmbientColor * uAmbientIntensity * baseColor * ambientOcclusion;

    // Diffuse (Lambert)
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * uLightColor * uLightIntensity * baseColor;

    // Specular (Blinn-Phong approximation of GGX)
    float shininess = mix(8.0, 256.0, 1.0 - roughness);
    float spec = pow(max(dot(norm, halfDir), 0.0), shininess) * mix(0.18, 1.0, 1.0 - roughness);
    vec3 specColor = mix(vec3(0.04), baseColor, uMetallic);
    vec3 specular = spec * uLightColor * uLightIntensity * specColor;

    // Combine
    vec3 result = ambient + diffuse + specular;

    // Tone mapping (Reinhard)
    result = vec3(1.0) - exp(-result * uExposure);

    // White balance before gamma
    result = ApplyWhiteBalance(result, uWhiteBalance);

    // Gamma correction
    result = pow(result, vec3(1.0 / uGamma));

    // Contrast pivot around mid-gray
    result = (result - vec3(0.5)) * uContrast + vec3(0.5);
    result = clamp(result, vec3(0.0), vec3(1.0));

    FragColor = vec4(result, uOpacity);
}
";

    public const string GridVertexShader = @"#version 330 core
layout (location = 0) in vec3 aPos;
uniform mat4 uView;
uniform mat4 uProjection;
out vec3 FragPos;
void main()
{
    FragPos = aPos;
    gl_Position = uProjection * uView * vec4(aPos, 1.0);
}
";

    public const string GridFragmentShader = @"#version 330 core
in vec3 FragPos;
uniform vec3 uGridColor;
uniform float uGridAlpha;
out vec4 FragColor;
void main()
{
    float dist = length(FragPos.xz);
    float fade = 1.0 - smoothstep(75.0, 150.0, dist);
    FragColor = vec4(uGridColor, uGridAlpha * fade);
}
";
}
