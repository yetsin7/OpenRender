using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenRender.Services;

public static partial class SoftwareRenderExporter
{
    private static void FillRectangle(Image<Rgba32> image, int left, int top, int width, int height, Rgba32 color, float opacity)
    {
        int x0 = Math.Clamp(left, 0, image.Width - 1);
        int x1 = Math.Clamp(left + width, 0, image.Width);
        int y0 = Math.Clamp(top, 0, image.Height - 1);
        int y1 = Math.Clamp(top + height, 0, image.Height);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = y0; y < y1; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = x0; x < x1; x++)
                    Blend(ref row[x], color, opacity);
            }
        });
    }

    private static void FillCircle(Image<Rgba32> image, int centerX, int centerY, int radius, Rgba32 color, float opacity)
    {
        int radiusSquared = radius * radius;
        int x0 = Math.Clamp(centerX - radius, 0, image.Width - 1);
        int x1 = Math.Clamp(centerX + radius, 0, image.Width - 1);
        int y0 = Math.Clamp(centerY - radius, 0, image.Height - 1);
        int y1 = Math.Clamp(centerY + radius, 0, image.Height - 1);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = y0; y <= y1; y++)
            {
                var row = accessor.GetRowSpan(y);
                int dy = y - centerY;
                for (int x = x0; x <= x1; x++)
                {
                    int dx = x - centerX;
                    int distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared > radiusSquared)
                        continue;

                    float falloff = 1f - distanceSquared / Math.Max(1f, radiusSquared);
                    Blend(ref row[x], color, opacity * MathF.Pow(falloff, 0.35f));
                }
            }
        });
    }

    private static void FillPolygon(Image<Rgba32> image, IReadOnlyList<Vector2> points, Rgba32 color, float opacity)
    {
        if (points.Count < 3)
            return;

        int minY = Math.Clamp((int)MathF.Floor(points.Min(point => point.Y)), 0, image.Height - 1);
        int maxY = Math.Clamp((int)MathF.Ceiling(points.Max(point => point.Y)), 0, image.Height - 1);
        var intersections = new List<float>(points.Count);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = minY; y <= maxY; y++)
            {
                intersections.Clear();
                for (int index = 0; index < points.Count; index++)
                {
                    var start = points[index];
                    var end = points[(index + 1) % points.Count];
                    if ((start.Y <= y && end.Y > y) || (end.Y <= y && start.Y > y))
                    {
                        float t = (y - start.Y) / (end.Y - start.Y);
                        intersections.Add(start.X + t * (end.X - start.X));
                    }
                }

                if (intersections.Count < 2)
                    continue;

                intersections.Sort();
                var row = accessor.GetRowSpan(y);
                for (int index = 0; index < intersections.Count - 1; index += 2)
                {
                    int x0 = Math.Clamp((int)MathF.Ceiling(intersections[index]), 0, image.Width - 1);
                    int x1 = Math.Clamp((int)MathF.Floor(intersections[index + 1]), 0, image.Width - 1);
                    for (int x = x0; x <= x1; x++)
                        Blend(ref row[x], color, opacity);
                }
            }
        });
    }

    private static void DrawLine(Image<Rgba32> image, Vector2 start, Vector2 end, Rgba32 color, float opacity)
    {
        DrawLine(image, (int)start.X, (int)start.Y, (int)end.X, (int)end.Y, color, opacity);
    }

    private static void DrawLine(Image<Rgba32> image, int x0, int y0, int x1, int y1, Rgba32 color, float opacity)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            BlendPixel(image, x0, y0, color, opacity);
            if (x0 == x1 && y0 == y1)
                break;

            int doubledError = 2 * error;
            if (doubledError >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (doubledError <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void BlendPixel(Image<Rgba32> image, int x, int y, Rgba32 color, float opacity)
    {
        if ((uint)x >= image.Width || (uint)y >= image.Height)
            return;

        image.ProcessPixelRows(accessor =>
        {
            var row = accessor.GetRowSpan(y);
            Blend(ref row[x], color, opacity);
        });
    }

    private static void Blend(ref Rgba32 target, Rgba32 source, float opacity)
    {
        float alpha = Math.Clamp(opacity * source.A / 255f, 0f, 1f);
        float inverseAlpha = 1f - alpha;
        target.R = (byte)Math.Clamp(source.R * alpha + target.R * inverseAlpha, 0f, 255f);
        target.G = (byte)Math.Clamp(source.G * alpha + target.G * inverseAlpha, 0f, 255f);
        target.B = (byte)Math.Clamp(source.B * alpha + target.B * inverseAlpha, 0f, 255f);
        target.A = 255;
    }

    private static Rgba32 ToColor(Vector3 color, float exposure, float gamma, float contrast)
    {
        return new Rgba32(ToByte(color.X, exposure, gamma, contrast), ToByte(color.Y, exposure, gamma, contrast), ToByte(color.Z, exposure, gamma, contrast), 255);
    }

    private static byte ToByte(float value, float exposure, float gamma, float contrast)
    {
        float corrected = Math.Clamp(value * exposure, 0f, 4f);
        corrected = (corrected - 0.5f) * contrast + 0.5f;
        corrected = Math.Clamp(corrected, 0f, 1f);
        corrected = MathF.Pow(corrected, 1f / gamma);
        return (byte)Math.Clamp(corrected * 255f, 0f, 255f);
    }
}
