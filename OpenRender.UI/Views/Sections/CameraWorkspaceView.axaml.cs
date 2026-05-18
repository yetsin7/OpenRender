using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;
using OpenRender.Controls;

namespace OpenRender.Views.Sections;

public partial class CameraWorkspaceView : UserControl
{
    private bool _viewportLayerInitialized;

    public CameraWorkspaceView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => EnsureViewportLayers();
    }

    private void EnsureViewportLayers()
    {
        if (_viewportLayerInitialized)
            return;

        var safeViewport = this.GetVisualDescendants().OfType<SoftwareViewportControl>().FirstOrDefault();
        if (safeViewport?.Parent is not Panel panel)
            return;

        if (!panel.Children.OfType<VulkanViewportControl>().Any())
        {
            var nativeViewport = new VulkanViewportControl();
            nativeViewport.Bind(IsVisibleProperty, new Binding("ShowNativeViewport"));
            panel.Children.Insert(panel.Children.IndexOf(safeViewport), nativeViewport);
        }

        safeViewport.Bind(IsVisibleProperty, new Binding("ShowSafeViewportPreview"));
        _viewportLayerInitialized = true;
    }
}
