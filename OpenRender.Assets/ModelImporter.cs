using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Silk.NET.Assimp;
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
        uint postProcess = (uint)(PostProcessSteps.Triangulate |
                                  PostProcessSteps.JoinIdenticalVertices |
                                  PostProcessSteps.ImproveCacheLocality |
                                  PostProcessSteps.FindDegenerates |
                                  PostProcessSteps.FindInvalidData |
                                  PostProcessSteps.FlipUVs |
                                  PostProcessSteps.CalculateTangentSpace);

        postProcess |= (uint)(options.GenerateNormals
            ? PostProcessSteps.GenerateSmoothNormals
            : PostProcessSteps.GenerateNormals);

        var scene = _assimp.ImportFile(filePath, postProcess);
        
        if (scene == null || scene->MFlags == (uint)Assimp.SceneFlagsIncomplete || scene->MRootNode == null)
            throw new Exception($"Assimp error: {_assimp.GetErrorStringS()}");

        try
        {
            var result = new Scene3D { Name = Path.GetFileNameWithoutExtension(filePath) };
            result.Lights.Add(LightSource.CreateSun());
            string modelDirectory = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();

            LoadMaterials(scene, result, modelDirectory);

            int processedMeshes = 0;
            int totalMeshes = Math.Max(1, (int)scene->MNumMeshes);
            ProcessNode(scene->MRootNode, scene, result, null, Matrix4x4.Identity, options, progress, ref processedMeshes, totalMeshes);

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
        Matrix4x4 parentTransform,
        ImportOptions options,
        IProgress<double>? progress,
        ref int processedMeshes,
        int totalMeshes)
    {
        var newNode = new SceneNode { Name = node->MName.ToString() };
        Matrix4x4 localTransform = ToNumericsMatrix(node->MTransformation);
        Matrix4x4 combinedTransform = localTransform * parentTransform;
        
        if (parent == null) result.RootNodes.Add(newNode);
        else parent.Children.Add(newNode);

        for (int i = 0; i < node->MNumMeshes; i++)
        {
            var mesh = scene->MMeshes[node->MMeshes[i]];
            var meshNode = i == 0 && node->MNumMeshes == 1
                ? newNode
                : new SceneNode { Name = $"{newNode.Name}_Mesh_{i + 1}" };

            if (!ReferenceEquals(meshNode, newNode))
                newNode.Children.Add(meshNode);

            meshNode.Mesh = new MeshComponent { Data = ProcessMesh(mesh, combinedTransform, options) };
            meshNode.MaterialIndex = (int)mesh->MMaterialIndex;
            processedMeshes++;
            progress?.Report(10 + Math.Min(82, processedMeshes * 82.0 / totalMeshes));
        }

        for (int i = 0; i < node->MNumChildren; i++)
        {
            ProcessNode(node->MChildren[i], scene, result, newNode, combinedTransform, options, progress, ref processedMeshes, totalMeshes);
        }
    }

    private unsafe MeshData ProcessMesh(Silk.NET.Assimp.Mesh* mesh, Matrix4x4 transform, ImportOptions options)
    {
        var data = new MeshData();
        
        var vertices = new float[mesh->MNumVertices * 3];
        var normals = new float[mesh->MNumVertices * 3];
        var texCoords = new float[mesh->MNumVertices * 2];

        for (int i = 0; i < mesh->MNumVertices; i++)
        {
            var vertex = new Vector3(mesh->MVertices[i].X, mesh->MVertices[i].Y, mesh->MVertices[i].Z);
            vertex = Vector3.Transform(vertex, transform);
            if (options.SwapYZ)
                vertex = new Vector3(vertex.X, vertex.Z, vertex.Y);

            vertices[i * 3] = vertex.X;
            vertices[i * 3 + 1] = vertex.Y;
            vertices[i * 3 + 2] = vertex.Z;

            if (mesh->MNormals != null)
            {
                var normal = new Vector3(mesh->MNormals[i].X, mesh->MNormals[i].Y, mesh->MNormals[i].Z);
                normal = Vector3.TransformNormal(normal, transform);
                if (normal.LengthSquared() > 0.000001f)
                    normal = Vector3.Normalize(normal);
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
        EnsureNormals(data);

        return data;
    }

    private unsafe void LoadMaterials(Silk.NET.Assimp.Scene* scene, Scene3D result, string modelDirectory)
    {
        for (int index = 0; index < scene->MNumMaterials; index++)
        {
            var source = scene->MMaterials[index];
            string materialName = ReadMaterialName(source, index);
            Vector4 diffuseColor = ReadMaterialColor(source, Assimp.MaterialColorDiffuseBase, new Vector4(0.78f, 0.78f, 0.78f, 1f));
            Vector4 emissiveColor = ReadMaterialColor(source, Assimp.MaterialColorEmissiveBase, Vector4.Zero);
            float opacity = ReadMaterialFloat(source, Assimp.MaterialOpacityBase, diffuseColor.W <= 0f ? 1f : diffuseColor.W);
            float shininess = ReadMaterialFloat(source, Assimp.MaterialShininessBase, 12f);
            float reflectivity = ReadMaterialFloat(source, Assimp.MaterialReflectivityBase, 0.04f);
            float metallic = Math.Clamp(ReadMaterialFloat(source, "$mat.metallicFactor", reflectivity), 0f, 1f);
            float roughnessFactor = ReadMaterialFloat(source, "$mat.roughnessFactor", -1f);
            float roughness = roughnessFactor >= 0f
                ? Math.Clamp(roughnessFactor, 0.04f, 1f)
                : Math.Clamp(1f - shininess / 128f, 0.08f, 1f);

            var material = new PbrMaterial
            {
                Name = materialName,
                SourceName = materialName,
                Albedo = new Vector3(diffuseColor.X, diffuseColor.Y, diffuseColor.Z),
                Emissive = new Vector3(emissiveColor.X, emissiveColor.Y, emissiveColor.Z),
                Opacity = Math.Clamp(opacity, 0.05f, 1f),
                Roughness = roughness,
                Metallic = metallic,
                AmbientOcclusion = 1f
            };

            material.AlbedoTexturePath = ReadTexturePath(source, TextureType.Diffuse, modelDirectory) ??
                                         ReadTexturePath(source, TextureType.BaseColor, modelDirectory);
            material.NormalTexturePath = ReadTexturePath(source, TextureType.Normals, modelDirectory) ??
                                         ReadTexturePath(source, TextureType.NormalCamera, modelDirectory) ??
                                         ReadTexturePath(source, TextureType.Height, modelDirectory);
            material.RoughnessTexturePath = ReadTexturePath(source, TextureType.DiffuseRoughness, modelDirectory);
            material.MetalnessTexturePath = ReadTexturePath(source, TextureType.Metalness, modelDirectory);
            material.AoTexturePath = ReadTexturePath(source, TextureType.AmbientOcclusion, modelDirectory);

            result.Materials.Add(material);
        }
    }

    private unsafe string ReadMaterialName(Material* material, int materialIndex)
    {
        AssimpString value = default;
        return _assimp.GetMaterialString(material, Assimp.MaterialNameBase, 0, 0, &value) == Return.Success &&
               !string.IsNullOrWhiteSpace(value.AsString)
            ? value.AsString
            : $"Material {materialIndex + 1}";
    }

    private unsafe Vector4 ReadMaterialColor(Material* material, string key, Vector4 fallback)
    {
        Vector4 value = fallback;
        return _assimp.GetMaterialColor(material, key, 0, 0, &value) == Return.Success ? value : fallback;
    }

    private unsafe float ReadMaterialFloat(Material* material, string key, float fallback)
    {
        uint max = 1;
        float value = fallback;
        return _assimp.GetMaterialFloatArray(material, key, 0, 0, &value, &max) == Return.Success ? value : fallback;
    }

    private unsafe string? ReadTexturePath(Material* material, TextureType type, string modelDirectory)
    {
        if (_assimp.GetMaterialTextureCount(material, type) == 0)
            return null;

        AssimpString texturePath = default;
        TextureMapping mapping = default;
        uint uvIndex = 0;
        float blend = 0;
        TextureOp textureOp = default;
        TextureMapMode mapMode = default;
        uint flags = 0;

        if (_assimp.GetMaterialTexture(
                material,
                type,
                0,
                &texturePath,
                &mapping,
                &uvIndex,
                &blend,
                &textureOp,
                &mapMode,
                &flags) != Return.Success)
        {
            return null;
        }

        string rawPath = texturePath.AsString;
        if (string.IsNullOrWhiteSpace(rawPath) || rawPath.StartsWith('*'))
            return null;

        string cleanedPath = rawPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string absolutePath = Path.IsPathRooted(cleanedPath)
            ? cleanedPath
            : Path.GetFullPath(Path.Combine(modelDirectory, cleanedPath));

        return System.IO.File.Exists(absolutePath) ? absolutePath : null;
    }

    private static Matrix4x4 ToNumericsMatrix(Matrix4x4 matrix) => matrix;

    private static void EnsureNormals(MeshData mesh)
    {
        if (mesh.Vertices.Length == 0)
            return;

        bool hasUsefulNormals = mesh.Normals.Length == mesh.Vertices.Length &&
                                mesh.Normals.Any(value => MathF.Abs(value) > 0.0001f);

        if (hasUsefulNormals)
            return;

        var normals = new Vector3[mesh.Vertices.Length / 3];
        int triangleCount = mesh.Indices.Length >= 3 ? mesh.Indices.Length / 3 : mesh.Vertices.Length / 9;

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            if (!TryReadTriangle(mesh, triangleIndex, out int ia, out int ib, out int ic, out var a, out var b, out var c))
                continue;

            Vector3 faceNormal = Vector3.Cross(b - a, c - a);
            if (faceNormal.LengthSquared() < 0.000001f)
                continue;

            faceNormal = Vector3.Normalize(faceNormal);
            normals[ia] += faceNormal;
            normals[ib] += faceNormal;
            normals[ic] += faceNormal;
        }

        mesh.Normals = new float[mesh.Vertices.Length];
        for (int index = 0; index < normals.Length; index++)
        {
            Vector3 normal = normals[index].LengthSquared() > 0.000001f ? Vector3.Normalize(normals[index]) : Vector3.UnitY;
            mesh.Normals[index * 3] = normal.X;
            mesh.Normals[index * 3 + 1] = normal.Y;
            mesh.Normals[index * 3 + 2] = normal.Z;
        }
    }

    private static bool TryReadTriangle(MeshData mesh, int triangleIndex, out int ia, out int ib, out int ic, out Vector3 a, out Vector3 b, out Vector3 c)
    {
        if (mesh.Indices.Length >= 3)
        {
            int baseIndex = triangleIndex * 3;
            if (baseIndex + 2 >= mesh.Indices.Length)
            {
                ia = ib = ic = 0;
                a = b = c = default;
                return false;
            }

            ia = (int)mesh.Indices[baseIndex];
            ib = (int)mesh.Indices[baseIndex + 1];
            ic = (int)mesh.Indices[baseIndex + 2];
        }
        else
        {
            ia = triangleIndex * 3;
            ib = ia + 1;
            ic = ia + 2;
        }

        a = ReadVertex(mesh.Vertices, ia);
        b = ReadVertex(mesh.Vertices, ib);
        c = ReadVertex(mesh.Vertices, ic);
        return true;
    }

    private static Vector3 ReadVertex(float[] vertices, int vertexIndex)
    {
        int offset = vertexIndex * 3;
        return new Vector3(vertices[offset], vertices[offset + 1], vertices[offset + 2]);
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
