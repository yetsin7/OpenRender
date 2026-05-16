using System.Numerics;
using OpenRender.Core.Scene;
using OpenRender.Rendering.Primitives;

namespace OpenRender.Rendering;

/// <summary>
/// Creates a demo architectural scene for testing the rendering pipeline.
/// Builds a simple building with walls, floor, and columns.
/// </summary>
public static class DemoScene
{
    /// <summary>
    /// Creates a demo scene with a simple architectural structure.
    /// </summary>
    public static Scene3D Create()
    {
        var scene = new Scene3D
        {
            Name = "Demo Architecture",
            AmbientIntensity = 0.2f,
            BackgroundColor = new Vector3(0.52f, 0.68f, 0.85f) // Sky blue
        };

        // Materials
        scene.Materials.Add(new PbrMaterial
        {
            Name = "Floor",
            Albedo = new Vector3(0.65f, 0.63f, 0.58f),
            Metallic = 0.0f,
            Roughness = 0.8f
        });
        scene.Materials.Add(new PbrMaterial
        {
            Name = "Wall",
            Albedo = new Vector3(0.9f, 0.88f, 0.85f),
            Metallic = 0.0f,
            Roughness = 0.6f
        });
        scene.Materials.Add(new PbrMaterial
        {
            Name = "Column",
            Albedo = new Vector3(0.75f, 0.73f, 0.7f),
            Metallic = 0.0f,
            Roughness = 0.5f
        });
        scene.Materials.Add(new PbrMaterial
        {
            Name = "Roof",
            Albedo = new Vector3(0.4f, 0.38f, 0.35f),
            Metallic = 0.1f,
            Roughness = 0.7f
        });
        scene.Materials.Add(PbrMaterial.Glass);

        // Ground plane
        var ground = new SceneNode
        {
            Name = "Ground",
            Mesh = PrimitiveGenerator.CreatePlane(30f),
            MaterialIndex = 0,
            Position = new Vector3(0, -0.01f, 0)
        };
        scene.RootNodes.Add(ground);

        // Main floor platform
        var platform = new SceneNode
        {
            Name = "Platform",
            Mesh = PrimitiveGenerator.CreateArchBox(12f, 0.3f, 8f),
            MaterialIndex = 0,
            Position = new Vector3(0, 0, 0)
        };
        scene.RootNodes.Add(platform);

        // Back wall
        var backWall = new SceneNode
        {
            Name = "Back Wall",
            Mesh = PrimitiveGenerator.CreateArchBox(12f, 4f, 0.25f),
            MaterialIndex = 1,
            Position = new Vector3(0, 0.3f, -3.875f)
        };
        scene.RootNodes.Add(backWall);

        // Left wall
        var leftWall = new SceneNode
        {
            Name = "Left Wall",
            Mesh = PrimitiveGenerator.CreateArchBox(0.25f, 4f, 8f),
            MaterialIndex = 1,
            Position = new Vector3(-5.875f, 0.3f, 0)
        };
        scene.RootNodes.Add(leftWall);

        // Columns on the right side (open facade)
        float[] columnPositions = { -3f, -1f, 1f, 3f };
        foreach (float z in columnPositions)
        {
            var column = new SceneNode
            {
                Name = $"Column_{z}",
                Mesh = PrimitiveGenerator.CreateArchBox(0.4f, 4f, 0.4f),
                MaterialIndex = 2,
                Position = new Vector3(5.8f, 0.3f, z)
            };
            scene.RootNodes.Add(column);
        }

        // Roof slab
        var roof = new SceneNode
        {
            Name = "Roof",
            Mesh = PrimitiveGenerator.CreateArchBox(13f, 0.35f, 9f),
            MaterialIndex = 3,
            Position = new Vector3(0, 4.3f, 0)
        };
        scene.RootNodes.Add(roof);

        // Interior cube (furniture placeholder)
        var table = new SceneNode
        {
            Name = "Table",
            Mesh = PrimitiveGenerator.CreateArchBox(2f, 0.8f, 1f),
            MaterialIndex = 2,
            Position = new Vector3(0, 0.3f, 0)
        };
        scene.RootNodes.Add(table);

        // Lighting: Sun
        scene.Lights.Add(LightSource.CreateSun(1.8f));

        // Camera
        scene.Camera = new Camera();
        scene.Camera.Position = new Vector3(12f, 8f, 12f);
        scene.Camera.Yaw = -135f;
        scene.Camera.Pitch = -25f;

        return scene;
    }
}
