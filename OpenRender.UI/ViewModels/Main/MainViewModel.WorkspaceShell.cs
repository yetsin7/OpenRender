using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenRender.ViewModels;

public partial class MainViewModel
{
    private const string ReferenceImagePath = "avares://OpenRender.UI/Assets/Reference/villa-sunset.png";
    private const string DashboardVillaImagePath = "avares://OpenRender.UI/Assets/Reference/dashboard-villa.png";
    private const string DashboardLoftImagePath = "avares://OpenRender.UI/Assets/Reference/dashboard-loft.png";
    private const string CameraPreviewImagePath = "avares://OpenRender.UI/Assets/Reference/camera-preview.png";
    private const string ProjectAlphaThumbnailPath = "avares://OpenRender.UI/Assets/Reference/project-alpha-thumb.png";
    private static readonly Bitmap ReferenceImageSourceValue = LoadBitmap(ReferenceImagePath);
    private static readonly Bitmap DashboardVillaImageSourceValue = LoadBitmap(DashboardVillaImagePath);
    private static readonly Bitmap DashboardLoftImageSourceValue = LoadBitmap(DashboardLoftImagePath);
    private static readonly Bitmap CameraPreviewImageSourceValue = LoadBitmap(CameraPreviewImagePath);
    private static readonly Bitmap ProjectAlphaThumbnailSourceValue = LoadBitmap(ProjectAlphaThumbnailPath);

    [ObservableProperty] private WorkspaceSection _activeWorkspaceSection = WorkspaceSection.Dashboard;
    [ObservableProperty] private WorkspaceAssetItemViewModel? _selectedWorkspaceAsset;
    [ObservableProperty] private WorkspaceRenderJobViewModel? _selectedRenderQueueJob;
    [ObservableProperty] private string _projectSearchText = "";
    [ObservableProperty] private string _librarySearchText = "";
    [ObservableProperty] private bool _isLibraryGridView = true;
    [ObservableProperty] private string _activeLibraryCategory = "Nature";
    [ObservableProperty] private double _sunAzimuth = 215;
    [ObservableProperty] private double _sunAltitude = 15;
    [ObservableProperty] private bool _isHdriEnabled = true;
    [ObservableProperty] private double _hdriRotation = 45;
    [ObservableProperty] private bool _isGlobalIlluminationEnabled = true;
    [ObservableProperty] private bool _isHardwareRayTracingEnabled = true;
    [ObservableProperty] private double _renderBounceCount = 16;
    [ObservableProperty] private string _renderOutputPath = @"D:\Projects\Alpha\Renders\";
    [ObservableProperty] private string _selectedExportFormatLabel = "MP4 (H.264)";
    [ObservableProperty] private int _selectedFrameRate = 60;

    public ObservableCollection<WorkspaceProjectTemplateViewModel> DashboardReferenceProjects { get; } = new();
    public ObservableCollection<WorkspaceAssetItemViewModel> AssetLibraryItems { get; } = new();
    public ObservableCollection<WorkspaceAssetItemViewModel> VisibleAssetLibraryItems { get; } = new();
    public ObservableCollection<WorkspaceRenderJobViewModel> RenderQueueJobs { get; } = new();
    public WorkspaceUiText Text { get; } = WorkspaceUiText.CreateDefault();

