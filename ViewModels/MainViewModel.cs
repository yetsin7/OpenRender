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
using OpenRender.Controls;
using OpenRender.Core.Import;
using OpenRender.Core.Rendering;
using OpenRender.Core.Scene;
using OpenRender.Rendering;
using OpenRender.Services;

namespace OpenRender.ViewModels;

public partial class SceneNodeViewModel : ObservableObject
{
    public SceneNode? Node { get; init; }
    public LightSource? Light { get; init; }
    public int? MaterialIndex { get; init; }
    public bool SupportsVisibility => Node != null || Light != null;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _icon = "NODE";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isModelScope;

    partial void OnIsVisibleChanged(bool value)
    {
        if (Node != null)
            Node.IsVisible = value;

        if (Light != null)
            Light.IsEnabled = value;
    }
}

public partial class MainViewModel : ObservableObject
{
    private static MainViewModel? _instance;
    private readonly RenderSettings _renderSettings;
    private readonly LocalTextureCatalog _localTextureCatalog;
    private readonly StudioLibraryStore _studioLibraryStore;
    private readonly List<SceneNodeViewModel> _allSceneNodes = new();
    private readonly List<PbrMaterial> _trackedSceneMaterials = new();
    private CancellationTokenSource? _materialStateSaveCts;
    private bool _isRestoringStoredMaterials;

    public static void ReportGlError(string message)
    {
        if (_instance != null)
        {
            Dispatcher.UIThread.Post(() => _instance.StatusText = message);
        }
        else
        {
            GlErrorMessage = message;
        }
    }

