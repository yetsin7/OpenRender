using System.Numerics;

namespace OpenRender.Core.Scene;

/// <summary>
/// Represents raw mesh geometry data with vertices, normals, 
/// texture coordinates, and indices for GPU rendering.
/// </summary>
public class MeshData
{
    /// <summary>
    /// Human-readable name for this mesh.
    /// </summary>
    public string Name { get; set; } = "Mesh";

    /// <summary>
    /// Vertex positions (3 floats per vertex: x, y, z).
    /// </summary>
    public float[] Vertices { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Vertex normals (3 floats per vertex: nx, ny, nz).
    /// </summary>
    public float[] Normals { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Texture coordinates (2 floats per vertex: u, v).
    /// </summary>
    public float[] TexCoords { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Triangle indices for indexed rendering.
    /// </summary>
    public uint[] Indices { get; set; } = Array.Empty<uint>();

    /// <summary>
    /// Gets the number of vertices in this mesh.
    /// </summary>
    public int VertexCount => Vertices.Length / 3;

    /// <summary>
    /// Gets the number of triangles in this mesh.
    /// </summary>
    public int TriangleCount => Indices.Length / 3;

    /// <summary>
    /// Computes the axis-aligned bounding box of this mesh.
    /// </summary>
    public (Vector3 Min, Vector3 Max) ComputeBoundingBox()
    {
        if (Vertices.Length == 0)
            return (Vector3.Zero, Vector3.Zero);

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        for (int i = 0; i < Vertices.Length; i += 3)
        {
            var v = new Vector3(Vertices[i], Vertices[i + 1], Vertices[i + 2]);
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        return (min, max);
    }

    /// <summary>
    /// Creates interleaved vertex data: [pos.x, pos.y, pos.z, norm.x, norm.y, norm.z, tex.u, tex.v]
    /// Suitable for GPU buffer upload.
    /// </summary>
    public float[] GetInterleavedData()
    {
        bool hasNormals = Normals.Length > 0;
        bool hasTexCoords = TexCoords.Length > 0;
        int stride = 3 + (hasNormals ? 3 : 0) + (hasTexCoords ? 2 : 0);
        var data = new float[VertexCount * stride];

        for (int i = 0; i < VertexCount; i++)
        {
            int offset = i * stride;
            data[offset] = Vertices[i * 3];
            data[offset + 1] = Vertices[i * 3 + 1];
            data[offset + 2] = Vertices[i * 3 + 2];

            int next = 3;
            if (hasNormals)
            {
                data[offset + next] = Normals[i * 3];
                data[offset + next + 1] = Normals[i * 3 + 1];
                data[offset + next + 2] = Normals[i * 3 + 2];
                next += 3;
            }

            if (hasTexCoords)
            {
                data[offset + next] = TexCoords[i * 2];
                data[offset + next + 1] = TexCoords[i * 2 + 1];
            }
        }

        return data;
    }
}
