using System;
using System.Collections.Generic;
using System.Numerics;
using OpenRender.Scene;
using OpenRender.Tools;

namespace OpenRender.Editor;

public class SelectionSystem
{
    private SceneNode? _selectedNode;
    public SceneNode? SelectedNode => _selectedNode;

    public event Action<SceneNode?>? SelectionChanged;

    public void Select(SceneNode? node)
    {
        if (_selectedNode == node) return;
        _selectedNode = node;
        SelectionChanged?.Invoke(node);
    }

    public SceneNode? Pick(Ray ray, Scene3D scene)
    {
        SceneNode? closestNode = null;
        float minDistance = float.MaxValue;

        foreach (var node in scene.GetAllNodes())
        {
            if (node.Mesh?.Data == null) continue;

            // Compute world-space AABB for the node
            var (localMin, localMax) = node.Mesh.Data.ComputeBoundingBox();
            
            // Simplified: apply node transform to AABB
            // A more precise version would transform all 8 corners
            Vector3 worldMin = localMin + node.Transform.Position;
            Vector3 worldMax = localMax + node.Transform.Position;
            var worldBox = new BoundingBox(worldMin, worldMax);

            float? distance = ray.Intersects(worldBox);
            if (distance.HasValue && distance.Value < minDistance)
            {
                minDistance = distance.Value;
                closestNode = node;
            }
        }

        return closestNode;
    }
}
