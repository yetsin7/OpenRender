using OpenRender.Scene;

namespace OpenRender.Rendering;

public unsafe partial class VulkanRenderer
{
    public void LoadScene(Scene3D scene)
    {
        ClearSceneMeshes();

        foreach (var upload in VulkanSceneMeshBuilder.Build(scene))
            _meshes.Add(new GpuMesh(_context, upload.Vertices, upload.Indices));
    }

    public void ClearSceneMeshes()
    {
        foreach (var mesh in _meshes)
            mesh.Dispose();

        _meshes.Clear();
    }
}
