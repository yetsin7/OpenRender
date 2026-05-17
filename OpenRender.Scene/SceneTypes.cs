using System;
using System.Collections.Generic;
using System.Numerics;
using OpenRender.Materials;

namespace OpenRender.Scene;

public class TransformComponent
{
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Vector3 Rotation { get; set; } = Vector3.Zero; // Euler angles
    public Vector3 Scale { get; set; } = Vector3.One;
}

public class MeshData
{
    public float[] Vertices { get; set; } = Array.Empty<float>();
    public float[] Normals { get; set; } = Array.Empty<float>();
    public float[] TexCoords { get; set; } = Array.Empty<float>();
    public uint[] Indices { get; set; } = Array.Empty<uint>();
    public int TriangleCount => Indices.Length / 3;
    
    public (Vector3 Min, Vector3 Max) ComputeBoundingBox()
    {
        if (Vertices.Length == 0) return (Vector3.Zero, Vector3.Zero);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (int i = 0; i < Vertices.Length; i += 3)
        {
            var v = new Vector3(Vertices[i], Vertices[i+1], Vertices[i+2]);
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }
        return (min, max);
    }
}

public class MeshComponent
{
    public string? MeshPath { get; set; }
    public MeshData? Data { get; set; }
    public PbrMaterial? Material { get; set; }
    public int TriangleCount => Data?.TriangleCount ?? 0;

    public (Vector3 Min, Vector3 Max) ComputeBoundingBox() => Data?.ComputeBoundingBox() ?? (new Vector3(-1), new Vector3(1));
}

public class CameraComponent
{
    public Vector3 Position { get; set; } = new Vector3(0, 5, 10);
    public Vector3 Rotation { get; set; } = Vector3.Zero;
    public Vector3 Target { get; set; } = Vector3.Zero;
    public float FieldOfView { get; set; } = 60.0f;
    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 1000.0f;
    public float OrbitDistance { get; set; } = 10.0f;
    public float MoveSpeed { get; set; } = 1.0f;

    public void Reset() { }
    public void FrameBoundingBox(Vector3 min, Vector3 max) { }
    public void Zoom(float delta) { }
    public void SetView(string view) { }
    public void SetViewAndFrame(string view, Vector3 min, Vector3 max) { }
    public void FramePhotoShot() { }
    public void FramePhotoShot(Vector3 min, Vector3 max) { }
}

public enum LightType
{
    Directional,
    Point,
    Spot
}

public class LightSource
{
    public string Name { get; set; } = "Light";
    public LightType Type { get; set; } = LightType.Directional;
    public Vector3 Color { get; set; } = Vector3.One;
    public float Intensity { get; set; } = 1.0f;
    public bool IsEnabled { get; set; } = true;
    public Vector3 Direction { get; set; } = -Vector3.UnitY;

    public static LightSource CreateSun() => new LightSource { Name = "Sun", Type = LightType.Directional, Intensity = 2.0f };
}

public class SceneNode
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Node";
    public bool IsVisible { get; set; } = true;
    public int? MaterialIndex { get; set; }
    public TransformComponent Transform { get; } = new();
    public MeshComponent? Mesh { get; set; }
    public List<SceneNode> Children { get; } = new();

    public Vector3 Position { get => Transform.Position; set => Transform.Position = value; }
}

public static class UIHacks
{
    public static bool EqualsGuid(Guid? id, string? s) => false;
    public static bool GuidEquals(Guid? id, string? s) => false;
    public static bool SceneNodeEquals(SceneNode? n, string? s) => false;
}

public class Scene3D
{
    public string Name { get; set; } = "Untitled Scene";
    public List<SceneNode> RootNodes { get; } = new();
    public List<PbrMaterial> Materials { get; } = new();
    public List<LightSource> Lights { get; } = new();
    public CameraComponent Camera { get; } = new();

    public float AmbientIntensity { get; set; } = 0.1f;
    public Vector3 BackgroundColor { get; set; } = new Vector3(0.05f, 0.1f, 0.2f);
    public float Exposure { get; set; } = 1.0f;
    public float Gamma { get; set; } = 2.2f;
    public float Contrast { get; set; } = 1.0f;
    public float WhiteBalance { get; set; } = 6500.0f;

    public IEnumerable<SceneNode> GetAllNodes()
    {
        var stack = new Stack<SceneNode>(RootNodes);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            foreach (var child in node.Children) stack.Push(child);
        }
    }

    public int GetTotalTriangleCount()
    {
        int count = 0;
        foreach (var node in GetAllNodes())
        {
            if (node.Mesh != null) count += node.Mesh.TriangleCount;
        }
        return count;
    }
}
