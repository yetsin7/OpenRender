using System;

namespace OpenRender.ViewModels;

/// <summary>
/// Centraliza textos visibles de la shell Pro Dark para poder alternar
/// entre ingles y espanol sin duplicar cadenas en las vistas XAML.
/// </summary>
public sealed class WorkspaceUiText
{
    public string Brand { get; init; } = "";
    public string NavDashboard { get; init; } = "";
    public string NavLibrary { get; init; } = "";
    public string NavRender { get; init; } = "";
    public string NavCamera { get; init; } = "";
    public string SearchProjects { get; init; } = "";
    public string DashboardTitle { get; init; } = "";
    public string DashboardSubtitle { get; init; } = "";
    public string Filter { get; init; } = "";
    public string Sort { get; init; } = "";
    public string NewProject { get; init; } = "";
    public string NewProjectSubtitle { get; init; } = "";
    public string ProjectAlpha { get; init; } = "";
    public string EngineActive { get; init; } = "";
    public string Content { get; init; } = "";
    public string Materials { get; init; } = "";
    public string Landscape { get; init; } = "";
    public string Weather { get; init; } = "";
    public string Layers { get; init; } = "";
    public string RenderImage { get; init; } = "";
    public string Settings { get; init; } = "";
    public string Library { get; init; } = "";
    public string Inspector { get; init; } = "";
    public string SelectionCube { get; init; } = "";
    public string Albedo { get; init; } = "";
    public string Roughness { get; init; } = "";
    public string Metalness { get; init; } = "";
    public string Normal { get; init; } = "";
    public string Strength { get; init; } = "";
    public string Transform { get; init; } = "";
    public string Selection { get; init; } = "";
    public string Visibility { get; init; } = "";
    public string SceneHierarchy { get; init; } = "";
    public string Environment { get; init; } = "";
    public string Architecture { get; init; } = "";
    public string Lighting { get; init; } = "";
    public string Terrain { get; init; } = "";
    public string Vegetation { get; init; } = "";
    public string Sky { get; init; } = "";
    public string Effects { get; init; } = "";
    public string SunPosition { get; init; } = "";
    public string HdriEnvironment { get; init; } = "";
    public string Illumination { get; init; } = "";
    public string AssetLibrary { get; init; } = "";
    public string LibrarySubtitle { get; init; } = "";
    public string SearchAssets { get; init; } = "";
    public string AllAssets { get; init; } = "";
    public string Nature { get; init; } = "";
    public string People { get; init; } = "";
    public string Indoor { get; init; } = "";
    public string Outdoor { get; init; } = "";
    public string LibrarySettings { get; init; } = "";
    public string SyncLibrary { get; init; } = "";
    public string Properties { get; init; } = "";
    public string PlaceInScene { get; init; } = "";
    public string Type { get; init; } = "";
    public string Polygons { get; init; } = "";
    public string FileSize { get; init; } = "";
    public string Lods { get; init; } = "";
    public string Author { get; init; } = "";
    public string Notes { get; init; } = "";
    public string RenderQueue { get; init; } = "";
    public string ExportSettings { get; init; } = "";
    public string Resolution { get; init; } = "";
    public string Format { get; init; } = "";
    public string FrameRate { get; init; } = "";
    public string OutputPath { get; init; } = "";
    public string Width { get; init; } = "";
    public string Height { get; init; } = "";
    public string StartRender { get; init; } = "";
    public string Complete { get; init; } = "";
    public string Perspective { get; init; } = "";
    public string HighQuality { get; init; } = "";
    public string CameraMain { get; init; } = "";
    public string SceneMainVilla { get; init; } = "";
    public string ScenePoolDeck { get; init; } = "";
    public string Location { get; init; } = "";
    public string Rotation { get; init; } = "";

    /// <summary>
    /// Crea el paquete de textos activo; ingles es el valor por defecto
    /// para coincidir con las referencias visuales entregadas.
    /// </summary>
    public static WorkspaceUiText CreateDefault()
    {
        var culture = System.Environment.GetEnvironmentVariable("OPENRENDER_UI_CULTURE");
        return string.Equals(culture, "es", StringComparison.OrdinalIgnoreCase)
            ? CreateSpanish()
            : CreateEnglish();
    }

