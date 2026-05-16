using System.Numerics;
using Silk.NET.OpenGL;
using OpenRender.Core.Scene;
using OpenRender.Rendering.Shaders;

namespace OpenRender.Rendering;

/// <summary>
/// Main scene renderer using OpenGL via Silk.NET.
/// Handles rendering the 3D scene with PBR-like materials and lighting.
/// </summary>
public class SceneRenderer : IDisposable
{
    private readonly GL _gl;
    private ShaderProgram? _pbrShader;
    private ShaderProgram? _gridShader;
    private ViewCubeRenderer? _viewCube;
    private readonly Dictionary<string, GpuMesh> _meshCache = new();
    private GpuMesh? _gridMesh;
    private bool _disposed;

    /// <summary>
    /// Gets the ViewCube renderer instance.
    /// </summary>
    public ViewCubeRenderer? ViewCube => _viewCube;

    /// <summary>
    /// Whether to show the ground grid.
    /// </summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>
    /// Whether to show the Revit-style ViewCube overlay.
    /// </summary>
    public bool ShowViewCube { get; set; } = true;

    /// <summary>
    /// Background color for the viewport.
    /// </summary>
    public Vector3 BackgroundColor { get; set; } = new(1.0f, 1.0f, 1.0f);

    public SceneRenderer(GL gl)
    {
        _gl = gl;
    }

    /// <summary>
    /// Initializes shaders and GPU resources. Must be called once after GL context is ready.
    /// </summary>
    public void Initialize()
    {
        _pbrShader = new ShaderProgram(_gl, ShaderSources.VertexShader, ShaderSources.FragmentShader);
        _gridShader = new ShaderProgram(_gl, ShaderSources.GridVertexShader, ShaderSources.GridFragmentShader);
        _viewCube = new ViewCubeRenderer(_gl);
        _viewCube.Initialize();

        // Create grid mesh (large enough for architectural models)
        var gridData = Primitives.PrimitiveGenerator.CreateGrid(400, 10.0f);
        _gridMesh = new GpuMesh(_gl, gridData);

        // OpenGL state
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        
        // Disable culling and depth test for diagnostic
        _gl.Disable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
    }

    /// <summary>
    /// Renders the complete scene from the given camera's perspective.
    /// </summary>
    public void Render(Scene3D scene, int viewportWidth, int viewportHeight)
    {
        BackgroundColor = scene.BackgroundColor;
        _gl.Viewport(0, 0, (uint)viewportWidth, (uint)viewportHeight);
        _gl.ClearColor(BackgroundColor.X, BackgroundColor.Y, BackgroundColor.Z, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var camera = scene.Camera;
        camera.AspectRatio = (float)viewportWidth / viewportHeight;

        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix();

        // Render grid
        if (ShowGrid && _gridShader != null && _gridMesh != null)
        {
            RenderGrid(view, projection);
        }

        // Render scene objects
        if (_pbrShader != null)
        {
            RenderSceneNodes(scene, view, projection);
        }

        // Render View Cube overlay
        if (ShowViewCube)
            _viewCube?.Render(scene.Camera, viewportWidth, viewportHeight);
    }

    private void RenderGrid(Matrix4x4 view, Matrix4x4 projection)
    {
        _gl.Disable(EnableCap.CullFace);
        _gridShader!.Use();
        _gridShader.SetMat4("uView", view);
        _gridShader.SetMat4("uProjection", projection);
        _gridShader.SetVec3("uGridColor", 0.3f, 0.3f, 0.35f);
        _gridShader.SetFloat("uGridAlpha", 0.5f);

        // Draw grid as lines
        _gl.BindVertexArray(0); // Unbind any previous
        _gridMesh!.Draw(PrimitiveType.Lines);
        _gl.Enable(EnableCap.CullFace);
    }

    private void RenderSceneNodes(Scene3D scene, Matrix4x4 view, Matrix4x4 projection)
    {
        _pbrShader!.Use();
        _pbrShader.SetMat4("uView", view);
        _pbrShader.SetMat4("uProjection", projection);
        _pbrShader.SetVec3("uViewPos", scene.Camera.Position);
        _pbrShader.SetFloat("uExposure", 1.0f);
        _pbrShader.SetFloat("uGamma", 2.2f);

        // Set lighting (use first directional light or default sun)
        var sun = scene.Lights.FirstOrDefault(l => l.Type == LightType.Directional && l.IsEnabled);
        if (sun != null)
        {
            _pbrShader.SetVec3("uLightDir", sun.Direction);
            _pbrShader.SetVec3("uLightColor", sun.Color);
            _pbrShader.SetFloat("uLightIntensity", sun.Intensity);
        }
        else
        {
            _pbrShader.SetVec3("uLightDir", Vector3.Normalize(new(-0.3f, -1f, -0.4f)));
            _pbrShader.SetVec3("uLightColor", 1f, 0.96f, 0.9f);
            _pbrShader.SetFloat("uLightIntensity", 1.5f);
        }

        _pbrShader.SetVec3("uAmbientColor", 1.0f, 1.0f, 1.0f);
        _pbrShader.SetFloat("uAmbientIntensity", Math.Max(0.05f, scene.AmbientIntensity));

        // Traverse and render all nodes
        foreach (var node in scene.GetAllNodes())
        {
            if (!node.IsVisible || node.Mesh == null)
                continue;

            RenderNode(node, scene, Matrix4x4.Identity);
        }
    }

    private void RenderNode(SceneNode node, Scene3D scene, Matrix4x4 parentTransform)
    {
        var model = node.GetLocalTransform() * parentTransform;

        // Get or create GPU mesh
        var gpuMesh = GetOrCreateGpuMesh(node.Mesh!);

        // Set model matrix and normal matrix
        _pbrShader!.SetMat4("uModel", model);

        Matrix4x4.Invert(model, out var modelInverse);
        var normalMatrix = Matrix4x4.Transpose(modelInverse);
        _pbrShader.SetMat3("uNormalMatrix", normalMatrix);

        // Set material
        PbrMaterial material;
        if (node.MaterialIndex.HasValue && node.MaterialIndex.Value < scene.Materials.Count)
        {
            material = scene.Materials[node.MaterialIndex.Value];
        }
        else
        {
            material = PbrMaterial.Default;
        }

        _pbrShader.SetVec3("uAlbedo", material.Albedo);
        _pbrShader.SetFloat("uMetallic", material.Metallic);
        _pbrShader.SetFloat("uRoughness", material.Roughness);
        _pbrShader.SetFloat("uOpacity", material.Opacity);

        // Draw
        gpuMesh.Draw();
    }

    private GpuMesh GetOrCreateGpuMesh(MeshData meshData)
    {
        string key = meshData.Name + "_" + meshData.VertexCount;
        if (!_meshCache.TryGetValue(key, out var gpuMesh))
        {
            gpuMesh = new GpuMesh(_gl, meshData);
            _meshCache[key] = gpuMesh;
        }
        return gpuMesh;
    }

    /// <summary>
    /// Clears the GPU mesh cache (call when scene changes).
    /// </summary>
    public void ClearMeshCache()
    {
        foreach (var mesh in _meshCache.Values)
            mesh.Dispose();
        _meshCache.Clear();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            ClearMeshCache();
            _gridMesh?.Dispose();
            _pbrShader?.Dispose();
            _gridShader?.Dispose();
            _viewCube?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
