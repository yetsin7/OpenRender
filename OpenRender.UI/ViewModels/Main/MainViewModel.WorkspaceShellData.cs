using System.Linq;

namespace OpenRender.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// Prepara datos de muestra consistentes con la shell de escritorio
    /// mientras se conecta la fuente real de proyectos, biblioteca y cola.
    /// </summary>
    private void InitializeWorkspaceShell()
    {
        DashboardReferenceProjects.Clear();
        DashboardReferenceProjects.Add(new WorkspaceProjectTemplateViewModel
        {
            PreviewImagePath = DashboardVillaImagePath,
            PreviewImageSource = DashboardVillaImageSourceValue,
            Title = "Modern Villa Exterior",
            Resolution = "4K UHD",
            EditedText = "Edited 2h ago",
            EngineLabel = "V-Ray",
            EngineAccentHex = "#82CFFF"
        });
        DashboardReferenceProjects.Add(new WorkspaceProjectTemplateViewModel
        {
            PreviewImagePath = DashboardLoftImagePath,
            PreviewImageSource = DashboardLoftImageSourceValue,
            Title = "Urban Loft Interior",
            Resolution = "1080p",
            EditedText = "Edited yesterday",
            EngineLabel = "Corona",
            EngineAccentHex = "#FFB876"
        });

        AssetLibraryItems.Clear();
        AssetLibraryItems.Add(new WorkspaceAssetItemViewModel { Title = "Maple Tree - Autumn 01", Kind = "3D", LibraryGroup = "Nature", Category = "Nature > Trees > Deciduous", Subtitle = "1.2M polygons", Badge = "High Poly", TagSecondary = "PBR", Notes = "Highly detailed scan-based model suitable for foreground placement.", FileSize = "45 MB", LodCount = "4 Levels", AccentHex = "#D9741C", IsSelected = true });
        AssetLibraryItems.Add(new WorkspaceAssetItemViewModel { Title = "Scots Pine - Mature", Kind = "3D", LibraryGroup = "Nature", Category = "Nature > Trees > Conifers", Subtitle = "Mid poly forest asset", Badge = "Mid Poly", TagSecondary = "Wind", Notes = "Balanced asset for background vegetation and forest edges.", FileSize = "29 MB", LodCount = "3 Levels", AccentHex = "#486B45" });
        AssetLibraryItems.Add(new WorkspaceAssetItemViewModel { Title = "Lawn Grass - Manicured", Kind = "MAT", LibraryGroup = "Materials", Category = "Materials > Landscape", Subtitle = "Includes albedo and normal maps", Badge = "4K Material", TagSecondary = "Tileable", Notes = "Tileable grass material for landscape surfaces and close-up shots.", FileSize = "128 MB", LodCount = "Maps Set", AccentHex = "#5B7E29", CardHeight = 296, PreviewHeight = 212, IsMaterial = true });
        AssetLibraryItems.Add(new WorkspaceAssetItemViewModel { Title = "Monstera Deliciosa", Kind = "3D", LibraryGroup = "Indoor", Category = "Indoor > Decor > Plants", Subtitle = "Decor asset", Badge = "Hero Prop", TagSecondary = "PBR", Notes = "Indoor plant with balanced detail for hero corners and lounge shots.", FileSize = "18 MB", LodCount = "2 Levels", AccentHex = "#2F6E5E" });
        AssetLibraryItems.Add(new WorkspaceAssetItemViewModel { Title = "River Rocks Cluster 02", Kind = "3D", LibraryGroup = "Outdoor", Category = "Outdoor > Stones > Scan", Subtitle = "Photogrammetry scan", Badge = "Scan", TagSecondary = "PBR", Notes = "Outdoor scan asset for landscaping edges, riverbeds and hardscape scenes.", FileSize = "52 MB", LodCount = "3 Levels", AccentHex = "#6A6258" });
        AssetLibraryItems.Add(new WorkspaceAssetItemViewModel { Title = "Oak Tree - Summer", Kind = "3D", LibraryGroup = "Nature", Category = "Nature > Trees > Deciduous", Subtitle = "Queued for sync", Badge = "Not Downloaded", TagSecondary = "Cloud", Notes = "Cloud asset pending download from the shared library.", FileSize = "68 MB", LodCount = "4 Levels", AccentHex = "#3A3A3A", IsDownloaded = false, ShowDownloadOverlay = true });

        RenderQueueJobs.Clear();
        RenderQueueJobs.Add(new WorkspaceRenderJobViewModel { Title = "Exterior_Night_V2", Meta = "8K • EXR • 16-bit", IconGlyph = "\uE114", Status = "Rendering", StatusAccentHex = "#82CFFF", Timing = "Est. 12m 40s remaining", Progress = 45 });
        RenderQueueJobs.Add(new WorkspaceRenderJobViewModel { Title = "Flythrough_Seq_01", Meta = "4K • MP4 • 60fps", IconGlyph = "\uE714", Status = "Waiting", StatusAccentHex = "#5F6C76", Timing = "Queued for export", Progress = 0, IsVideo = true, IsSelected = true });
        RenderQueueJobs.Add(new WorkspaceRenderJobViewModel { Title = "Interior_LivingRoom_Day", Meta = "4K • PNG • 8-bit", IconGlyph = "\uE114", Status = "Completed", StatusAccentHex = "#C08A54", Timing = "Done in 4m 12s", Progress = 100 });

        RefreshVisibleAssetLibraryItems();
        SelectedWorkspaceAsset = AssetLibraryItems.FirstOrDefault(item => item.IsSelected) ?? VisibleAssetLibraryItems.FirstOrDefault();
        SelectedRenderQueueJob = RenderQueueJobs.FirstOrDefault(item => item.IsSelected) ?? RenderQueueJobs.FirstOrDefault();
    }

    /// <summary>
    /// Sincroniza la lista visible de activos según la categoría elegida y
    /// conserva una selección válida para el panel inspector.
    /// </summary>
    private void RefreshVisibleAssetLibraryItems()
    {
        var filteredItems = IsLibraryAllAssetsCategoryActive
            ? AssetLibraryItems.ToList()
            : AssetLibraryItems.Where(item => string.Equals(item.LibraryGroup, ActiveLibraryCategory, System.StringComparison.OrdinalIgnoreCase)).ToList();

        VisibleAssetLibraryItems.Clear();
        foreach (var item in filteredItems)
            VisibleAssetLibraryItems.Add(item);

        if (SelectedWorkspaceAsset == null || !VisibleAssetLibraryItems.Contains(SelectedWorkspaceAsset))
        {
            var nextSelection = VisibleAssetLibraryItems.FirstOrDefault() ?? AssetLibraryItems.FirstOrDefault();
            if (nextSelection == null)
                return;

            foreach (var item in AssetLibraryItems)
                item.IsSelected = ReferenceEquals(item, nextSelection);

            SelectedWorkspaceAsset = nextSelection;
        }
    }

    /// <summary>
    /// Notifica estados derivados usados por los botones de categoría.
    /// </summary>
    private void NotifyLibraryCategoryProperties()
    {
        OnPropertyChanged(nameof(IsLibraryAllAssetsCategoryActive));
        OnPropertyChanged(nameof(IsLibraryNatureCategoryActive));
        OnPropertyChanged(nameof(IsLibraryPeopleCategoryActive));
        OnPropertyChanged(nameof(IsLibraryIndoorCategoryActive));
        OnPropertyChanged(nameof(IsLibraryOutdoorCategoryActive));
        OnPropertyChanged(nameof(IsLibraryMaterialsCategoryActive));
        OnPropertyChanged(nameof(ActiveLibraryItemCountText));
    }
}
