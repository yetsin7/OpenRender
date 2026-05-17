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
{    partial void OnHasModelChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowViewportOverlay));
        OnPropertyChanged(nameof(ViewportOverlayTitle));
        OnPropertyChanged(nameof(ViewportOverlayBody));
        OnPropertyChanged(nameof(WorkspaceModeText));
        LoadLumionAssetCategories();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowViewportOverlay));
    }

    partial void OnSceneChanged(Scene3D value)
    {
        OnPropertyChanged(nameof(CurrentSceneLabel));
        OnPropertyChanged(nameof(CameraFocusText));
    }

    partial void OnSelectedSceneNodeChanged(SceneNodeViewModel? value)
    {
        if (value?.MaterialIndex is int materialIndex && materialIndex >= 0 && materialIndex < Scene.Materials.Count)
            SelectedMaterial = Scene.Materials[materialIndex];
        else
            SelectedMaterial = null;

        OnPropertyChanged(nameof(SelectedNodeTitle));
        OnPropertyChanged(nameof(SelectedNodeDetails));
        OnPropertyChanged(nameof(HasSceneSelection));
        OnPropertyChanged(nameof(HasSelectedMeshNode));
        OnPropertyChanged(nameof(SelectedNodePositionXText));
        OnPropertyChanged(nameof(SelectedNodePositionYText));
        OnPropertyChanged(nameof(SelectedNodePositionZText));
        OnPropertyChanged(nameof(SelectedNodeRotationXText));
        OnPropertyChanged(nameof(SelectedNodeRotationYText));
        OnPropertyChanged(nameof(SelectedNodeRotationZText));

        if (value != null)
            StatusText = value.IsModelScope ? $"Modelo seleccionado: {value.Name}." : $"Inspector enfocado en {value.Name}.";
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
        OnPropertyChanged(nameof(SelectedMaterialDisplayName));
        OnPropertyChanged(nameof(SelectedMaterialTextureName));
        OnPropertyChanged(nameof(SelectedMaterialNormalTextureName));
    }

    partial void OnSceneFilterTextChanged(string value) => RefreshVisibleSceneNodes();

    partial void OnSunIntensityChanged(float value)
    {
        var sun = Scene.Lights.FirstOrDefault(l => l.Type == LightType.Directional);
        if (sun != null)
            sun.Intensity = value;
    }

    partial void OnAmbientIntensityChanged(float value) => Scene.AmbientIntensity = value;
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
    partial void OnPhotoContrastChanged(float value) => Scene.Contrast = value;
    partial void OnPhotoWhiteBalanceChanged(float value) => Scene.WhiteBalance = value;
    partial void OnCameraFovChanged(float value) => Scene.Camera.FieldOfView = value;
    partial void OnCameraDistanceChanged(float value)
    {
        Scene.Camera.OrbitDistance = value;
        NavigationSpeed = Scene.Camera.MoveSpeed;
        OnPropertyChanged(nameof(CameraFocusText));
    }
    partial void OnNavigationSpeedChanged(float value) => Scene.Camera.MoveSpeed = value;
    partial void OnCurrentSourceFilePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasLoadedSourceFile));
        OnPropertyChanged(nameof(CurrentProjectDisplayName));
    }
    partial void OnActiveSidePanelChanged(LumionSidePanel value)
    {
        NotifyLumionPanelProperties();
        OnPropertyChanged(nameof(ShowPrimaryCameraHud));
        OnPropertyChanged(nameof(ShowTransformInspector));
        OnPropertyChanged(nameof(ShowMaterialInspector));
        OnPropertyChanged(nameof(ShowWeatherInspector));
    }
    partial void OnActiveLumionToolChanged(LumionWorkspaceTool value)
    {
        foreach (var item in LumionTools)
            item.IsSelected = string.Equals(item.ToolKey, value.ToString(), StringComparison.OrdinalIgnoreCase);
    }
    partial void OnShowLumionAssetBrowserChanged(bool value) => UpdateLayoutMarginsProperties();
    partial void OnIsLeftToolRailExpandedChanged(bool value) => UpdateLayoutMarginsProperties();
    partial void OnIsBottomDockExpandedChanged(bool value) => UpdateLayoutMarginsProperties();
    partial void OnIsViewportFallbackModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowViewportOverlay));
        OnPropertyChanged(nameof(ShowSafeViewportPreview));
        OnPropertyChanged(nameof(ShowNativeViewport));
        OnPropertyChanged(nameof(ViewportOverlayTitle));
        OnPropertyChanged(nameof(ViewportOverlayBody));
        OnPropertyChanged(nameof(CurrentProjectStatusText));
    }
}
