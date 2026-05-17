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
{    private void RestoreStoredMaterialOverrides()
    {
        if (string.IsNullOrWhiteSpace(CurrentSourceFilePath))
            return;

        var record = _studioLibraryStore.Find(CurrentSourceFilePath);
        if (record?.MaterialOverrides == null || record.MaterialOverrides.Count == 0)
            return;

        var surfaceOverrides = record.MaterialOverrides.Where(item => !string.IsNullOrWhiteSpace(item.SurfaceKey)).GroupBy(item => item.SurfaceKey, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var sourceMaterialOverrides = record.MaterialOverrides.Where(item => !string.IsNullOrWhiteSpace(item.SourceMaterialName)).GroupBy(item => item.SourceMaterialName, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        int restoredCount = 0;
        var createdMaterials = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        _isRestoringStoredMaterials = true;
        try
        {
            foreach (var node in Scene.GetAllNodes().Where(node => node.Mesh != null))
            {
                if (node.MaterialIndex is not int materialIndex || materialIndex < 0 || materialIndex >= Scene.Materials.Count)
                    continue;

                var currentMaterial = Scene.Materials[materialIndex];
                string sourceMaterialName = currentMaterial.SourceName ?? currentMaterial.Name;

                if (!surfaceOverrides.TryGetValue(node.Name, out var overrideRecord) && !sourceMaterialOverrides.TryGetValue(sourceMaterialName, out overrideRecord))
                    continue;

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

    private bool ApplyStoredOverrideToNode(SceneNode node, StoredMaterialOverride overrideRecord, Dictionary<string, int> createdMaterials)
    {
        if (node.MaterialIndex is not int materialIndex || materialIndex < 0 || materialIndex >= Scene.Materials.Count)
            return false;

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
        material.Name = string.IsNullOrWhiteSpace(overrideRecord.DisplayMaterialName) ? material.Name : overrideRecord.DisplayMaterialName;
        material.SourceName = string.IsNullOrWhiteSpace(overrideRecord.SourceMaterialName) ? material.SourceName ?? material.Name : overrideRecord.SourceMaterialName;
        if (Enum.TryParse<MaterialCategory>(overrideRecord.Category, out var category))
            material.Category = category;
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
               string.Equals(material.Category.ToString(), overrideRecord.Category ?? "", StringComparison.OrdinalIgnoreCase) &&
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
                DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? Path.GetFileNameWithoutExtension(item.SourcePath) : item.DisplayName,
                Summary = BuildImportedHistorySummary(item),
                Meta = BuildImportedHistoryMeta(item, existsOnDisk),
                ExistsOnDisk = existsOnDisk
            });

            if (existsOnDisk && RecentFiles.Count < 8)
                RecentFiles.Add(item.SourcePath);
        }

        OnPropertyChanged(nameof(HasImportedHistory));
        OnPropertyChanged(nameof(ShowDashboardReferenceProjects));
        OnPropertyChanged(nameof(ImportedLibraryInfoText));
        LoadLumionAssetCategories();
        OnPropertyChanged(nameof(CurrentProjectDisplayName));
    }

    private static string BuildImportedHistorySummary(ImportedModelRecord item) => $"{item.ObjectCount} objs · {item.TriangleCount:N0} tris · {item.MaterialCount} mats · {FormatHistoryMoment(item.LastImportedUtc)}";

    private static string BuildImportedHistoryMeta(ImportedModelRecord item, bool existsOnDisk)
    {
        string sizeText = item.FileSizeBytes > 0 ? $"{item.FileSizeBytes / (1024f * 1024f):F1} MB" : "tamaño desconocido";
        string diskState = existsOnDisk ? sizeText : "archivo no encontrado";
        return $"{diskState} · {item.SourcePath}";
    }

    private static string FormatHistoryMoment(DateTime utcValue) => utcValue == default ? "sin fecha" : utcValue.ToLocalTime().ToString("dd MMM yyyy HH:mm");

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

}
