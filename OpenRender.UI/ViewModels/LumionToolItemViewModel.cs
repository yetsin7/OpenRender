using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenRender.ViewModels;

public partial class LumionToolItemViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _icon = "";
    [ObservableProperty] private string _toolKey = "";
    [ObservableProperty] private bool _isSelected;
}