    public string ReferenceImagePathValue => ReferenceImagePath;
    public string DashboardVillaImagePathValue => DashboardVillaImagePath;
    public string DashboardLoftImagePathValue => DashboardLoftImagePath;
    public string CameraPreviewImagePathValue => CameraPreviewImagePath;
    public string ProjectAlphaThumbnailPathValue => ProjectAlphaThumbnailPath;
    public Bitmap ReferenceImageSource => ReferenceImageSourceValue;
    public Bitmap DashboardVillaImageSource => DashboardVillaImageSourceValue;
    public Bitmap DashboardLoftImageSource => DashboardLoftImageSourceValue;
    public Bitmap CameraPreviewImageSource => CameraPreviewImageSourceValue;
    public Bitmap ProjectAlphaThumbnailSource => ProjectAlphaThumbnailSourceValue;
    public bool IsDashboardSectionActive => ActiveWorkspaceSection == WorkspaceSection.Dashboard;
    public bool IsLibrarySectionActive => ActiveWorkspaceSection == WorkspaceSection.Library;
    public bool IsRenderSectionActive => ActiveWorkspaceSection == WorkspaceSection.Render;
    public bool IsCameraSectionActive => ActiveWorkspaceSection == WorkspaceSection.Camera;
    public bool ShowProjectSearchChrome => ActiveWorkspaceSection != WorkspaceSection.Camera;
    public bool ShowMenuChrome => ActiveWorkspaceSection != WorkspaceSection.Camera;
    public bool ShowDashboardReferenceProjects => true;
    public bool ShowDashboardImportedProjects => false;
    public bool ShowPrimaryCameraHud => !IsMaterialsPanelActive && !IsWeatherPanelActive;
    public bool ShowTransformInspector => !IsMaterialsPanelActive && !IsWeatherPanelActive;
    public bool ShowMaterialInspector => IsMaterialsPanelActive;
    public bool ShowWeatherInspector => IsWeatherPanelActive;
    public bool IsLibraryListView => !IsLibraryGridView;
    public bool IsLibraryAllAssetsCategoryActive => IsLibraryCategoryActive("All Assets");
    public bool IsLibraryNatureCategoryActive => IsLibraryCategoryActive("Nature");
    public bool IsLibraryPeopleCategoryActive => IsLibraryCategoryActive("People");
    public bool IsLibraryIndoorCategoryActive => IsLibraryCategoryActive("Indoor");
    public bool IsLibraryOutdoorCategoryActive => IsLibraryCategoryActive("Outdoor");
    public bool IsLibraryMaterialsCategoryActive => IsLibraryCategoryActive("Materials");
    public bool CanPlaceSelectedAssetInScene => SelectedWorkspaceAsset?.IsDownloaded != false;

    public string CurrentProjectDisplayName => HasLoadedSourceFile
        ? Path.GetFileNameWithoutExtension(CurrentSourceFilePath!)
        : Text.ProjectAlpha;

    public string CurrentProjectStatusText => Text.EngineActive;

    public string CameraModeTabLabel => ActiveLumionTool switch
    {
        LumionWorkspaceTool.Materials => "Materials",
        LumionWorkspaceTool.Weather => "Weather",
        LumionWorkspaceTool.Nature => "Landscape",
        _ => "Content"
    };

    public string SelectedNodePositionXText => $"{SelectedSceneNode?.Node?.Transform.Position.X ?? 124.5f:0.00}";
    public string SelectedNodePositionYText => $"{SelectedSceneNode?.Node?.Transform.Position.Y ?? 45.2f:0.00}";
    public string SelectedNodePositionZText => $"{SelectedSceneNode?.Node?.Transform.Position.Z ?? 0f:0.00}";
    public string SelectedNodeRotationXText => $"{SelectedSceneNode?.Node?.Transform.Rotation.X ?? 0f:0.0}";
    public string SelectedNodeRotationYText => $"{SelectedSceneNode?.Node?.Transform.Rotation.Y ?? 90f:0.0}";
    public string SelectedNodeRotationZText => $"{SelectedSceneNode?.Node?.Transform.Rotation.Z ?? 0f:0.0}";
    public string SelectedMaterialDisplayName => SelectedMaterial?.Name ?? "Glass_Clear_01";
    public string SelectedMaterialTextureName => Path.GetFileName(SelectedMaterial?.AlbedoTexturePath ?? "None");
    public string SelectedMaterialNormalTextureName => Path.GetFileName(SelectedMaterial?.NormalTexturePath ?? "glass_nrm.png");
    public string SelectedMaterialRoughnessText => $"{SelectedMaterial?.Roughness ?? 0.15f:0.00}";
    public string SelectedMaterialMetalnessText => $"{SelectedMaterial?.Metallic ?? 0.80f:0.00}";
    public string SelectedMaterialNormalStrengthText => $"{SelectedMaterial?.NormalStrength ?? 1.0f:0.0}";
    public string SelectedWorkspaceAssetTitle => SelectedWorkspaceAsset?.Title ?? "Maple Tree - Autumn 01";
    public string SelectedWorkspaceAssetCategoryText => SelectedWorkspaceAsset?.Category ?? "Nature > Trees > Deciduous";
    public string SelectedWorkspaceAssetNotes => SelectedWorkspaceAsset?.Notes
        ?? "Highly detailed scan-based model suitable for foreground placement.";
    public string SelectedWorkspaceAssetBadge => SelectedWorkspaceAsset?.Badge ?? "High Poly";
    public string SelectedWorkspaceAssetSecondaryTag => SelectedWorkspaceAsset?.TagSecondary ?? "PBR";
    public string SelectedWorkspaceAssetFileSize => SelectedWorkspaceAsset?.FileSize ?? "45 MB";
    public string SelectedWorkspaceAssetLodCount => SelectedWorkspaceAsset?.LodCount ?? "4 Levels";
    public string SelectedWorkspaceAssetAuthor => SelectedWorkspaceAsset?.Author ?? "OpenRender Originals";
    public string SelectedWorkspaceAssetType => SelectedWorkspaceAsset?.IsMaterial == true ? "Material" : "3D Mesh";
    public string ActiveLibraryItemCountText => ActiveLibraryCategory switch
    {
        "All Assets" => "701 Items",
        "Materials" => "86 Items",
        "People" => "68 Items",
        "Indoor" => "154 Items",
        "Outdoor" => "112 Items",
        _ => "245 Items"
    };

