using System.Numerics;
using OpenRender.Materials;
using OpenRender.Rendering;
using OpenRender.Scene;

namespace OpenRender.Services;

internal sealed class SceneGeometryCache
{
    public required List<SceneTriangle> Triangles { get; init; }
    public required Dictionary<Guid, SceneBounds> NodeBounds { get; init; }
    public required SceneBounds SceneBounds { get; init; }

    public static SceneGeometryCache Create(Scene3D scene, RenderQuality quality)
    {
        var meshNodes = scene.GetAllNodes()
            .Where(node => node.Mesh?.Data != null)
            .ToList();

        int totalTriangles = meshNodes.Sum(GetTriangleCount);
        var sceneBounds = ComputeSceneBounds(meshNodes);
        var previewNodes = quality >= RenderQuality.High
            ? meshNodes
            : meshNodes
                .Where(node => ViewportPreviewNodeFilter.ShouldInclude(node, ResolveMaterial(scene, node), sceneBounds.Min, sceneBounds.Max))
                .ToList();

        if (previewNodes.Count == 0)
            previewNodes = meshNodes;

        int targetTriangles = ResolveTriangleBudget(totalTriangles, quality);
        var triangles = new List<SceneTriangle>(Math.Min(targetTriangles, Math.Max(512, totalTriangles)));
        var bounds = new Dictionary<Guid, SceneBounds>(meshNodes.Count);

        foreach (var node in meshNodes)
        {
            var (min, max) = node.Mesh!.ComputeBoundingBox();
            bounds[node.Id] = new SceneBounds(min, max);
        }

        foreach (var node in previewNodes)
        {
            var mesh = node.Mesh!.Data!;
            int triangleCount = GetTriangleCount(node);
            if (triangleCount == 0)
                continue;

            int targetForNode = Math.Max(12, (int)Math.Round(targetTriangles * (triangleCount / (double)Math.Max(1, totalTriangles))));
            CollectNodeTriangles(mesh, node, targetForNode, triangles);
        }

        return new SceneGeometryCache
        {
            Triangles = triangles,
            NodeBounds = bounds,
            SceneBounds = sceneBounds
        };
    }

    private static int ResolveTriangleBudget(int totalTriangles, RenderQuality quality)
    {
        int desired = quality switch
        {
            RenderQuality.Draft => 18_000,
            RenderQuality.Low => 40_000,
            RenderQuality.Medium => 90_000,
            RenderQuality.High => 260_000,
            _ => 420_000
        };

        if (totalTriangles <= desired)
            return Math.Max(totalTriangles, 1);

        return totalTriangles switch
        {
            > 3_000_000 => Math.Min(desired, quality >= RenderQuality.High ? 360_000 : 140_000),
            > 1_000_000 => Math.Min(desired, quality >= RenderQuality.High ? 300_000 : 160_000),
            > 250_000 => Math.Min(desired, quality >= RenderQuality.High ? 260_000 : 120_000),
            _ => desired
        };
    }

    private static SceneBounds ComputeSceneBounds(IReadOnlyList<SceneNode> nodes)
    {
        if (nodes.Count == 0)
            return new SceneBounds(new Vector3(-1f), new Vector3(1f));

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        foreach (var node in nodes)
        {
            var (nodeMin, nodeMax) = node.Mesh!.ComputeBoundingBox();
            min = Vector3.Min(min, nodeMin + node.Position);
            max = Vector3.Max(max, nodeMax + node.Position);
        }

        return new SceneBounds(min, max);
    }

    private static int GetTriangleCount(SceneNode node)
    {
        var mesh = node.Mesh!.Data!;
        return mesh.Indices.Length >= 3 ? mesh.Indices.Length / 3 : mesh.Vertices.Length / 9;
    }

