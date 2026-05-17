using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenRender.Materials;
using OpenRender.Scene;
using OpenRender.Rendering;
using OpenRender.Assets;
using OpenRender.Services;

namespace OpenRender.ViewModels;

public partial class MainViewModel : ObservableObject
{    [RelayCommand]
    private async Task ImportFileAsync()
    {
        try
        {
            var window = GetMainWindow();
            if (window == null)
            {
                StatusText = "No pude abrir el selector de archivos.";
                return;
            }

            var filters = new FilePickerFileType[]
            {
                new("Modelos 3D") { Patterns = new[] { "*.obj", "*.stl", "*.dxf", "*.3ds", "*.dae", "*.ply", "*.fbx", "*.gltf", "*.glb", "*.ifc", "*.step", "*.stp" } },
                new("Wavefront OBJ") { Patterns = new[] { "*.obj" } },
                new("FBX") { Patterns = new[] { "*.fbx" } },
                new("glTF / GLB") { Patterns = new[] { "*.gltf", "*.glb" } },
                new("IFC") { Patterns = new[] { "*.ifc" } },
                new("Todos los archivos") { Patterns = new[] { "*.*" } }
            };

            var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Importar modelo 3D",
                AllowMultiple = false,
                FileTypeFilter = filters
            });

            if (result.Count == 0)
            {
                StatusText = "Importación cancelada.";
                return;
            }

            await LoadFileAsync(result[0].Path.LocalPath);
        }
        catch (Exception ex)
        {
            StatusText = $"Error al importar: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenRecentFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            RecentFiles.Remove(filePath);
            _studioLibraryStore.RemoveEntry(filePath);
            RefreshImportedHistory();
            StatusText = "Ese archivo reciente ya no existe.";
            return;
        }

        await LoadFileAsync(filePath);
    }

    [RelayCommand]
    private async Task ReloadCurrentModelAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentSourceFilePath))
        {
            StatusText = "No hay un archivo importado para reimportar.";
            return;
        }

        if (!File.Exists(CurrentSourceFilePath))
        {
            StatusText = "El archivo actual ya no existe en disco.";
            RefreshImportedHistory();
            return;
        }

        await LoadFileAsync(CurrentSourceFilePath);
        StatusText = $"Reimportación lista: {Path.GetFileName(CurrentSourceFilePath)}.";
    }

    public async Task LoadStartupFileAsync(string filePath, bool runSmokeTest = false, string? capturePath = null)
    {
        if (!File.Exists(filePath))
        {
            StatusText = $"No encontré el archivo de inicio: {filePath}";
            return;
        }

        await LoadFileAsync(filePath);

        if (runSmokeTest)
            await RunNavigationSmokeTestAsync(capturePath);
    }

    private async Task LoadFileAsync(string filePath)
    {
        bool waitingForViewport = false;
        string normalizedPath = Path.GetFullPath(filePath);

        try
        {
            StatusText = $"Importando {Path.GetFileName(normalizedPath)}...";
            IsLoading = true;
            ProgressValue = 0;
            await Task.Delay(50);

            var manager = new OpenRender.Assets.ImportManager();
            var options = new ImportOptions
            {
                SwapYZ = AutoFixOrientation,
                Recenter = AutoRecenter,
                GenerateNormals = true
            };

            var progress = new Progress<double>(value =>
            {
                ProgressValue = value;
                if (value < 100)
                    StatusText = $"Leyendo geometría... {value:F0}%";
            });

            var sw = Stopwatch.StartNew();
            var importResult = await manager.ImportAsync(normalizedPath, options, progress);
            sw.Stop();

            if (!importResult.Success || importResult.Scene == null)
            {
                IsLoading = false;
                StatusText = importResult.ErrorMessage ?? "No se pudo importar el archivo.";
                return;
            }

            CurrentSourceFilePath = normalizedPath;
            ApplyScene(importResult.Scene, Path.GetFileName(normalizedPath), sw.Elapsed);
            RestoreStoredMaterialOverrides();
            _studioLibraryStore.UpsertImportRecord(normalizedPath, Scene, sw.Elapsed);
            PersistCurrentSceneMaterialState();
            UpdateLoadedModelInfo(Path.GetFileName(normalizedPath), sw.Elapsed);

            ProgressValue = 100;
            StatusText = $"Modelo importado. Preparando viewport para {Path.GetFileName(normalizedPath)}...";
            RenderInfoText = $"{RenderResolution} | {OutputFormatText} | viewport listo";
            ActiveWorkspaceSection = WorkspaceSection.Camera;
            SetLumionToolCore(LumionWorkspaceTool.Build, LumionSidePanel.Build, updateStatus: false);
            waitingForViewport = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Error al cargar: {ex.Message}";
        }
        finally
        {
            if (!waitingForViewport)
                IsLoading = false;
        }
    }

    [RelayCommand]
    private void NewScene()
    {
        CurrentSourceFilePath = null;
        ApplyScene(CreateWorkspaceScene(), "Estudio limpio");
        ActiveWorkspaceSection = WorkspaceSection.Camera;
        SetLumionToolCore(LumionWorkspaceTool.Import, LumionSidePanel.Import, updateStatus: false);
        StatusText = "Estudio limpio listo para importar.";
    }

    [RelayCommand]
    private async Task ExportRenderAsync()
    {
        try
        {
            var window = GetMainWindow();
            if (window == null)
            {
                StatusText = "No pude abrir el diálogo de exportación.";
                return;
            }

            var result = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar imagen",
                DefaultExtension = GetExtensionForFormat(_renderSettings.Format).TrimStart('.'),
                SuggestedFileName = $"{SanitizeFileName(Scene.Name)}_{DateTime.Now:yyyyMMdd_HHmmss}",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } },
                    new FilePickerFileType("JPEG") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                    new FilePickerFileType("BMP") { Patterns = new[] { "*.bmp" } },
                    new FilePickerFileType("TIFF") { Patterns = new[] { "*.tiff", "*.tif" } }
                }
            });

            if (result == null)
            {
                StatusText = "Exportación cancelada.";
                return;
            }

            var requestedPath = result.Path.LocalPath;
            var format = ResolveFormatFromPath(requestedPath, _renderSettings.Format);
            var outputPath = EnsureOutputExtension(requestedPath, format);

            StatusText = $"Exportando {Path.GetFileName(outputPath)}...";
            IsLoading = true;
            ProgressValue = 35;
            await Task.Run(() => SoftwareRenderExporter.ExportAsync(Scene, _renderSettings, outputPath, format));
            ProgressValue = 100;

            RenderInfoText = $"{RenderResolution} | {format.ToString().ToUpperInvariant()} | exportada {DateTime.Now:HH:mm:ss}";
            StatusText = $"Imagen exportada: {Path.GetFileName(outputPath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error al exportar: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
