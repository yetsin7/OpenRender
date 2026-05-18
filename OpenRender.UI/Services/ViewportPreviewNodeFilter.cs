using System.Linq;
using System.Numerics;
using OpenRender.Materials;
using OpenRender.Scene;

namespace OpenRender.Services;

/// <summary>
/// Decide qué nodos conviene priorizar en el preview para que la lectura
/// arquitectónica siga clara aunque el modelo exportado traiga mucho detalle.
/// </summary>
public static class ViewportPreviewNodeFilter
{
    public static bool ShouldInclude(SceneNode node, PbrMaterial? material, Vector3 min, Vector3 max)
    {
        if (node.Mesh == null)
            return false;

        var (nodeMin, nodeMax) = node.Mesh.ComputeBoundingBox();
        float sceneDiagonal = Vector3.Distance(min, max);
        float nodeDiagonal = Vector3.Distance(nodeMin + node.Position, nodeMax + node.Position);
        string hint = Normalize($"{node.Name} {material?.SourceName} {material?.Name}");

        if (sceneDiagonal <= 0.001f)
            return true;
        if (material?.Opacity < 0.95f)
            return nodeDiagonal >= sceneDiagonal * 0.005f;
        if (ContainsAny(hint, "wall", "muro", "roof", "techo", "cubierta", "column", "columna", "beam", "viga", "window", "ventana", "door", "puerta", "slab", "losa", "floor", "piso", "ceiling", "cielo", "facade", "fachada", "jamba"))
            return nodeDiagonal >= sceneDiagonal * 0.008f;
        if (ContainsAny(hint, "chair", "table", "mesa", "gabinete", "cabinet", "tv", "tap", "basin", "mirror", "sofa", "lamp", "light", "appliance"))
            return nodeDiagonal >= sceneDiagonal * 0.04f;

        return nodeDiagonal >= sceneDiagonal * 0.018f;
    }

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ").Replace("\\", " ");

    private static bool ContainsAny(string text, params string[] tokens) => tokens.Any(text.Contains);
}
