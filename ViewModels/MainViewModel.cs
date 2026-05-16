using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    private readonly List<SceneNodeViewModel> _allSceneNodes = new();

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

        Scene = CreateDefaultScene();
        ApplyScene(Scene, "Estudio demo");
        StatusText = "Estudio listo. Importa un modelo o usa la villa demo.";

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

    [ObservableProperty] private bool _hasModel;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private double _progressValue;

    [ObservableProperty] private float _cameraFov;
    [ObservableProperty] private float _cameraDistance;
    [ObservableProperty] private float _navigationSpeed;
    [ObservableProperty] private float _sunIntensity = 1.8f;
    [ObservableProperty] private float _ambientIntensity = 0.2f;
    [ObservableProperty] private string _sunStatusText = "Sol activo";

    [ObservableProperty] private int _objectCount;
    [ObservableProperty] private int _triangleCount;
    [ObservableProperty] private int _materialCount;

    [ObservableProperty] private bool _autoFixOrientation = true;
    [ObservableProperty] private bool _autoRecenter = true;

    public ObservableCollection<SceneNodeViewModel> SceneNodes { get; } = new();
    public ObservableCollection<PbrMaterial> SceneMaterials { get; } = new();
    public ObservableCollection<MaterialPresetDefinition> MaterialLibraryPresets { get; } = new();
    public ObservableCollection<string> RecentFiles { get; } = new();

    public string RenderResolution => $"{_renderSettings.Width} x {_renderSettings.Height}";
    public string RenderQualityText => _renderSettings.Quality.ToString();
    public string OutputFormatText => _renderSettings.Format.ToString().ToUpperInvariant();
    public int SampleCount => _renderSettings.SampleCount;
    public int SceneNodeCount => SceneNodes.Count;
    public bool HasSelectedMaterial => SelectedMaterial != null;
    public bool HasSceneSelection => SelectedSceneNode != null;
    public bool HasRecentFiles => RecentFiles.Count > 0;
    public bool ShowEmptyState => !HasModel;
    public string WorkspaceModeText => HasModel ? "Proyecto cargado" : "Estudio base";
    public string CurrentSceneLabel => Scene.Name;
    public string SelectedNodeTitle => SelectedSceneNode?.Name ?? "Selecciona un objeto";
    public string SelectedNodeDetails => SelectedSceneNode?.Subtitle ?? "Elige una malla o una luz desde la escena.";
    public bool HasSelectedMeshNode => SelectedSceneNode?.Node?.Mesh != null;
    public string CameraFocusText => $"Objetivo {Scene.Camera.Target.X:F1}, {Scene.Camera.Target.Y:F1}, {Scene.Camera.Target.Z:F1}";
    public string SupportedFormatsText => "OBJ listo hoy. FBX, glTF/GLB e IFC quedan como siguiente fase del pipeline.";
    public string SelectedMaterialCategory => SelectedMaterial?.Category ?? "Sin categoría";
    public string SelectedMaterialUsageText => SelectedMaterial != null ? $"{SelectedMaterial.UsageCount} superficies" : "Sin material";
    public string MaterialLibraryInfoText => $"{MaterialLibraryPresets.Count} presets arquitectónicos";

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
            StatusText = "Ese archivo reciente ya no existe.";
            return;
        }

        await LoadFileAsync(filePath);
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

        try
        {
            StatusText = $"Importando {Path.GetFileName(filePath)}...";
            IsLoading = true;
            ProgressValue = 0;
            await Task.Delay(50);

            var manager = new OpenRender.Rendering.Import.ImportManager();
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
            var importResult = await manager.ImportAsync(filePath, options, progress);
            sw.Stop();

            if (!importResult.Success || importResult.Scene == null)
            {
                IsLoading = false;
                StatusText = importResult.ErrorMessage ?? "No se pudo importar el archivo.";
                return;
            }

            ApplyScene(importResult.Scene, Path.GetFileName(filePath), sw.Elapsed);
            AddRecentFile(filePath);

            ProgressValue = 100;
            StatusText = $"Modelo importado. Preparando viewport para {Path.GetFileName(filePath)}...";
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
        ApplyScene(CreateDefaultScene(), "Estudio demo");
        StatusText = "Estudio demo recargado.";
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
        UpdateMaterialBindings();
        LoadSceneNodes();
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

        if (TryGetSceneBounds(out var min, out var max))
            Scene.Camera.FramePhotoShot(min, max);
        else
            Scene.Camera.SetView("3D");

        UpdateAllProperties();
        StatusText = "Encuadre fotográfico listo. Ahora puedes exportar el still limpio.";
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
                break;
            case "overcast":
                Scene.BackgroundColor = new Vector3(0.45f, 0.52f, 0.62f);
                Scene.AmbientIntensity = 0.42f;
                sun.Intensity = 0.9f;
                sun.Color = new Vector3(0.92f, 0.95f, 1.0f);
                sun.Direction = Vector3.Normalize(new Vector3(-0.20f, -1f, -0.10f));
                break;
            case "sunset":
                Scene.BackgroundColor = new Vector3(0.76f, 0.46f, 0.30f);
                Scene.AmbientIntensity = 0.22f;
                sun.Intensity = 1.4f;
                sun.Color = new Vector3(1.0f, 0.78f, 0.58f);
                sun.Direction = Vector3.Normalize(new Vector3(0.55f, -0.55f, -0.20f));
                break;
            default:
                Scene.BackgroundColor = new Vector3(0.09f, 0.12f, 0.18f);
                Scene.AmbientIntensity = 0.16f;
                sun.Intensity = 1.1f;
                sun.Color = new Vector3(0.94f, 0.96f, 1.0f);
                sun.Direction = Vector3.Normalize(new Vector3(-0.15f, -1f, 0.10f));
                break;
        }

        UpdateAllProperties();
        if (!suppressStatus)
            StatusText = $"Entorno aplicado: {presetName}.";
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
            StatusText = $"Inspector enfocado en {value.Name}.";
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
        LoadSceneMaterials();
        LoadSceneNodes();
        UpdateAllProperties();
        UpdateLoadedModelInfo(sourceLabel, importDuration);
    }

    private void LoadSceneMaterials()
    {
        SceneMaterials.Clear();
        foreach (var material in Scene.Materials)
            SceneMaterials.Add(material);

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
            material.UsageCount = usageByMaterial.TryGetValue(index, out int usageCount) ? usageCount : 0;

            if (autoApplyMatches && MaterialCatalog.TryMatchPreset(material.Name, out var matchedPreset))
            {
                MaterialCatalog.ApplyPreset(material, matchedPreset);
            }
            else
            {
                material.Category = MaterialCatalog.GuessCategory(material.Name);
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

        _allSceneNodes.Clear();

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

        _allSceneNodes.Add(new SceneNodeViewModel
        {
            Name = "Camera",
            Icon = "CAM",
            Subtitle = $"FOV {Scene.Camera.FieldOfView:F0}° · Dist {Scene.Camera.OrbitDistance:F1}",
            IsVisible = true
        });

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

        RefreshVisibleSceneNodes(selectedNodeId, selectedLightName);
    }

    private string BuildNodeSubtitle(SceneNode node)
    {
        if (node.Mesh == null)
            return "Grupo o transform";

        string materialName = "Sin material";
        if (node.MaterialIndex is int materialIndex &&
            materialIndex >= 0 &&
            materialIndex < Scene.Materials.Count)
        {
            materialName = Scene.Materials[materialIndex].Name;
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
        LoadSceneMaterials();
        LoadSceneNodes();
        UpdateAllProperties();
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
        LoadSceneMaterials();
        LoadSceneNodes();
        UpdateAllProperties();
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
            if (selectMaterial)
                SelectedMaterial = existingMaterial;

            return false;
        }

        if (existingMaterial != null && existingMaterial.UsageCount <= 1)
        {
            MaterialCatalog.ApplyPreset(existingMaterial, preset);
            existingMaterial.Name = $"{preset.Name} · {TrimNodeName(node.Name)}";

            if (selectMaterial)
                SelectedMaterial = existingMaterial;

            return true;
        }

        var localizedMaterial = preset.Material.Clone($"{preset.Name} · {TrimNodeName(node.Name)}");
        localizedMaterial.Category = preset.Category;
        localizedMaterial.PresetKey = preset.Key;
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
        LoadedModelInfo = $"{sourceLabel} · {TriangleCount:N0} tris · {MaterialCount} materiales{timeInfo}";
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

    private void AddRecentFile(string filePath)
    {
        if (RecentFiles.Contains(filePath))
            RecentFiles.Remove(filePath);

        RecentFiles.Insert(0, filePath);

        while (RecentFiles.Count > 8)
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
    }

    private void UpdateMaterialBindings()
    {
        OnPropertyChanged(nameof(HasSelectedMaterial));
        OnPropertyChanged(nameof(MaterialAlbedoR));
        OnPropertyChanged(nameof(MaterialAlbedoG));
        OnPropertyChanged(nameof(MaterialAlbedoB));
        OnPropertyChanged(nameof(SelectedMaterialCategory));
        OnPropertyChanged(nameof(SelectedMaterialUsageText));
        OnPropertyChanged(nameof(HasSelectedMeshNode));
    }

    private void OnRecentFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasRecentFiles));
    }

    private void RefreshVisibleSceneNodes(string? preferredNodeId = null, string? preferredLightName = null)
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
