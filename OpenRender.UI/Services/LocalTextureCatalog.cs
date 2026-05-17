using OpenRender.Materials;
using OpenRender.Scene;

namespace OpenRender.Services;

public sealed class LocalTextureCatalog
{
    private readonly string? _assetsRoot;
    private readonly IReadOnlyDictionary<string, TextureSetDefinition> _presetTextureSets;

    public LocalTextureCatalog(string? assetsRoot = null)
    {
        _assetsRoot = ResolveAssetsRoot(assetsRoot);
        _presetTextureSets = BuildPresetTextureSets();
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_assetsRoot) && Directory.Exists(_assetsRoot);

    public bool ApplyPresetTextures(PbrMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        ClearTextureMaps(material);
        return TryAssignTextureSet(material, updateScalarSettings: true);
    }

    public bool BackfillPresetTexturesIfMissing(PbrMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (HasAnyTextureMaps(material))
            return false;

        return TryAssignTextureSet(material, updateScalarSettings: false);
    }

    private bool TryAssignTextureSet(PbrMaterial material, bool updateScalarSettings)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(material.PresetKey))
            return false;

        if (!_presetTextureSets.TryGetValue(material.PresetKey, out var textureSet))
            return false;

        string directoryPath = Path.Combine(_assetsRoot!, textureSet.DirectoryName);
        if (!Directory.Exists(directoryPath))
            return false;

        material.AlbedoTexturePath = ResolveTexturePath(directoryPath, textureSet.AlbedoFileName);
        material.NormalTexturePath = ResolveTexturePath(directoryPath, textureSet.NormalFileName);
        material.RoughnessTexturePath = ResolveTexturePath(directoryPath, textureSet.RoughnessFileName);
        material.AoTexturePath = ResolveTexturePath(directoryPath, textureSet.AoFileName);

        if (updateScalarSettings)
        {
            material.UvScale = textureSet.UvScale;
            material.NormalStrength = textureSet.NormalStrength;
        }

        return HasAnyTextureMaps(material);
    }

    private static IReadOnlyDictionary<string, TextureSetDefinition> BuildPresetTextureSets()
    {
        return new Dictionary<string, TextureSetDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["concrete-polished"] = TextureSetDefinition.Create("Concrete046", "Concrete046_2K-JPG", aoFileName: null),
            ["concrete-block"] = TextureSetDefinition.Create("Concrete046", "Concrete046_2K-JPG", aoFileName: null),
            ["brick-red"] = TextureSetDefinition.Create("Bricks059", "Bricks059_2K-JPG"),
            ["stone-cantera"] = TextureSetDefinition.Create("Travertine008", "Travertine008_2K-JPG"),
            ["stone-travertine"] = TextureSetDefinition.Create("Travertine008", "Travertine008_2K-JPG"),
            ["wood-oak"] = TextureSetDefinition.Create("WoodFloor062", "WoodFloor062_2K-JPG", aoFileName: null),
            ["wood-walnut"] = TextureSetDefinition.Create("WoodFloor046", "WoodFloor046_2K-JPG", aoFileName: null),
            ["ceramic-ivory"] = TextureSetDefinition.Create("Tiles002", "Tiles002_2K-JPG", aoFileName: null),
            ["roof-terracotta"] = TextureSetDefinition.Create("RoofingTiles013A", "RoofingTiles013A_2K-JPG"),
            ["landscape-grass"] = TextureSetDefinition.Create("Grass002", "Grass002_2K-JPG")
        };
    }

    private static string? ResolveAssetsRoot(string? explicitPath)
    {
        foreach (var candidate in EnumerateRootCandidates(explicitPath))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (int depth = 0; current != null && depth < 8; depth++, current = current.Parent)
        {
            string candidate = Path.Combine(current.FullName, "Assets", "Materials", "CC0");
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string?> EnumerateRootCandidates(string? explicitPath)
    {
        yield return explicitPath;
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "Materials", "CC0");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Materials", "CC0");
    }

    private static string? ResolveTexturePath(string directoryPath, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        string resolvedPath = Path.Combine(directoryPath, fileName);
        return File.Exists(resolvedPath) ? resolvedPath : null;
    }

    private static bool HasAnyTextureMaps(PbrMaterial material)
    {
        return !string.IsNullOrWhiteSpace(material.AlbedoTexturePath) ||
               !string.IsNullOrWhiteSpace(material.NormalTexturePath) ||
               !string.IsNullOrWhiteSpace(material.RoughnessTexturePath) ||
               !string.IsNullOrWhiteSpace(material.AoTexturePath);
    }

    private static void ClearTextureMaps(PbrMaterial material)
    {
        material.AlbedoTexturePath = null;
        material.NormalTexturePath = null;
        material.RoughnessTexturePath = null;
        material.AoTexturePath = null;
    }

    private sealed record TextureSetDefinition(
        string DirectoryName,
        string AlbedoFileName,
        string NormalFileName,
        string RoughnessFileName,
        string? AoFileName,
        float UvScale,
        float NormalStrength)
    {
        public static TextureSetDefinition Create(
            string directoryName,
            string fileStem,
            string? aoFileName = null,
            float uvScale = 1.0f,
            float normalStrength = 1.0f)
        {
            return new TextureSetDefinition(
                directoryName,
                $"{fileStem}_Color.jpg",
                $"{fileStem}_NormalGL.jpg",
                $"{fileStem}_Roughness.jpg",
                aoFileName ?? $"{fileStem}_AmbientOcclusion.jpg",
                uvScale,
                normalStrength);
        }
    }
}
