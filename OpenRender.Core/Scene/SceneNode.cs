using System.Numerics;

namespace OpenRender.Core.Scene;

/// <summary>
/// Represents a node in the 3D scene graph.
/// Each node can have a transform (position, rotation, scale),
/// child nodes, and an optional mesh reference.
/// </summary>
public class SceneNode
{
    /// <summary>
    /// Unique identifier for this node.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Human-readable name for this node.
    /// </summary>
    public string Name { get; set; } = "Node";

    /// <summary>
    /// Position in world space.
    /// </summary>
    public Vector3 Position { get; set; } = Vector3.Zero;

    /// <summary>
    /// Rotation in Euler angles (degrees).
    /// </summary>
    public Vector3 Rotation { get; set; } = Vector3.Zero;

    /// <summary>
    /// Scale factor per axis.
    /// </summary>
    public Vector3 Scale { get; set; } = Vector3.One;

    /// <summary>
    /// Whether this node is visible in the scene.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Child nodes in the scene hierarchy.
    /// </summary>
    public List<SceneNode> Children { get; } = new();

    /// <summary>
    /// Optional reference to a mesh for rendering.
    /// </summary>
    public MeshData? Mesh { get; set; }

    /// <summary>
    /// Optional material applied to this node.
    /// </summary>
    public int? MaterialIndex { get; set; }

    /// <summary>
    /// Computes the local transform matrix from position, rotation, and scale.
    /// </summary>
    public Matrix4x4 GetLocalTransform()
    {
        var scale = Matrix4x4.CreateScale(Scale);
        var rotX = Matrix4x4.CreateRotationX(MathF.PI / 180f * Rotation.X);
        var rotY = Matrix4x4.CreateRotationY(MathF.PI / 180f * Rotation.Y);
        var rotZ = Matrix4x4.CreateRotationZ(MathF.PI / 180f * Rotation.Z);
        var translation = Matrix4x4.CreateTranslation(Position);
        return scale * rotX * rotY * rotZ * translation;
    }
}