    public static void ReportViewportReady()
    {
        if (_instance == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            _instance.IsLoading = false;
            if (_instance.ProgressValue >= 100)
                _instance.StatusText = "Viewport listo para navegar.";
        });
    }

    public static string? GlErrorMessage { get; set; }

    public MainViewModel()
    {
        _instance = this;
        _localTextureCatalog = new LocalTextureCatalog();
        _studioLibraryStore = new StudioLibraryStore();
        _renderSettings = new RenderSettings
        {
            Width = 1920,
            Height = 1080,
            Quality = RenderQuality.High,
            SampleCount = 4,
            Format = OutputFormat.Png
        };

        RecentFiles.CollectionChanged += OnRecentFilesChanged;
        LoadMaterialLibrary();
        RefreshImportedHistory();

        Scene = CreateWorkspaceScene();
        ApplyScene(Scene, "Estudio limpio");
        StatusText = "Estudio listo. Importa un modelo para comenzar.";

        if (!string.IsNullOrEmpty(GlErrorMessage))
            StatusText = GlErrorMessage;
    }

    [ObservableProperty] private Scene3D _scene = new();
    [ObservableProperty] private SceneNodeViewModel? _selectedSceneNode;
    [ObservableProperty] private PbrMaterial? _selectedMaterial;
    [ObservableProperty] private MaterialPresetDefinition? _selectedLibraryMaterial;

    [ObservableProperty] private string _statusText = "Estudio listo";
    [ObservableProperty] private string _sceneInfoText = "";
    [ObservableProperty] private string _renderInfoText = "";
    [ObservableProperty] private string _viewportTitle = "Open Render Studio";
    [ObservableProperty] private string _viewportText = "Importa un modelo y trabaja materiales en tiempo real.";
    [ObservableProperty] private string _loadedModelInfo = "";
    [ObservableProperty] private string _workspaceTitle = "Architectural Study";
    [ObservableProperty] private string _workspaceSubtitle = "Viewport en tiempo real, materiales y salida de imagen.";
    [ObservableProperty] private string _sceneFilterText = "";
    [ObservableProperty] private string? _currentSourceFilePath;
    [ObservableProperty] private string _interactionMode = "Object";

    [ObservableProperty] private bool _hasModel;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private double _progressValue;

    [ObservableProperty] private float _cameraFov;
    [ObservableProperty] private float _cameraDistance;
    [ObservableProperty] private float _navigationSpeed;
    [ObservableProperty] private float _sunIntensity = 1.8f;
    [ObservableProperty] private float _ambientIntensity = 0.2f;
    [ObservableProperty] private float _photoExposure = 1.05f;
    [ObservableProperty] private float _photoGamma = 2.2f;
    [ObservableProperty] private float _photoContrast = 1.02f;
    [ObservableProperty] private float _photoWhiteBalance;
    [ObservableProperty] private string _sunStatusText = "Sol activo";

    [ObservableProperty] private int _objectCount;
    [ObservableProperty] private int _triangleCount;
    [ObservableProperty] private int _materialCount;

    [ObservableProperty] private bool _autoFixOrientation = true;
    [ObservableProperty] private bool _autoRecenter = true;

    public ObservableCollection<SceneNodeViewModel> SceneNodes { get; } = new();
    public ObservableCollection<PbrMaterial> SceneMaterials { get; } = new();
    public ObservableCollection<MaterialPresetDefinition> MaterialLibraryPresets { get; } = new();
    public ObservableCollection<ImportedModelHistoryItemViewModel> ImportedHistory { get; } = new();
    public ObservableCollection<string> RecentFiles { get; } = new();

    public string RenderResolution => $"{_renderSettings.Width} x {_renderSettings.Height}";
    public string RenderQualityText => _renderSettings.Quality.ToString();
    public string OutputFormatText => _renderSettings.Format.ToString().ToUpperInvariant();
    public int SampleCount => _renderSettings.SampleCount;
    public int SceneNodeCount => SceneNodes.Count;
    public bool HasSelectedMaterial => SelectedMaterial != null;
    public bool HasSceneSelection => SelectedSceneNode != null;
    public bool HasRecentFiles => RecentFiles.Count > 0;
    public bool HasImportedHistory => ImportedHistory.Count > 0;
    public bool HasLoadedSourceFile => !string.IsNullOrWhiteSpace(CurrentSourceFilePath);
    public bool ShowEmptyState => !HasModel;
    public bool IsObjectSelectionMode => !string.Equals(InteractionMode, "Material", StringComparison.OrdinalIgnoreCase);
    public bool IsMaterialPaintMode => !IsObjectSelectionMode;
    public string WorkspaceModeText => HasModel ? "Proyecto cargado" : "Estudio base";
    public string CurrentSceneLabel => Scene.Name;
    public string SelectedNodeTitle => SelectedSceneNode?.Name ?? "Selecciona un objeto";
    public string SelectedNodeDetails => SelectedSceneNode?.Subtitle ?? "Elige una malla o una luz desde la escena.";
    public bool HasSelectedMeshNode => SelectedSceneNode?.Node?.Mesh != null;
    public string CameraFocusText => $"Objetivo {Scene.Camera.Target.X:F1}, {Scene.Camera.Target.Y:F1}, {Scene.Camera.Target.Z:F1}";
    public string SupportedFormatsText => "OBJ listo hoy. FBX, glTF/GLB e IFC quedan como siguiente fase del pipeline.";
    public string SelectedMaterialCategory => SelectedMaterial?.Category ?? "Sin categoría";
    public string SelectedMaterialSourceText => SelectedMaterial?.SourceName ?? SelectedMaterial?.Name ?? "Sin origen importado";
    public string SelectedMaterialUsageText => SelectedMaterial != null ? $"{SelectedMaterial.UsageCount} superficies" : "Sin material";
    public string MaterialLibraryInfoText => $"{MaterialLibraryPresets.Count} presets arquitectónicos";
    public string ImportedLibraryInfoText => $"{ImportedHistory.Count} modelos guardados en la biblioteca local";

    public float MaterialAlbedoR
    {
        get => SelectedMaterial?.Albedo.X ?? 0f;
        set
        {
            if (SelectedMaterial == null) return;
            var albedo = SelectedMaterial.Albedo;
            albedo.X = value;
            SelectedMaterial.Albedo = albedo;
            OnPropertyChanged(nameof(MaterialAlbedoR));
        }
    }

    public float MaterialAlbedoG
    {
        get => SelectedMaterial?.Albedo.Y ?? 0f;
        set
        {
            if (SelectedMaterial == null) return;
            var albedo = SelectedMaterial.Albedo;
            albedo.Y = value;
            SelectedMaterial.Albedo = albedo;
            OnPropertyChanged(nameof(MaterialAlbedoG));
        }
    }

    public float MaterialAlbedoB
    {
        get => SelectedMaterial?.Albedo.Z ?? 0f;
        set
        {
            if (SelectedMaterial == null) return;
            var albedo = SelectedMaterial.Albedo;
            albedo.Z = value;
            SelectedMaterial.Albedo = albedo;
            OnPropertyChanged(nameof(MaterialAlbedoB));
        }
    }

    [RelayCommand]
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
                new("Modelos 3D") { Patterns = new[] {
                    "*.obj","*.stl","*.dxf","*.3ds","*.dae","*.ply",
                    "*.fbx","*.gltf","*.glb","*.ifc","*.step","*.stp" } },
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

            var manager = new OpenRender.Rendering.Import.ImportManager();
            var options = new ImportOptions
            {
                SwapYZ = true,
                Recenter = true,
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
            await ViewportCaptureService.CaptureAsync(
                outputPath,
                _renderSettings.Width,
                _renderSettings.Height,
                format,
                _renderSettings.JpegQuality,
                cleanViewport: true);

            RenderInfoText = $"{RenderResolution} | {format.ToString().ToUpperInvariant()} | exportada {DateTime.Now:HH:mm:ss}";
            StatusText = $"Imagen exportada: {Path.GetFileName(outputPath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error al exportar: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    [RelayCommand]
    private void ResetCamera()
    {
        if (!TryGetSceneBounds(out var min, out var max))
        {
            Scene.Camera.Reset();
        }
        else
        {
            Scene.Camera.FrameBoundingBox(min, max);
        }

        UpdateCameraProps();
        StatusText = "Cámara reiniciada.";
    }

    [RelayCommand]
    private void ZoomIn()
    {
        Scene.Camera.Zoom(1.5f);
        UpdateCameraProps();
    }

    [RelayCommand]
    private void ZoomOut()
    {
        Scene.Camera.Zoom(-1.5f);
        UpdateCameraProps();
    }

    [RelayCommand]
    private void SetView(string viewType)
    {
        if (TryGetSceneBounds(out var min, out var max))
        {
            Scene.Camera.SetViewAndFrame(viewType, min, max);
        }
        else
        {
            Scene.Camera.SetView(viewType);
        }

        UpdateCameraProps();
        StatusText = $"Vista cambiada a {viewType}.";
    }

    [RelayCommand]
    private void FrameAll()
    {
        if (!TryGetSceneBounds(out var min, out var max))
            return;

        Scene.Camera.FrameBoundingBox(min, max);
        UpdateCameraProps();
        StatusText = "Modelo encuadrado.";
    }

    [RelayCommand]
    private async Task RenderAsync()
    {
        StatusText = $"Actualizando preview {_renderSettings.Quality}...";
        int delay = _renderSettings.Quality switch
        {
            RenderQuality.Draft => 120,
            RenderQuality.Medium => 220,
            RenderQuality.High => 380,
            _ => 520
        };

        await Task.Delay(delay);
        RenderInfoText = $"{RenderResolution} | {OutputFormatText} | preview {DateTime.Now:HH:mm:ss}";
        StatusText = "Preview actualizado. Si te gusta el encuadre, expórtalo.";
    }

    [RelayCommand]
    private void SetQuality(string qualityValue)
    {
        if (!Enum.TryParse<RenderQuality>(qualityValue, out var quality))
            return;

        _renderSettings.Quality = quality;
        _renderSettings.SampleCount = quality switch
        {
            RenderQuality.Draft => 1,
            RenderQuality.Medium => 2,
            RenderQuality.High => 4,
            _ => 8
        };

        UpdateAllProperties();
        StatusText = $"Calidad configurada en {quality}.";
    }

    [RelayCommand]
    private void ApplyMaterialPreset(string presetName)
    {
        if (!MaterialCatalog.TryGetPreset(presetName, out var preset))
        {
            StatusText = $"No encontré el preset {presetName}.";
            return;
        }

        if (SelectedSceneNode?.Node?.Mesh != null)
        {
            ApplyPresetToSelectedNode(preset);
            return;
        }

        if (SelectedMaterial == null)
        {
            StatusText = "Selecciona primero un objeto o un material.";
            return;
        }

        MaterialCatalog.ApplyPreset(SelectedMaterial, preset);
        _localTextureCatalog.ApplyPresetTextures(SelectedMaterial);
        UpdateMaterialBindings();
        LoadSceneNodes();
        SchedulePersistCurrentSceneMaterialState();
        StatusText = $"Preset global aplicado: {preset.Name}.";
    }

    [RelayCommand]
    private void ApplySelectedLibraryPreset()
    {
        if (SelectedLibraryMaterial == null)
        {
            StatusText = "Elige un preset de la biblioteca.";
            return;
        }

        ApplyMaterialPreset(SelectedLibraryMaterial.Key);
    }

    [RelayCommand]
    private void AutoStyleSceneMaterials()
    {
        PrepareSceneMaterials(Scene, autoApplyMatches: true);
        LoadSceneMaterials();
        LoadSceneNodes();
        UpdateAllProperties();
        PersistCurrentSceneMaterialState();
        StatusText = "Materiales reordenados y sugeridos por nombre.";
    }

    [RelayCommand]
    private void PreparePhotoShot()
    {
        if (!HasModel)
        {
            StatusText = "Importa un modelo antes de preparar una foto.";
            return;
        }

        AutoStylePrimarySurfacesForPhoto();
        ApplyEnvironmentPresetCore("Day", suppressStatus: true);
        ApplyPhotoLookPreset("ExteriorDay");

        if (TryGetSceneBounds(out var min, out var max))
            Scene.Camera.FramePhotoShot(min, max);
        else
            Scene.Camera.SetView("3D");

        UpdateAllProperties();
        StatusText = "Encuadre fotográfico listo. Ahora puedes exportar el still limpio.";
    }

    [RelayCommand]
    private void SetInteractionMode(string mode)
    {
        InteractionMode = string.Equals(mode, "Material", StringComparison.OrdinalIgnoreCase)
            ? "Material"
            : "Object";

        if (HasModel)
        {
            if (IsObjectSelectionMode)
                SelectWholeModelFromViewport();
            else if (SelectedSceneNode == null || SelectedSceneNode.IsModelScope)
                SelectFirstRenderableSurface();
        }

        StatusText = IsMaterialPaintMode
            ? "Modo material activo. Haz clic en una superficie para editarla."
            : "Modo objeto activo. Un clic selecciona el modelo completo.";
    }

    [RelayCommand]
    private void SetResolutionPreset(string preset)
    {
        switch (preset.ToUpperInvariant())
        {
            case "HD":
                _renderSettings.Width = 1280;
                _renderSettings.Height = 720;
                break;
            case "QHD":
                _renderSettings.Width = 2560;
                _renderSettings.Height = 1440;
                break;
            case "4K":
                _renderSettings.Width = 3840;
                _renderSettings.Height = 2160;
                break;
            default:
                _renderSettings.Width = 1920;
                _renderSettings.Height = 1080;
                break;
        }

        UpdateAllProperties();
        StatusText = $"Resolución lista para exportar: {RenderResolution}.";
    }

    [RelayCommand]
    private void SetOutputFormat(string formatValue)
    {
        if (!Enum.TryParse<OutputFormat>(formatValue, true, out var format))
            return;

        _renderSettings.Format = format;
        UpdateAllProperties();
        StatusText = $"Formato de salida: {OutputFormatText}.";
    }

    [RelayCommand]
    private void ApplyEnvironmentPreset(string presetName)
    {
        ApplyEnvironmentPresetCore(presetName, suppressStatus: false);
    }

    private void ApplyEnvironmentPresetCore(string presetName, bool suppressStatus)
    {
        var sun = EnsureSun();

        switch (presetName.ToLowerInvariant())
        {
            case "day":
                Scene.BackgroundColor = new Vector3(0.60f, 0.73f, 0.92f);
                Scene.AmbientIntensity = 0.28f;
                sun.Intensity = 1.8f;
                sun.Color = new Vector3(1.0f, 0.97f, 0.92f);
                sun.Direction = Vector3.Normalize(new Vector3(-0.35f, -1f, -0.25f));
                ApplyPhotoLookPreset("ExteriorDay");
                break;
            case "overcast":
                Scene.BackgroundColor = new Vector3(0.45f, 0.52f, 0.62f);
                Scene.AmbientIntensity = 0.42f;
                sun.Intensity = 0.9f;
                sun.Color = new Vector3(0.92f, 0.95f, 1.0f);
                sun.Direction = Vector3.Normalize(new Vector3(-0.20f, -1f, -0.10f));
                ApplyPhotoLookPreset("Overcast");
                break;
            case "sunset":
                Scene.BackgroundColor = new Vector3(0.76f, 0.46f, 0.30f);
                Scene.AmbientIntensity = 0.22f;
                sun.Intensity = 1.4f;
                sun.Color = new Vector3(1.0f, 0.78f, 0.58f);
                sun.Direction = Vector3.Normalize(new Vector3(0.55f, -0.55f, -0.20f));
                ApplyPhotoLookPreset("Sunset");
                break;
            default:
                Scene.BackgroundColor = new Vector3(0.09f, 0.12f, 0.18f);
                Scene.AmbientIntensity = 0.16f;
                sun.Intensity = 1.1f;
                sun.Color = new Vector3(0.94f, 0.96f, 1.0f);
                sun.Direction = Vector3.Normalize(new Vector3(-0.15f, -1f, 0.10f));
                ApplyPhotoLookPreset("Studio");
                break;
        }

        UpdateAllProperties();
        if (!suppressStatus)
            StatusText = $"Entorno aplicado: {presetName}.";
    }

    private void ApplyPhotoLookPreset(string presetName)
    {
        switch (presetName.ToLowerInvariant())
        {
            case "exteriorday":
                Scene.Exposure = 1.10f;
                Scene.Gamma = 2.15f;
                Scene.Contrast = 1.08f;
                Scene.WhiteBalance = 0.08f;
                break;
            case "overcast":
                Scene.Exposure = 1.18f;
                Scene.Gamma = 2.20f;
                Scene.Contrast = 0.96f;
                Scene.WhiteBalance = -0.04f;
                break;
            case "sunset":
                Scene.Exposure = 1.04f;
                Scene.Gamma = 2.10f;
                Scene.Contrast = 1.12f;
                Scene.WhiteBalance = 0.22f;
                break;
            default:
                Scene.Exposure = 0.98f;
                Scene.Gamma = 2.24f;
                Scene.Contrast = 1.04f;
                Scene.WhiteBalance = -0.02f;
                break;
        }

        PhotoExposure = Scene.Exposure;
        PhotoGamma = Scene.Gamma;
        PhotoContrast = Scene.Contrast;
        PhotoWhiteBalance = Scene.WhiteBalance;
        _renderSettings.Exposure = Scene.Exposure;
        _renderSettings.Gamma = Scene.Gamma;
    }

    [RelayCommand]
    private void About()
    {
        StatusText = "Open Render Studio: viewport en tiempo real, materiales PBR y exportación de imagen.";
    }

    [RelayCommand]
    private void Shortcuts()
    {
        StatusText = "Atajos: Ctrl+O importar, Ctrl+N estudio demo, Ctrl+S exportar, F5 preview.";
    }

    [RelayCommand]
    private void OpenGithub()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/yetsin7/OpenRender") { UseShellExecute = true });
        }
        catch
        {
            StatusText = "Repositorio: https://github.com/yetsin7/OpenRender";
        }
    }

    partial void OnHasModelChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(WorkspaceModeText));
    }

    partial void OnSceneChanged(Scene3D value)
    {
        OnPropertyChanged(nameof(CurrentSceneLabel));
        OnPropertyChanged(nameof(CameraFocusText));
    }

    partial void OnSelectedSceneNodeChanged(SceneNodeViewModel? value)
    {
        if (value?.MaterialIndex is int materialIndex &&
            materialIndex >= 0 &&
            materialIndex < Scene.Materials.Count)
        {
            SelectedMaterial = Scene.Materials[materialIndex];
        }
        else
        {
            SelectedMaterial = null;
        }

        OnPropertyChanged(nameof(SelectedNodeTitle));
        OnPropertyChanged(nameof(SelectedNodeDetails));
        OnPropertyChanged(nameof(HasSceneSelection));
        OnPropertyChanged(nameof(HasSelectedMeshNode));

        if (value != null)
            StatusText = value.IsModelScope
                ? $"Modelo seleccionado: {value.Name}."
                : $"Inspector enfocado en {value.Name}.";
    }

    partial void OnInteractionModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsObjectSelectionMode));
        OnPropertyChanged(nameof(IsMaterialPaintMode));
    }

    partial void OnSelectedMaterialChanged(PbrMaterial? value)
    {
        if (value != null)
        {
            SelectedLibraryMaterial = MaterialLibraryPresets
                .FirstOrDefault(item => string.Equals(item.Key, value.PresetKey, StringComparison.OrdinalIgnoreCase));
        }

        UpdateMaterialBindings();
    }

    partial void OnSceneFilterTextChanged(string value)
    {
        RefreshVisibleSceneNodes();
    }

    partial void OnSunIntensityChanged(float value)
    {
        var sun = Scene.Lights.FirstOrDefault(l => l.Type == LightType.Directional);
        if (sun != null)
            sun.Intensity = value;
    }

    partial void OnAmbientIntensityChanged(float value)
    {
        Scene.AmbientIntensity = value;
    }

    partial void OnPhotoExposureChanged(float value)
    {
        Scene.Exposure = value;
        _renderSettings.Exposure = value;
    }

    partial void OnPhotoGammaChanged(float value)
    {
        Scene.Gamma = value;
        _renderSettings.Gamma = value;
    }

    partial void OnPhotoContrastChanged(float value)
    {
        Scene.Contrast = value;
    }

    partial void OnPhotoWhiteBalanceChanged(float value)
    {
        Scene.WhiteBalance = value;
    }

    partial void OnCameraFovChanged(float value)
    {
        Scene.Camera.FieldOfView = value;
    }

    partial void OnCameraDistanceChanged(float value)
    {
        Scene.Camera.OrbitDistance = value;
        NavigationSpeed = Scene.Camera.MoveSpeed;
        OnPropertyChanged(nameof(CameraFocusText));
    }

    partial void OnNavigationSpeedChanged(float value)
    {
        Scene.Camera.MoveSpeed = value;
    }

    partial void OnCurrentSourceFilePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasLoadedSourceFile));
    }

    private void ApplyScene(Scene3D scene, string sourceLabel, TimeSpan? importDuration = null)
    {
        Scene = scene;
        HasModel = Scene.GetAllNodes().Any(node => node.Mesh != null);

        WorkspaceTitle = Scene.Name;
        WorkspaceSubtitle = HasModel
            ? "Navega, materializa y exporta un still directamente desde el viewport."
            : "Importa un modelo para comenzar a montar el proyecto.";

        ViewportTitle = sourceLabel;
        ViewportText = HasModel
            ? "Editor listo: cambia cámara, materiales y exporta la vista actual."
            : "Importa un modelo para poblar la escena.";

        PrepareSceneMaterials(Scene, autoApplyMatches: HasModel);
        RefreshScenePresentation();
        UpdateLoadedModelInfo(sourceLabel, importDuration);
    }

    private void LoadSceneMaterials()
    {
        DetachSceneMaterialHandlers();
        SceneMaterials.Clear();
        foreach (var material in Scene.Materials)
            SceneMaterials.Add(material);

        AttachSceneMaterialHandlers();
        OnPropertyChanged(nameof(MaterialLibraryInfoText));
    }

    private void LoadMaterialLibrary()
    {
        MaterialLibraryPresets.Clear();
        foreach (var preset in MaterialCatalog.Presets
                     .OrderBy(preset => GetCategoryOrder(preset.Category))
                     .ThenBy(preset => preset.Name))
        {
            MaterialLibraryPresets.Add(preset);
        }

        SelectedLibraryMaterial = MaterialLibraryPresets.FirstOrDefault();
        OnPropertyChanged(nameof(MaterialLibraryInfoText));
    }

    private void PrepareSceneMaterials(Scene3D scene, bool autoApplyMatches)
    {
        var usageByMaterial = scene.GetAllNodes()
            .Where(node => node.MaterialIndex.HasValue)
            .GroupBy(node => node.MaterialIndex!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        for (int index = 0; index < scene.Materials.Count; index++)
        {
            var material = scene.Materials[index];
            material.SourceName ??= material.Name;
            material.UsageCount = usageByMaterial.TryGetValue(index, out int usageCount) ? usageCount : 0;

            string descriptor = $"{material.SourceName} {material.Name}";

            if (autoApplyMatches && MaterialCatalog.TryMatchPreset(descriptor, out var matchedPreset))
            {
                MaterialCatalog.ApplyPreset(material, matchedPreset);
                _localTextureCatalog.ApplyPresetTextures(material);
            }
            else if (autoApplyMatches &&
                     material.Opacity < 0.99f &&
                     MaterialCatalog.TryGetPreset("glass-clear", out var transparentPreset))
            {
                MaterialCatalog.ApplyPreset(material, transparentPreset);
                _localTextureCatalog.ApplyPresetTextures(material);
            }
            else
            {
                material.Category = MaterialCatalog.GuessCategory(descriptor);
            }
        }

        var orderedMaterials = scene.Materials
            .Select((material, oldIndex) => new { material, oldIndex })
            .OrderBy(item => GetCategoryOrder(item.material.Category))
            .ThenByDescending(item => item.material.UsageCount)
            .ThenBy(item => item.material.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var indexMap = orderedMaterials
            .Select((item, newIndex) => new { item.oldIndex, newIndex })
            .ToDictionary(item => item.oldIndex, item => item.newIndex);

        scene.Materials.Clear();
        foreach (var entry in orderedMaterials)
            scene.Materials.Add(entry.material);

        foreach (var node in scene.GetAllNodes())
        {
            if (node.MaterialIndex is int materialIndex && indexMap.TryGetValue(materialIndex, out int newIndex))
                node.MaterialIndex = newIndex;
        }
    }

    private void LoadSceneNodes()
    {
        var selectedNodeId = SelectedSceneNode?.Node?.Id;
        var selectedLightName = SelectedSceneNode?.Light?.Name;
        bool selectedModelScope = SelectedSceneNode?.IsModelScope == true;

        _allSceneNodes.Clear();

        if (HasModel)
        {
            _allSceneNodes.Add(new SceneNodeViewModel
            {
                Name = string.IsNullOrWhiteSpace(CurrentSourceFilePath)
                    ? WorkspaceTitle
                    : Path.GetFileNameWithoutExtension(CurrentSourceFilePath),
                Icon = "MOD",
                Subtitle = SceneInfoText,
                IsVisible = true,
                IsModelScope = true
            });
        }

        foreach (var light in Scene.Lights)
        {
            _allSceneNodes.Add(new SceneNodeViewModel
            {
                Name = light.Name,
                Icon = light.Type == LightType.Directional ? "SUN" : "LGT",
                Subtitle = light.Type == LightType.Directional ? "Luz principal" : "Luz auxiliar",
                Light = light,
                IsVisible = light.IsEnabled
            });
        }

        foreach (var node in Scene.GetAllNodes())
        {
            _allSceneNodes.Add(new SceneNodeViewModel
            {
                Name = node.Name,
                Icon = node.Mesh != null ? "MESH" : "NODE",
                Subtitle = BuildNodeSubtitle(node),
                Node = node,
                MaterialIndex = node.MaterialIndex,
                IsVisible = node.IsVisible
            });
        }

        RefreshVisibleSceneNodes(selectedNodeId, selectedLightName, selectedModelScope);
    }

    private string BuildNodeSubtitle(SceneNode node)
    {
        if (node.Mesh == null)
            return "Grupo o transform";

        string materialName = "Sin material";
        string? sourceMaterialName = null;
        if (node.MaterialIndex is int materialIndex &&
            materialIndex >= 0 &&
            materialIndex < Scene.Materials.Count)
        {
            var material = Scene.Materials[materialIndex];
            materialName = material.Name;
            sourceMaterialName = material.SourceName;
        }

        if (!string.IsNullOrWhiteSpace(sourceMaterialName) &&
            !string.Equals(sourceMaterialName, materialName, StringComparison.OrdinalIgnoreCase))
        {
            return $"{node.Mesh.TriangleCount:N0} tris · {materialName} <- {sourceMaterialName}";
        }

        return $"{node.Mesh.TriangleCount:N0} tris · {materialName}";
    }

    private void ApplyPresetToSelectedNode(MaterialPresetDefinition preset)
    {
        var node = SelectedSceneNode?.Node;
        if (node?.Mesh == null)
        {
            StatusText = "Selecciona una superficie u objeto real.";
            return;
        }

        if (!ApplyPresetToNodeCore(node, preset, selectMaterial: true))
        {
            StatusText = $"Ese objeto ya usa {preset.Name}.";
            return;
        }

        PrepareSceneMaterials(Scene, autoApplyMatches: false);
        RefreshScenePresentation();
        PersistCurrentSceneMaterialState();
        StatusText = $"Material aplicado a {node.Name}: {preset.Name}.";
    }

    private void AutoStylePrimarySurfacesForPhoto()
    {
        bool appliedAny = false;

        foreach (var node in Scene.GetAllNodes().Where(item => item.Mesh != null))
        {
            if (node.MaterialIndex is not int materialIndex ||
                materialIndex < 0 ||
                materialIndex >= Scene.Materials.Count)
            {
                continue;
            }

            var currentMaterial = Scene.Materials[materialIndex];
            string descriptor = $"{node.Name} {currentMaterial.Name}";

            if (!MaterialCatalog.TryMatchPreset(descriptor, out var preset))
                continue;

            if (!ShouldApplyPhotoSurfacePreset(node.Name, currentMaterial, preset))
                continue;

            appliedAny |= ApplyPresetToNodeCore(node, preset, selectMaterial: false);
        }

        if (!appliedAny)
            return;

        PrepareSceneMaterials(Scene, autoApplyMatches: false);
        RefreshScenePresentation();
        PersistCurrentSceneMaterialState();
    }

    private bool ApplyPresetToNodeCore(SceneNode node, MaterialPresetDefinition preset, bool selectMaterial)
    {
        var existingMaterial = node.MaterialIndex is int materialIndex &&
                               materialIndex >= 0 &&
                               materialIndex < Scene.Materials.Count
            ? Scene.Materials[materialIndex]
            : null;

        if (existingMaterial != null &&
            string.Equals(existingMaterial.PresetKey, preset.Key, StringComparison.OrdinalIgnoreCase))
        {
            bool backfilledTextures = _localTextureCatalog.BackfillPresetTexturesIfMissing(existingMaterial);
            if (selectMaterial)
                SelectedMaterial = existingMaterial;

            return backfilledTextures;
        }

        if (existingMaterial != null && existingMaterial.UsageCount <= 1)
        {
            existingMaterial.SourceName ??= existingMaterial.Name;
            MaterialCatalog.ApplyPreset(existingMaterial, preset);
            _localTextureCatalog.ApplyPresetTextures(existingMaterial);
            existingMaterial.Name = $"{preset.Name} · {TrimNodeName(node.Name)}";

            if (selectMaterial)
                SelectedMaterial = existingMaterial;

            return true;
        }

        var localizedMaterial = preset.Material.Clone($"{preset.Name} · {TrimNodeName(node.Name)}");
        localizedMaterial.Category = preset.Category;
        localizedMaterial.PresetKey = preset.Key;
        localizedMaterial.SourceName = existingMaterial?.SourceName ?? existingMaterial?.Name ?? node.Name;
        _localTextureCatalog.ApplyPresetTextures(localizedMaterial);
        Scene.Materials.Add(localizedMaterial);
        node.MaterialIndex = Scene.Materials.Count - 1;

        if (selectMaterial)
            SelectedMaterial = localizedMaterial;

        return true;
    }

    private static bool ShouldApplyPhotoSurfacePreset(string nodeName, PbrMaterial currentMaterial, MaterialPresetDefinition preset)
    {
        if (string.Equals(currentMaterial.PresetKey, preset.Key, StringComparison.OrdinalIgnoreCase))
            return false;

        string hint = NormalizeSurfaceHint(nodeName);
        bool hasExplicitSurfaceHint = ContainsAnyHint(
            hint,
            "roof",
            "techo",
            "ventana",
            "window",
            "piedra",
            "cantera",
            "barandilla",
            "railing",
            "reja",
            "montante",
            "puerta",
            "door",
            "ceram",
            "azulejo",
            "folha",
            "leaf");

        if (!hasExplicitSurfaceHint)
            return false;

        if (currentMaterial.UsageCount <= 1)
            return true;

        return string.IsNullOrWhiteSpace(currentMaterial.PresetKey) ||
               string.Equals(currentMaterial.PresetKey, "paint-soft-white", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(currentMaterial.PresetKey, "paint-warm-gray", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(currentMaterial.PresetKey, "clay-soft", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateLoadedModelInfo(string sourceLabel, TimeSpan? importDuration)
    {
        if (!HasModel)
        {
            LoadedModelInfo = "Sin geometría cargada.";
            return;
        }

        string timeInfo = importDuration.HasValue ? $" · {importDuration.Value.TotalMilliseconds:F0} ms" : "";
        string materialStateInfo = "";

        var storedRecord = string.IsNullOrWhiteSpace(CurrentSourceFilePath)
            ? null
            : _studioLibraryStore.Find(CurrentSourceFilePath);

        if (storedRecord?.MaterialOverrides.Count > 0)
        {
            materialStateInfo = $" · {storedRecord.MaterialOverrides.Count} superficies guardadas";
        }

        LoadedModelInfo = $"{sourceLabel} · {TriangleCount:N0} tris · {MaterialCount} materiales{timeInfo}{materialStateInfo}";
    }

    private void UpdateCameraProps()
    {
        CameraFov = Scene.Camera.FieldOfView;
        CameraDistance = Scene.Camera.OrbitDistance;
        NavigationSpeed = Scene.Camera.MoveSpeed;
        OnPropertyChanged(nameof(CameraFocusText));
    }

    private void UpdateAllProperties()
    {
        UpdateCameraProps();

        ObjectCount = Scene.GetAllNodes().Count(node => node.Mesh != null);
        TriangleCount = Scene.GetTotalTriangleCount();
        MaterialCount = Scene.Materials.Count;

        var sun = Scene.Lights.FirstOrDefault(light => light.Type == LightType.Directional);
        if (sun != null)
        {
            SunIntensity = sun.Intensity;
            SunStatusText = sun.IsEnabled ? "Sol activo" : "Sol apagado";
        }

        AmbientIntensity = Scene.AmbientIntensity;
        PhotoExposure = Scene.Exposure;
        PhotoGamma = Scene.Gamma;
        PhotoContrast = Scene.Contrast;
        PhotoWhiteBalance = Scene.WhiteBalance;
        SceneInfoText = $"{ObjectCount} objetos · {TriangleCount:N0} tris · {MaterialCount} materiales";
        RenderInfoText = $"{RenderResolution} | {OutputFormatText} | {_renderSettings.Quality}";

        OnPropertyChanged(nameof(RenderResolution));
        OnPropertyChanged(nameof(RenderQualityText));
        OnPropertyChanged(nameof(OutputFormatText));
        OnPropertyChanged(nameof(SampleCount));
        OnPropertyChanged(nameof(CameraFocusText));
        UpdateMaterialBindings();
    }

    private bool TryGetSceneBounds(out Vector3 min, out Vector3 max)
    {
        var nodes = Scene.GetAllNodes().Where(node => node.Mesh != null).ToList();
        if (nodes.Count == 0)
        {
            min = Vector3.Zero;
            max = Vector3.Zero;
            return false;
        }

        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);

        foreach (var node in nodes)
        {
            var (localMin, localMax) = node.Mesh!.ComputeBoundingBox();
            min = Vector3.Min(min, localMin + node.Position);
            max = Vector3.Max(max, localMax + node.Position);
        }

        return true;
    }

    private LightSource EnsureSun()
    {
        var sun = Scene.Lights.FirstOrDefault(light => light.Type == LightType.Directional);
        if (sun != null)
            return sun;

        sun = LightSource.CreateSun();
        Scene.Lights.Add(sun);
        return sun;
    }

    private void RefreshScenePresentation()
    {
        LoadSceneMaterials();
        LoadSceneNodes();
        UpdateAllProperties();
    }

    private void RestoreStoredMaterialOverrides()
    {
        if (string.IsNullOrWhiteSpace(CurrentSourceFilePath))
            return;

        var record = _studioLibraryStore.Find(CurrentSourceFilePath);
        if (record?.MaterialOverrides == null || record.MaterialOverrides.Count == 0)
            return;

        var surfaceOverrides = record.MaterialOverrides
            .Where(item => !string.IsNullOrWhiteSpace(item.SurfaceKey))
            .GroupBy(item => item.SurfaceKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var sourceMaterialOverrides = record.MaterialOverrides
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceMaterialName))
            .GroupBy(item => item.SourceMaterialName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        int restoredCount = 0;
        var createdMaterials = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        _isRestoringStoredMaterials = true;
        try
        {
            foreach (var node in Scene.GetAllNodes().Where(node => node.Mesh != null))
            {
                if (node.MaterialIndex is not int materialIndex ||
                    materialIndex < 0 ||
                    materialIndex >= Scene.Materials.Count)
                {
                    continue;
                }

                var currentMaterial = Scene.Materials[materialIndex];
                string sourceMaterialName = currentMaterial.SourceName ?? currentMaterial.Name;

                if (!surfaceOverrides.TryGetValue(node.Name, out var overrideRecord) &&
                    !sourceMaterialOverrides.TryGetValue(sourceMaterialName, out overrideRecord))
                {
                    continue;
                }

                if (ApplyStoredOverrideToNode(node, overrideRecord, createdMaterials))
                    restoredCount++;
            }
        }
        finally
        {
            _isRestoringStoredMaterials = false;
        }

        if (restoredCount <= 0)
            return;

        PrepareSceneMaterials(Scene, autoApplyMatches: false);
        RefreshScenePresentation();
        StatusText = $"Modelo importado. Restauré {restoredCount} materiales desde la biblioteca local.";
    }

    private bool ApplyStoredOverrideToNode(
        SceneNode node,
        StoredMaterialOverride overrideRecord,
        Dictionary<string, int> createdMaterials)
    {
        if (node.MaterialIndex is not int materialIndex ||
            materialIndex < 0 ||
            materialIndex >= Scene.Materials.Count)
        {
            return false;
        }

        string overrideKey = BuildOverrideCacheKey(overrideRecord);
        if (createdMaterials.TryGetValue(overrideKey, out int cachedMaterialIndex))
        {
            if (node.MaterialIndex == cachedMaterialIndex)
                return false;

            node.MaterialIndex = cachedMaterialIndex;
            return true;
        }

        var existingMaterial = Scene.Materials[materialIndex];
        if (MaterialMatchesOverride(existingMaterial, overrideRecord))
        {
            createdMaterials[overrideKey] = materialIndex;
            return false;
        }

        if (existingMaterial.UsageCount <= 1)
        {
            ApplyStoredOverrideValues(existingMaterial, overrideRecord);
            _localTextureCatalog.BackfillPresetTexturesIfMissing(existingMaterial);
            createdMaterials[overrideKey] = materialIndex;
            return true;
        }

        var localizedMaterial = existingMaterial.Clone(overrideRecord.DisplayMaterialName);
        ApplyStoredOverrideValues(localizedMaterial, overrideRecord);
        _localTextureCatalog.BackfillPresetTexturesIfMissing(localizedMaterial);
        Scene.Materials.Add(localizedMaterial);
        node.MaterialIndex = Scene.Materials.Count - 1;
        createdMaterials[overrideKey] = node.MaterialIndex.Value;
        return true;
    }

    private static void ApplyStoredOverrideValues(PbrMaterial material, StoredMaterialOverride overrideRecord)
    {
        material.Name = string.IsNullOrWhiteSpace(overrideRecord.DisplayMaterialName)
            ? material.Name
            : overrideRecord.DisplayMaterialName;
        material.SourceName = string.IsNullOrWhiteSpace(overrideRecord.SourceMaterialName)
            ? material.SourceName ?? material.Name
            : overrideRecord.SourceMaterialName;
        material.Category = overrideRecord.Category ?? material.Category;
        material.PresetKey = overrideRecord.PresetKey;
        material.Albedo = overrideRecord.Albedo.ToVector3();
        material.Metallic = overrideRecord.Metallic;
        material.Roughness = overrideRecord.Roughness;
        material.AmbientOcclusion = overrideRecord.AmbientOcclusion;
        material.Opacity = overrideRecord.Opacity;
        material.Emissive = overrideRecord.Emissive.ToVector3();
        material.NormalStrength = overrideRecord.NormalStrength;
        material.UvScale = overrideRecord.UvScale;
        material.AlbedoTexturePath = overrideRecord.AlbedoTexturePath;
        material.NormalTexturePath = overrideRecord.NormalTexturePath;
        material.RoughnessTexturePath = overrideRecord.RoughnessTexturePath;
        material.AoTexturePath = overrideRecord.AoTexturePath;
    }

    private static bool MaterialMatchesOverride(PbrMaterial material, StoredMaterialOverride overrideRecord)
    {
        return string.Equals(material.Name, overrideRecord.DisplayMaterialName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(material.SourceName ?? material.Name, overrideRecord.SourceMaterialName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(material.PresetKey ?? "", overrideRecord.PresetKey ?? "", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(material.Category ?? "", overrideRecord.Category ?? "", StringComparison.OrdinalIgnoreCase) &&
               NearlyEqual(material.Metallic, overrideRecord.Metallic) &&
               NearlyEqual(material.Roughness, overrideRecord.Roughness) &&
               NearlyEqual(material.AmbientOcclusion, overrideRecord.AmbientOcclusion) &&
               NearlyEqual(material.Opacity, overrideRecord.Opacity) &&
               NearlyEqual(material.NormalStrength, overrideRecord.NormalStrength) &&
               NearlyEqual(material.UvScale, overrideRecord.UvScale) &&
               string.Equals(material.AlbedoTexturePath ?? "", overrideRecord.AlbedoTexturePath ?? "", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(material.NormalTexturePath ?? "", overrideRecord.NormalTexturePath ?? "", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(material.RoughnessTexturePath ?? "", overrideRecord.RoughnessTexturePath ?? "", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(material.AoTexturePath ?? "", overrideRecord.AoTexturePath ?? "", StringComparison.OrdinalIgnoreCase) &&
               NearlyEqual(material.Albedo, overrideRecord.Albedo.ToVector3()) &&
               NearlyEqual(material.Emissive, overrideRecord.Emissive.ToVector3());
    }

    private void AttachSceneMaterialHandlers()
    {
        foreach (var material in SceneMaterials)
        {
            material.PropertyChanged += OnSceneMaterialChanged;
            _trackedSceneMaterials.Add(material);
        }
    }

    private void DetachSceneMaterialHandlers()
    {
        foreach (var material in _trackedSceneMaterials)
            material.PropertyChanged -= OnSceneMaterialChanged;

        _trackedSceneMaterials.Clear();
    }

    private void OnSceneMaterialChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isRestoringStoredMaterials || string.IsNullOrWhiteSpace(CurrentSourceFilePath) || !HasModel)
            return;

        if (string.Equals(e.PropertyName, nameof(PbrMaterial.UsageCount), StringComparison.Ordinal))
            return;

        SchedulePersistCurrentSceneMaterialState();
    }

    private async void SchedulePersistCurrentSceneMaterialState()
    {
        _materialStateSaveCts?.Cancel();
        _materialStateSaveCts = new CancellationTokenSource();
        var token = _materialStateSaveCts.Token;

        try
        {
            await Task.Delay(180, token);
            if (!token.IsCancellationRequested)
                PersistCurrentSceneMaterialState();
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void PersistCurrentSceneMaterialState()
    {
        if (_isRestoringStoredMaterials || string.IsNullOrWhiteSpace(CurrentSourceFilePath) || !HasModel)
            return;

        _studioLibraryStore.SaveSceneMaterialState(CurrentSourceFilePath, Scene);
        RefreshImportedHistory();
    }

    private void RefreshImportedHistory()
    {
        var history = _studioLibraryStore.GetHistory();

        ImportedHistory.Clear();
        RecentFiles.Clear();

        foreach (var item in history)
        {
            bool existsOnDisk = File.Exists(item.SourcePath);
            ImportedHistory.Add(new ImportedModelHistoryItemViewModel
            {
                FilePath = item.SourcePath,
                DisplayName = string.IsNullOrWhiteSpace(item.DisplayName)
                    ? Path.GetFileNameWithoutExtension(item.SourcePath)
                    : item.DisplayName,
                Summary = BuildImportedHistorySummary(item),
                Meta = BuildImportedHistoryMeta(item, existsOnDisk),
                ExistsOnDisk = existsOnDisk
            });

            if (existsOnDisk && RecentFiles.Count < 8)
                RecentFiles.Add(item.SourcePath);
        }

        OnPropertyChanged(nameof(HasImportedHistory));
        OnPropertyChanged(nameof(ImportedLibraryInfoText));
    }

    private static string BuildImportedHistorySummary(ImportedModelRecord item)
    {
        return $"{item.ObjectCount} objs · {item.TriangleCount:N0} tris · {item.MaterialCount} mats · {FormatHistoryMoment(item.LastImportedUtc)}";
    }

    private static string BuildImportedHistoryMeta(ImportedModelRecord item, bool existsOnDisk)
    {
        string sizeText = item.FileSizeBytes > 0
            ? $"{item.FileSizeBytes / (1024f * 1024f):F1} MB"
            : "tamaño desconocido";

        string diskState = existsOnDisk ? sizeText : "archivo no encontrado";
        return $"{diskState} · {item.SourcePath}";
    }

    private static string FormatHistoryMoment(DateTime utcValue)
    {
        if (utcValue == default)
            return "sin fecha";

        return utcValue.ToLocalTime().ToString("dd MMM yyyy HH:mm");
    }

    private static string BuildOverrideCacheKey(StoredMaterialOverride overrideRecord)
    {
        return string.Join("|",
            overrideRecord.SourceMaterialName,
            overrideRecord.DisplayMaterialName,
            overrideRecord.PresetKey ?? "",
            overrideRecord.Category ?? "",
            overrideRecord.Albedo.X.ToString("F4"),
            overrideRecord.Albedo.Y.ToString("F4"),
            overrideRecord.Albedo.Z.ToString("F4"),
            overrideRecord.Metallic.ToString("F4"),
            overrideRecord.Roughness.ToString("F4"),
            overrideRecord.AmbientOcclusion.ToString("F4"),
            overrideRecord.Opacity.ToString("F4"),
            overrideRecord.Emissive.X.ToString("F4"),
            overrideRecord.Emissive.Y.ToString("F4"),
            overrideRecord.Emissive.Z.ToString("F4"),
            overrideRecord.NormalStrength.ToString("F4"),
            overrideRecord.UvScale.ToString("F4"),
            overrideRecord.AlbedoTexturePath ?? "",
            overrideRecord.NormalTexturePath ?? "",
            overrideRecord.RoughnessTexturePath ?? "",
            overrideRecord.AoTexturePath ?? "");
    }

    private void UpdateMaterialBindings()
    {
        OnPropertyChanged(nameof(HasSelectedMaterial));
        OnPropertyChanged(nameof(MaterialAlbedoR));
        OnPropertyChanged(nameof(MaterialAlbedoG));
        OnPropertyChanged(nameof(MaterialAlbedoB));
        OnPropertyChanged(nameof(SelectedMaterialCategory));
        OnPropertyChanged(nameof(SelectedMaterialSourceText));
        OnPropertyChanged(nameof(SelectedMaterialUsageText));
        OnPropertyChanged(nameof(HasSelectedMeshNode));
    }

    private void OnRecentFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasRecentFiles));
    }

    public void SelectViewportHit(string? nodeId)
    {
        if (!HasModel)
            return;

        if (IsObjectSelectionMode || string.IsNullOrWhiteSpace(nodeId))
        {
            SelectWholeModelFromViewport();
            return;
        }

        var sceneItem = _allSceneNodes.FirstOrDefault(item => string.Equals(item.Node?.Id, nodeId, StringComparison.Ordinal));
        if (sceneItem != null)
            SelectedSceneNode = sceneItem;
    }

    private void SelectWholeModelFromViewport()
    {
        SelectedSceneNode = _allSceneNodes.FirstOrDefault(item => item.IsModelScope)
            ?? _allSceneNodes.FirstOrDefault(item => item.Node?.Mesh != null)
            ?? _allSceneNodes.FirstOrDefault();
    }

    private void SelectFirstRenderableSurface()
    {
        SelectedSceneNode = _allSceneNodes.FirstOrDefault(item => item.Node?.Mesh != null)
            ?? _allSceneNodes.FirstOrDefault(item => item.IsModelScope)
            ?? _allSceneNodes.FirstOrDefault();
    }

    private static bool NearlyEqual(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.0005f;
    }

    private static bool NearlyEqual(Vector3 left, Vector3 right)
    {
        return NearlyEqual(left.X, right.X) &&
               NearlyEqual(left.Y, right.Y) &&
               NearlyEqual(left.Z, right.Z);
    }

    private void RefreshVisibleSceneNodes(string? preferredNodeId = null, string? preferredLightName = null, bool preferModelScope = false)
    {
        string filter = SceneFilterText.Trim();
        IEnumerable<SceneNodeViewModel> source = _allSceneNodes;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            source = source.Where(item =>
                item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Subtitle.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        var visibleNodes = source.ToList();

        SceneNodes.Clear();
        foreach (var item in visibleNodes)
            SceneNodes.Add(item);

        OnPropertyChanged(nameof(SceneNodeCount));

        SelectedSceneNode =
            (preferModelScope || IsObjectSelectionMode
                ? SceneNodes.FirstOrDefault(item => item.IsModelScope)
                : null) ??
            SceneNodes.FirstOrDefault(item => item.Node?.Id == preferredNodeId) ??
            SceneNodes.FirstOrDefault(item => item.Light?.Name == preferredLightName) ??
            SceneNodes.FirstOrDefault(item => item.Node?.Mesh != null) ??
            SceneNodes.FirstOrDefault();
    }

    private async Task RunNavigationSmokeTestAsync(string? capturePath)
    {
        if (!HasModel)
            return;

        StatusText = "Ejecutando smoke test de navegación...";
        PrepareSceneMaterials(Scene, autoApplyMatches: true);
        LoadSceneMaterials();
        LoadSceneNodes();
        UpdateAllProperties();

        FrameAll();
        await Task.Delay(250);
        SetView("Front");
        await Task.Delay(250);

        if (!string.IsNullOrWhiteSpace(capturePath))
        {
            await ViewportCaptureService.CaptureAsync(
                WithSuffix(capturePath, "_front"),
                _renderSettings.Width,
                _renderSettings.Height,
                _renderSettings.Format,
                _renderSettings.JpegQuality);
        }

        SetView("Right");
        await Task.Delay(250);
        SetView("Top");
        await Task.Delay(250);

        if (!string.IsNullOrWhiteSpace(capturePath))
        {
            await ViewportCaptureService.CaptureAsync(
                WithSuffix(capturePath, "_top"),
                _renderSettings.Width,
                _renderSettings.Height,
                _renderSettings.Format,
                _renderSettings.JpegQuality);
        }

        PreparePhotoShot();
        await Task.Delay(260);

        if (!string.IsNullOrWhiteSpace(capturePath))
        {
            await ViewportCaptureService.CaptureAsync(
                capturePath,
                _renderSettings.Width,
                _renderSettings.Height,
                _renderSettings.Format,
                _renderSettings.JpegQuality,
                cleanViewport: true);

            StatusText = $"Smoke test completo. Captura guardada en {Path.GetFileName(capturePath)}.";
        }
        else
        {
            StatusText = "Smoke test de navegación completo.";
        }
    }

    private static Scene3D CreateWorkspaceScene()
    {
        return new Scene3D
        {
            Name = "Estudio vacío",
            AmbientIntensity = 0.18f,
            BackgroundColor = new Vector3(0.52f, 0.68f, 0.85f),
            Exposure = 1.02f,
            Gamma = 2.18f,
            Contrast = 1.01f,
            WhiteBalance = 0.02f
        };
    }

    private static Scene3D CreateDefaultScene()
    {
        var scene = DemoScene.Create();
        scene.Name = "Villa Demo";
        return scene;
    }

    private static string GetExtensionForFormat(OutputFormat format) =>
        format switch
        {
            OutputFormat.Jpeg => ".jpg",
            OutputFormat.Bmp => ".bmp",
            OutputFormat.Tiff => ".tiff",
            _ => ".png"
        };

    private static OutputFormat ResolveFormatFromPath(string filePath, OutputFormat fallback)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => OutputFormat.Jpeg,
            ".bmp" => OutputFormat.Bmp,
            ".tif" or ".tiff" => OutputFormat.Tiff,
            ".png" => OutputFormat.Png,
            _ => fallback
        };
    }

    private static string EnsureOutputExtension(string filePath, OutputFormat format)
    {
        if (!string.IsNullOrWhiteSpace(Path.GetExtension(filePath)))
            return filePath;

        return filePath + GetExtensionForFormat(format);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        return string.IsNullOrWhiteSpace(name) ? "render" : name;
    }

    private static int GetCategoryOrder(string? category) =>
        category?.ToLowerInvariant() switch
        {
            "walls" => 0,
            "accent" => 1,
            "ceiling" => 2,
            "stone" => 3,
            "masonry" => 4,
            "concrete" => 5,
            "wood" => 6,
            "ceramic" => 7,
            "metal" => 8,
            "glass" => 9,
            "roof" => 10,
            "landscape" => 11,
            "textile" => 12,
            "synthetic" => 13,
            "concept" => 14,
            _ => 99
        };

    private static string NormalizeSurfaceHint(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace("\\", " ");
    }

    private static bool ContainsAnyHint(string text, params string[] tokens)
    {
        return tokens.Any(text.Contains);
    }

    private static string TrimNodeName(string name)
    {
        const int maxLength = 34;
        return name.Length <= maxLength ? name : name[..maxLength];
    }

    private static string WithSuffix(string filePath, string suffix)
    {
        string directory = Path.GetDirectoryName(filePath) ?? ".";
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);
        return Path.Combine(directory, $"{fileName}{suffix}{extension}");
    }

    private static Window? GetMainWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
