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
{    private void ApplyScene(Scene3D scene, string sourceLabel, TimeSpan? importDuration = null)
    {
        Scene = scene;
        HasModel = Scene.GetAllNodes().Any(node => node.Mesh != null);
        ActiveWorkspaceSection = HasModel ? WorkspaceSection.Camera : WorkspaceSection.Dashboard;

        WorkspaceTitle = Scene.Name;
        WorkspaceSubtitle = HasModel ? "Navega, materializa y exporta un still directamente desde el viewport." : "Importa un modelo para comenzar a montar el proyecto.";
        ViewportTitle = sourceLabel;
        ViewportText = HasModel ? "Editor listo: cambia cámara, materiales y exporta la vista actual." : "Importa un modelo para poblar la escena.";

        PrepareSceneMaterials(Scene, autoApplyMatches: HasModel);
        RefreshScenePresentation();
        UpdateLoadedModelInfo(sourceLabel, importDuration);
        LoadLumionAssetCategories();
        OnPropertyChanged(nameof(CurrentProjectDisplayName));
        OnPropertyChanged(nameof(CurrentProjectStatusText));
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
        foreach (var preset in MaterialCatalog.Presets.OrderBy(preset => GetCategoryOrder(preset.Category)).ThenBy(preset => preset.Name))
            MaterialLibraryPresets.Add(preset);

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
            else if (autoApplyMatches && material.Opacity < 0.99f && MaterialCatalog.TryGetPreset("glass-clear", out var transparentPreset))
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
            .OrderBy(item => GetCategoryOrder(item.material.Category.ToString()))
            .ThenByDescending(item => item.material.UsageCount)
            .ThenBy(item => item.material.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var indexMap = orderedMaterials.Select((item, newIndex) => new { item.oldIndex, newIndex }).ToDictionary(item => item.oldIndex, item => item.newIndex);

        scene.Materials.Clear();
        foreach (var entry in orderedMaterials)
            scene.Materials.Add(entry.material);

        foreach (var node in scene.GetAllNodes())
        {
            if (node.MaterialIndex is int materialIndex && indexMap.TryGetValue(materialIndex, out int updatedIndex))
            {
                node.MaterialIndex = updatedIndex;
            }
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
                Name = string.IsNullOrWhiteSpace(CurrentSourceFilePath) ? WorkspaceTitle : Path.GetFileNameWithoutExtension(CurrentSourceFilePath),
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

        RefreshVisibleSceneNodes(selectedNodeId?.ToString(), selectedLightName, selectedModelScope);
    }

    private string BuildNodeSubtitle(SceneNode node)
    {
        if (node.Mesh == null)
            return "Grupo o transform";

        string materialName = "Sin material";
        string? sourceMaterialName = null;
        if (node.MaterialIndex is int materialIndex && materialIndex >= 0 && materialIndex < Scene.Materials.Count)
        {
            var material = Scene.Materials[materialIndex];
            materialName = material.Name;
            sourceMaterialName = material.SourceName;
        }

        if (!string.IsNullOrWhiteSpace(sourceMaterialName) && !string.Equals(sourceMaterialName, materialName, StringComparison.OrdinalIgnoreCase))
            return $"{node.Mesh.TriangleCount:N0} tris · {materialName} <- {sourceMaterialName}";

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
            if (node.MaterialIndex is not int materialIndex || materialIndex < 0 || materialIndex >= Scene.Materials.Count)
                continue;

            var currentMaterial = Scene.Materials[materialIndex];
            string descriptor = $"{node.Name} {currentMaterial.Name}";

            if (!MaterialCatalog.TryMatchPreset(descriptor, out var preset) || preset == null)
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
        var existingMaterial = node.MaterialIndex is int materialIndex && materialIndex >= 0 && materialIndex < Scene.Materials.Count
            ? Scene.Materials[materialIndex]
            : null;

        if (existingMaterial != null && string.Equals(existingMaterial.PresetKey, preset.Key, StringComparison.OrdinalIgnoreCase))
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
        localizedMaterial.Category = preset.CategoryEnum;
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
        bool hasExplicitSurfaceHint = ContainsAnyHint(hint, "roof", "techo", "ventana", "window", "piedra", "cantera", "barandilla", "railing", "reja", "montante", "puerta", "door", "ceram", "azulejo", "folha", "leaf");

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
        var storedRecord = string.IsNullOrWhiteSpace(CurrentSourceFilePath) ? null : _studioLibraryStore.Find(CurrentSourceFilePath);

        if (storedRecord?.MaterialOverrides.Count > 0)
            materialStateInfo = $" · {storedRecord.MaterialOverrides.Count} superficies guardadas";

        LoadedModelInfo = $"{sourceLabel} · {TriangleCount:N0} tris · {MaterialCount} materiales{timeInfo}{materialStateInfo}";
    }

    private void UpdateCameraProps()
    {
        CameraFov = Scene.Camera.FieldOfView;
        CameraDistance = Scene.Camera.OrbitDistance;
        NavigationSpeed = Scene.Camera.MoveSpeed;
        OnPropertyChanged(nameof(CameraFocusText));
    }
}
