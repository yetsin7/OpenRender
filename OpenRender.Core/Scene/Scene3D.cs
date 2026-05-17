using System.Numerics;

namespace OpenRender.Core.Scene;

/// <summary>
/// Represents a complete 3D scene containing nodes, materials, and lights.
/// This is the root container for all scene data.
/// </summary>
public class Scene3D
{
    /// <summary>
    /// Name of the scene.
    /// </summary>
    public string Name { get; set; } = "Untitled Scene";

    /// <summary>
    /// Root nodes of the scene hierarchy.
    /// </summary>
    public List<SceneNode> RootNodes { get; } = new();

    /// <summary>
    /// Materials available in this scene.
    /// </summary>
    public List<PbrMaterial> Materials { get; } = new();

    /// <summary>
    /// Lights in the scene.
    /// </summary>
    public List<LightSource> Lights { get; } = new();

    /// <summary>
    /// The active camera for rendering.
    /// </summary>
    public Camera Camera { get; set; } = new();

    /// <summary>
    /// Background color of the scene.
    /// </summary>
    public Vector3 BackgroundColor { get; set; } = new(0.1f, 0.1f, 0.12f);

    /// <summary>
    /// Ambient light intensity (0 to 1).
    /// </summary>
    public float AmbientIntensity { get; set; } = 0.15f;

    /// <summary>
    /// Exposure multiplier used by tone mapping.
    /// </summary>
    public float Exposure { get; set; } = 1.05f;

    /// <summary>
    /// Gamma correction value.
    /// </summary>
    public float Gamma { get; set; } = 2.2f;

    /// <summary>
    /// Simple contrast control for photo styling.
    /// </summary>
    public float Contrast { get; set; } = 1.02f;

    /// <summary>
    /// White balance offset. Negative cools the image, positive warms it.
    /// </summary>
    public float WhiteBalance { get; set; } = 0.0f;

    /// <summary>
    /// Gets all nodes in the scene using a depth-first traversal.
    /// </summary>
    public IEnumerable<SceneNode> GetAllNodes()
    {
        var stack = new Stack<SceneNode>(RootNodes);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            foreach (var child in node.Children)
                stack.Push(child);
        }
    }

    /// <summary>
    /// Gets the total number of triangles across all meshes in the scene.
    /// </summary>
    public int GetTotalTriangleCount()
    {
        return GetAllNodes()
            .Where(n => n.Mesh != null)
            .Sum(n => n.Mesh!.TriangleCount);
    }
}
