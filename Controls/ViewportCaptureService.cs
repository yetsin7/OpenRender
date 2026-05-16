using OpenRender.Core.Rendering;

namespace OpenRender.Controls;

/// <summary>
/// Bridges the active viewport control with view-model driven export commands.
/// </summary>
public static class ViewportCaptureService
{
    private static WeakReference<ViewportControl>? _activeViewport;

    public static void Register(ViewportControl viewport)
    {
        _activeViewport = new WeakReference<ViewportControl>(viewport);
    }

    public static async Task CaptureAsync(string outputPath, int width, int height, OutputFormat format, int jpegQuality = 95, bool cleanViewport = true)
    {
        if (_activeViewport == null || !_activeViewport.TryGetTarget(out var viewport))
            throw new InvalidOperationException("The viewport is not ready yet.");

        await viewport.CaptureFrameAsync(outputPath, width, height, format, jpegQuality, cleanViewport);
    }
}
