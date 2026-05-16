using OpenRender.Core.Import;
using OpenRender.Core.Scene;

namespace OpenRender.Rendering.Import;

/// <summary>
/// Registry and manager for all available model importers.
/// Selects the correct importer based on file extension.
/// </summary>
public class ImportManager
{
    private readonly List<IModelImporter> _importers = new();

    public ImportManager()
    {
        // Register built-in importers
        RegisterImporter(new ObjImporter());
    }

    /// <summary>
    /// Registers a model importer.
    /// </summary>
    public void RegisterImporter(IModelImporter importer)
    {
        _importers.Add(importer);
    }

    /// <summary>
    /// Gets all supported file extensions across all registered importers.
    /// </summary>
    public IEnumerable<string> GetSupportedExtensions()
    {
        return _importers.SelectMany(i => i.SupportedExtensions).Distinct();
    }

    /// <summary>
    /// Gets a file filter string for open file dialogs.
    /// Example: "3D Models|*.obj;*.fbx;*.gltf;*.glb"
    /// </summary>
    public string GetFileFilter()
    {
        var extensions = GetSupportedExtensions().Select(e => $"*{e}");
        return $"3D Models|{string.Join(';', extensions)}|All Files|*.*";
    }

    /// <summary>
    /// Finds an appropriate importer for the given file.
    /// </summary>
    public IModelImporter? FindImporter(string filePath)
    {
        return _importers.FirstOrDefault(i => i.CanImport(filePath));
    }

    /// <summary>
    /// Imports a model file using the appropriate importer.
    /// </summary>
    public async Task<ImportResult> ImportAsync(string filePath, ImportOptions? options = null, IProgress<double>? progress = null)
    {
        var importer = FindImporter(filePath);
        if (importer == null)
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = $"No importer found for file: {Path.GetFileName(filePath)}"
            };
        }

        return await importer.ImportAsync(filePath, options, progress);
    }
}
