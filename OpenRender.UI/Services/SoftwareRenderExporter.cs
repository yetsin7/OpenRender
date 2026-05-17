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
    public static async Task ExportAsync(Scene3D scene, RenderSettings settings, string outputPath, OutputFormat format, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        int width = Math.Clamp(settings.Width, 640, 7680);
        int height = Math.Clamp(settings.Height, 360, 4320);

        using var image = new Image<Rgba32>(width, height);
        RenderScene(image, scene, settings, cancellationToken);

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

    private static void RenderScene(Image<Rgba32> image, Scene3D scene, RenderSettings settings, CancellationToken cancellationToken)
    {
        float exposure = Math.Clamp(scene.Exposure <= 0 ? settings.Exposure : scene.Exposure, 0.25f, 3.0f);
        float gamma = Math.Clamp(scene.Gamma <= 0 ? settings.Gamma : scene.Gamma, 1.2f, 3.0f);
        float contrast = Math.Clamp(scene.Contrast <= 0 ? 1.0f : scene.Contrast, 0.65f, 1.6f);

        FillAtmosphere(image, scene, exposure, gamma, contrast, cancellationToken);
        DrawSun(image, scene, exposure, gamma, contrast);
        DrawGroundGrid(image, exposure, gamma, contrast);
        DrawSceneMasses(image, scene, settings, exposure, gamma, contrast);
        DrawCameraSafeFrame(image);
    }

    private static void FillAtmosphere(Image<Rgba32> image, Scene3D scene, float exposure, float gamma, float contrast, CancellationToken cancellationToken)
    {
        int width = image.Width;
        int height = image.Height;
        int horizon = (int)(height * 0.52f);
        var skyTop = Mix(scene.BackgroundColor, new Vector3(0.40f, 0.62f, 0.86f), 0.55f);
        var skyBottom = Mix(scene.BackgroundColor, new Vector3(0.82f, 0.91f, 0.92f), 0.72f);
        var groundNear = new Vector3(0.18f, 0.32f, 0.19f);
        var groundFar = new Vector3(0.55f, 0.65f, 0.40f);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = accessor.GetRowSpan(y);
                bool sky = y < horizon;
                float t = sky ? y / Math.Max(1f, horizon) : (y - horizon) / Math.Max(1f, height - horizon);
                var color = sky
                    ? ToColor(Mix(skyTop, skyBottom, Smooth(t)), exposure, gamma, contrast)
                    : ToColor(Mix(groundFar, groundNear, Smooth(t)), exposure, gamma, contrast);

                for (int x = 0; x < width; x++)
                    row[x] = color;
            }
        });

        FillRectangle(image, 0, horizon - 4, width, 8, ToColor(new Vector3(0.82f, 0.88f, 0.78f), exposure, gamma, contrast), 0.32f);
    }

    private static void DrawSun(Image<Rgba32> image, Scene3D scene, float exposure, float gamma, float contrast)
    {
        var sun = scene.Lights.FirstOrDefault(light => light.Type == LightType.Directional && light.IsEnabled);
        float strength = Math.Clamp(sun?.Intensity ?? 1.2f, 0.25f, 3.2f);
        int radius = (int)(Math.Min(image.Width, image.Height) * (0.035f + strength * 0.006f));
        int x = (int)(image.Width * 0.78f);
        int y = (int)(image.Height * 0.15f);
        var glow = ToColor(new Vector3(1.0f, 0.89f, 0.52f), exposure, gamma, contrast);
        var core = ToColor(new Vector3(1.0f, 0.96f, 0.70f), exposure, gamma, contrast);

        FillCircle(image, x, y, radius * 3, glow, 0.10f);
        FillCircle(image, x, y, radius, core, 0.92f);
    }

    private static void DrawGroundGrid(Image<Rgba32> image, float exposure, float gamma, float contrast)
    {
        int width = image.Width;
        int height = image.Height;
        int centerX = width / 2;
        int horizon = (int)(height * 0.52f);
        var line = ToColor(new Vector3(0.88f, 0.94f, 0.82f), exposure, gamma, contrast);

        for (int index = -7; index <= 7; index++)
        {
            int x = centerX + index * width / 14;
            DrawLine(image, centerX, horizon, x, height, line, 0.16f);
        }

        for (int index = 1; index <= 9; index++)
        {
            float t = index / 9f;
            int y = horizon + (int)(Math.Pow(t, 1.8) * (height - horizon));
            int inset = (int)((1 - t) * width * 0.46f);
            DrawLine(image, inset, y, width - inset, y, line, 0.18f);
        }
    }

    private static void DrawSceneMasses(Image<Rgba32> image, Scene3D scene, RenderSettings settings, float exposure, float gamma, float contrast)
    {
        var nodes = scene.GetAllNodes()
            .Where(node => node.IsVisible && node.Mesh != null)
            .Select(node => new RenderNode(node, GetNodeBounds(node)))
            .Where(item => IsUsableBounds(item.Bounds.Min, item.Bounds.Max))
            .ToList();

        if (nodes.Count == 0)
        {
            DrawDemoMasses(image, exposure, gamma, contrast);
            return;
        }

        int maxNodes = settings.Quality switch
        {
            RenderQuality.Draft => 50,
            RenderQuality.Low => 70,
            RenderQuality.Medium => 110,
            RenderQuality.High => 170,
            _ => 240
        };

        nodes = nodes
            .OrderBy(item => item.Bounds.Center.X + item.Bounds.Center.Z)
            .Take(maxNodes)
            .ToList();

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var item in nodes)
        {
            min = Vector3.Min(min, item.Bounds.Min);
            max = Vector3.Max(max, item.Bounds.Max);
        }

        var center = (min + max) * 0.5f;
        var size = Vector3.Max(max - min, new Vector3(1f));
        float footprint = Math.Max(size.X + size.Z, 1f);
        float scale = Math.Min(image.Width * 0.46f / footprint, image.Height * 0.46f / Math.Max(size.Y + footprint * 0.22f, 1f));
        scale = Math.Clamp(scale, 0.8f, Math.Min(image.Width, image.Height) * 0.16f);

        foreach (var item in nodes)
        {
            var material = ResolveMaterial(scene, item.Node);
            var baseColor = material?.Albedo ?? new Vector3(0.78f, 0.78f, 0.72f);
            if (material?.Opacity < 0.65f)
                baseColor = Mix(baseColor, new Vector3(0.58f, 0.82f, 0.90f), 0.45f);

            DrawIsoBox(image, item.Bounds.Min, item.Bounds.Max, center, scale, baseColor, exposure, gamma, contrast);
        }
    }

    private static void DrawDemoMasses(Image<Rgba32> image, float exposure, float gamma, float contrast)
    {
        var center = Vector3.Zero;
        float scale = Math.Min(image.Width, image.Height) * 0.085f;
        DrawIsoBox(image, new Vector3(-2.2f, 0f, -0.9f), new Vector3(-0.7f, 1.4f, 0.9f), center, scale, new Vector3(0.74f, 0.78f, 0.72f), exposure, gamma, contrast);
        DrawIsoBox(image, new Vector3(-0.6f, 0f, -1.2f), new Vector3(1.4f, 2.1f, 1.0f), center, scale, new Vector3(0.93f, 0.91f, 0.84f), exposure, gamma, contrast);
        DrawIsoBox(image, new Vector3(1.5f, 0f, -0.7f), new Vector3(2.5f, 1.2f, 0.8f), center, scale, new Vector3(0.62f, 0.51f, 0.42f), exposure, gamma, contrast);
    }

    private static void DrawIsoBox(Image<Rgba32> image, Vector3 rawMin, Vector3 rawMax, Vector3 center, float scale, Vector3 baseColor, float exposure, float gamma, float contrast)
    {
        var min = rawMin;
        var max = rawMax;
        ExpandThinAxis(ref min.X, ref max.X);
        ExpandThinAxis(ref min.Y, ref max.Y);
        ExpandThinAxis(ref min.Z, ref max.Z);

        Vector2 p000 = Project(new Vector3(min.X, min.Y, min.Z), center, scale, image.Width, image.Height);
        Vector2 p100 = Project(new Vector3(max.X, min.Y, min.Z), center, scale, image.Width, image.Height);
        Vector2 p110 = Project(new Vector3(max.X, min.Y, max.Z), center, scale, image.Width, image.Height);
        Vector2 p010 = Project(new Vector3(min.X, min.Y, max.Z), center, scale, image.Width, image.Height);
        Vector2 p001 = Project(new Vector3(min.X, max.Y, min.Z), center, scale, image.Width, image.Height);
        Vector2 p101 = Project(new Vector3(max.X, max.Y, min.Z), center, scale, image.Width, image.Height);
        Vector2 p111 = Project(new Vector3(max.X, max.Y, max.Z), center, scale, image.Width, image.Height);
        Vector2 p011 = Project(new Vector3(min.X, max.Y, max.Z), center, scale, image.Width, image.Height);

        var shadow = ToColor(new Vector3(0.04f, 0.05f, 0.05f), exposure, gamma, contrast);
        FillPolygon(image, new[] { p000, p100, p110, p010 }, shadow, 0.20f);
        FillPolygon(image, new[] { p001, p011, p010, p000 }, ToColor(baseColor * 0.68f, exposure, gamma, contrast), 0.96f);
        FillPolygon(image, new[] { p101, p111, p110, p100 }, ToColor(baseColor * 0.86f, exposure, gamma, contrast), 0.98f);
        FillPolygon(image, new[] { p001, p101, p111, p011 }, ToColor(Vector3.Min(baseColor * 1.12f, Vector3.One), exposure, gamma, contrast), 1.0f);

        var edge = ToColor(new Vector3(0.12f, 0.16f, 0.16f), exposure, gamma, contrast);
        DrawLine(image, p001, p101, edge, 0.28f);
        DrawLine(image, p101, p111, edge, 0.26f);
        DrawLine(image, p111, p011, edge, 0.24f);
        DrawLine(image, p011, p001, edge, 0.24f);
        DrawLine(image, p001, p000, edge, 0.20f);
        DrawLine(image, p101, p100, edge, 0.20f);
        DrawLine(image, p111, p110, edge, 0.20f);
        DrawLine(image, p011, p010, edge, 0.20f);
    }

    private static Vector2 Project(Vector3 point, Vector3 center, float scale, int width, int height)
    {
        var translatedPoint = point - center;
        float x = width * 0.50f + (translatedPoint.X - translatedPoint.Z) * scale * 0.82f;
        float y = height * 0.59f + (translatedPoint.X + translatedPoint.Z) * scale * 0.25f - translatedPoint.Y * scale * 0.92f;
        return new Vector2(x, y);
    }

    private static SceneBounds GetNodeBounds(SceneNode node)
    {
        var (min, max) = node.Mesh!.ComputeBoundingBox();
        min += node.Position;
        max += node.Position;
        return new SceneBounds(min, max);
    }

    private static PbrMaterial? ResolveMaterial(Scene3D scene, SceneNode node)
    {
        if (node.MaterialIndex is not int materialIndex || materialIndex < 0 || materialIndex >= scene.Materials.Count)
            return null;

        return scene.Materials[materialIndex];
    }

    private static bool IsUsableBounds(Vector3 min, Vector3 max)
    {
        return IsFinite(min) && IsFinite(max) && Vector3.DistanceSquared(min, max) > 0.000001f;
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static void ExpandThinAxis(ref float min, ref float max)
    {
        if (Math.Abs(max - min) >= 0.04f)
            return;

        float center = (min + max) * 0.5f;
        min = center - 0.02f;
        max = center + 0.02f;
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

    private static Vector3 Mix(Vector3 a, Vector3 b, float t) => a * (1f - t) + b * t;
    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private sealed record RenderNode(SceneNode Node, SceneBounds Bounds);
    private readonly record struct SceneBounds(Vector3 Min, Vector3 Max)
    {
        public Vector3 Center => (Min + Max) * 0.5f;
    }
}
