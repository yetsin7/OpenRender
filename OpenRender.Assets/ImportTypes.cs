using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenRender.Scene;
using OpenRender.Rendering;

namespace OpenRender.Assets;

public class ImportOptions
{
    public bool SwapYZ { get; set; }
    public bool Recenter { get; set; }
    public bool GenerateNormals { get; set; }
}

public class ImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Scene3D? Scene { get; set; }
}

public class ImportManager
{
    public async Task<ImportResult> ImportAsync(string filePath, ImportOptions? options = null, IProgress<double>? progress = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = "No se recibio una ruta de modelo."
                };
            }

            if (!File.Exists(filePath))
            {
                return new ImportResult
                {
                    Success = false,
                    ErrorMessage = $"No encontre el archivo: {filePath}"
                };
            }

            progress?.Report(2);
            var scene = await Task.Run(() =>
            {
                using var importer = new ModelImporter();
                return importer.LoadModel(filePath, options ?? new ImportOptions(), progress);
            });

            progress?.Report(100);
            return new ImportResult
            {
                Success = true,
                Scene = scene
            };
        }
        catch (Exception ex)
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = $"Error importando modelo: {ex.Message}"
            };
        }
    }
}
