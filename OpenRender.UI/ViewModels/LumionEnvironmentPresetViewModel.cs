using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenRender.ViewModels;

public partial class LumionEnvironmentPresetViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _presetKey = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _icon = "";
    [ObservableProperty] private bool _isSelected;
}
