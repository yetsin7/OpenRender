using System.Numerics;
using OpenRender.Materials;
using OpenRender.Rendering;
using OpenRender.Scene;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenRender.Services;

/// <summary>
/// Genera una exportación raster simple para previsualizaciones
/// cuando el pipeline final todavía no existe.
/// </summary>
public static partial class SoftwareRenderExporter
{
    /// <summary>
    /// Exporta una imagen fija de la escena actual en el formato solicitado.
    /// </summary>
    public static async Task ExportAsync(
        Scene3D scene,
        RenderSettings settings,
        string outputPath,
        OutputFormat format,
        CameraComponent? cameraOverride = null,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        int width = Math.Clamp(settings.Width, 640, 7680);
        int height = Math.Clamp(settings.Height, 360, 4320);

        using var image = new Image<Rgba32>(width, height);
        RenderScene(image, scene, cameraOverride ?? scene.Camera, settings, cancellationToken);

        switch (format)
        {
            case OutputFormat.Jpeg:
                await image.SaveAsync(outputPath, new JpegEncoder { Quality = Math.Clamp(settings.JpegQuality, 60, 100) }, cancellationToken);
                break;
            case OutputFormat.Bmp:
                await image.SaveAsync(outputPath, new BmpEncoder(), cancellationToken);
                break;
            case OutputFormat.Tiff:
                await image.SaveAsync(outputPath, new TiffEncoder(), cancellationToken);
                break;
            default:
                await image.SaveAsync(outputPath, new PngEncoder(), cancellationToken);
                break;
        }
    }

    private static void RenderScene(Image<Rgba32> image, Scene3D scene, CameraComponent camera, RenderSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rasterizer = new SoftwareSceneRasterizer();
        var exportQuality = settings.Quality >= RenderQuality.High ? RenderQuality.Ultra : settings.Quality;
        var frame = rasterizer.Render(scene, camera, image.Width, image.Height, exportQuality, drawGrid: false);
        CopyFrameToImage(image, frame);
        DrawCameraSafeFrame(image);
    }

    private static void DrawCameraSafeFrame(Image<Rgba32> image)
    {
        var color = new Rgba32(255, 255, 255, 135);
        int marginX = (int)(image.Width * 0.045f);
        int marginY = (int)(image.Height * 0.06f);
        int length = (int)(Math.Min(image.Width, image.Height) * 0.055f);

        DrawLine(image, marginX, marginY, marginX + length, marginY, color, 0.45f);
        DrawLine(image, marginX, marginY, marginX, marginY + length, color, 0.45f);
        DrawLine(image, image.Width - marginX, marginY, image.Width - marginX - length, marginY, color, 0.45f);
        DrawLine(image, image.Width - marginX, marginY, image.Width - marginX, marginY + length, color, 0.45f);
        DrawLine(image, marginX, image.Height - marginY, marginX + length, image.Height - marginY, color, 0.45f);
        DrawLine(image, marginX, image.Height - marginY, marginX, image.Height - marginY - length, color, 0.45f);
        DrawLine(image, image.Width - marginX, image.Height - marginY, image.Width - marginX - length, image.Height - marginY, color, 0.45f);
        DrawLine(image, image.Width - marginX, image.Height - marginY, image.Width - marginX, image.Height - marginY - length, color, 0.45f);
    }

    private static void CopyFrameToImage(Image<Rgba32> image, SoftwareSceneFrame frame)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < image.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                int sourceRow = y * frame.Width;
                for (int x = 0; x < image.Width; x++)
                {
                    int color = frame.Pixels[sourceRow + x];
                    row[x] = new Rgba32(
                        (byte)((color >> 16) & 0xFF),
                        (byte)((color >> 8) & 0xFF),
                        (byte)(color & 0xFF),
                        255);
                }
            }
        });
    }
}
