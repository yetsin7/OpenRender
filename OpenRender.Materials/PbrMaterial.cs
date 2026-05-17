using System.Numerics;
using System.ComponentModel;

namespace OpenRender.Materials;

public enum MaterialCategory
{
    General,
    Nature,
    Interior,
    Exterior,
    Custom
}

public static class MaterialCategoryExtensions
{
    public static string GetName(this MaterialCategory category) => category.ToString();
    public static string GetName(this string name) => name; // Hack for UI
}

/// <summary>
/// Professional PBR Material definition (AAA standard).
/// </summary>
public class PbrMaterial : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; set; } = "New Material";
    public string? PresetKey { get; set; }
    public string? SourceName { get; set; }
    public MaterialCategory Category { get; set; } = MaterialCategory.General;
    public int UsageCount { get; set; }
    
    public Vector3 Albedo { get; set; } = Vector3.One;
    public float Roughness { get; set; } = 0.5f;
    public float Metalness { get; set; } = 0.0f;
    public float Metallic { get => Metalness; set => Metalness = value; } // Alias for UI
    public float AmbientOcclusion { get; set; } = 1.0f;
    public float Opacity { get; set; } = 1.0f;
    public Vector3 Emissive { get; set; } = Vector3.Zero;
    
    public float UvScale { get; set; } = 1.0f;
    public float NormalStrength { get; set; } = 1.0f;

    public string? AlbedoTexturePath { get; set; }
    public string? NormalTexturePath { get; set; }
    public string? RoughnessTexturePath { get; set; }
    public string? AoTexturePath { get; set; }
    public string? MetalnessTexturePath { get; set; }

    // Legacy property names to maintain compatibility with existing UI code
    public string? AlbedoMap { get => AlbedoTexturePath; set => AlbedoTexturePath = value; }
    public string? NormalMap { get => NormalTexturePath; set => NormalTexturePath = value; }
    public string? RoughnessMap { get => RoughnessTexturePath; set => RoughnessTexturePath = value; }

    public PbrMaterial Clone() => (PbrMaterial)MemberwiseClone();
    public PbrMaterial Clone(string name) 
    { 
        var clone = Clone();
        clone.Name = name;
        return clone;
    }
    public static PbrMaterial StaticClone(PbrMaterial material) => material.Clone();
}

public record MaterialPresetDefinition(string Name, PbrMaterial Data)
{
    public string Key { get; init; } = Name; // Allow override
    public PbrMaterial Material => Data;
    public string Category => Data.Category.ToString();
    public MaterialCategory CategoryEnum => Data.Category;

    // Implicit conversions for UI logic
    public static bool operator !(MaterialPresetDefinition? p) => p == null;
    public static bool operator true(MaterialPresetDefinition? p) => p != null;
    public static bool operator false(MaterialPresetDefinition? p) => p == null;
    
    public static implicit operator string(MaterialPresetDefinition? p) => p?.Name ?? string.Empty;
}
