using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenRender.ViewModels;

public partial class LumionAssetCategoryViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _icon = "";
    [ObservableProperty] private int _itemCount;
    [ObservableProperty] private bool _isSelected;
}