    private static WorkspaceUiText CreateEnglish() => new()
    {
        Brand = "OpenRender",
        NavDashboard = "Dashboard",
        NavLibrary = "Library",
        NavRender = "Render",
        NavCamera = "Camera",
        SearchProjects = "Search projects...",
        DashboardTitle = "Projects",
        DashboardSubtitle = "Manage and organize your rendering workspaces.",
        Filter = "Filter",
        Sort = "Sort",
        NewProject = "New Project",
        NewProjectSubtitle = "Create a blank workspace",
        ProjectAlpha = "Project Alpha",
        EngineActive = "V-Ray Engine Active",
        Content = "Content",
        Materials = "Materials",
        Landscape = "Landscape",
        Weather = "Weather",
        Layers = "Layers",
        RenderImage = "Render Image",
        Settings = "Settings",
        Library = "Library",
        Inspector = "Inspector",
        SelectionCube = "Selection: Cube_01",
        Albedo = "Albedo",
        Roughness = "Roughness",
        Metalness = "Metalness",
        Normal = "Normal",
        Strength = "STR",
        Transform = "Transform",
        Selection = "Selection",
        Visibility = "Vis",
        SceneHierarchy = "SCENE HIERARCHY",
        Environment = "Environment",
        Architecture = "Architecture",
        Lighting = "Lighting",
        Terrain = "Terrain",
        Vegetation = "Vegetation",
        Sky = "Sky",
        Effects = "Effects",
        SunPosition = "SUN POSITION",
        HdriEnvironment = "HDRI ENVIRONMENT",
        Illumination = "ILLUMINATION",
        AssetLibrary = "Asset Library",
        LibrarySubtitle = "Browse and filter resources",
        SearchAssets = "Search assets...",
        AllAssets = "All Assets",
        Nature = "Nature",
        People = "People",
        Indoor = "Indoor",
        Outdoor = "Outdoor",
        LibrarySettings = "Library Settings",
        SyncLibrary = "Sync Library",
        Properties = "Properties",
        PlaceInScene = "Place in Scene",
        Type = "Type",
        Polygons = "Polygons",
        FileSize = "File Size",
        Lods = "LODs",
        Author = "Author",
        Notes = "NOTES",
        RenderQueue = "Render Queue",
        ExportSettings = "Export Settings",
        Resolution = "RESOLUTION",
        Format = "FORMAT",
        FrameRate = "FRAME RATE",
        OutputPath = "OUTPUT PATH",
        Width = "Width",
        Height = "Height",
        StartRender = "Start Render",
        Complete = "Complete",
        Perspective = "Perspective",
        HighQuality = "High Quality",
        CameraMain = "Cam_01_Main",
        SceneMainVilla = "Main_Villa_Geo",
        ScenePoolDeck = "Pool_Deck",
        Location = "LOCATION",
        Rotation = "ROTATION"
    };

    private static WorkspaceUiText CreateSpanish() => new()
    {
        Brand = "OpenRender",
        NavDashboard = "Panel",
        NavLibrary = "Biblioteca",
        NavRender = "Render",
        NavCamera = "Camara",
        SearchProjects = "Buscar proyectos...",
        DashboardTitle = "Proyectos",
        DashboardSubtitle = "Gestiona y organiza tus espacios de render.",
        Filter = "Filtrar",
        Sort = "Ordenar",
        NewProject = "Nuevo proyecto",
        NewProjectSubtitle = "Crear un espacio en blanco",
        ProjectAlpha = "Proyecto Alpha",
        EngineActive = "Motor V-Ray activo",
        Content = "Contenido",
        Materials = "Materiales",
        Landscape = "Paisaje",
        Weather = "Clima",
        Layers = "Capas",
        RenderImage = "Renderizar imagen",
        Settings = "Ajustes",
        Library = "Biblioteca",
        Inspector = "Inspector",
        SelectionCube = "Seleccion: Cube_01",
        Albedo = "Albedo",
        Roughness = "Rugosidad",
        Metalness = "Metal",
        Normal = "Normal",
        Strength = "INT",
        Transform = "Transformar",
        Selection = "Seleccion",
        Visibility = "Vis",
        SceneHierarchy = "JERARQUIA DE ESCENA",
        Environment = "Entorno",
        Architecture = "Arquitectura",
        Lighting = "Iluminacion",
        Terrain = "Terreno",
        Vegetation = "Vegetacion",
        Sky = "Cielo",
        Effects = "Efectos",
        SunPosition = "POSICION DEL SOL",
        HdriEnvironment = "ENTORNO HDRI",
        Illumination = "ILUMINACION",
        AssetLibrary = "Biblioteca de activos",
        LibrarySubtitle = "Explora y filtra recursos",
        SearchAssets = "Buscar activos...",
        AllAssets = "Todos",
        Nature = "Naturaleza",
        People = "Personas",
        Indoor = "Interior",
        Outdoor = "Exterior",
        LibrarySettings = "Ajustes de biblioteca",
        SyncLibrary = "Sincronizar",
        Properties = "Propiedades",
        PlaceInScene = "Colocar en escena",
        Type = "Tipo",
        Polygons = "Poligonos",
        FileSize = "Tamano",
        Lods = "LODs",
        Author = "Autor",
        Notes = "NOTAS",
        RenderQueue = "Cola de render",
        ExportSettings = "Ajustes de exportacion",
        Resolution = "RESOLUCION",
        Format = "FORMATO",
        FrameRate = "FPS",
        OutputPath = "RUTA DE SALIDA",
        Width = "Ancho",
        Height = "Alto",
        StartRender = "Iniciar render",
        Complete = "Completo",
        Perspective = "Perspectiva",
        HighQuality = "Alta calidad",
        CameraMain = "Cam_01_Main",
        SceneMainVilla = "Main_Villa_Geo",
        ScenePoolDeck = "Pool_Deck",
        Location = "UBICACION",
        Rotation = "ROTACION"
    };
}
