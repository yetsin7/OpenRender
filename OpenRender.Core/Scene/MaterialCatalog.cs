using System.Numerics;
using System.Text;

namespace OpenRender.Core.Scene;

public sealed record MaterialPresetDefinition(
    string Key,
    string Name,
    string Category,
    string Description,
    PbrMaterial Material)
{
    public string DisplayLabel => $"{Name} · {Category}";
}

public static class MaterialCatalog
{
    private static readonly IReadOnlyList<MaterialPresetDefinition> _presets = BuildPresets();

    public static IReadOnlyList<MaterialPresetDefinition> Presets => _presets;

    public static PbrMaterial CreateMaterial(string key)
    {
        return TryGetPreset(key, out var preset)
            ? preset.Material.Clone(preset.Name)
            : PbrMaterial.Default;
    }

    public static bool TryGetPreset(string key, out MaterialPresetDefinition preset)
    {
        preset = _presets.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?? _presets[0];

        return _presets.Any(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public static void ApplyPreset(PbrMaterial target, MaterialPresetDefinition preset, bool rename = false)
    {
        target.Category = preset.Category;
        target.PresetKey = preset.Key;
        target.Albedo = preset.Material.Albedo;
        target.Metallic = preset.Material.Metallic;
        target.Roughness = preset.Material.Roughness;
        target.AmbientOcclusion = preset.Material.AmbientOcclusion;
        target.Opacity = preset.Material.Opacity;
        target.Emissive = preset.Material.Emissive;
        target.NormalStrength = preset.Material.NormalStrength;

        if (rename)
            target.Name = preset.Name;
    }

    public static bool TryMatchPreset(string materialName, out MaterialPresetDefinition preset)
    {
        string normalized = Normalize(materialName);
        preset = _presets[0];

        if (ContainsAny(normalized, "vidrio", "glass", "cristal", "glazing", "ventana", "window"))
        {
            string key = ContainsAny(normalized, "black", "gris", "gray", "grey", "smoke") ? "glass-smoke" : "glass-clear";
            return TryGetPreset(key, out preset);
        }

        if (ContainsAny(normalized, "mirror", "espejo"))
            return TryGetPreset("glass-smoke", out preset);

        if (ContainsAny(normalized, "cantera", "travert", "stone", "piedra", "marmol", "marble"))
        {
            string key = ContainsAny(normalized, "travert") ? "stone-travertine" : "stone-cantera";
            return TryGetPreset(key, out preset);
        }

        if (ContainsAny(normalized, "brick", "ladrillo"))
            return TryGetPreset("brick-red", out preset);

        if (ContainsAny(normalized, "concrete", "hormigon", "hormigón", "masonry", "cascote", "cast in situ", "precast"))
        {
            string key = ContainsAny(normalized, "masonry", "units", "bloques") ? "concrete-block" : "concrete-polished";
            return TryGetPreset(key, out preset);
        }

        if (ContainsAny(normalized, "pvc", "cielo", "ceiling"))
            return TryGetPreset("pvc-warm-white", out preset);

        if (ContainsAny(normalized, "ceram", "azulejo", "tile", "porcelana"))
        {
            string key = ContainsAny(normalized, "wood pattern", "madera") ? "wood-oak" : "ceramic-ivory";
            return TryGetPreset(key, out preset);
        }

        if (ContainsAny(normalized, "wood", "madera", "walnut", "jamb", "door", "puerta", "cabinet", "casement", "laminado", "rodapie"))
        {
            string key = ContainsAny(normalized, "dark", "walnut", "nero", "marron", "brown", "tinte") ? "wood-walnut" : "wood-oak";
            return TryGetPreset(key, out preset);
        }

        if (ContainsAny(normalized, "roof", "teja", "fascia"))
            return TryGetPreset("roof-terracotta", out preset);

        if (ContainsAny(normalized, "grass", "grama"))
            return TryGetPreset("landscape-grass", out preset);

        if (ContainsAny(normalized, "metal", "steel", "alumin", "aluminum", "aluminium", "chrome", "acero", "stainless", "inox", "iron", "barandilla", "railing", "reja", "montante", "frame", "metalica"))
        {
            string key = ContainsAny(normalized, "black", "graphite", "negro", "nero") ? "metal-black" : "metal-brushed";
            return TryGetPreset(key, out preset);
        }

        if (ContainsAny(normalized, "plastic", "plastico", "plástico"))
        {
            string key = ContainsAny(normalized, "white", "blanco", "off white", "cream") ? "paint-soft-white" : "polymer-charcoal";
            return TryGetPreset(key, out preset);
        }

        if (ContainsAny(normalized, "textil", "textile", "fabric", "polyester", "fiber"))
            return TryGetPreset("fabric-linen", out preset);

        if (ContainsAny(normalized, "navy", "azul marino", "azul profundo"))
            return TryGetPreset("paint-navy", out preset);

        if (ContainsAny(normalized, "salvia", "sage"))
            return TryGetPreset("paint-sage", out preset);

        if (ContainsAny(normalized, "turquesa", "turquoise", "aqua"))
            return TryGetPreset("paint-turquoise", out preset);

        if (ContainsAny(normalized, "paint", "pintura", "gypsum", "wall board", "plasterboard", "plaster", "yeso", "blanco", "white", "revestimiento", "colour", "color"))
        {
            string key = ContainsAny(normalized, "gris", "gray", "grey", "perla", "graphite") ? "paint-warm-gray" : "paint-soft-white";

            if (ContainsAny(normalized, "navy", "azul marino", "azul profundo"))
                key = "paint-navy";
            else if (ContainsAny(normalized, "salvia", "sage"))
                key = "paint-sage";
            else if (ContainsAny(normalized, "turquesa", "turquoise", "aqua"))
                key = "paint-turquoise";

            return TryGetPreset(key, out preset);
        }

        if (ContainsAny(normalized, "folha", "folhas", "leaf", "leaves"))
            return TryGetPreset("landscape-grass", out preset);

        if (ContainsAny(normalized, "default", "material not defined"))
            return TryGetPreset("clay-soft", out preset);

        return false;
    }

    public static string GuessCategory(string materialName)
    {
        return TryMatchPreset(materialName, out var preset)
            ? preset.Category
            : "Generic";
    }

    private static IReadOnlyList<MaterialPresetDefinition> BuildPresets()
    {
        return new List<MaterialPresetDefinition>
        {
            Create("clay-soft", "Clay Soft", "Concept", "Arcilla neutra para pruebas de volumen.", new PbrMaterial
            {
                Category = "Concept",
                Albedo = new Vector3(0.86f, 0.85f, 0.82f),
                Metallic = 0f,
                Roughness = 0.84f
            }),
            Create("paint-soft-white", "Paint Soft White", "Walls", "Pintura interior blanca ligeramente cálida.", new PbrMaterial
            {
                Category = "Walls",
                Albedo = new Vector3(0.92f, 0.91f, 0.88f),
                Metallic = 0f,
                Roughness = 0.72f
            }),
            Create("paint-warm-gray", "Paint Warm Gray", "Walls", "Pintura gris perla para interiores.", new PbrMaterial
            {
                Category = "Walls",
                Albedo = new Vector3(0.72f, 0.72f, 0.70f),
                Metallic = 0f,
                Roughness = 0.70f
            }),
            Create("paint-navy", "Paint Navy", "Accent", "Pintura azul profunda para muros de acento.", new PbrMaterial
            {
                Category = "Accent",
                Albedo = new Vector3(0.16f, 0.24f, 0.34f),
                Metallic = 0f,
                Roughness = 0.64f
            }),
            Create("paint-sage", "Paint Sage", "Accent", "Pintura verde salvia suave.", new PbrMaterial
            {
                Category = "Accent",
                Albedo = new Vector3(0.54f, 0.61f, 0.53f),
                Metallic = 0f,
                Roughness = 0.68f
            }),
            Create("paint-turquoise", "Paint Turquoise", "Accent", "Pintura turquesa clara para acentos.", new PbrMaterial
            {
                Category = "Accent",
                Albedo = new Vector3(0.44f, 0.73f, 0.76f),
                Metallic = 0f,
                Roughness = 0.60f
            }),
            Create("concrete-polished", "Concrete Polished", "Concrete", "Hormigón gris medio con acabado uniforme.", new PbrMaterial
            {
                Category = "Concrete",
                Albedo = new Vector3(0.61f, 0.60f, 0.58f),
                Metallic = 0f,
                Roughness = 0.82f
            }),
            Create("concrete-block", "Concrete Block", "Concrete", "Bloque de concreto más seco y claro.", new PbrMaterial
            {
                Category = "Concrete",
                Albedo = new Vector3(0.67f, 0.66f, 0.63f),
                Metallic = 0f,
                Roughness = 0.88f
            }),
            Create("brick-red", "Brick Red", "Masonry", "Ladrillo rojizo tradicional.", new PbrMaterial
            {
                Category = "Masonry",
                Albedo = new Vector3(0.58f, 0.31f, 0.24f),
                Metallic = 0f,
                Roughness = 0.90f
            }),
            Create("stone-cantera", "Cantera Beige", "Stone", "Piedra cantera clara para exteriores.", new PbrMaterial
            {
                Category = "Stone",
                Albedo = new Vector3(0.76f, 0.70f, 0.58f),
                Metallic = 0f,
                Roughness = 0.86f
            }),
            Create("stone-travertine", "Travertine Sand", "Stone", "Piedra travertino suave.", new PbrMaterial
            {
                Category = "Stone",
                Albedo = new Vector3(0.82f, 0.77f, 0.68f),
                Metallic = 0f,
                Roughness = 0.78f
            }),
            Create("wood-oak", "Oak Natural", "Wood", "Madera roble clara.", new PbrMaterial
            {
                Category = "Wood",
                Albedo = new Vector3(0.67f, 0.53f, 0.35f),
                Metallic = 0f,
                Roughness = 0.63f
            }),
            Create("wood-walnut", "Walnut Deep", "Wood", "Madera nogal oscura.", new PbrMaterial
            {
                Category = "Wood",
                Albedo = new Vector3(0.41f, 0.28f, 0.18f),
                Metallic = 0f,
                Roughness = 0.58f
            }),
            Create("ceramic-ivory", "Ceramic Ivory", "Ceramic", "Cerámica cálida para pisos y baños.", new PbrMaterial
            {
                Category = "Ceramic",
                Albedo = new Vector3(0.84f, 0.80f, 0.74f),
                Metallic = 0f,
                Roughness = 0.22f
            }),
            Create("metal-brushed", "Brushed Steel", "Metal", "Metal cepillado para perfiles y herrajes.", new PbrMaterial
            {
                Category = "Metal",
                Albedo = new Vector3(0.74f, 0.75f, 0.78f),
                Metallic = 1f,
                Roughness = 0.27f
            }),
            Create("metal-black", "Graphite Metal", "Metal", "Metal grafito mate.", new PbrMaterial
            {
                Category = "Metal",
                Albedo = new Vector3(0.18f, 0.19f, 0.20f),
                Metallic = 0.92f,
                Roughness = 0.33f
            }),
            Create("glass-clear", "Glass Clear", "Glass", "Vidrio claro para ventanas.", new PbrMaterial
            {
                Category = "Glass",
                Albedo = new Vector3(0.94f, 0.97f, 1.0f),
                Metallic = 0f,
                Roughness = 0.03f,
                Opacity = 0.22f
            }),
            Create("glass-smoke", "Glass Smoke", "Glass", "Vidrio ahumado suave.", new PbrMaterial
            {
                Category = "Glass",
                Albedo = new Vector3(0.48f, 0.54f, 0.60f),
                Metallic = 0f,
                Roughness = 0.05f,
                Opacity = 0.34f
            }),
            Create("roof-terracotta", "Terracotta Roof", "Roof", "Cubierta cálida para techos.", new PbrMaterial
            {
                Category = "Roof",
                Albedo = new Vector3(0.53f, 0.24f, 0.18f),
                Metallic = 0f,
                Roughness = 0.72f
            }),
            Create("landscape-grass", "Landscape Grass", "Landscape", "Verde suave para césped.", new PbrMaterial
            {
                Category = "Landscape",
                Albedo = new Vector3(0.27f, 0.43f, 0.21f),
                Metallic = 0f,
                Roughness = 0.95f
            }),
            Create("polymer-charcoal", "Polymer Charcoal", "Synthetic", "Plástico oscuro para accesorios.", new PbrMaterial
            {
                Category = "Synthetic",
                Albedo = new Vector3(0.21f, 0.22f, 0.24f),
                Metallic = 0f,
                Roughness = 0.58f
            }),
            Create("fabric-linen", "Fabric Linen", "Textile", "Textil claro para tapicerías y cortinas.", new PbrMaterial
            {
                Category = "Textile",
                Albedo = new Vector3(0.86f, 0.84f, 0.79f),
                Metallic = 0f,
                Roughness = 0.88f
            }),
            Create("pvc-warm-white", "PVC Warm White", "Ceiling", "PVC blanco cálido para plafones y cielos.", new PbrMaterial
            {
                Category = "Ceiling",
                Albedo = new Vector3(0.88f, 0.85f, 0.80f),
                Metallic = 0f,
                Roughness = 0.52f
            }),
        };
    }

    private static MaterialPresetDefinition Create(string key, string name, string category, string description, PbrMaterial material)
    {
        material.Name = name;
        material.Category = category;
        material.PresetKey = key;
        return new MaterialPresetDefinition(key, name, category, description, material);
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        return tokens.Any(text.Contains);
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace("\\", " ")
            .Normalize(NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c), sb => sb.ToString());
    }
}
