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
{    private void UpdateMaterialBindings()
    {
        OnPropertyChanged(nameof(HasSelectedMaterial));
        OnPropertyChanged(nameof(MaterialAlbedoR));
        OnPropertyChanged(nameof(MaterialAlbedoG));
        OnPropertyChanged(nameof(MaterialAlbedoB));
        OnPropertyChanged(nameof(SelectedMaterialCategory));
        OnPropertyChanged(nameof(SelectedMaterialSourceText));
        OnPropertyChanged(nameof(SelectedMaterialUsageText));
        OnPropertyChanged(nameof(SelectedMaterialRoughnessText));
        OnPropertyChanged(nameof(SelectedMaterialMetalnessText));
        OnPropertyChanged(nameof(SelectedMaterialNormalStrengthText));
        OnPropertyChanged(nameof(HasSelectedMeshNode));
    }

    private void OnRecentFilesChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(HasRecentFiles));

    public void SelectViewportHit(string? nodeId)
    {
        if (!HasModel)
            return;

        if (IsObjectSelectionMode || string.IsNullOrWhiteSpace(nodeId))
        {
            SelectWholeModelFromViewport();
            return;
        }

        var sceneItem = _allSceneNodes.FirstOrDefault(item => string.Equals(item.Node?.Id.ToString(), nodeId, StringComparison.Ordinal));
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

    private void RefreshVisibleSceneNodes(string? preferredNodeId = null, string? preferredLightName = null, bool preferModelScope = false)
    {
        string filter = SceneFilterText.Trim();
        IEnumerable<SceneNodeViewModel> source = _allSceneNodes;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            source = source.Where(item => item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || item.Subtitle.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        var visibleNodes = source.ToList();
        SceneNodes.Clear();
        foreach (var item in visibleNodes)
            SceneNodes.Add(item);

        OnPropertyChanged(nameof(SceneNodeCount));

        SelectedSceneNode =
            (preferModelScope || IsObjectSelectionMode ? SceneNodes.FirstOrDefault(item => item.IsModelScope) : null) ??
            SceneNodes.FirstOrDefault(item => item.Node?.Id.ToString() == preferredNodeId) ??
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
        await Task.Delay(150);

        if (!TryGetSceneBounds(out var min, out var max))
        {
            StatusText = "Smoke test cancelado: no pude calcular bounds del modelo.";
            return;
        }

        var frontCamera = Scene.Camera.Clone();
        frontCamera.SetViewAndFrame("Front", min, max);

        var topCamera = Scene.Camera.Clone();
        topCamera.SetViewAndFrame("Top", min, max);

        var heroCamera = Scene.Camera.Clone();
        heroCamera.FramePhotoShot(min, max);

        if (!string.IsNullOrWhiteSpace(capturePath))
        {
            await SoftwareRenderExporter.ExportAsync(Scene, _renderSettings, WithSuffix(capturePath, "_front"), _renderSettings.Format, frontCamera);
            await SoftwareRenderExporter.ExportAsync(Scene, _renderSettings, WithSuffix(capturePath, "_top"), _renderSettings.Format, topCamera);
            await SoftwareRenderExporter.ExportAsync(Scene, _renderSettings, capturePath, _renderSettings.Format, heroCamera);
            StatusText = $"Smoke test completo. Captura guardada en {Path.GetFileName(capturePath)}.";
            return;
        }

        StatusText = "Smoke test de navegación completo.";
    }

    private void NotifyLumionPanelProperties()
    {
        OnPropertyChanged(nameof(IsImportPanelActive));
        OnPropertyChanged(nameof(IsBuildPanelActive));
        OnPropertyChanged(nameof(IsMaterialsPanelActive));
        OnPropertyChanged(nameof(IsObjectsPanelActive));
        OnPropertyChanged(nameof(IsNaturePanelActive));
        OnPropertyChanged(nameof(IsWeatherPanelActive));
        OnPropertyChanged(nameof(IsCameraPanelActive));
        OnPropertyChanged(nameof(IsRenderPanelActive));
        OnPropertyChanged(nameof(IsLibraryPanelActive));
        OnPropertyChanged(nameof(HasActiveLumionPanel));
        UpdateLayoutMarginsProperties();
    }
}