    private static void CollectNodeTriangles(MeshData mesh, SceneNode node, int targetForNode, List<SceneTriangle> triangles)
    {
        int triangleCount = mesh.Indices.Length >= 3 ? mesh.Indices.Length / 3 : mesh.Vertices.Length / 9;
        if (triangleCount <= 0)
            return;

        if (triangleCount <= targetForNode)
        {
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
                AddTriangle(mesh, node, triangleIndex, triangles);
            return;
        }

        int bucketSize = Math.Max(1, (int)Math.Ceiling(triangleCount / (double)Math.Max(1, targetForNode)));
        for (int bucketStart = 0; bucketStart < triangleCount; bucketStart += bucketSize)
        {
            int bucketEnd = Math.Min(triangleCount, bucketStart + bucketSize);
            int bestTriangle = -1;
            float bestScore = float.MinValue;

            for (int triangleIndex = bucketStart; triangleIndex < bucketEnd; triangleIndex++)
            {
                if (!TryGetTriangle(mesh, triangleIndex, out var a, out var b, out var c, out _, out _, out _))
                    continue;

                float area = Vector3.Cross(b - a, c - a).LengthSquared();
                float centroidHeight = (a.Y + b.Y + c.Y) / 3f;
                float score = area + MathF.Abs(centroidHeight) * 0.01f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestTriangle = triangleIndex;
            }

            if (bestTriangle >= 0)
                AddTriangle(mesh, node, bestTriangle, triangles);
        }
    }

    private static void AddTriangle(MeshData mesh, SceneNode node, int triangleIndex, List<SceneTriangle> triangles)
    {
        if (!TryGetTriangle(mesh, triangleIndex, out var a, out var b, out var c, out var na, out var nb, out var nc))
            return;

        var faceNormal = Vector3.Cross(b - a, c - a);
        if (faceNormal.LengthSquared() < 0.000001f)
            return;

        faceNormal = Vector3.Normalize(faceNormal);
        na = NormalizeOrFallback(na, faceNormal);
        nb = NormalizeOrFallback(nb, faceNormal);
        nc = NormalizeOrFallback(nc, faceNormal);

        triangles.Add(new SceneTriangle(node, a, b, c, na, nb, nc));
    }

    private static bool TryGetTriangle(
        MeshData mesh,
        int triangleIndex,
        out Vector3 a,
        out Vector3 b,
        out Vector3 c,
        out Vector3 na,
        out Vector3 nb,
        out Vector3 nc)
    {
        if (mesh.Indices.Length >= 3)
        {
            int baseIndex = triangleIndex * 3;
            if (baseIndex + 2 >= mesh.Indices.Length)
            {
                a = b = c = na = nb = nc = default;
                return false;
            }

            int ia = (int)mesh.Indices[baseIndex];
            int ib = (int)mesh.Indices[baseIndex + 1];
            int ic = (int)mesh.Indices[baseIndex + 2];
            a = ReadVertex(mesh.Vertices, ia * 3);
            b = ReadVertex(mesh.Vertices, ib * 3);
            c = ReadVertex(mesh.Vertices, ic * 3);
            na = ReadNormal(mesh.Normals, ia * 3);
            nb = ReadNormal(mesh.Normals, ib * 3);
            nc = ReadNormal(mesh.Normals, ic * 3);
            return true;
        }

        int vertexOffset = triangleIndex * 9;
        if (vertexOffset + 8 >= mesh.Vertices.Length)
        {
            a = b = c = na = nb = nc = default;
            return false;
        }

        a = ReadVertex(mesh.Vertices, vertexOffset);
        b = ReadVertex(mesh.Vertices, vertexOffset + 3);
        c = ReadVertex(mesh.Vertices, vertexOffset + 6);
        na = ReadNormal(mesh.Normals, vertexOffset);
        nb = ReadNormal(mesh.Normals, vertexOffset + 3);
        nc = ReadNormal(mesh.Normals, vertexOffset + 6);
        return true;
    }

    private static Vector3 ReadVertex(float[] vertices, int index) => new(vertices[index], vertices[index + 1], vertices[index + 2]);

    private static Vector3 ReadNormal(float[] normals, int index)
    {
        if (normals.Length <= index + 2)
            return Vector3.Zero;

        return new Vector3(normals[index], normals[index + 1], normals[index + 2]);
    }

    private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        if (value.LengthSquared() < 0.000001f)
            return fallback;

        return Vector3.Normalize(value);
    }

    private static PbrMaterial? ResolveMaterial(Scene3D scene, SceneNode node) =>
        node.MaterialIndex is int materialIndex && materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
}

internal readonly record struct SceneTriangle(
    SceneNode Node,
    Vector3 A,
    Vector3 B,
    Vector3 C,
    Vector3 Na,
    Vector3 Nb,
    Vector3 Nc);

internal readonly record struct SceneBounds(Vector3 Min, Vector3 Max)
{
    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Size => Vector3.Max(Max - Min, new Vector3(0.001f));
    public float Radius => MathF.Max(Size.Length() * 0.5f, 0.5f);
}
