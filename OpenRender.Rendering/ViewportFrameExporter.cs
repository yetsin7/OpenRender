using Silk.NET.OpenGL;
using OpenRender.Core.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace OpenRender.Rendering;

/// <summary>
/// Saves the current OpenGL framebuffer to an image file.
/// </summary>
public static class ViewportFrameExporter
{
    public static unsafe void SaveFramebuffer(
        GL gl,
        int sourceWidth,
        int sourceHeight,
        string outputPath,
        int targetWidth,
        int targetHeight,
        OutputFormat format,
        int jpegQuality = 95)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
            throw new InvalidOperationException("Viewport has no visible size to export.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        byte[] pixels = new byte[sourceWidth * sourceHeight * 4];
        fixed (byte* ptr = pixels)
        {
            gl.ReadPixels(0, 0, (uint)sourceWidth, (uint)sourceHeight, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        FlipVertically(pixels, sourceWidth, sourceHeight);

        using var image = Image.LoadPixelData<Rgba32>(pixels, sourceWidth, sourceHeight);
        if (targetWidth > 0 && targetHeight > 0 && (targetWidth != sourceWidth || targetHeight != sourceHeight))
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(targetWidth, targetHeight),
                Mode = ResizeMode.Stretch
            }));
        }

        using var stream = File.Create(outputPath);
        switch (format)
        {
            case OutputFormat.Jpeg:
                image.Save(stream, new JpegEncoder { Quality = jpegQuality });
                break;
            case OutputFormat.Bmp:
                image.Save(stream, new BmpEncoder());
                break;
            case OutputFormat.Tiff:
                image.Save(stream, new TiffEncoder());
                break;
            default:
                image.Save(stream, new PngEncoder());
                break;
        }
    }

    private static void FlipVertically(byte[] pixels, int width, int height)
    {
        int stride = width * 4;
        byte[] row = new byte[stride];

        for (int y = 0; y < height / 2; y++)
        {
            int top = y * stride;
            int bottom = (height - 1 - y) * stride;

            System.Buffer.BlockCopy(pixels, top, row, 0, stride);
            System.Buffer.BlockCopy(pixels, bottom, pixels, top, stride);
            System.Buffer.BlockCopy(row, 0, pixels, bottom, stride);
        }
    }
}
