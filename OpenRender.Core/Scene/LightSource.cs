using System.Numerics;

namespace OpenRender.Core.Scene;

/// <summary>
/// Type of light source in the scene.
/// </summary>
public enum LightType
{
    /// <summary>
    /// Directional light simulating sunlight (parallel rays).
    /// </summary>
    Directional,

    /// <summary>
    /// Point light emitting in all directions from a position.
    /// </summary>
    Point,

    /// <summary>
    /// Spot light emitting in a cone from a position.
    /// </summary>
    Spot
}

/// <summary>
/// Represents a light source in the 3D scene.
/// Supports directional (sun), point, and spot light types.
/// </summary>
public class LightSource
{
    /// <summary>
    /// Name of the light.
    /// </summary>
    public string Name { get; set; } = "Light";

    /// <summary>
    /// Type of this light source.
    /// </summary>
    public LightType Type { get; set; } = LightType.Directional;

    /// <summary>
    /// Position in world space (used by Point and Spot lights).
    /// </summary>
    public Vector3 Position { get; set; } = new(0, 10, 0);

    /// <summary>
    /// Direction of the light (used by Directional and Spot lights).
    /// </summary>
    public Vector3 Direction { get; set; } = Vector3.Normalize(new(-0.5f, -1f, -0.5f));

    /// <summary>
    /// Light color in linear RGB.
    /// </summary>
    public Vector3 Color { get; set; } = Vector3.One;

    /// <summary>
    /// Light intensity multiplier.
    /// </summary>
    public float Intensity { get; set; } = 1.0f;

    /// <summary>
    /// Attenuation range for point/spot lights (in world units).
    /// </summary>
    public float Range { get; set; } = 50f;

    /// <summary>
    /// Inner cone angle in degrees (spot light only).
    /// </summary>
    public float InnerConeAngle { get; set; } = 30f;

    /// <summary>
    /// Outer cone angle in degrees (spot light only).
    /// </summary>
    public float OuterConeAngle { get; set; } = 45f;

    /// <summary>
    /// Whether this light casts shadows.
    /// </summary>
    public bool CastsShadows { get; set; } = true;

    /// <summary>
    /// Whether this light is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Creates a default sun light.
    /// </summary>
    public static LightSource CreateSun(float intensity = 1.5f) => new()
    {
        Name = "Sun",
        Type = LightType.Directional,
        Direction = Vector3.Normalize(new(-0.3f, -1f, -0.4f)),
        Color = new Vector3(1.0f, 0.96f, 0.9f), // Warm white
        Intensity = intensity,
        CastsShadows = true
    };

    /// <summary>
    /// Creates a point light at a given position.
    /// </summary>
    public static LightSource CreatePointLight(Vector3 position, Vector3 color, float intensity = 1.0f) => new()
    {
        Name = "Point Light",
        Type = LightType.Point,
        Position = position,
        Color = color,
        Intensity = intensity,
        Range = 20f
    };
}
