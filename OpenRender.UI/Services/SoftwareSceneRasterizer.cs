using System.Numerics;
using OpenRender.Materials;
using OpenRender.Rendering;
using OpenRender.Scene;

namespace OpenRender.Services;

/// <summary>
/// Rasteriza la geometría real de la escena para el viewport y para las capturas
/// exportadas cuando el backend final aún no está disponible.
/// </summary>
public sealed class SoftwareSceneRasterizer
{
    private Scene3D? _scene;
    private SceneGeometryCache? _cache;

    public void SetScene(Scene3D scene)
    {
        if (ReferenceEquals(_scene, scene))
            return;

        _scene = scene;
        _cache = null;
    }

    public SoftwareSceneFrame Render(
        Scene3D scene,
        CameraComponent camera,
        int requestedWidth,
        int requestedHeight,
        RenderQuality quality,
        SceneNode? selectedNode = null,
        bool drawGrid = true)
    {
        SetScene(scene);
        _cache ??= SceneGeometryCache.Create(scene, quality);

        int width = Math.Clamp(requestedWidth, 320, 7680);
        int height = Math.Clamp(requestedHeight, 220, 4320);
        int superSampling = quality >= RenderQuality.High ? 2 : 1;
        int renderWidth = width * superSampling;
        int renderHeight = height * superSampling;

        int[] pixels = new int[renderWidth * renderHeight];
        float[] depth = new float[pixels.Length];
        Array.Fill(depth, float.MaxValue);

        var tone = ToneSettings.Create(scene);
        FillBackground(pixels, renderWidth, renderHeight, scene.BackgroundColor, tone);
        var viewProjection = BuildViewProjection(camera, renderWidth, renderHeight);
        if (drawGrid)
            DrawGrid(pixels, renderWidth, renderHeight, viewProjection, tone);

        DrawTriangles(pixels, depth, renderWidth, renderHeight, scene, camera, viewProjection, tone, _cache, selectedNode);
        ViewportPreviewPostProcessor.SealForeground(pixels, depth, renderWidth, renderHeight);
        ViewportPreviewPostProcessor.SoftenHighlights(pixels, renderWidth, renderHeight);

        if (superSampling > 1)
            pixels = Downsample(pixels, renderWidth, renderHeight, superSampling, out renderWidth, out renderHeight);

        return new SoftwareSceneFrame(renderWidth, renderHeight, pixels);
    }

    private static void DrawTriangles(
        int[] pixels,
        float[] depth,
        int width,
        int height,
        Scene3D scene,
        CameraComponent camera,
        Matrix4x4 viewProjection,
        ToneSettings tone,
        SceneGeometryCache cache,
        SceneNode? selectedNode)
    {
        Vector3 lightDirection = ResolveLightDirection(scene);
        float ambient = Math.Clamp(scene.AmbientIntensity * 0.75f + 0.18f, 0.14f, 0.48f);
        var opaque = new List<RasterTriangle>(cache.Triangles.Count);
        var transparent = new List<RasterTriangle>(64);

        foreach (var triangle in cache.Triangles)
        {
            if (!triangle.Node.IsVisible)
                continue;

            Vector3 worldA = triangle.A + triangle.Node.Position;
            Vector3 worldB = triangle.B + triangle.Node.Position;
            Vector3 worldC = triangle.C + triangle.Node.Position;
            var material = ResolveMaterial(scene, triangle.Node);

            if ((material?.Opacity ?? 1f) >= 0.98f)
            {
                Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(worldB - worldA, worldC - worldA));
                Vector3 toCamera = camera.Position - ((worldA + worldB + worldC) / 3f);
                if (faceNormal.LengthSquared() > 0.000001f && Vector3.Dot(faceNormal, toCamera) <= 0f)
                    continue;
            }

            if (!TryProject(worldA, triangle.Na, viewProjection, width, height, out var a) ||
                !TryProject(worldB, triangle.Nb, viewProjection, width, height, out var b) ||
                !TryProject(worldC, triangle.Nc, viewProjection, width, height, out var c))
            {
                continue;
            }

            float area = Edge(a.X, a.Y, b.X, b.Y, c.X, c.Y);
            if (MathF.Abs(area) < 1f)
                continue;

            var raster = new RasterTriangle(a, b, c, (a.Depth + b.Depth + c.Depth) / 3f, material, ReferenceEquals(triangle.Node, selectedNode));
            if ((material?.Opacity ?? 1f) < 0.98f)
                transparent.Add(raster);
            else
                opaque.Add(raster);
        }

