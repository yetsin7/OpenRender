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
uniform float uOpacity;

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

out vec4 FragColor;

void main()
{
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(-uLightDir);
    vec3 viewDir = normalize(uViewPos - FragPos);
    vec3 halfDir = normalize(lightDir + viewDir);

    // Ambient
    vec3 ambient = uAmbientColor * uAmbientIntensity * uAlbedo;

    // Diffuse (Lambert)
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * uLightColor * uLightIntensity * uAlbedo;

    // Specular (Blinn-Phong approximation of GGX)
    float shininess = mix(8.0, 256.0, 1.0 - uRoughness);
    float spec = pow(max(dot(norm, halfDir), 0.0), shininess);
    vec3 specColor = mix(vec3(0.04), uAlbedo, uMetallic);
    vec3 specular = spec * uLightColor * uLightIntensity * specColor;

    // Combine
    vec3 result = ambient + diffuse + specular;

    // Tone mapping (Reinhard)
    result = vec3(1.0) - exp(-result * uExposure);

    // Gamma correction
    result = pow(result, vec3(1.0 / uGamma));

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
    float fade = 1.0 - smoothstep(2000.0, 5000.0, dist);
    FragColor = vec4(uGridColor, uGridAlpha * fade);
}
";
}
