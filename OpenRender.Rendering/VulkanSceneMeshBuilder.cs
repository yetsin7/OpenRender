using System.Numerics;
using OpenRender.Scene;

namespace OpenRender.Rendering;

internal static class VulkanSceneMeshBuilder
{
    private const int MaxTriangleBudget = 1_500_000;
    private const int MaxMeshCount = 512;

    public static IReadOnlyList<SceneUploadMesh> Build(Scene3D scene)
    {
        var candidates = scene.GetAllNodes()
            .Where(node => node.IsVisible && node.Mesh?.Data != null && node.Mesh.Data.Indices.Length >= 3)
            .Select(node => new MeshCandidate(node, node.Mesh!.Data!, node.Mesh.TriangleCount))
            .OrderByDescending(candidate => candidate.TriangleCount)
            .Take(MaxMeshCount)
            .ToList();

        if (candidates.Count == 0)
            return Array.Empty<SceneUploadMesh>();

        var uploads = new List<SceneUploadMesh>(candidates.Count);
        int trianglesLeft = MaxTriangleBudget;

        foreach (var candidate in candidates)
        {
            if (trianglesLeft <= 0)
                break;

            int triangleLimit = Math.Min(candidate.TriangleCount, trianglesLeft);
            var upload = BuildMesh(candidate.Node, candidate.Mesh, triangleLimit);
            if (upload.Vertices.Length == 0 || upload.Indices.Length == 0)
                continue;

            uploads.Add(upload);
            trianglesLeft -= upload.Indices.Length / 3;
        }

        return uploads;
    }

    private static SceneUploadMesh BuildMesh(SceneNode node, MeshData mesh, int triangleLimit)
    {
        int vertexCount = mesh.Vertices.Length / 3;
        if (vertexCount == 0)
            return default;

        var transform = BuildTransform(node.Transform);
        var normalMatrix = Matrix4x4.Transpose(Matrix4x4.Invert(transform, out var inverseTransform) ? inverseTransform : Matrix4x4.Identity);
        var vertices = new Vertex[vertexCount];

        for (int index = 0; index < vertexCount; index++)
        {
            int vertexOffset = index * 3;
            Vector3 position = new(mesh.Vertices[vertexOffset], mesh.Vertices[vertexOffset + 1], mesh.Vertices[vertexOffset + 2]);
            Vector3 normal = ReadVector3(mesh.Normals, vertexOffset, Vector3.UnitY);
            Vector2 texCoord = ReadVector2(mesh.TexCoords, index * 2, Vector2.Zero);

            position = Vector3.Transform(position, transform);
            normal = Vector3.Normalize(Vector3.TransformNormal(normal, normalMatrix));
            if (!IsFinite(normal) || normal.LengthSquared() < 0.0001f)
                normal = Vector3.UnitY;

            vertices[index] = new Vertex
            {
                Position = position,
                Normal = normal,
                TexCoord = texCoord,
                Tangent = BuildTangent(normal)
            };
        }

        int indexCount = Math.Min(mesh.Indices.Length, triangleLimit * 3);
        indexCount -= indexCount % 3;
        if (indexCount <= 0)
            return default;

        var indices = new uint[indexCount];
        Array.Copy(mesh.Indices, indices, indexCount);
        return new SceneUploadMesh(vertices, indices);
    }

    private static Matrix4x4 BuildTransform(TransformComponent transform)
    {
        Vector3 radians = transform.Rotation * (MathF.PI / 180f);
        return
            Matrix4x4.CreateScale(transform.Scale) *
            Matrix4x4.CreateFromYawPitchRoll(radians.Y, radians.X, radians.Z) *
            Matrix4x4.CreateTranslation(transform.Position);
    }

    private static Vector3 ReadVector3(float[] values, int offset, Vector3 fallback)
    {
        if (offset + 2 >= values.Length)
            return fallback;

        return new Vector3(values[offset], values[offset + 1], values[offset + 2]);
    }

    private static Vector2 ReadVector2(float[] values, int offset, Vector2 fallback)
    {
        if (offset + 1 >= values.Length)
            return fallback;

        return new Vector2(values[offset], values[offset + 1]);
    }

    private static Vector3 BuildTangent(Vector3 normal)
    {
        Vector3 axis = MathF.Abs(normal.Y) > 0.92f ? Vector3.UnitX : Vector3.UnitY;
        Vector3 tangent = Vector3.Normalize(Vector3.Cross(axis, normal));
        return IsFinite(tangent) && tangent.LengthSquared() > 0.0001f ? tangent : Vector3.UnitX;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);

    private readonly record struct MeshCandidate(SceneNode Node, MeshData Mesh, int TriangleCount);
}

internal readonly record struct SceneUploadMesh(Vertex[] Vertices, uint[] Indices);