    public string SelectedRenderQueueJobTitle => SelectedRenderQueueJob?.Title ?? "Flythrough_Seq_01";
    public string SelectedRenderQueueJobMeta => SelectedRenderQueueJob?.Meta ?? "4K • MP4 • 60fps";
    public string ExportWidthText => _renderSettings.Width.ToString();
    public string ExportHeightText => _renderSettings.Height.ToString();
    public bool IsFrameRate24 => SelectedFrameRate == 24;
    public bool IsFrameRate30 => SelectedFrameRate == 30;
    public bool IsFrameRate60 => SelectedFrameRate == 60;
    public bool IsResolutionHd => _renderSettings.Width == 1920 && _renderSettings.Height == 1080;
    public bool IsResolution4K => _renderSettings.Width == 3840 && _renderSettings.Height == 2160;
    public bool IsResolution8K => _renderSettings.Width == 7680 && _renderSettings.Height == 4320;

    /// <summary>
    /// Aplica una seccion y herramienta inicial desde argumentos de CLI
    /// para facilitar capturas repetibles de verificacion visual.
    /// </summary>
    public void ApplyStartupWorkspace(string? sectionKey, string? toolKey)
    {
        if (!string.IsNullOrWhiteSpace(sectionKey))
            SetWorkspaceSection(sectionKey);

        if (!string.IsNullOrWhiteSpace(toolKey))
            SetLumionTool(toolKey);
    }

    [RelayCommand]
    private void SetWorkspaceSection(string sectionKey)
    {
        if (!Enum.TryParse<WorkspaceSection>(sectionKey, true, out var section))
            return;

        ActiveWorkspaceSection = section;
        if (section == WorkspaceSection.Camera && ActiveSidePanel != LumionSidePanel.Materials)
            SetLumionToolCore(LumionWorkspaceTool.Materials, LumionSidePanel.Materials, updateStatus: false);

        StatusText = section switch
        {
            WorkspaceSection.Dashboard => "Vista de proyectos activa.",
            WorkspaceSection.Library => "Biblioteca de activos activa.",
            WorkspaceSection.Render => "Cola de render activa.",
            _ => "Viewport principal activo."
        };
    }

    [RelayCommand]
    private void SetLibraryCategory(string categoryTitle)
    {
        ActiveLibraryCategory = string.IsNullOrWhiteSpace(categoryTitle) ? "Nature" : categoryTitle;
        StatusText = $"Categoría activa: {ActiveLibraryCategory}.";
    }

    [RelayCommand]
    private void SetLibraryViewMode(string mode)
    {
        IsLibraryGridView = !string.Equals(mode, "List", StringComparison.OrdinalIgnoreCase);
        StatusText = IsLibraryGridView ? "Biblioteca en cuadrícula." : "Biblioteca en lista.";
    }

    [RelayCommand]
    private void SelectWorkspaceAsset(string assetTitle)
    {
        var asset = AssetLibraryItems.FirstOrDefault(item => string.Equals(item.Title, assetTitle, StringComparison.OrdinalIgnoreCase));
        if (asset == null)
            return;

        foreach (var item in AssetLibraryItems)
            item.IsSelected = ReferenceEquals(item, asset);

        ActiveLibraryCategory = asset.LibraryGroup;
        RefreshVisibleAssetLibraryItems();
        SelectedWorkspaceAsset = asset;
        StatusText = $"Activo en biblioteca: {asset.Title}.";
    }

    [RelayCommand]
    private void SelectRenderQueueJob(string jobTitle)
    {
        var job = RenderQueueJobs.FirstOrDefault(item => string.Equals(item.Title, jobTitle, StringComparison.OrdinalIgnoreCase));
        if (job == null)
            return;

        foreach (var item in RenderQueueJobs)
            item.IsSelected = ReferenceEquals(item, job);

        SelectedRenderQueueJob = job;
        StatusText = $"Job activo: {job.Title}.";
    }

