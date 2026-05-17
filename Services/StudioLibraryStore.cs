using System.Numerics;
using System.Text.Json;
using OpenRender.Core.Scene;

namespace OpenRender.Services;

public sealed class StudioLibraryStore
{
    private readonly string _storePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private StudioLibraryDocument _document;

    public StudioLibraryStore(string? storePath = null)
    {
        _storePath = storePath ?? GetDefaultStorePath();
        _document = LoadDocument();
    }

    public string StorePath => _storePath;

    public IReadOnlyList<ImportedModelRecord> GetHistory()
    {
        return _document.ImportedModels
            .OrderByDescending(item => item.LastOpenedUtc == default ? item.LastImportedUtc : item.LastOpenedUtc)
            .ToList();
    }

    public ImportedModelRecord UpsertImportRecord(string sourcePath, Scene3D scene, TimeSpan importDuration)
    {
        var item = GetOrCreateEntry(sourcePath);
        var fileInfo = SafeGetFileInfo(item.SourcePath);

        item.DisplayName = Path.GetFileNameWithoutExtension(item.SourcePath);
        item.LastImportedUtc = DateTime.UtcNow;
        item.LastOpenedUtc = item.LastImportedUtc;
        item.FileSizeBytes = fileInfo?.Length ?? item.FileSizeBytes;
        item.FileModifiedUtc = fileInfo?.LastWriteTimeUtc ?? item.FileModifiedUtc;
        item.ObjectCount = scene.GetAllNodes().Count(node => node.Mesh != null);
        item.TriangleCount = scene.GetTotalTriangleCount();
        item.MaterialCount = scene.Materials.Count;
        item.ImportDurationMs = importDuration.TotalMilliseconds;

        Save();
        return item;
    }

    public ImportedModelRecord? Find(string sourcePath)
    {
        string normalized = NormalizePath(sourcePath);
        return _document.ImportedModels.FirstOrDefault(item => PathsEqual(item.SourcePath, normalized));
    }

    public bool RemoveEntry(string sourcePath)
    {
        string normalized = NormalizePath(sourcePath);
        int removed = _document.ImportedModels.RemoveAll(item => PathsEqual(item.SourcePath, normalized));
        if (removed == 0)
            return false;

        Save();
        return true;
    }

    public void SaveSceneMaterialState(string sourcePath, Scene3D scene)
    {
        var item = GetOrCreateEntry(sourcePath);
        var fileInfo = SafeGetFileInfo(item.SourcePath);

        item.DisplayName = Path.GetFileNameWithoutExtension(item.SourcePath);
        item.LastOpenedUtc = DateTime.UtcNow;
        item.FileSizeBytes = fileInfo?.Length ?? item.FileSizeBytes;
        item.FileModifiedUtc = fileInfo?.LastWriteTimeUtc ?? item.FileModifiedUtc;
        item.ObjectCount = scene.GetAllNodes().Count(node => node.Mesh != null);
        item.TriangleCount = scene.GetTotalTriangleCount();
        item.MaterialCount = scene.Materials.Count;
        item.MaterialOverrides = CaptureSceneMaterialOverrides(scene);

        Save();
    }

    private ImportedModelRecord GetOrCreateEntry(string sourcePath)
    {
        string normalized = NormalizePath(sourcePath);
        var existing = _document.ImportedModels.FirstOrDefault(item => PathsEqual(item.SourcePath, normalized));
        if (existing != null)
            return existing;

        var created = new ImportedModelRecord
        {
            SourcePath = normalized,
            DisplayName = Path.GetFileNameWithoutExtension(normalized)
        };

        _document.ImportedModels.Add(created);
        return created;
    }

    private void Save()
    {
        string? directory = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(_storePath, JsonSerializer.Serialize(_document, _jsonOptions));
    }

