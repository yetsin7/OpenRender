using OpenRender.Core.Scene;

namespace OpenRender.Core.Import;

/// <summary>
/// Interface for 3D model importers.
/// Each supported file format should implement this interface.
/// </summary>
public interface IModelImporter
{
    /// <summary>
    /// Gets the file extensions this importer supports (e.g., ".obj", ".fbx").
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Gets a human-readable description of the format.
    /// </summary>
    string FormatDescription { get; }

    /// <summary>
    /// Returns true if the given file path can be imported by this importer.
    /// </summary>
    bool CanImport(string filePath);

    /// <summary>
    /// Imports a 3D model file and returns it as a Scene3D.
    /// </summary>
    /// <param name="filePath">Path to the 3D model file.</param>
    /// <param name="options">Optional import settings.</param>
    /// <returns>The imported scene data.</returns>
    Task<ImportResult> ImportAsync(string filePath, ImportOptions? options = null, IProgress<double>? progress = null);
}

/// <summary>
/// Options to control how a 3D model is imported.
/// </summary>
public class ImportOptions
{
    /// <summary>
    /// Whether to merge meshes with the same material.
    /// </summary>
    public bool MergeMeshes { get; set; } = false;

    /// <summary>
    /// Whether to generate normals if not present.
    /// </summary>
    public bool GenerateNormals { get; set; } = true;

    /// <summary>
    /// Whether to flip UV coordinates vertically.
    /// </summary>
    public bool FlipUVs { get; set; } = false;

    /// <summary>
    /// Whether to swap Y and Z axes (converts Z-up to Y-up).
    /// </summary>
    public bool SwapYZ { get; set; } = false;

    /// <summary>
    /// Whether to recenter the model to the origin.
    /// </summary>
    public bool Recenter { get; set; } = true;

    /// <summary>
    /// Uniform scale to apply on import.
    /// </summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>
    /// Whether to triangulate non-triangle polygons.
    /// </summary>
    public bool Triangulate { get; set; } = true;
}

/// <summary>
/// Result of a model import operation.
/// </summary>
public class ImportResult
{
    /// <summary>
    /// Whether the import was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The imported scene (null if import failed).
    /// </summary>
    public Scene3D? Scene { get; set; }

    /// <summary>
    /// Error message if import failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Warning messages generated during import.
    /// </summary>
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// Import statistics.
    /// </summary>
    public ImportStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Statistics about an import operation.
/// </summary>
public class ImportStatistics
{
    public int MeshCount { get; set; }
    public int MaterialCount { get; set; }
    public int TotalVertices { get; set; }
    public int TotalTriangles { get; set; }
    public TimeSpan ImportDuration { get; set; }
}
