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
    private void About()
    {
        StatusText = "Open Render Studio: viewport en tiempo real, materiales PBR y exportación de imagen.";
    }

    [RelayCommand]
    private void Shortcuts()
    {
        StatusText = "Atajos: Ctrl+O importar, Ctrl+N nuevo, Ctrl+S exportar, F5 preview, F encuadrar, 1/3/7 vistas.";
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

    [RelayCommand]
    private void SetLumionTool(string toolKey)
    {
        if (!Enum.TryParse<LumionWorkspaceTool>(toolKey, true, out var tool))
        {
            StatusText = $"No reconozco la herramienta: {toolKey}.";
            return;
        }

        var panel = tool switch
        {
            LumionWorkspaceTool.Import => LumionSidePanel.Import,
            LumionWorkspaceTool.Build => LumionSidePanel.Build,
            LumionWorkspaceTool.Materials => LumionSidePanel.Materials,
            LumionWorkspaceTool.Objects => LumionSidePanel.Objects,
            LumionWorkspaceTool.Nature => LumionSidePanel.Nature,
            LumionWorkspaceTool.Weather => LumionSidePanel.Weather,
            LumionWorkspaceTool.Camera => LumionSidePanel.Camera,
            LumionWorkspaceTool.Render => LumionSidePanel.Render,
            LumionWorkspaceTool.Library => LumionSidePanel.Library,
            _ => LumionSidePanel.None
        };

        SetLumionToolCore(tool, panel, updateStatus: true);
    }

    private void SetLumionToolCore(LumionWorkspaceTool tool, LumionSidePanel panel, bool updateStatus)
    {
        ActiveLumionTool = tool;
        ActiveSidePanel = panel;
        IsRightInspectorExpanded = panel != LumionSidePanel.None;

        foreach (var item in LumionTools)
            item.IsSelected = string.Equals(item.ToolKey, tool.ToString(), StringComparison.OrdinalIgnoreCase);

        switch (tool)
        {
            case LumionWorkspaceTool.Import:
                ActiveLumionToolTitle = "Importar modelo";
                ActiveLumionToolSubtitle = "Carga OBJ, FBX, GLB, IFC o STEP según el pipeline disponible.";
                LumionModeBadge = "IMPORT";
                break;
            case LumionWorkspaceTool.Build:
                ActiveLumionToolTitle = "Construir escena";
                ActiveLumionToolSubtitle = "Organiza el modelo, encuadra y prepara el proyecto.";
                LumionModeBadge = "BUILD";
                SetInteractionMode("Object");
                break;
            case LumionWorkspaceTool.Materials:
                ActiveLumionToolTitle = "Materiales";
                ActiveLumionToolSubtitle = "Selecciona superficies y aplica presets PBR.";
                LumionModeBadge = "MATERIAL";
                SetInteractionMode("Material");
                break;
            case LumionWorkspaceTool.Objects:
                ActiveLumionToolTitle = "Objetos";
                ActiveLumionToolSubtitle = "Biblioteca visual para añadir elementos a la escena.";
                LumionModeBadge = "OBJECTS";
                break;
            case LumionWorkspaceTool.Nature:
                ActiveLumionToolTitle = "Naturaleza";
                ActiveLumionToolSubtitle = "Panel preparado para vegetación, contexto y exteriores.";
                LumionModeBadge = "NATURE";
                break;
            case LumionWorkspaceTool.Weather:
                ActiveLumionToolTitle = "Clima y cielo";
                ActiveLumionToolSubtitle = "Ajusta sol, ambiente, exposición y apariencia del cielo.";
                LumionModeBadge = "WEATHER";
                break;
            case LumionWorkspaceTool.Camera:
                ActiveLumionToolTitle = "Cámara";
                ActiveLumionToolSubtitle = "Vistas rápidas, zoom, encuadre y foto arquitectónica.";
                LumionModeBadge = "CAMERA";
                break;
            case LumionWorkspaceTool.Render:
                ActiveLumionToolTitle = "Render";
                ActiveLumionToolSubtitle = "Previsualiza, cambia calidad y exporta el still final.";
                LumionModeBadge = "RENDER";
                break;
            case LumionWorkspaceTool.Library:
                ActiveLumionToolTitle = "Biblioteca local";
                ActiveLumionToolSubtitle = "Modelos importados, recientes y materiales guardados.";
                LumionModeBadge = "LIBRARY";
                break;
        }

        NotifyLumionPanelProperties();
        if (updateStatus)
            StatusText = $"Herramienta activa: {ActiveLumionToolTitle}.";
    }

    [RelayCommand]
    private void ToggleLumionPanel()
    {
        if (ActiveSidePanel == LumionSidePanel.None)
        {
            SetLumionTool(ActiveLumionTool.ToString());
            return;
        }

        ActiveSidePanel = LumionSidePanel.None;
        IsRightInspectorExpanded = false;
        NotifyLumionPanelProperties();
    }

    [RelayCommand]
    private void ToggleLumionHelpOverlay()
    {
        ShowLumionHelpOverlay = !ShowLumionHelpOverlay;
        StatusText = ShowLumionHelpOverlay ? "Ayuda de navegación visible." : "Ayuda de navegación oculta.";
    }

    [RelayCommand]
    private void ToggleLumionLeftRail() => IsLeftToolRailExpanded = !IsLeftToolRailExpanded;
    [RelayCommand]
    private void ToggleLumionBottomDock() => IsBottomDockExpanded = !IsBottomDockExpanded;
    [RelayCommand]
    private void ToggleLumionScenePanel() => ShowLumionScenePanel = !ShowLumionScenePanel;

    [RelayCommand]
    private void SetLumionAssetCategory(string categoryTitle)
    {
        SelectedAssetCategoryTitle = categoryTitle;
        foreach (var category in LumionAssetCategories)
            category.IsSelected = string.Equals(category.Title, categoryTitle, StringComparison.OrdinalIgnoreCase);
        StatusText = $"Categoría activa: {categoryTitle}.";
    }

    [RelayCommand]
    private void ApplyLumionEnvironmentPreset(string presetKey)
    {
        SelectedEnvironmentPreset = presetKey;
        foreach (var preset in LumionEnvironmentPresets)
            preset.IsSelected = string.Equals(preset.PresetKey, presetKey, StringComparison.OrdinalIgnoreCase);
        ApplyEnvironmentPreset(presetKey);
    }

    [RelayCommand]
    private void PrepareLumionPresentationShot()
    {
        if (!HasModel)
        {
            StatusText = "Importa un modelo antes de preparar una toma tipo Lumion.";
            return;
        }

        ApplyLumionEnvironmentPreset("Day");
        PreparePhotoShot();
        SetLumionToolCore(LumionWorkspaceTool.Render, LumionSidePanel.Render, updateStatus: false);
        StatusText = "Toma de presentación lista: materiales, luz diurna y cámara encuadrada.";
    }

    [RelayCommand]
    private void QuickLumionExteriorLook()
    {
        ApplyLumionEnvironmentPreset("Day");
        AutoStyleSceneMaterials();
        StatusText = "Look exterior aplicado: luz diurna y materiales sugeridos.";
    }

    [RelayCommand]
    private void QuickLumionClayLook()
    {
        ApplyEnvironmentPreset("Studio");
        SetQuality("High");
        StatusText = "Look de estudio aplicado para revisar masa, sombras y composición.";
    }

    [RelayCommand]
    private void RefreshLumionLibrary()
    {
        RefreshImportedHistory();
        LoadLumionAssetCategories();
        StatusText = "Biblioteca Lumion-style actualizada.";
    }

    [RelayCommand]
    private void SetLumionResolutionPreset(string preset)
    {
        SetResolutionPreset(preset);
        SetLumionToolCore(LumionWorkspaceTool.Render, LumionSidePanel.Render, updateStatus: false);
    }
}
