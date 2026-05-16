using OpenRender.Core.Scene;

namespace OpenRender.Rendering.Primitives;

/// <summary>
/// Generates primitive mesh data for testing and default scene objects.
/// </summary>
public static class PrimitiveGenerator
{
    /// <summary>
    /// Creates a unit cube mesh centered at origin.
    /// </summary>
    public static MeshData CreateCube(float size = 1.0f)
    {
        float h = size / 2f;

        // 24 vertices (4 per face, each with unique normal)
        float[] vertices =
        {
            // Front face
            -h, -h,  h,   h, -h,  h,   h,  h,  h,  -h,  h,  h,
            // Back face
             h, -h, -h,  -h, -h, -h,  -h,  h, -h,   h,  h, -h,
            // Top face
            -h,  h,  h,   h,  h,  h,   h,  h, -h,  -h,  h, -h,
            // Bottom face
            -h, -h, -h,   h, -h, -h,   h, -h,  h,  -h, -h,  h,
            // Right face
             h, -h,  h,   h, -h, -h,   h,  h, -h,   h,  h,  h,
            // Left face
            -h, -h, -h,  -h, -h,  h,  -h,  h,  h,  -h,  h, -h,
        };

        float[] normals =
        {
            // Front
            0,0,1, 0,0,1, 0,0,1, 0,0,1,
            // Back
            0,0,-1, 0,0,-1, 0,0,-1, 0,0,-1,
            // Top
            0,1,0, 0,1,0, 0,1,0, 0,1,0,
            // Bottom
            0,-1,0, 0,-1,0, 0,-1,0, 0,-1,0,
            // Right
            1,0,0, 1,0,0, 1,0,0, 1,0,0,
            // Left
            -1,0,0, -1,0,0, -1,0,0, -1,0,0,
        };

        float[] texCoords =
        {
            0,0, 1,0, 1,1, 0,1,
            0,0, 1,0, 1,1, 0,1,
            0,0, 1,0, 1,1, 0,1,
            0,0, 1,0, 1,1, 0,1,
            0,0, 1,0, 1,1, 0,1,
            0,0, 1,0, 1,1, 0,1,
        };

        uint[] indices =
        {
            0,1,2, 0,2,3,       // Front
            4,5,6, 4,6,7,       // Back
            8,9,10, 8,10,11,    // Top
            12,13,14, 12,14,15, // Bottom
            16,17,18, 16,18,19, // Right
            20,21,22, 20,22,23, // Left
        };

        return new MeshData
        {
            Name = "Cube",
            Vertices = vertices,
            Normals = normals,
            TexCoords = texCoords,
            Indices = indices
        };
    }

    /// <summary>
    /// Creates a ground plane mesh.
    /// </summary>
    public static MeshData CreatePlane(float size = 20f)
    {
        float h = size / 2f;

        float[] vertices = { -h, 0, -h, h, 0, -h, h, 0, h, -h, 0, h };
        float[] normals = { 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0 };
        float[] texCoords = { 0, 0, size, 0, size, size, 0, size };
        uint[] indices = { 0, 1, 2, 0, 2, 3 };

        return new MeshData
        {
            Name = "Ground Plane",
            Vertices = vertices,
            Normals = normals,
            TexCoords = texCoords,
            Indices = indices
        };
    }

    /// <summary>
    /// Creates a grid mesh for the viewport ground reference.
    /// </summary>
    public static MeshData CreateGrid(int divisions = 20, float size = 1.0f)
    {
        float half = divisions * size / 2f;
        var vertices = new List<float>();
        var indices = new List<uint>();
        uint idx = 0;

        for (int i = 0; i <= divisions; i++)
        {
            float pos = -half + i * size;

            // Line along Z
            vertices.AddRange(new[] { pos, 0, -half });
            vertices.AddRange(new[] { pos, 0, half });
            indices.Add(idx++);
            indices.Add(idx++);

            // Line along X
            vertices.AddRange(new[] { -half, 0, pos });
            vertices.AddRange(new[] { half, 0, pos });
            indices.Add(idx++);
            indices.Add(idx++);
        }

        return new MeshData
        {
            Name = "Grid",
            Vertices = vertices.ToArray(),
            Normals = Array.Empty<float>(),
            TexCoords = Array.Empty<float>(),
            Indices = indices.ToArray()
        };
    }

    /// <summary>
    /// Creates a simple architectural box (building block).
    /// </summary>
    public static MeshData CreateArchBox(float width, float height, float depth)
    {
        float hw = width / 2f;
        float hd = depth / 2f;

        float[] vertices =
        {
            // Front
            -hw, 0, hd,   hw, 0, hd,   hw, height, hd,  -hw, height, hd,
            // Back
            hw, 0, -hd,  -hw, 0, -hd,  -hw, height, -hd,  hw, height, -hd,
            // Top
            -hw, height, hd,   hw, height, hd,   hw, height, -hd,  -hw, height, -hd,
            // Bottom
            -hw, 0, -hd,   hw, 0, -hd,   hw, 0, hd,  -hw, 0, hd,
            // Right
            hw, 0, hd,   hw, 0, -hd,   hw, height, -hd,   hw, height, hd,
            // Left
            -hw, 0, -hd,  -hw, 0, hd,  -hw, height, hd,  -hw, height, -hd,
        };

        float[] normals =
        {
            0,0,1, 0,0,1, 0,0,1, 0,0,1,
            0,0,-1, 0,0,-1, 0,0,-1, 0,0,-1,
            0,1,0, 0,1,0, 0,1,0, 0,1,0,
            0,-1,0, 0,-1,0, 0,-1,0, 0,-1,0,
            1,0,0, 1,0,0, 1,0,0, 1,0,0,
            -1,0,0, -1,0,0, -1,0,0, -1,0,0,
        };

        float[] texCoords =
        {
            0,0, 1,0, 1,1, 0,1,
            0,0, 1,0, 1,1, 0,1,
            0,0, 1,0, 1,1, 0,1,
            0,0, 1,0, 1,1, 0,1,
            0,0, 1,0, 1,1, 0,1,
            0,0, 1,0, 1,1, 0,1,
        };

        uint[] indices =
        {
            0,1,2, 0,2,3,
            4,5,6, 4,6,7,
            8,9,10, 8,10,11,
            12,13,14, 12,14,15,
            16,17,18, 16,18,19,
            20,21,22, 20,22,23,
        };

        return new MeshData
        {
            Name = "ArchBox",
            Vertices = vertices,
            Normals = normals,
            TexCoords = texCoords,
            Indices = indices
        };
    }
}
