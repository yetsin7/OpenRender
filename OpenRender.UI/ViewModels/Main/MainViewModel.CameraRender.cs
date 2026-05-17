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
    private void Exit()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    [RelayCommand]
    private void ResetCamera()
    {
        if (!TryGetSceneBounds(out var min, out var max))
            Scene.Camera.Reset();
        else
            Scene.Camera.FrameBoundingBox(min, max);

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
            Scene.Camera.SetViewAndFrame(viewType, min, max);
        else
            Scene.Camera.SetView(viewType);

        UpdateCameraProps();
        SetLumionToolCore(LumionWorkspaceTool.Camera, LumionSidePanel.Camera, updateStatus: false);
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
    private void ApplyPerformanceProfile(string profileValue)
    {
        if (!Enum.TryParse<PerformanceProfile>(profileValue, true, out var profile))
            return;

        ConfigurePerformanceProfile(profile, updateStatus: true);
    }

    [RelayCommand]
    private void ApplyMaterialPreset(string presetName)
    {
        if (!MaterialCatalog.TryGetPreset(presetName, out var preset) || preset == null)
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
        InteractionMode = string.Equals(mode, "Material", StringComparison.OrdinalIgnoreCase) ? "Material" : "Object";

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
            case "1080P":
                _renderSettings.Width = 1920;
                _renderSettings.Height = 1080;
                break;
            case "QHD":
                _renderSettings.Width = 2560;
                _renderSettings.Height = 1440;
                break;
            case "4K":
                _renderSettings.Width = 3840;
                _renderSettings.Height = 2160;
                break;
            case "8K":
                _renderSettings.Width = 7680;
                _renderSettings.Height = 4320;
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
}
