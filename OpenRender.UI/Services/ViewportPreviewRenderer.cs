using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OpenRender.Rendering;
using OpenRender.Scene;
using AvaloniaVector = Avalonia.Vector;

namespace OpenRender.Services;

/// <summary>
/// Adapta el rasterizador compartido para dibujar el viewport de Avalonia.
/// </summary>
public sealed class ViewportPreviewRenderer
{
    private readonly SoftwareSceneRasterizer _rasterizer = new();

    public void SetScene(Scene3D scene) => _rasterizer.SetScene(scene);

    public WriteableBitmap Render(Scene3D scene, CameraComponent camera, PixelSize requestedSize, SceneNode? selectedNode)
    {
        var frame = _rasterizer.Render(
            scene,
            camera,
            Math.Clamp(requestedSize.Width, 320, 1280),
            Math.Clamp(requestedSize.Height, 220, 820),
            RenderQuality.Medium,
            selectedNode,
            drawGrid: true);

        return CreateBitmap(frame);
    }

    private static WriteableBitmap CreateBitmap(SoftwareSceneFrame frame)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(frame.Width, frame.Height),
            new AvaloniaVector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using var buffer = bitmap.Lock();
        if (buffer.RowBytes == frame.Width * 4)
        {
            Marshal.Copy(frame.Pixels, 0, buffer.Address, frame.Pixels.Length);
            return bitmap;
        }

        for (int row = 0; row < frame.Height; row++)
            Marshal.Copy(frame.Pixels, row * frame.Width, buffer.Address + row * buffer.RowBytes, frame.Width);

        return bitmap;
    }
}
