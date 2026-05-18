using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace OpenRender.Materials;

public static class MaterialCatalog
{
    public static List<MaterialPresetDefinition> Presets { get; } = BuildPresets();

    public static List<MaterialPresetDefinition> GetPresets() => Presets;

    public static MaterialPresetDefinition? TryGetPreset(string key) =>
        Presets.FirstOrDefault(preset =>
            string.Equals(preset.Key, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(preset.Name, key, StringComparison.OrdinalIgnoreCase));

    public static bool TryGetPreset(string key, out MaterialPresetDefinition? preset)
    {
        preset = TryGetPreset(key);
        return preset != null;
    }

    public static MaterialPresetDefinition? TryGetPreset(string key, MaterialCategory category) =>
        Presets.FirstOrDefault(preset =>
            preset.CategoryEnum == category &&
            (string.Equals(preset.Key, key, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(preset.Name, key, StringComparison.OrdinalIgnoreCase)));

    public static void ApplyPreset(PbrMaterial material, string key)
    {
        if (TryGetPreset(key, out var preset))
            ApplyPreset(material, preset);
    }

    public static void ApplyPreset(PbrMaterial material, MaterialPresetDefinition? preset)
    {
        if (material == null || preset == null)
            return;

        material.PresetKey = preset.Key;
        material.Category = preset.CategoryEnum;
        material.Albedo = preset.Material.Albedo;
        material.Roughness = preset.Material.Roughness;
        material.Metallic = preset.Material.Metallic;
        material.AmbientOcclusion = preset.Material.AmbientOcclusion;
        material.Opacity = preset.Material.Opacity;
        material.Emissive = preset.Material.Emissive;
        material.UvScale = preset.Material.UvScale;
        material.NormalStrength = preset.Material.NormalStrength;
        material.AlbedoTexturePath = preset.Material.AlbedoTexturePath;
        material.NormalTexturePath = preset.Material.NormalTexturePath;
        material.RoughnessTexturePath = preset.Material.RoughnessTexturePath;
        material.AoTexturePath = preset.Material.AoTexturePath;
        material.MetalnessTexturePath = preset.Material.MetalnessTexturePath;
    }

    public static void ApplyPreset(PbrMaterial material, string key, MaterialCategory category)
    {
        var preset = TryGetPreset(key, category) ?? TryGetPreset(key);
        ApplyPreset(material, preset);
    }

    public static MaterialPresetDefinition? TryMatchPreset(PbrMaterial material) =>
        material == null ? null : MatchPreset($"{material.SourceName} {material.Name}");

    public static bool TryMatchPreset(PbrMaterial material, out bool exact, out MaterialPresetDefinition? preset)
    {
        (exact, preset) = MatchPresetInternal($"{material?.SourceName} {material?.Name}");
        return preset != null;
    }

    public static bool TryMatchPreset(string name, out MaterialPresetDefinition? preset)
    {
        (_, preset) = MatchPresetInternal(name);
        return preset != null;
    }

    public static MaterialPresetDefinition? TryMatchPreset(PbrMaterial material, MaterialPresetDefinition? preset) =>
        preset ?? TryMatchPreset(material);

    public static MaterialPresetDefinition? TryMatchPreset(PbrMaterial material, bool exact)
    {
        var result = MatchPresetInternal($"{material?.SourceName} {material?.Name}");
        return !exact || result.exact ? result.preset : null;
    }

    public static MaterialPresetDefinition? TryMatchPreset(string name, bool exact)
    {
        var result = MatchPresetInternal(name);
        return !exact || result.exact ? result.preset : null;
    }

    public static MaterialPresetDefinition? TryMatchPreset(PbrMaterial material, string? name, out bool exact)
    {
        (exact, var preset) = MatchPresetInternal($"{material?.SourceName} {material?.Name} {name}");
        return preset;
    }

    public static MaterialCategory GuessCategory(string name)
    {
        string hint = Normalize(name);
        if (ContainsAny(hint, "tree", "leaf", "grass", "bush", "plant", "foliage", "hedge", "palm"))
            return MaterialCategory.Nature;
        if (ContainsAny(hint, "wall", "roof", "facade", "window", "door", "stone", "concrete", "metal", "glass", "brick"))
            return MaterialCategory.Exterior;
        if (ContainsAny(hint, "floor", "tile", "wood", "kitchen", "interior", "furniture", "fabric"))
            return MaterialCategory.Interior;
        return MaterialCategory.General;
    }

    private static MaterialPresetDefinition? MatchPreset(string name) => MatchPresetInternal(name).preset;

    private static (bool exact, MaterialPresetDefinition? preset) MatchPresetInternal(string? name)
    {
        string hint = Normalize(name);
        if (string.IsNullOrWhiteSpace(hint))
            return (false, null);

        foreach (var rule in MatchRules)
        {
            if (rule.Tokens.All(hint.Contains))
                return (true, TryGetPreset(rule.Key));
        }

        foreach (var rule in MatchRules)
        {
            if (rule.Tokens.Any(hint.Contains))
                return (false, TryGetPreset(rule.Key));
        }

        return (false, null);
    }

    private static string Normalize(string? value) =>
        RemoveDiacritics((value ?? string.Empty).Trim().ToLowerInvariant())
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace("\\", " ");

    private static string RemoveDiacritics(string value)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (char character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool ContainsAny(string text, params string[] tokens) => tokens.Any(text.Contains);

    private static List<MaterialPresetDefinition> BuildPresets() =>
    [
        CreatePreset("paint-soft-white", "Soft White Paint", MaterialCategory.Exterior, new Vector3(0.86f, 0.85f, 0.82f), 0.82f, 0.02f),
        CreatePreset("paint-warm-gray", "Warm Gray Paint", MaterialCategory.Exterior, new Vector3(0.70f, 0.69f, 0.66f), 0.80f, 0.02f),
        CreatePreset("concrete-soft", "Soft Concrete", MaterialCategory.Exterior, new Vector3(0.67f, 0.68f, 0.66f), 0.92f, 0.04f),
        CreatePreset("stone-warm", "Warm Stone", MaterialCategory.Exterior, new Vector3(0.74f, 0.68f, 0.58f), 0.88f, 0.04f),
        CreatePreset("brick-red", "Brick Red", MaterialCategory.Exterior, new Vector3(0.58f, 0.28f, 0.22f), 0.86f, 0.03f),
        CreatePreset("wood-oak", "Oak Wood", MaterialCategory.Interior, new Vector3(0.60f, 0.46f, 0.29f), 0.56f, 0.06f),
        CreatePreset("ceramic-light", "Light Ceramic", MaterialCategory.Interior, new Vector3(0.84f, 0.83f, 0.80f), 0.28f, 0.01f),
        CreatePreset("glass-clear", "Clear Glass", MaterialCategory.Exterior, new Vector3(0.68f, 0.84f, 0.93f), 0.08f, 0.02f, 0.28f),
        CreatePreset("metal-dark", "Dark Metal", MaterialCategory.Exterior, new Vector3(0.26f, 0.28f, 0.31f), 0.24f, 0.92f),
        CreatePreset("roof-terracotta", "Terracotta Roof", MaterialCategory.Exterior, new Vector3(0.57f, 0.26f, 0.18f), 0.74f, 0.05f),
        CreatePreset("grass-fresh", "Fresh Grass", MaterialCategory.Nature, new Vector3(0.22f, 0.42f, 0.18f), 0.98f, 0.01f),
        CreatePreset("foliage-deep", "Deep Foliage", MaterialCategory.Nature, new Vector3(0.18f, 0.33f, 0.16f), 0.94f, 0.01f),
        CreatePreset("water-blue", "Water Blue", MaterialCategory.Exterior, new Vector3(0.19f, 0.36f, 0.52f), 0.06f, 0.12f, 0.74f)
    ];

    private static MaterialPresetDefinition CreatePreset(string key, string name, MaterialCategory category, Vector3 albedo, float roughness, float metallic, float opacity = 1f) =>
        new(name, new PbrMaterial
        {
            Name = name,
            PresetKey = key,
            Category = category,
            Albedo = albedo,
            Roughness = roughness,
            Metallic = metallic,
            Opacity = opacity,
            AmbientOcclusion = 1f,
            UvScale = 1f,
            NormalStrength = 1f
        })
        {
            Key = key
        };

    private static readonly MatchRule[] MatchRules =
    [
        new("glass-clear", "glass", "window"),
        new("glass-clear", "vidrio"),
        new("glass-clear", "verre"),
        new("glass-clear", "cristal"),
        new("glass-clear", "glazing"),
        new("roof-terracotta", "roof"),
        new("roof-terracotta", "techo"),
        new("roof-terracotta", "cubierta"),
        new("roof-terracotta", "roofing"),
        new("wood-oak", "wood"),
        new("wood-oak", "madera"),
        new("wood-oak", "laminado"),
        new("wood-oak", "walnut"),
        new("wood-oak", "pine"),
        new("wood-oak", "marvin"),
        new("stone-warm", "stone"),
        new("stone-warm", "cantera"),
        new("stone-warm", "piedra"),
        new("stone-warm", "travertine"),
        new("concrete-soft", "concrete"),
        new("concrete-soft", "concreto"),
        new("concrete-soft", "cement"),
        new("concrete-soft", "hormigon"),
        new("brick-red", "brick"),
        new("brick-red", "ladrillo"),
        new("metal-dark", "metal"),
        new("metal-dark", "steel"),
        new("metal-dark", "stainless"),
        new("metal-dark", "chrome"),
        new("metal-dark", "cromo"),
        new("metal-dark", "acero"),
        new("metal-dark", "alumin"),
        new("metal-dark", "iron"),
        new("metal-dark", "inox"),
        new("ceramic-light", "tile"),
        new("ceramic-light", "ceram"),
        new("ceramic-light", "azulejo"),
        new("ceramic-light", "porcelain"),
        new("water-blue", "water"),
        new("water-blue", "pool"),
        new("grass-fresh", "grass"),
        new("grass-fresh", "grama"),
        new("grass-fresh", "lawn"),
        new("foliage-deep", "tree"),
        new("foliage-deep", "leaf"),
        new("foliage-deep", "folha"),
        new("foliage-deep", "folhas"),
        new("foliage-deep", "plant"),
        new("paint-soft-white", "wall"),
        new("paint-soft-white", "paint"),
        new("paint-soft-white", "pintura"),
        new("paint-soft-white", "blanco"),
        new("paint-soft-white", "yeso"),
        new("paint-soft-white", "gypsum"),
        new("paint-soft-white", "plasterboard"),
        new("paint-soft-white", "revestimiento"),
        new("paint-warm-gray", "facade"),
        new("paint-warm-gray", "fachada")
    ];

    private sealed record MatchRule(string Key, params string[] Tokens);
}
