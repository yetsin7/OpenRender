using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenRender.Core.Scene;

/// <summary>
/// Physically-Based Rendering (PBR) material definition.
/// Supports the metallic-roughness workflow standard.
/// </summary>
public partial class PbrMaterial : ObservableObject
{
    /// <summary>
    /// Material name.
    /// </summary>
    [ObservableProperty] private string _name = "Default Material";

    /// <summary>
    /// Category shown in the UI material organizer.
    /// </summary>
    [ObservableProperty] private string _category = "Generic";

    /// <summary>
    /// Optional key of the matched preset from the internal library.
    /// </summary>
    [ObservableProperty] private string? _presetKey;

    /// <summary>
    /// Number of scene nodes using this material.
    /// </summary>
    [ObservableProperty] private int _usageCount;

    /// <summary>
    /// Base color (albedo) in linear RGB.
    /// </summary>
    [ObservableProperty] private Vector3 _albedo = new(0.8f, 0.8f, 0.8f);

    /// <summary>
    /// Metallic factor (0 = dielectric, 1 = metal).
    /// </summary>
    [ObservableProperty] private float _metallic = 0.0f;

    /// <summary>
    /// Roughness factor (0 = smooth/mirror, 1 = rough/diffuse).
    /// </summary>
    [ObservableProperty] private float _roughness = 0.5f;

    /// <summary>
    /// Ambient occlusion factor (0 = fully occluded, 1 = no occlusion).
    /// </summary>
    [ObservableProperty] private float _ambientOcclusion = 1.0f;

    /// <summary>
    /// Opacity (0 = fully transparent, 1 = fully opaque).
    /// </summary>
    [ObservableProperty] private float _opacity = 1.0f;

    /// <summary>
    /// Emissive color for self-illuminating surfaces.
    /// </summary>
    [ObservableProperty] private Vector3 _emissive = Vector3.Zero;

    /// <summary>
    /// Normal map intensity (0 = flat, 1 = full effect).
    /// </summary>
    [ObservableProperty] private float _normalStrength = 1.0f;

    // Texture paths (resolved at render time)
    [ObservableProperty] private string? _albedoTexturePath;
    [ObservableProperty] private string? _normalTexturePath;
    [ObservableProperty] private string? _metallicTexturePath;
    [ObservableProperty] private string? _roughnessTexturePath;
    [ObservableProperty] private string? _aoTexturePath;

    public PbrMaterial Clone(string? newName = null)
    {
        return new PbrMaterial
        {
            Name = newName ?? Name,
            Category = Category,
            PresetKey = PresetKey,
            UsageCount = UsageCount,
            Albedo = Albedo,
            Metallic = Metallic,
            Roughness = Roughness,
            AmbientOcclusion = AmbientOcclusion,
            Opacity = Opacity,
            Emissive = Emissive,
            NormalStrength = NormalStrength,
            AlbedoTexturePath = AlbedoTexturePath,
            NormalTexturePath = NormalTexturePath,
            MetallicTexturePath = MetallicTexturePath,
            RoughnessTexturePath = RoughnessTexturePath,
            AoTexturePath = AoTexturePath
        };
    }

    /// <summary>
    /// Creates a default gray material.
    /// </summary>
    public static PbrMaterial Default => new()
    {
        Name = "Default",
        Category = "Base",
        Albedo = new Vector3(0.7f, 0.7f, 0.7f),
        Metallic = 0.0f,
        Roughness = 0.5f
    };

    /// <summary>
    /// Creates a polished concrete material.
    /// </summary>
    public static PbrMaterial Concrete => new()
    {
        Name = "Concrete",
        Category = "Concrete",
        Albedo = new Vector3(0.6f, 0.58f, 0.55f),
        Metallic = 0.0f,
        Roughness = 0.85f
    };

    /// <summary>
    /// Creates a glass material.
    /// </summary>
    public static PbrMaterial Glass => new()
    {
        Name = "Glass",
        Category = "Glass",
        Albedo = new Vector3(0.95f, 0.97f, 1.0f),
        Metallic = 0.0f,
        Roughness = 0.05f,
        Opacity = 0.3f
    };

    /// <summary>
    /// Creates a brushed metal material.
    /// </summary>
    public static PbrMaterial Metal => new()
    {
        Name = "Brushed Metal",
        Category = "Metal",
        Albedo = new Vector3(0.85f, 0.85f, 0.87f),
        Metallic = 1.0f,
        Roughness = 0.3f
    };

    /// <summary>
    /// Creates a wood material.
    /// </summary>
    public static PbrMaterial Wood => new()
    {
        Name = "Wood",
        Category = "Wood",
        Albedo = new Vector3(0.55f, 0.35f, 0.2f),
        Metallic = 0.0f,
        Roughness = 0.7f
    };
}
