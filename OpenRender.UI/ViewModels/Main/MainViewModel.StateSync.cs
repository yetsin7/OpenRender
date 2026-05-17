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
{
    private void ConfigurePerformanceProfile(PerformanceProfile profile, bool updateStatus)
    {
        ActivePerformanceProfile = profile;

        switch (profile)
        {
            case PerformanceProfile.LaptopSaver:
                _renderSettings.Width = 1280;
                _renderSettings.Height = 720;
                _renderSettings.Quality = RenderQuality.Draft;
                _renderSettings.SampleCount = 1;
                break;
            case PerformanceProfile.Presentation:
                _renderSettings.Width = 2560;
                _renderSettings.Height = 1440;
                _renderSettings.Quality = RenderQuality.High;
                _renderSettings.SampleCount = 4;
                break;
            default:
                _renderSettings.Width = 1920;
                _renderSettings.Height = 1080;
                _renderSettings.Quality = RenderQuality.Medium;
                _renderSettings.SampleCount = 2;
                break;
        }

        UpdateAllProperties();

        if (updateStatus)
            StatusText = $"Perfil activo: {PerformanceProfileBadge}. {PerformanceProfileDescription}";
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
        RenderInfoText = $"{RenderResolution} | {OutputFormatText} | {_renderSettings.Quality} | {PerformanceProfileBadge}";

        OnPropertyChanged(nameof(RenderResolution));
        OnPropertyChanged(nameof(RenderQualityText));
        OnPropertyChanged(nameof(OutputFormatText));
        OnPropertyChanged(nameof(SampleCount));
        OnPropertyChanged(nameof(CameraFocusText));
        OnPropertyChanged(nameof(ExportWidthText));
        OnPropertyChanged(nameof(ExportHeightText));
        OnPropertyChanged(nameof(IsResolutionHd));
        OnPropertyChanged(nameof(IsResolution4K));
        OnPropertyChanged(nameof(IsResolution8K));
        UpdateViewportStateProperties();
        UpdateMaterialBindings();
        LoadLumionAssetCategories();
    }

    private void UpdateViewportStateProperties()
    {
        OnPropertyChanged(nameof(PerformanceProfileBadge));
        OnPropertyChanged(nameof(PerformanceProfileDescription));
        OnPropertyChanged(nameof(ResourceBudgetText));
        OnPropertyChanged(nameof(ViewportModeBadge));
        OnPropertyChanged(nameof(ViewportStatusText));
        OnPropertyChanged(nameof(CurrentProjectStatusText));
    }

    private void UpdateLayoutMarginsProperties()
    {
        OnPropertyChanged(nameof(ViewportHostMargin));
        OnPropertyChanged(nameof(BottomDockMargin));
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
}