    [RelayCommand]
    private void SetFrameRate(int frameRate)
    {
        SelectedFrameRate = frameRate;
        OnPropertyChanged(nameof(IsFrameRate24));
        OnPropertyChanged(nameof(IsFrameRate30));
        OnPropertyChanged(nameof(IsFrameRate60));
    }

    [RelayCommand]
    private void PauseSelectedRenderQueueJob()
    {
        if (SelectedRenderQueueJob == null)
            return;

        SelectedRenderQueueJob.Status = "Paused";
        SelectedRenderQueueJob.StatusAccentHex = "#87929B";
        SelectedRenderQueueJob.Timing = "Paused in queue";
        StatusText = $"Job pausado: {SelectedRenderQueueJob.Title}.";
    }

    [RelayCommand]
    private void RemoveSelectedRenderQueueJob()
    {
        if (SelectedRenderQueueJob == null)
            return;

        var removedTitle = SelectedRenderQueueJob.Title;
        RenderQueueJobs.Remove(SelectedRenderQueueJob);
        SelectedRenderQueueJob = RenderQueueJobs.FirstOrDefault();
        if (SelectedRenderQueueJob != null)
            SelectedRenderQueueJob.IsSelected = true;

        StatusText = $"Job eliminado de la cola: {removedTitle}.";
    }

    [RelayCommand]
    private void PlaceSelectedAssetInScene()
    {
        if (!CanPlaceSelectedAssetInScene)
        {
            StatusText = "Ese activo aún no está descargado.";
            return;
        }

        ActiveWorkspaceSection = WorkspaceSection.Camera;
        SetLumionToolCore(LumionWorkspaceTool.Objects, LumionSidePanel.Objects, updateStatus: false);
        StatusText = $"Activo para colocar: {SelectedWorkspaceAssetTitle}.";
    }

    partial void OnActiveWorkspaceSectionChanged(WorkspaceSection value)
    {
        OnPropertyChanged(nameof(IsDashboardSectionActive));
        OnPropertyChanged(nameof(IsLibrarySectionActive));
        OnPropertyChanged(nameof(IsRenderSectionActive));
        OnPropertyChanged(nameof(IsCameraSectionActive));
        OnPropertyChanged(nameof(ShowProjectSearchChrome));
        OnPropertyChanged(nameof(ShowMenuChrome));
    }

    partial void OnSelectedWorkspaceAssetChanged(WorkspaceAssetItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedWorkspaceAssetTitle));
        OnPropertyChanged(nameof(SelectedWorkspaceAssetCategoryText));
        OnPropertyChanged(nameof(SelectedWorkspaceAssetNotes));
        OnPropertyChanged(nameof(SelectedWorkspaceAssetBadge));
        OnPropertyChanged(nameof(SelectedWorkspaceAssetSecondaryTag));
        OnPropertyChanged(nameof(SelectedWorkspaceAssetFileSize));
        OnPropertyChanged(nameof(SelectedWorkspaceAssetLodCount));
        OnPropertyChanged(nameof(SelectedWorkspaceAssetAuthor));
        OnPropertyChanged(nameof(SelectedWorkspaceAssetType));
        OnPropertyChanged(nameof(CanPlaceSelectedAssetInScene));
    }

    partial void OnSelectedRenderQueueJobChanged(WorkspaceRenderJobViewModel? value)
    {
        if (value == null)
            return;

        SelectedExportFormatLabel = value.IsVideo ? "MP4 (H.264)" : "PNG Sequence";
        OnPropertyChanged(nameof(SelectedRenderQueueJobTitle));
        OnPropertyChanged(nameof(SelectedRenderQueueJobMeta));
    }

    partial void OnActiveLibraryCategoryChanged(string value)
    {
        NotifyLibraryCategoryProperties();
        RefreshVisibleAssetLibraryItems();
    }

    partial void OnIsLibraryGridViewChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLibraryGridView));
        OnPropertyChanged(nameof(IsLibraryListView));
    }

    private bool IsLibraryCategoryActive(string categoryTitle) =>
        string.Equals(ActiveLibraryCategory, categoryTitle, StringComparison.OrdinalIgnoreCase);

    private static Bitmap LoadBitmap(string resourceUri)
    {
        using var stream = AssetLoader.Open(new Uri(resourceUri));
        return new Bitmap(stream);
    }
}
