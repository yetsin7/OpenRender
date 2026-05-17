using System.Linq;
using System.Collections.Generic;
using OpenRender.Materials;

namespace OpenRender.Materials;

public static class MaterialCatalog
{
    public static List<MaterialPresetDefinition> Presets { get; } = new();
    public static List<MaterialPresetDefinition> GetPresets() => Presets;
    
    public static MaterialPresetDefinition? TryGetPreset(string key) => Presets.FirstOrDefault(p => p.Key == key);
    
    // UI compatibility signatures
    public static bool TryGetPreset(string key, out MaterialPresetDefinition? preset) 
    { 
        preset = TryGetPreset(key); 
        return preset != null; 
    }
    
    public static MaterialPresetDefinition? TryGetPreset(string key, MaterialCategory category) => Presets.FirstOrDefault(p => p.Key == key && p.Data.Category == category);

    public static void ApplyPreset(PbrMaterial material, string key) { }
    public static void ApplyPreset(PbrMaterial material, MaterialPresetDefinition? preset) { }
    public static void ApplyPreset(PbrMaterial material, string key, MaterialCategory category) { }

    public static MaterialPresetDefinition? TryMatchPreset(PbrMaterial material) => null;
    
    public static bool TryMatchPreset(PbrMaterial material, out bool exact, out MaterialPresetDefinition? preset) 
    { 
        exact = false; 
        preset = null; 
        return false; 
    }

    public static bool TryMatchPreset(string name, out MaterialPresetDefinition? preset)
    {
        preset = null;
        return false;
    }
    
    public static MaterialPresetDefinition? TryMatchPreset(PbrMaterial material, MaterialPresetDefinition? preset) => preset;
    public static MaterialPresetDefinition? TryMatchPreset(PbrMaterial material, bool exact) => null;
    public static MaterialPresetDefinition? TryMatchPreset(string name, bool exact) => null;
    public static MaterialPresetDefinition? TryMatchPreset(PbrMaterial material, string? name, out bool exact) { exact = false; return null; }

    public static MaterialCategory GuessCategory(string name) => MaterialCategory.General;
}
