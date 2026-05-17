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
{    private void InitializeLumionUiState()
    {
        LoadLumionToolRail();
        LoadLumionAssetCategories();
        LoadLumionEnvironmentPresets();
        SetLumionToolCore(LumionWorkspaceTool.Import, LumionSidePanel.Import, updateStatus: false);
    }

    private void LoadLumionToolRail()
    {
        LumionTools.Clear();
        LumionTools.Add(new LumionToolItemViewModel { Title = "Importar", Subtitle = "Modelos 3D", Icon = "IMP", ToolKey = nameof(LumionWorkspaceTool.Import) });
        LumionTools.Add(new LumionToolItemViewModel { Title = "Construir", Subtitle = "Escena y base", Icon = "BLD", ToolKey = nameof(LumionWorkspaceTool.Build) });
        LumionTools.Add(new LumionToolItemViewModel { Title = "Materiales", Subtitle = "PBR y presets", Icon = "MAT", ToolKey = nameof(LumionWorkspaceTool.Materials) });
        LumionTools.Add(new LumionToolItemViewModel { Title = "Objetos", Subtitle = "Biblioteca", Icon = "OBJ", ToolKey = nameof(LumionWorkspaceTool.Objects) });
        LumionTools.Add(new LumionToolItemViewModel { Title = "Naturaleza", Subtitle = "Exteriores", Icon = "NAT", ToolKey = nameof(LumionWorkspaceTool.Nature) });
        LumionTools.Add(new LumionToolItemViewModel { Title = "Clima", Subtitle = "Sol y cielo", Icon = "SUN", ToolKey = nameof(LumionWorkspaceTool.Weather) });
        LumionTools.Add(new LumionToolItemViewModel { Title = "Cámara", Subtitle = "Vistas", Icon = "CAM", ToolKey = nameof(LumionWorkspaceTool.Camera) });
        LumionTools.Add(new LumionToolItemViewModel { Title = "Render", Subtitle = "Exportar", Icon = "RND", ToolKey = nameof(LumionWorkspaceTool.Render) });
        LumionTools.Add(new LumionToolItemViewModel { Title = "Biblioteca", Subtitle = "Historial", Icon = "LIB", ToolKey = nameof(LumionWorkspaceTool.Library) });
    }

    private void LoadLumionAssetCategories()
    {
        LumionAssetCategories.Clear();
        LumionAssetCategories.Add(new LumionAssetCategoryViewModel { Title = "Modelos importados", Subtitle = "Historial y recientes", Icon = "MOD", ItemCount = ImportedHistory.Count, IsSelected = true });
        LumionAssetCategories.Add(new LumionAssetCategoryViewModel { Title = "Materiales", Subtitle = "Presets arquitectónicos", Icon = "MAT", ItemCount = MaterialLibraryPresets.Count });
        LumionAssetCategories.Add(new LumionAssetCategoryViewModel { Title = "Naturaleza", Subtitle = "Vegetación y exteriores", Icon = "TREE", ItemCount = 0 });
        LumionAssetCategories.Add(new LumionAssetCategoryViewModel { Title = "Luces", Subtitle = "Sol y luces auxiliares", Icon = "LGT", ItemCount = Scene.Lights.Count });
        LumionAssetCategories.Add(new LumionAssetCategoryViewModel { Title = "Cámaras", Subtitle = "Vistas rápidas", Icon = "CAM", ItemCount = 4 });
    }

    private void LoadLumionEnvironmentPresets()
    {
        LumionEnvironmentPresets.Clear();
        LumionEnvironmentPresets.Add(new LumionEnvironmentPresetViewModel { Title = "Día claro", PresetKey = "Day", Description = "Cielo azul, sol definido y colores limpios.", Icon = "DAY", IsSelected = true });
        LumionEnvironmentPresets.Add(new LumionEnvironmentPresetViewModel { Title = "Nublado", PresetKey = "Overcast", Description = "Luz suave para revisar materiales sin sombras duras.", Icon = "CLD" });
        LumionEnvironmentPresets.Add(new LumionEnvironmentPresetViewModel { Title = "Atardecer", PresetKey = "Sunset", Description = "Luz cálida y contraste cinematográfico.", Icon = "SET" });
        LumionEnvironmentPresets.Add(new LumionEnvironmentPresetViewModel { Title = "Estudio", PresetKey = "Studio", Description = "Fondo oscuro y luz controlada para presentación.", Icon = "STD" });
    }
}
