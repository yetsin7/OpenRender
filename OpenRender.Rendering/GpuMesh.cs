using Silk.NET.OpenGL;
using OpenRender.Core.Scene;

namespace OpenRender.Rendering;

/// <summary>
/// Manages GPU buffers (VAO, VBO, EBO) for a single mesh.
/// Handles uploading mesh data to the GPU and drawing.
/// </summary>
public class GpuMesh : IDisposable
{
    private readonly GL _gl;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;
    private readonly uint _indexCount;
    private bool _disposed;

    /// <summary>
    /// Uploads mesh data to the GPU and creates vertex attribute layout.
    /// Layout: position (3f) + normal (3f) + texcoord (2f) = 8 floats per vertex.
    /// </summary>
    public unsafe GpuMesh(GL gl, MeshData meshData)
    {
        _gl = gl;

        float[] interleavedData = meshData.GetInterleavedData();
        uint[] indices = meshData.Indices;
        _indexCount = (uint)indices.Length;

        // Create and bind VAO
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        // Create and upload VBO
        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* data = interleavedData)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(interleavedData.Length * sizeof(float)),
                data, BufferUsageARB.StaticDraw);
        }

        // Create and upload EBO
        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* data = indices)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(indices.Length * sizeof(uint)),
                data, BufferUsageARB.StaticDraw);
        }

        bool hasNormals = meshData.Normals.Length > 0;
        bool hasTexCoords = meshData.TexCoords.Length > 0;
        uint stride = (uint)((3 + (hasNormals ? 3 : 0) + (hasTexCoords ? 2 : 0)) * sizeof(float));
        uint offset = 0;

        // Position attribute (location 0)
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)offset);
        _gl.EnableVertexAttribArray(0);
        offset += 3 * sizeof(float);

        // Normal attribute (location 1)
        if (hasNormals)
        {
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)offset);
            _gl.EnableVertexAttribArray(1);
            offset += 3 * sizeof(float);
        }

        // TexCoord attribute (location 2)
        if (hasTexCoords)
        {
            _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)offset);
            _gl.EnableVertexAttribArray(2);
        }

        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Draws this mesh using indexed rendering.
    /// </summary>
    public unsafe void Draw(PrimitiveType mode = PrimitiveType.Triangles)
    {
        _gl.BindVertexArray(_vao);
        _gl.DrawElements(mode, _indexCount, DrawElementsType.UnsignedInt, null);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteBuffer(_ebo);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
