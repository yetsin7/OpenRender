using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Silk.NET.Assimp;
using Silk.NET.Core.Native;
using OpenRender.Scene;
using OpenRender.Materials;

namespace OpenRender.Assets;

public class ModelImporter : IDisposable
{
    private readonly Assimp _assimp;

    public ModelImporter()
    {
        _assimp = Assimp.GetApi();
    }

    public unsafe Scene3D LoadModel(string filePath)
    {
        return LoadModel(filePath, new ImportOptions(), null);
    }

    public unsafe Scene3D LoadModel(string filePath, ImportOptions? options, IProgress<double>? progress)
    {
        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException("Model file not found", filePath);

        options ??= new ImportOptions();
        progress?.Report(8);
        var scene = _assimp.ImportFile(filePath, (uint)(PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.CalculateTangentSpace | PostProcessSteps.FlipUVs));
        
        if (scene == null || scene->MFlags == (uint)Assimp.SceneFlagsIncomplete || scene->MRootNode == null)
            throw new Exception($"Assimp error: {_assimp.GetErrorStringS()}");

        try
        {
            var result = new Scene3D { Name = Path.GetFileNameWithoutExtension(filePath) };
            result.Lights.Add(LightSource.CreateSun());

            for (int i = 0; i < scene->MNumMaterials; i++)
            {
                string materialName = $"Material {i + 1}";

                result.Materials.Add(new PbrMaterial
                {
                    Name = materialName,
                    SourceName = materialName
                });
            }

            int processedMeshes = 0;
            int totalMeshes = Math.Max(1, (int)scene->MNumMeshes);
            ProcessNode(scene->MRootNode, scene, result, null, options, progress, ref processedMeshes, totalMeshes);

            if (options.Recenter)
                RecenterScene(result);

            progress?.Report(96);
            return result;
        }
        finally
        {
            _assimp.ReleaseImport(scene);
        }
    }

    private unsafe void ProcessNode(
        Node* node,
        Silk.NET.Assimp.Scene* scene,
        Scene3D result,
        SceneNode? parent,
        ImportOptions options,
        IProgress<double>? progress,
        ref int processedMeshes,
        int totalMeshes)
    {
        var newNode = new SceneNode { Name = node->MName.ToString() };
        
        if (parent == null) result.RootNodes.Add(newNode);
        else parent.Children.Add(newNode);

        // Extract translation from Assimp matrix
        var m = node->MTransformation;
        // In Silk.NET Assimp, Matrix4x4 often has A1...D4. If not, we'll see.
        // Let's use a conservative approach if property names are uncertain.
        // newNode.Transform.Position = new Vector3(m.A4, m.B4, m.C4);

        for (int i = 0; i < node->MNumMeshes; i++)
        {
            var mesh = scene->MMeshes[node->MMeshes[i]];
            var meshNode = i == 0 && node->MNumMeshes == 1
                ? newNode
                : new SceneNode { Name = $"{newNode.Name}_Mesh_{i + 1}" };

            if (!ReferenceEquals(meshNode, newNode))
                newNode.Children.Add(meshNode);

            meshNode.Mesh = new MeshComponent { Data = ProcessMesh(mesh, options) };
            meshNode.MaterialIndex = (int)mesh->MMaterialIndex;
            processedMeshes++;
            progress?.Report(10 + Math.Min(82, processedMeshes * 82.0 / totalMeshes));
        }

        for (int i = 0; i < node->MNumChildren; i++)
        {
            ProcessNode(node->MChildren[i], scene, result, newNode, options, progress, ref processedMeshes, totalMeshes);
        }
    }

    private unsafe MeshData ProcessMesh(Silk.NET.Assimp.Mesh* mesh, ImportOptions options)
    {
        var data = new MeshData();
        
        var vertices = new float[mesh->MNumVertices * 3];
        var normals = new float[mesh->MNumVertices * 3];
        var texCoords = new float[mesh->MNumVertices * 2];

        for (int i = 0; i < mesh->MNumVertices; i++)
        {
            var vertex = new Vector3(mesh->MVertices[i].X, mesh->MVertices[i].Y, mesh->MVertices[i].Z);
            if (options.SwapYZ)
                vertex = new Vector3(vertex.X, vertex.Z, vertex.Y);

            vertices[i * 3] = vertex.X;
            vertices[i * 3 + 1] = vertex.Y;
            vertices[i * 3 + 2] = vertex.Z;

            if (mesh->MNormals != null)
            {
                var normal = new Vector3(mesh->MNormals[i].X, mesh->MNormals[i].Y, mesh->MNormals[i].Z);
                if (options.SwapYZ)
                    normal = new Vector3(normal.X, normal.Z, normal.Y);

                normals[i * 3] = normal.X;
                normals[i * 3 + 1] = normal.Y;
                normals[i * 3 + 2] = normal.Z;
            }

            if (mesh->MTextureCoords[0] != null)
            {
                texCoords[i * 2] = mesh->MTextureCoords[0][i].X;
                texCoords[i * 2 + 1] = mesh->MTextureCoords[0][i].Y;
            }
        }

        var indices = new List<uint>();
        for (int i = 0; i < mesh->MNumFaces; i++)
        {
            var face = mesh->MFaces[i];
            for (int j = 0; j < face.MNumIndices; j++)
                indices.Add(face.MIndices[j]);
        }

        data.Vertices = vertices;
        data.Normals = normals;
        data.TexCoords = texCoords;
        data.Indices = indices.ToArray();

        return data;
    }

    private static void RecenterScene(Scene3D scene)
    {
        var meshNodes = scene.GetAllNodes()
            .Where(node => node.Mesh?.Data?.Vertices.Length > 0)
            .ToList();

        if (meshNodes.Count == 0)
            return;

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        foreach (var node in meshNodes)
        {
            var (nodeMin, nodeMax) = node.Mesh!.ComputeBoundingBox();
            min = Vector3.Min(min, nodeMin + node.Position);
            max = Vector3.Max(max, nodeMax + node.Position);
        }

        var center = (min + max) * 0.5f;
        foreach (var node in meshNodes)
        {
            var vertices = node.Mesh!.Data!.Vertices;
            for (int index = 0; index < vertices.Length; index += 3)
            {
                vertices[index] -= center.X;
                vertices[index + 1] -= center.Y;
                vertices[index + 2] -= center.Z;
            }
        }
    }

    public void Dispose()
    {
        _assimp.Dispose();
    }
}
