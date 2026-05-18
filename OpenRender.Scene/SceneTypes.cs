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

    public CameraComponent Clone()
    {
        return new CameraComponent
        {
            Position = Position,
            Rotation = Rotation,
            Target = Target,
            FieldOfView = FieldOfView,
            NearPlane = NearPlane,
            FarPlane = FarPlane,
            OrbitDistance = OrbitDistance,
            MoveSpeed = MoveSpeed
        };
    }

    /// <summary>
    /// Restablece una cámara orbital segura para iniciar una sesión
    /// cuando todavía no existe geometría en escena.
    /// </summary>
    public void Reset()
    {
        Target = Vector3.Zero;
        OrbitDistance = 10.0f;
        Position = new Vector3(0, 4.5f, 10.0f);
        Rotation = CalculateEulerFromView(Position, Target);
        NearPlane = 0.1f;
        FarPlane = 2000.0f;
        MoveSpeed = 4.0f;
    }

    /// <summary>
    /// Encuadra el bounding box completo con un ángulo 3D útil para revisión arquitectónica.
    /// </summary>
    public void FrameBoundingBox(Vector3 min, Vector3 max)
    {
        if (!IsValidBounds(min, max))
        {
            Reset();
            return;
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = Vector3.Max(max - min, new Vector3(0.01f));
        float radius = MathF.Max(size.Length() * 0.5f, 0.5f);
        float fovRad = MathF.PI * FieldOfView / 180f;
        float distance = MathF.Max(radius / MathF.Tan(fovRad * 0.5f), radius * 1.55f);
        var direction = Vector3.Normalize(new Vector3(-0.85f, 0.52f, -1.1f));

        Target = center;
        OrbitDistance = distance * 1.20f;
        Position = Target - direction * OrbitDistance;
        NearPlane = MathF.Max(0.01f, radius * 0.01f);
        FarPlane = MathF.Max(500f, radius * 20f);
        MoveSpeed = MathF.Max(2.5f, radius * 0.08f);
        Rotation = CalculateEulerFromView(Position, Target);
    }

    /// <summary>
    /// Ajusta la distancia orbital sin perder el objetivo actual.
    /// </summary>
    public void Zoom(float delta)
    {
        var direction = GetViewDirection();
        float zoomStep = MathF.Max(0.25f, OrbitDistance * 0.08f);
        OrbitDistance = Math.Clamp(OrbitDistance - delta * zoomStep, 0.25f, 5000f);
        Position = Target - direction * OrbitDistance;
        Rotation = CalculateEulerFromView(Position, Target);
    }

    /// <summary>
    /// Cambia a una vista conocida reutilizando la distancia orbital actual.
    /// </summary>
    public void SetView(string view)
    {
        ApplyView(view, OrbitDistance <= 0.01f ? 10f : OrbitDistance);
    }

    /// <summary>
    /// Enmarca el modelo desde una vista ortogonal o 3D concreta.
    /// </summary>
    public void SetViewAndFrame(string view, Vector3 min, Vector3 max)
    {
        if (!IsValidBounds(min, max))
        {
            SetView(view);
            return;
        }

        Vector3 size = Vector3.Max(max - min, new Vector3(0.01f));
        Target = (min + max) * 0.5f;
        float radius = MathF.Max(size.Length() * 0.5f, 0.5f);
        float fovRad = MathF.PI * FieldOfView / 180f;
        float distance = MathF.Max(radius / MathF.Tan(fovRad * 0.5f), radius * 1.45f);

        OrbitDistance = distance;
        NearPlane = MathF.Max(0.01f, radius * 0.01f);
        FarPlane = MathF.Max(500f, radius * 20f);
        MoveSpeed = MathF.Max(2.5f, radius * 0.08f);
        ApplyView(view, OrbitDistance);
    }

    /// <summary>
    /// Prepara un encuadre agradable por defecto incluso sin bounds explícitos.
    /// </summary>
    public void FramePhotoShot()
    {
        ApplyView("3D", MathF.Max(OrbitDistance, 12f) * 0.92f, usePhotoAngle: true);
    }

    /// <summary>
    /// Prepara un still con un ángulo alto de tres cuartos a partir del modelo cargado.
    /// </summary>
    public void FramePhotoShot(Vector3 min, Vector3 max)
    {
        if (!IsValidBounds(min, max))
        {
            FramePhotoShot();
            return;
        }

        FrameBoundingBox(min, max);
        OrbitDistance *= 1.08f;
        ApplyView("3D", OrbitDistance, usePhotoAngle: true);
    }

    private void ApplyView(string view, float distance, bool usePhotoAngle = false)
    {
        distance = MathF.Max(0.25f, distance);

        var direction = (view ?? string.Empty).ToUpperInvariant() switch
        {
            "FRONT" => new Vector3(0f, 0f, -1f),
            "BACK" => new Vector3(0f, 0f, 1f),
            "RIGHT" => new Vector3(-1f, 0f, 0f),
            "LEFT" => new Vector3(1f, 0f, 0f),
            "TOP" => new Vector3(0f, -1f, 0f),
            "BOTTOM" => new Vector3(0f, 1f, 0f),
            _ when usePhotoAngle => Vector3.Normalize(new Vector3(-0.95f, 0.38f, -0.72f)),
            _ => Vector3.Normalize(new Vector3(-0.78f, 0.52f, -1.0f))
        };

        OrbitDistance = distance;
        Position = Target - direction * OrbitDistance;
        Rotation = CalculateEulerFromView(Position, Target);
    }

    private Vector3 GetViewDirection()
    {
        var direction = Target - Position;
        if (direction.LengthSquared() < 0.0001f)
            return Vector3.Normalize(new Vector3(0f, -0.35f, -1f));

        return Vector3.Normalize(direction);
    }

    private static Vector3 CalculateEulerFromView(Vector3 position, Vector3 target)
    {
        var direction = target - position;
        if (direction.LengthSquared() < 0.0001f)
            return Vector3.Zero;

        direction = Vector3.Normalize(direction);
        float yaw = MathF.Atan2(-direction.X, -direction.Z);
        float pitch = MathF.Asin(direction.Y);
        return new Vector3(pitch * 180f / MathF.PI, yaw * 180f / MathF.PI, 0f);
    }

    private static bool IsValidBounds(Vector3 min, Vector3 max)
    {
        return float.IsFinite(min.X) && float.IsFinite(min.Y) && float.IsFinite(min.Z) &&
               float.IsFinite(max.X) && float.IsFinite(max.Y) && float.IsFinite(max.Z) &&
               Vector3.DistanceSquared(min, max) > 0.000001f;
    }
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