        foreach (var triangle in opaque)
            FillTriangle(pixels, depth, width, height, triangle, camera.Position, lightDirection, ambient, tone, cache.SceneBounds);

        transparent.Sort(static (left, right) => right.Depth.CompareTo(left.Depth));
        foreach (var triangle in transparent)
            FillTriangle(pixels, depth, width, height, triangle, camera.Position, lightDirection, ambient, tone, cache.SceneBounds, blend: true);
    }

    private static void FillTriangle(
        int[] pixels,
        float[] depth,
        int width,
        int height,
        RasterTriangle triangle,
        Vector3 cameraPosition,
        Vector3 lightDirection,
        float ambient,
        ToneSettings tone,
        SceneBounds sceneBounds,
        bool blend = false)
    {
        var a = triangle.A;
        var b = triangle.B;
        var c = triangle.C;
        float area = Edge(a.X, a.Y, b.X, b.Y, c.X, c.Y);
        if (area < 0f)
        {
            (b, c) = (c, b);
            area = -area;
        }

        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float w0 = Edge(b.X, b.Y, c.X, c.Y, px, py);
                float w1 = Edge(c.X, c.Y, a.X, a.Y, px, py);
                float w2 = Edge(a.X, a.Y, b.X, b.Y, px, py);
                if (w0 < 0f || w1 < 0f || w2 < 0f)
                    continue;

                w0 /= area;
                w1 /= area;
                w2 /= area;
                float fragmentDepth = a.Depth * w0 + b.Depth * w1 + c.Depth * w2;
                int index = y * width + x;

                if (!blend && fragmentDepth >= depth[index])
                    continue;
                if (blend && fragmentDepth > depth[index] + 0.0008f)
                    continue;

                Vector3 world = a.World * w0 + b.World * w1 + c.World * w2;
                Vector3 normal = Vector3.Normalize(a.Normal * w0 + b.Normal * w1 + c.Normal * w2);
                Vector3 shaded = Shade(triangle.Material, world, normal, cameraPosition, lightDirection, ambient, tone, sceneBounds, triangle.IsSelected);

                if (blend)
                    pixels[index] = BlendOver(pixels[index], ToBgra(shaded), Math.Clamp(triangle.Material?.Opacity ?? 0.55f, 0.18f, 0.88f));
                else
                    pixels[index] = ToBgra(shaded);

                depth[index] = MathF.Min(depth[index], fragmentDepth);
            }
        }
    }

    private static Vector3 Shade(PbrMaterial? material, Vector3 world, Vector3 normal, Vector3 cameraPosition, Vector3 lightDirection, float ambient, ToneSettings tone, SceneBounds sceneBounds, bool isSelected)
    {
        Vector3 albedo = material?.Albedo ?? new Vector3(0.80f, 0.80f, 0.78f);
        float roughness = Math.Clamp(material?.Roughness ?? 0.72f, 0.04f, 1f);
        float metallic = Math.Clamp(material?.Metallic ?? 0.03f, 0f, 1f);
        float opacity = Math.Clamp(material?.Opacity ?? 1f, 0.12f, 1f);
        float ao = Math.Clamp(material?.AmbientOcclusion ?? 1f, 0.4f, 1f);

        if (opacity < 0.98f)
            albedo = Mix(albedo, new Vector3(0.63f, 0.82f, 0.94f), 0.30f);

        Vector3 viewDirection = Vector3.Normalize(cameraPosition - world);
        float ndotl = MathF.Max(0f, Vector3.Dot(normal, -lightDirection));
        float ndotv = MathF.Max(0f, Vector3.Dot(normal, viewDirection));
        float horizon = normal.Y * 0.5f + 0.5f;
        float bounce = MathF.Max(0f, -normal.Y) * 0.10f;
        float fresnel = MathF.Pow(1f - ndotv, 5f);

        Vector3 diffuse = albedo * (ambient * 0.72f + horizon * 0.24f + ndotl * 0.92f + bounce);
        Vector3 halfVector = Vector3.Normalize(viewDirection - lightDirection);
        float specularPower = Lerp(12f, 110f, 1f - roughness);
        float specularStrength = (0.05f + metallic * 0.35f + (1f - roughness) * 0.10f) * MathF.Pow(MathF.Max(0f, Vector3.Dot(normal, halfVector)), specularPower);
        Vector3 color = diffuse * ao + new Vector3(specularStrength) + fresnel * new Vector3(0.04f, 0.05f, 0.06f);

        float groundContact = Math.Clamp((world.Y - sceneBounds.Min.Y) / MathF.Max(sceneBounds.Size.Y, 1f), 0f, 1f);
        color *= 0.88f + groundContact * 0.12f;

        float fog = Math.Clamp((Vector3.Distance(world, cameraPosition) - sceneBounds.Radius * 1.2f) / (sceneBounds.Radius * 5.5f), 0f, 0.22f);
        color = Mix(color, tone.SkyFog, fog);

        if (isSelected)
            color = Mix(color, new Vector3(0.46f, 0.86f, 1.0f), 0.16f);

        color = ApplyWhiteBalance(color, tone.WhiteBalanceBias);
        color *= tone.Exposure;
        color = color / (Vector3.One + color);
        color = Vector3.Clamp((color - new Vector3(0.5f)) * tone.Contrast + new Vector3(0.5f), Vector3.Zero, Vector3.One);
        color = Pow(color, 1f / tone.Gamma);
        return Vector3.Clamp(color, Vector3.Zero, Vector3.One);
    }

    private static void FillBackground(int[] pixels, int width, int height, Vector3 background, ToneSettings tone)
    {
        Vector3 skyTop = Mix(background, new Vector3(0.09f, 0.17f, 0.28f), 0.36f);
        Vector3 skyBottom = Mix(background, new Vector3(0.82f, 0.88f, 0.92f), 0.68f);
        Vector3 groundNear = new Vector3(0.18f, 0.27f, 0.20f);
        Vector3 groundFar = new Vector3(0.59f, 0.68f, 0.44f);
        int horizon = (int)(height * 0.54f);

        for (int y = 0; y < height; y++)
        {
            bool isSky = y < horizon;
            float t = isSky
                ? y / MathF.Max(1f, horizon)
                : (y - horizon) / MathF.Max(1f, height - horizon);
            Vector3 color = isSky
                ? Mix(skyTop, skyBottom, Smooth(t))
                : Mix(groundFar, groundNear, Smooth(t));

            int packed = ToBgra(PostColor(color, tone));
            int start = y * width;
            for (int x = 0; x < width; x++)
                pixels[start + x] = packed;
        }
    }

    private static void DrawGrid(int[] pixels, int width, int height, Matrix4x4 viewProjection, ToneSettings tone)
    {
        int primary = ToBgra(PostColor(new Vector3(0.28f, 0.42f, 0.48f), tone));
        int secondary = ToBgra(PostColor(new Vector3(0.19f, 0.25f, 0.30f), tone));
        for (int line = -12; line <= 12; line++)
        {
            DrawProjectedSegment(pixels, width, height, viewProjection, new Vector3(line * 2f, 0f, -24f), new Vector3(line * 2f, 0f, 24f), secondary);
            DrawProjectedSegment(pixels, width, height, viewProjection, new Vector3(-24f, 0f, line * 2f), new Vector3(24f, 0f, line * 2f), secondary);
        }

        DrawProjectedSegment(pixels, width, height, viewProjection, new Vector3(0f, 0f, -24f), new Vector3(0f, 0f, 24f), primary);
        DrawProjectedSegment(pixels, width, height, viewProjection, new Vector3(-24f, 0f, 0f), new Vector3(24f, 0f, 0f), primary);
    }

    private static Matrix4x4 BuildViewProjection(CameraComponent camera, int width, int height)
    {
        float aspect = MathF.Max(0.1f, width / (float)Math.Max(1, height));
        float near = MathF.Max(0.01f, camera.NearPlane);
        float far = MathF.Max(near + 20f, camera.FarPlane);
        Vector3 viewDirection = Vector3.Normalize(camera.Target - camera.Position);
        Vector3 up = MathF.Abs(Vector3.Dot(viewDirection, Vector3.UnitY)) > 0.98f ? -Vector3.UnitZ : Vector3.UnitY;
        return Matrix4x4.CreateLookAt(camera.Position, camera.Target, up) *
               Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * camera.FieldOfView / 180f, aspect, near, far);
    }

    private static bool TryProject(Vector3 world, Vector3 normal, Matrix4x4 viewProjection, int width, int height, out RasterVertex vertex)
    {
        Vector4 clip = Vector4.Transform(new Vector4(world, 1f), viewProjection);
        if (!float.IsFinite(clip.X) || !float.IsFinite(clip.Y) || !float.IsFinite(clip.Z) || clip.W <= 0.001f)
        {
            vertex = default;
            return false;
        }

        float invW = 1f / clip.W;
        float ndcX = clip.X * invW;
        float ndcY = clip.Y * invW;
        float ndcZ = clip.Z * invW;
        if (ndcX < -1.3f || ndcX > 1.3f || ndcY < -1.3f || ndcY > 1.3f || ndcZ < -0.2f || ndcZ > 1.2f)
        {
            vertex = default;
            return false;
        }

        vertex = new RasterVertex(
            (ndcX * 0.5f + 0.5f) * (width - 1),
            (0.5f - ndcY * 0.5f) * (height - 1),
            ndcZ,
            world,
            normal);
        return true;
    }

    private static void DrawProjectedSegment(int[] pixels, int width, int height, Matrix4x4 viewProjection, Vector3 start, Vector3 end, int color)
    {
        if (!TryProject(start, Vector3.UnitY, viewProjection, width, height, out var a) ||
            !TryProject(end, Vector3.UnitY, viewProjection, width, height, out var b))
        {
            return;
        }

        DrawLine(pixels, width, height, (int)a.X, (int)a.Y, (int)b.X, (int)b.Y, color);
    }

    private static int[] Downsample(int[] pixels, int width, int height, int factor, out int targetWidth, out int targetHeight)
    {
        targetWidth = width / factor;
        targetHeight = height / factor;
        int[] output = new int[targetWidth * targetHeight];

        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                int r = 0;
                int g = 0;
                int b = 0;
                for (int oy = 0; oy < factor; oy++)
                {
                    for (int ox = 0; ox < factor; ox++)
                    {
                        int color = pixels[(y * factor + oy) * width + (x * factor + ox)];
                        b += color & 0xFF;
                        g += (color >> 8) & 0xFF;
                        r += (color >> 16) & 0xFF;
                    }
                }

                int samples = factor * factor;
                output[y * targetWidth + x] = unchecked((int)(0xFF000000u | ((uint)(r / samples) << 16) | ((uint)(g / samples) << 8) | (uint)(b / samples)));
            }
        }

        return output;
    }

    private static Vector3 ResolveLightDirection(Scene3D scene)
    {
        var sun = scene.Lights.FirstOrDefault(light => light.Type == LightType.Directional && light.IsEnabled);
        Vector3 direction = sun?.Direction ?? new Vector3(-0.35f, -1f, -0.2f);
        return direction.LengthSquared() > 0.0001f ? Vector3.Normalize(direction) : Vector3.Normalize(new Vector3(-0.35f, -1f, -0.2f));
    }

    private static PbrMaterial? ResolveMaterial(Scene3D scene, SceneNode node) =>
        node.MaterialIndex is int materialIndex && materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;

    private static int BlendOver(int background, int foreground, float alpha)
    {
        int br = (background >> 16) & 0xFF;
        int bg = (background >> 8) & 0xFF;
        int bb = background & 0xFF;
        int fr = (foreground >> 16) & 0xFF;
        int fg = (foreground >> 8) & 0xFF;
        int fb = foreground & 0xFF;
        int r = (int)Math.Clamp(fr * alpha + br * (1f - alpha), 0f, 255f);
        int g = (int)Math.Clamp(fg * alpha + bg * (1f - alpha), 0f, 255f);
        int b = (int)Math.Clamp(fb * alpha + bb * (1f - alpha), 0f, 255f);
        return unchecked((int)(0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b));
    }

    private static int ToBgra(Vector3 color)
    {
        int r = (int)Math.Clamp(color.X * 255f, 0f, 255f);
        int g = (int)Math.Clamp(color.Y * 255f, 0f, 255f);
        int b = (int)Math.Clamp(color.Z * 255f, 0f, 255f);
        return unchecked((int)(0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b));
    }

    private static Vector3 PostColor(Vector3 color, ToneSettings tone)
    {
        color = ApplyWhiteBalance(color * MathF.Max(0.6f, tone.Exposure * 0.72f), tone.WhiteBalanceBias);
        color = Vector3.Clamp((color - new Vector3(0.5f)) * tone.Contrast + new Vector3(0.5f), Vector3.Zero, Vector3.One);
        return Pow(color, 1f / tone.Gamma);
    }

    private static Vector3 ApplyWhiteBalance(Vector3 color, float bias) =>
        new(
            Math.Clamp(color.X * (1f + bias * 0.12f), 0f, 4f),
            Math.Clamp(color.Y, 0f, 4f),
            Math.Clamp(color.Z * (1f - bias * 0.16f), 0f, 4f));

    private static Vector3 Pow(Vector3 value, float exponent) =>
        new(MathF.Pow(value.X, exponent), MathF.Pow(value.Y, exponent), MathF.Pow(value.Z, exponent));

    private static float Edge(float ax, float ay, float bx, float by, float px, float py) => (px - ax) * (by - ay) - (py - ay) * (bx - ax);

    private static float Smooth(float t) => t * t * (3f - 2f * t);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static Vector3 Mix(Vector3 a, Vector3 b, float t) => a * (1f - t) + b * t;

    private static void DrawLine(int[] pixels, int width, int height, int x0, int y0, int x1, int y1, int color)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            if ((uint)x0 < width && (uint)y0 < height)
                pixels[y0 * width + x0] = color;
            if (x0 == x1 && y0 == y1)
                break;

            int doubled = error * 2;
            if (doubled >= dy) { error += dy; x0 += sx; }
            if (doubled <= dx) { error += dx; y0 += sy; }
        }
    }

    private readonly record struct RasterVertex(float X, float Y, float Depth, Vector3 World, Vector3 Normal);
    private readonly record struct RasterTriangle(RasterVertex A, RasterVertex B, RasterVertex C, float Depth, PbrMaterial? Material, bool IsSelected);

    private readonly record struct ToneSettings(float Exposure, float Gamma, float Contrast, float WhiteBalanceBias, Vector3 SkyFog)
    {
        public static ToneSettings Create(Scene3D scene)
        {
            float whiteBalance = scene.WhiteBalance;
            float bias = MathF.Abs(whiteBalance) > 100f
                ? Math.Clamp((6500f - whiteBalance) / 4000f, -0.4f, 0.4f)
                : Math.Clamp(whiteBalance, -0.4f, 0.4f);

            return new ToneSettings(
                Math.Clamp(scene.Exposure <= 0 ? 1f : scene.Exposure, 0.35f, 3.2f),
                Math.Clamp(scene.Gamma <= 0 ? 2.2f : scene.Gamma, 1.4f, 2.8f),
                Math.Clamp(scene.Contrast <= 0 ? 1f : scene.Contrast, 0.75f, 1.45f),
                bias,
                Mix(scene.BackgroundColor, new Vector3(0.84f, 0.90f, 0.95f), 0.55f));
        }
    }
}

public sealed record SoftwareSceneFrame(int Width, int Height, int[] Pixels);
