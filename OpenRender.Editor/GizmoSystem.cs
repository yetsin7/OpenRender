using System.Numerics;
using OpenRender.Tools;
using OpenRender.Scene;

namespace OpenRender.Editor;

public enum GizmoMode
{
    Translate,
    Rotate,
    Scale
}

public class GizmoSystem
{
    public GizmoMode Mode { get; set; } = GizmoMode.Translate;
    public bool IsLocalSpace { get; set; } = true;
    
    // Logic for dragging and calculating deltas based on mouse ray
    public Vector3? HandleDrag(Ray mouseRay, SceneNode target)
    {
        // Placeholder for gizmo intersection and delta logic
        return null;
    }
}
