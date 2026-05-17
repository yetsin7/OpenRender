using System;
using System.Collections.Generic;
using System.Numerics;
using OpenRender.Scene;

namespace OpenRender.Vegetation;

public class VegetationInstance
{
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public float Scale { get; set; } = 1.0f;
}

public class VegetationSpecies
{
    public string Name { get; set; } = "Species";
    public string? MeshPath { get; set; }
    public List<VegetationInstance> Instances { get; } = new();
}

public class VegetationManager
{
    private readonly List<VegetationSpecies> _species = new();

    public void AddSpecies(VegetationSpecies species)
    {
        _species.Add(species);
    }

    public Matrix4x4[] GetInstanceMatrices(VegetationSpecies species)
    {
        var matrices = new Matrix4x4[species.Instances.Count];
        for (int i = 0; i < species.Instances.Count; i++)
        {
            var inst = species.Instances[i];
            matrices[i] = Matrix4x4.CreateScale(inst.Scale) *
                          Matrix4x4.CreateFromYawPitchRoll(inst.Rotation.Y, inst.Rotation.X, inst.Rotation.Z) *
                          Matrix4x4.CreateTranslation(inst.Position);
        }
        return matrices;
    }

    public void ScatterCircular(VegetationSpecies species, Vector3 center, float radius, int count)
    {
        var random = new Random();
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2);
            float dist = (float)(random.NextDouble() * radius);
            
            species.Instances.Add(new VegetationInstance
            {
                Position = center + new Vector3(MathF.Cos(angle) * dist, 0, MathF.Sin(angle) * dist),
                Rotation = new Vector3(0, (float)(random.NextDouble() * Math.PI * 2), 0),
                Scale = 0.8f + (float)random.NextDouble() * 0.4f
            });
        }
    }
}
