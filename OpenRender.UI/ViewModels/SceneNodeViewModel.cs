using CommunityToolkit.Mvvm.ComponentModel;
using OpenRender.Scene;

namespace OpenRender.ViewModels;

public partial class SceneNodeViewModel : ObservableObject
{
    public SceneNode? Node { get; init; }
    public LightSource? Light { get; init; }
    public int? MaterialIndex { get; init; }
    public bool SupportsVisibility => Node != null || Light != null;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _icon = "NODE";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isModelScope;

    partial void OnIsVisibleChanged(bool value)
    {
        if (Node != null)
            Node.IsVisible = value;

        if (Light != null)
            Light.IsEnabled = value;
    }
}