    private StudioLibraryDocument LoadDocument()
    {
        try
        {
            if (!File.Exists(_storePath))
                return new StudioLibraryDocument();

            var json = File.ReadAllText(_storePath);
            return JsonSerializer.Deserialize<StudioLibraryDocument>(json, _jsonOptions) ?? new StudioLibraryDocument();
        }
        catch
        {
            return new StudioLibraryDocument();
        }
    }

    private static List<StoredMaterialOverride> CaptureSceneMaterialOverrides(Scene3D scene)
    {
        var overrides = new List<StoredMaterialOverride>();

        foreach (var node in scene.GetAllNodes().Where(node => node.Mesh != null))
        {
            if (node.MaterialIndex is not int materialIndex ||
                materialIndex < 0 ||
                materialIndex >= scene.Materials.Count)
            {
                continue;
            }

            var material = scene.Materials[materialIndex];
            overrides.Add(new StoredMaterialOverride
            {
                SurfaceKey = node.Name,
                SourceMaterialName = material.SourceName ?? material.Name,
                DisplayMaterialName = material.Name,
                PresetKey = material.PresetKey,
                Category = material.Category,
                Albedo = FloatVector3.FromVector3(material.Albedo),
                Metallic = material.Metallic,
                Roughness = material.Roughness,
                AmbientOcclusion = material.AmbientOcclusion,
                Opacity = material.Opacity,
                Emissive = FloatVector3.FromVector3(material.Emissive),
                NormalStrength = material.NormalStrength,
                UvScale = material.UvScale,
                AlbedoTexturePath = material.AlbedoTexturePath,
                NormalTexturePath = material.NormalTexturePath,
                RoughnessTexturePath = material.RoughnessTexturePath,
                AoTexturePath = material.AoTexturePath
            });
        }

        return overrides
            .OrderBy(item => item.SurfaceKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static FileInfo? SafeGetFileInfo(string sourcePath)
    {
        try
        {
            return File.Exists(sourcePath) ? new FileInfo(sourcePath) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizePath(string sourcePath)
    {
        return Path.GetFullPath(sourcePath.Trim().Trim('"'));
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            NormalizePath(left),
            NormalizePath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string GetDefaultStorePath()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, "OpenRender", "Library", "studio-library.json");
    }
}

public sealed class ImportedModelRecord
{
    public string SourcePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTime LastImportedUtc { get; set; }
    public DateTime LastOpenedUtc { get; set; }
    public DateTime FileModifiedUtc { get; set; }
    public long FileSizeBytes { get; set; }
    public int ObjectCount { get; set; }
    public int TriangleCount { get; set; }
    public int MaterialCount { get; set; }
    public double ImportDurationMs { get; set; }
    public List<StoredMaterialOverride> MaterialOverrides { get; set; } = new();
}

public sealed class StoredMaterialOverride
{
    public string SurfaceKey { get; set; } = "";
    public string SourceMaterialName { get; set; } = "";
    public string DisplayMaterialName { get; set; } = "";
    public string? PresetKey { get; set; }
    public string? Category { get; set; }
    public FloatVector3 Albedo { get; set; } = new();
    public float Metallic { get; set; }
    public float Roughness { get; set; } = 0.5f;
    public float AmbientOcclusion { get; set; } = 1.0f;
    public float Opacity { get; set; } = 1.0f;
    public FloatVector3 Emissive { get; set; } = new();
    public float NormalStrength { get; set; } = 1.0f;
    public float UvScale { get; set; } = 1.0f;
    public string? AlbedoTexturePath { get; set; }
    public string? NormalTexturePath { get; set; }
    public string? RoughnessTexturePath { get; set; }
    public string? AoTexturePath { get; set; }
}

public sealed class FloatVector3
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public Vector3 ToVector3()
    {
        return new Vector3(X, Y, Z);
    }

    public static FloatVector3 FromVector3(Vector3 value)
    {
        return new FloatVector3
        {
            X = value.X,
            Y = value.Y,
            Z = value.Z
        };
    }
}

public sealed class StudioLibraryDocument
{
    public List<ImportedModelRecord> ImportedModels { get; set; } = new();
}
