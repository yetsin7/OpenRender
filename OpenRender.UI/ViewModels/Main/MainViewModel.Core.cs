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
{    private static MainViewModel? _instance;
    private readonly RenderSettings _renderSettings;
    private readonly LocalTextureCatalog _localTextureCatalog;
    private readonly StudioLibraryStore _studioLibraryStore;
    private readonly List<SceneNodeViewModel> _allSceneNodes = new();
    private readonly List<PbrMaterial> _trackedSceneMaterials = new();
    private CancellationTokenSource? _materialStateSaveCts;
    private bool _isRestoringStoredMaterials;

    public static string? GlErrorMessage { get; set; }

    public static void ReportGlError(string message)
    {
        if (_instance != null)
            Dispatcher.UIThread.Post(() => _instance.StatusText = message);
        else
            GlErrorMessage = message;
    }

    public static void ReportViewportReady(string? detail = null)
    {
        if (_instance == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            _instance.IsLoading = false;
            _instance.IsViewportFallbackMode = false;
            _instance.ViewportBackendLabel = "Experimental Vulkan";
            _instance.ViewportStatusDetail = detail ?? "El viewport nativo respondió y quedó encendido.";
            _instance.UpdateViewportStateProperties();
            if (_instance.ProgressValue >= 100)
                _instance.StatusText = "Viewport listo para navegar.";
        });
    }

    public static void ReportViewportSafeMode(string message)
    {
        if (_instance != null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _instance.IsLoading = false;
                _instance.IsViewportFallbackMode = true;
                _instance.ViewportBackendLabel = "Safe Preview";
                _instance.ViewportStatusDetail = message;
                _instance.UpdateViewportStateProperties();
                _instance.StatusText = message;
            });
            return;
        }

        GlErrorMessage = message;
    }

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
        InitializeLumionUiState();
        InitializeWorkspaceShell();
        ConfigurePerformanceProfile(PerformanceProfile.LaptopSaver, updateStatus: false);

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

    // Lumion-style UI state
    [ObservableProperty] private LumionWorkspaceTool _activeLumionTool = LumionWorkspaceTool.Import;
    [ObservableProperty] private LumionSidePanel _activeSidePanel = LumionSidePanel.Import;
    [ObservableProperty] private string _activeLumionToolTitle = "Importar modelo";
    [ObservableProperty] private string _activeLumionToolSubtitle = "Carga modelos y prepara la escena.";
    [ObservableProperty] private string _lumionNavigationHint = "Mouse: orbitar / pan / zoom · WASD: navegar · Shift: rápido";
    [ObservableProperty] private string _lumionModeBadge = "IMPORT";
    [ObservableProperty] private bool _isLeftToolRailExpanded = true;
    [ObservableProperty] private bool _isRightInspectorExpanded = true;
    [ObservableProperty] private bool _isBottomDockExpanded = true;
    [ObservableProperty] private bool _isLumionImmersiveMode;
    [ObservableProperty] private bool _showLumionHelpOverlay;
    [ObservableProperty] private bool _showLumionAssetBrowser = true;
    [ObservableProperty] private bool _showLumionScenePanel = true;
    [ObservableProperty] private string _selectedAssetCategoryTitle = "Modelos importados";
    [ObservableProperty] private string _selectedEnvironmentPreset = "Day";
    [ObservableProperty] private PerformanceProfile _activePerformanceProfile = PerformanceProfile.LaptopSaver;
    [ObservableProperty] private bool _isViewportFallbackMode = !IsExperimentalVulkanRequested();
    [ObservableProperty] private string _viewportBackendLabel = IsExperimentalVulkanRequested() ? "Vulkan starting" : "Safe Preview";
    [ObservableProperty] private string _viewportStatusDetail = IsExperimentalVulkanRequested()
        ? "Vulkan experimental solicitado. Si falla, la app cae automaticamente al preview seguro."
        : "Modo seguro activo mientras estabilizamos el backend nativo.";

    public ObservableCollection<SceneNodeViewModel> SceneNodes { get; } = new();
    public ObservableCollection<PbrMaterial> SceneMaterials { get; } = new();
    public ObservableCollection<MaterialPresetDefinition> MaterialLibraryPresets { get; } = new();
    public ObservableCollection<ImportedModelHistoryItemViewModel> ImportedHistory { get; } = new();
    public ObservableCollection<string> RecentFiles { get; } = new();

    public ObservableCollection<LumionToolItemViewModel> LumionTools { get; } = new();
    public ObservableCollection<LumionAssetCategoryViewModel> LumionAssetCategories { get; } = new();
    public ObservableCollection<LumionEnvironmentPresetViewModel> LumionEnvironmentPresets { get; } = new();

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
    public bool ShowViewportOverlay => ShowEmptyState && !IsViewportFallbackMode && !IsLoading;
    public bool ShowSafeViewportPreview => IsViewportFallbackMode;
    public bool ShowNativeViewport => !IsViewportFallbackMode;
    public bool IsObjectSelectionMode => !string.Equals(InteractionMode, "Material", StringComparison.OrdinalIgnoreCase);
    public bool IsMaterialPaintMode => !IsObjectSelectionMode;
    public string WorkspaceModeText => HasModel ? "Proyecto cargado" : "Estudio base";
    public string CurrentSceneLabel => Scene.Name;
    public string SelectedNodeTitle => SelectedSceneNode?.Name ?? "Selecciona un objeto";
    public string SelectedNodeDetails => SelectedSceneNode?.Subtitle ?? "Elige una malla o una luz desde la escena.";
    public bool HasSelectedMeshNode => SelectedSceneNode?.Node?.Mesh != null;
    public string CameraFocusText => $"Objetivo {Scene.Camera.Target.X:F1}, {Scene.Camera.Target.Y:F1}, {Scene.Camera.Target.Z:F1}";
    public string SupportedFormatsText => "OBJ listo hoy. FBX, glTF/GLB e IFC quedan como siguiente fase del pipeline.";
    public string SelectedMaterialCategory => SelectedMaterial != null ? SelectedMaterial.Category.ToString() : "Sin categoría";
    public string SelectedMaterialSourceText => SelectedMaterial != null ? (SelectedMaterial.SourceName ?? SelectedMaterial.Name) : "Sin origen importado";
    public string SelectedMaterialUsageText => SelectedMaterial != null ? $"{SelectedMaterial.UsageCount} superficies" : "Sin material";
    public string MaterialLibraryInfoText => $"{MaterialLibraryPresets.Count} presets arquitectónicos";
    public string ImportedLibraryInfoText => $"{ImportedHistory.Count} modelos guardados en la biblioteca local";
    public string PerformanceProfileBadge => ActivePerformanceProfile switch
    {
        PerformanceProfile.LaptopSaver => "LAPTOP",
        PerformanceProfile.Balanced => "BAL",
        PerformanceProfile.Presentation => "PRESENT",
        _ => "PERF"
    };
    public string PerformanceProfileDescription => ActivePerformanceProfile switch
    {
        PerformanceProfile.LaptopSaver => "Prioriza estabilidad en laptop modesta: preview ligero, menor carga de VRAM y exportacion controlada.",
        PerformanceProfile.Balanced => "Equilibrio para trabajar escena, materiales y previews sin castigar demasiado GPU o RAM.",
        PerformanceProfile.Presentation => "Empuja calidad de preview y salida local. Usa mas muestras, mas resolucion y mayor carga de GPU.",
        _ => "Perfil de rendimiento no definido."
    };
    public string ResourceBudgetText => $"{PerformanceProfileBadge} · {RenderQualityText} · {SampleCount}x · {RenderResolution} · {ViewportBackendLabel}";
    public string ViewportModeBadge => IsViewportFallbackMode ? "SAFE UI" : "VULKAN";
    public string ViewportStatusText => ViewportStatusDetail;
    public string ViewportOverlayTitle => IsViewportFallbackMode ? "Modo seguro listo" : "Viewport listo";
    public string ViewportOverlayBody => IsViewportFallbackMode
        ? "La app esta estable y usable mientras el pipeline Vulkan se sigue afinando. Puedes importar, organizar escena y preparar materiales sin colgar la laptop."
        : "Importa un modelo para comenzar a montar la escena.";
    public Thickness ViewportHostMargin
    {
        get
        {
            double left = 18;
            if (IsLeftToolRailExpanded)
                left += 64 + 20;
            if (ShowLumionAssetBrowser)
                left += 318 + 18;

            double right = HasActiveLumionPanel ? 338 + 18 : 18;
            double bottom = IsBottomDockExpanded ? 124 : 18;
            return new Thickness(left, 18, right, bottom);
        }
    }
    public Thickness BottomDockMargin => new(ViewportHostMargin.Left, 0, ViewportHostMargin.Right, 18);

    public bool IsImportPanelActive => ActiveSidePanel == LumionSidePanel.Import;
    public bool IsBuildPanelActive => ActiveSidePanel == LumionSidePanel.Build;
    public bool IsMaterialsPanelActive => ActiveSidePanel == LumionSidePanel.Materials;
    public bool IsObjectsPanelActive => ActiveSidePanel == LumionSidePanel.Objects;
    public bool IsNaturePanelActive => ActiveSidePanel == LumionSidePanel.Nature;
    public bool IsWeatherPanelActive => ActiveSidePanel == LumionSidePanel.Weather;
    public bool IsCameraPanelActive => ActiveSidePanel == LumionSidePanel.Camera;
    public bool IsRenderPanelActive => ActiveSidePanel == LumionSidePanel.Render;
    public bool IsLibraryPanelActive => ActiveSidePanel == LumionSidePanel.Library;
    public bool HasActiveLumionPanel => ActiveSidePanel != LumionSidePanel.None;

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
}
