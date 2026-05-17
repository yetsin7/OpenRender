namespace OpenRender.ViewModels;

public sealed class ImportedModelHistoryItemViewModel
{
    public string FilePath { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Meta { get; init; } = "";
    public bool ExistsOnDisk { get; init; }
}
