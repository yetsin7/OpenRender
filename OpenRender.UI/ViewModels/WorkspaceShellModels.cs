using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media.Imaging;

namespace OpenRender.ViewModels;

public enum WorkspaceSection
{
    Dashboard,
    Library,
    Render,
    Camera
}

public sealed class WorkspaceProjectTemplateViewModel
{
    /// <summary>
    /// Ruta local de la miniatura usada por las tarjetas del dashboard.
    /// Existe para desacoplar el layout visual de una imagen compartida.
    /// </summary>
    public string PreviewImagePath { get; init; } = "";
    public Bitmap? PreviewImageSource { get; init; }
    public string Title { get; init; } = "";
    public string Resolution { get; init; } = "";
    public string EditedText { get; init; } = "";
    public string EngineLabel { get; init; } = "";
    public string EngineAccentHex { get; init; } = "#82CFFF";
}

public partial class WorkspaceAssetItemViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _kind = "";
    [ObservableProperty] private string _libraryGroup = "Nature";
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _badge = "";
    [ObservableProperty] private string _tagSecondary = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _author = "OpenRender Originals";
    [ObservableProperty] private string _fileSize = "45 MB";
    [ObservableProperty] private string _lodCount = "4 Levels";
    [ObservableProperty] private string _accentHex = "#82CFFF";
    [ObservableProperty] private double _cardWidth = 188;
    [ObservableProperty] private double _cardHeight = 208;
    [ObservableProperty] private double _previewHeight = 132;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isMaterial;
    [ObservableProperty] private bool _isDownloaded = true;
    [ObservableProperty] private bool _showDownloadOverlay;
}

public partial class WorkspaceRenderJobViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _meta = "";
    [ObservableProperty] private string _iconGlyph = "\uE114";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _statusAccentHex = "#82CFFF";
    [ObservableProperty] private string _timing = "";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isVideo;
    [ObservableProperty] private bool _isSelected;
}
