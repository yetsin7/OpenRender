using System.Globalization;
using System.Numerics;
using OpenRender.Core.Import;
using OpenRender.Core.Scene;

namespace OpenRender.Rendering.Import;

/// <summary>
/// Imports Wavefront OBJ files into the Open Render scene format.
/// Supports vertices, normals, texture coordinates, and face definitions.
/// </summary>
public class ObjImporter : IModelImporter
{
    public IReadOnlyList<string> SupportedExtensions => new[] { ".obj" };
    public string FormatDescription => "Wavefront OBJ - Simple 3D model format";

    public bool CanImport(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".obj", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ImportResult> ImportAsync(string filePath, ImportOptions? options = null, IProgress<double>? progress = null)
    {
        var result = new ImportResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            options ??= new ImportOptions();
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var texCoords = new List<Vector2>();

            var materials = new List<PbrMaterial>();
            var materialMap = new Dictionary<string, int>();
            var defaultMaterial = PbrMaterial.Default;
            defaultMaterial.SourceName = defaultMaterial.Name;
            materials.Add(defaultMaterial);
            materialMap["default"] = 0;

            var builders = new Dictionary<string, MeshBuilder>();
            MeshBuilder GetOrCreateBuilder(string groupName, string matName)
            {
                var key = $"{groupName}|{matName}";
                if (!builders.TryGetValue(key, out var builder))
                {
                    builder = new MeshBuilder
                    {
                        Name = BuildMeshName(groupName, matName),
                        GroupName = groupName,
                        MaterialName = matName
                    };

                    if (materialMap.TryGetValue(matName, out int matIdx))
                        builder.MaterialIndex = matIdx;
                    builders[key] = builder;
                }
                return builder;
            }

            string currentGroupName = Path.GetFileNameWithoutExtension(filePath);
            string currentMaterialName = "default";
            var currentBuilder = GetOrCreateBuilder(currentGroupName, currentMaterialName);
            
            long fileSizeBytes = new FileInfo(filePath).Length;
            using var reader = new StreamReader(filePath);
            string? rawLine;
            long bytesRead = 0;
            int currentLine = 0;

            while ((rawLine = await reader.ReadLineAsync()) != null)
            {
                bytesRead += rawLine.Length + 2; // Approximate bytes read
                currentLine++;
                
                if (currentLine % 50000 == 0 && fileSizeBytes > 0)
                {
                    progress?.Report((double)bytesRead / fileSizeBytes * 100.0);
                }

                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                    continue;

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                switch (parts[0])
                {
                    case "mtllib" when parts.Length >= 2:
                        var mtlFileName = string.Join(' ', parts.Skip(1));
                        var mtlPath = Path.Combine(Path.GetDirectoryName(filePath) ?? "", mtlFileName);
                        if (File.Exists(mtlPath))
                        {
                            ParseMtlFile(mtlPath, materials, materialMap);
                            foreach (var builder in builders.Values)
                            {
                                if (materialMap.TryGetValue(builder.MaterialName, out int parsedMaterialIndex))
                                    builder.MaterialIndex = parsedMaterialIndex;
                            }
                        }
                        break;
                    case "usemtl" when parts.Length >= 2:
                        currentMaterialName = string.Join(' ', parts.Skip(1));
                        currentBuilder = GetOrCreateBuilder(currentGroupName, currentMaterialName);
                        if (materialMap.TryGetValue(currentMaterialName, out int materialIndex))
                            currentBuilder.MaterialIndex = materialIndex;
                        break;
                    case "g" when parts.Length >= 2:
                    case "o" when parts.Length >= 2:
                        currentGroupName = string.Join(' ', parts.Skip(1));
                        currentBuilder = GetOrCreateBuilder(currentGroupName, currentMaterialName);
                        if (materialMap.TryGetValue(currentMaterialName, out int groupMaterialIndex))
                            currentBuilder.MaterialIndex = groupMaterialIndex;
                        break;
                    case "v" when parts.Length >= 4:
                        float vx = ParseFloat(parts[1]) * options.Scale;
                        float vy = ParseFloat(parts[2]) * options.Scale;
                        float vz = ParseFloat(parts[3]) * options.Scale;

                        if (options.SwapYZ)
                        {
                            // Convert Z-up to Y-up: Y = Z, Z = -Y
                            positions.Add(new Vector3(vx, vz, -vy));
                        }
                        else
                        {
                            positions.Add(new Vector3(vx, vy, vz));
                        }
                        break;

                    case "vn" when parts.Length >= 4:
                        float vnx = ParseFloat(parts[1]);
                        float vny = ParseFloat(parts[2]);
                        float vnz = ParseFloat(parts[3]);

                        Vector3 normal;
                        if (options.SwapYZ)
                        {
                            normal = Vector3.Normalize(new Vector3(vnx, vnz, -vny));
                        }
                        else
                        {
                            normal = Vector3.Normalize(new Vector3(vnx, vny, vnz));
                        }
                        normals.Add(normal);
                        break;

                    case "vt" when parts.Length >= 3:
                        texCoords.Add(new Vector2(
                            ParseFloat(parts[1]),
                            options.FlipUVs ? 1.0f - ParseFloat(parts[2]) : ParseFloat(parts[2])
                        ));
                        break;

                    case "f":
                        // Triangulate faces with more than 3 vertices
                        var faceVertices = new List<string>();
                        for (int i = 1; i < parts.Length; i++)
                            faceVertices.Add(parts[i]);

                        for (int i = 1; i < faceVertices.Count - 1; i++)
                        {
                            uint cidx = currentBuilder.CurrentIndex;
                            ProcessFaceVertex(faceVertices[0], positions, normals, texCoords,
                                currentBuilder.Vertices, currentBuilder.Normals, currentBuilder.TexCoords, currentBuilder.Indices,
                                currentBuilder.VertexMap, ref cidx);
                            currentBuilder.CurrentIndex = cidx;

                            ProcessFaceVertex(faceVertices[i], positions, normals, texCoords,
                                currentBuilder.Vertices, currentBuilder.Normals, currentBuilder.TexCoords, currentBuilder.Indices,
                                currentBuilder.VertexMap, ref cidx);
                            currentBuilder.CurrentIndex = cidx;

                            ProcessFaceVertex(faceVertices[i + 1], positions, normals, texCoords,
                                currentBuilder.Vertices, currentBuilder.Normals, currentBuilder.TexCoords, currentBuilder.Indices,
                                currentBuilder.VertexMap, ref cidx);
                            currentBuilder.CurrentIndex = cidx;
                        }
                        break;
                }
            }

            var scene = new Scene3D { Name = Path.GetFileNameWithoutExtension(filePath) };
            foreach (var mat in materials) scene.Materials.Add(mat);
            scene.Lights.Add(LightSource.CreateSun());

            int meshCount = 0;
            int totalVertices = 0;
            int totalTriangles = 0;
            var globalMin = new Vector3(float.MaxValue);
            var globalMax = new Vector3(float.MinValue);

            foreach (var kvp in builders)
            {
                var builder = kvp.Value;
                if (builder.Indices.Count == 0) continue;

                var bMin = new Vector3(float.MaxValue);
                var bMax = new Vector3(float.MinValue);
                for (int i = 0; i < builder.Vertices.Count; i += 3)
                {
                    var v = new Vector3(builder.Vertices[i], builder.Vertices[i+1], builder.Vertices[i+2]);
                    bMin = Vector3.Min(bMin, v);
                    bMax = Vector3.Max(bMax, v);
                }
                globalMin = Vector3.Min(globalMin, bMin);
                globalMax = Vector3.Max(globalMax, bMax);
            }

            var center = (globalMin + globalMax) * 0.5f;
            center.Y = globalMin.Y; // Bottom at Y=0

            foreach (var kvp in builders)
            {
                var builder = kvp.Value;
                if (builder.Indices.Count == 0) continue;

                if (options.Recenter)
                {
                    for (int i = 0; i < builder.Vertices.Count; i += 3)
                    {
                        builder.Vertices[i] -= center.X;
                        builder.Vertices[i+1] -= center.Y;
                        builder.Vertices[i+2] -= center.Z;
                    }
                }

                if (builder.Normals.Count == 0 && options.GenerateNormals)
                    builder.Normals.AddRange(GenerateFlatNormals(builder.Vertices, builder.Indices));

                var meshData = new MeshData
                {
                    Name = builder.Name,
                    Vertices = builder.Vertices.ToArray(),
                    Normals = builder.Normals.ToArray(),
                    TexCoords = builder.TexCoords.ToArray(),
                    Indices = builder.Indices.ToArray()
                };

                var node = new SceneNode
                {
                    Name = builder.Name,
                    Mesh = meshData,
                    MaterialIndex = builder.MaterialIndex
                };

                scene.RootNodes.Add(node);
                meshCount++;
                totalVertices += meshData.VertexCount;
                totalTriangles += meshData.TriangleCount;
            }

            if (options.Recenter)
            {
                globalMin -= center;
                globalMax -= center;
            }
            scene.Camera.FrameBoundingBox(globalMin, globalMax);

            sw.Stop();
            result.Success = true;
            result.Scene = scene;
            result.Statistics = new ImportStatistics
            {
                MeshCount = meshCount,
                MaterialCount = materials.Count,
                TotalVertices = totalVertices,
                TotalTriangles = totalTriangles,
                ImportDuration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to import OBJ: {ex.Message}";
        }

        return result;
    }

    private static void ProcessFaceVertex(
        string vertex,
        List<Vector3> positions, List<Vector3> normals, List<Vector2> texCoords,
        List<float> outVertices, List<float> outNormals, List<float> outTexCoords,
        List<uint> outIndices,
        Dictionary<string, uint> vertexMap, ref uint currentIndex)
    {
        if (vertexMap.TryGetValue(vertex, out uint existingIndex))
        {
            outIndices.Add(existingIndex);
            return;
        }

        var indices = vertex.Split('/');
        
        // Handle position index (required)
        int posIdxRaw = int.Parse(indices[0]);
        int posIdx = posIdxRaw > 0 ? posIdxRaw - 1 : positions.Count + posIdxRaw;

        outVertices.Add(positions[posIdx].X);
        outVertices.Add(positions[posIdx].Y);
        outVertices.Add(positions[posIdx].Z);

        bool addedTex = false;
        if (indices.Length > 1 && !string.IsNullOrEmpty(indices[1]))
        {
            int texIdxRaw = int.Parse(indices[1]);
            int texIdx = texIdxRaw > 0 ? texIdxRaw - 1 : texCoords.Count + texIdxRaw;
            
            if (texIdx >= 0 && texIdx < texCoords.Count)
            {
                outTexCoords.Add(texCoords[texIdx].X);
                outTexCoords.Add(texCoords[texIdx].Y);
                addedTex = true;
            }
        }
        if (!addedTex && texCoords.Count > 0)
        {
            outTexCoords.Add(0f);
            outTexCoords.Add(0f);
        }

        bool addedNorm = false;
        if (indices.Length > 2 && !string.IsNullOrEmpty(indices[2]))
        {
            int normIdxRaw = int.Parse(indices[2]);
            int normIdx = normIdxRaw > 0 ? normIdxRaw - 1 : normals.Count + normIdxRaw;

            if (normIdx >= 0 && normIdx < normals.Count)
            {
                outNormals.Add(normals[normIdx].X);
                outNormals.Add(normals[normIdx].Y);
                outNormals.Add(normals[normIdx].Z);
                addedNorm = true;
            }
        }
        if (!addedNorm && normals.Count > 0)
        {
            outNormals.Add(0f);
            outNormals.Add(1f);
            outNormals.Add(0f);
        }

        vertexMap[vertex] = currentIndex;
        outIndices.Add(currentIndex);
        currentIndex++;
    }

    private static List<float> GenerateFlatNormals(List<float> vertices, List<uint> indices)
    {
        var normals = new float[vertices.Count];

        for (int i = 0; i < indices.Count; i += 3)
        {
            int i0 = (int)indices[i] * 3;
            int i1 = (int)indices[i + 1] * 3;
            int i2 = (int)indices[i + 2] * 3;

            var v0 = new Vector3(vertices[i0], vertices[i0 + 1], vertices[i0 + 2]);
            var v1 = new Vector3(vertices[i1], vertices[i1 + 1], vertices[i1 + 2]);
            var v2 = new Vector3(vertices[i2], vertices[i2 + 1], vertices[i2 + 2]);

            var normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));

            for (int j = 0; j < 3; j++)
            {
                int idx = (int)indices[i + j] * 3;
                normals[idx] += normal.X;
                normals[idx + 1] += normal.Y;
                normals[idx + 2] += normal.Z;
            }
        }

        // Normalize accumulated normals
        for (int i = 0; i < normals.Length; i += 3)
        {
            var v = new Vector3(normals[i], normals[i + 1], normals[i + 2]);
            if (v.LengthSquared() > 0)
            {
                v = Vector3.Normalize(v);
                normals[i] = v.X;
                normals[i + 1] = v.Y;
                normals[i + 2] = v.Z;
            }
            else
            {
                normals[i] = 0;
                normals[i + 1] = 1;
                normals[i + 2] = 0;
            }
        }

        return normals.ToList();
    }

    private class MeshBuilder
    {
        public string Name { get; set; } = "Default";
        public string GroupName { get; set; } = "Default";
        public string MaterialName { get; set; } = "default";
        public List<float> Vertices { get; } = new();
        public List<float> Normals { get; } = new();
        public List<float> TexCoords { get; } = new();
        public List<uint> Indices { get; } = new();
        public Dictionary<string, uint> VertexMap { get; } = new();
        public uint CurrentIndex { get; set; } = 0;
        public int MaterialIndex { get; set; } = 0;
    }

    private static void ParseMtlFile(string mtlPath, List<PbrMaterial> materials, Dictionary<string, int> materialMap)
    {
        try
        {
            var lines = File.ReadAllLines(mtlPath);
            string materialDirectory = Path.GetDirectoryName(mtlPath) ?? "";
            PbrMaterial? current = null;
            foreach (var raw in lines)
            {
                var l = raw.Trim();
                if (string.IsNullOrEmpty(l) || l.StartsWith('#')) continue;
                var p = l.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (l.StartsWith("newmtl ", StringComparison.OrdinalIgnoreCase))
                {
                    string materialName = l["newmtl".Length..].Trim();
                    current = new PbrMaterial
                    {
                        Name = materialName,
                        SourceName = materialName
                    };
                    materials.Add(current);
                    materialMap[materialName] = materials.Count - 1;
                }
                else if (current != null)
                {
                    if (p[0] == "Kd" && p.Length >= 4)
                        current.Albedo = new Vector3(ParseFloat(p[1]), ParseFloat(p[2]), ParseFloat(p[3]));
                    else if (p[0] == "d" && p.Length >= 2)
                        current.Opacity = ParseFloat(p[1]);
                    else if (p[0] == "Tr" && p.Length >= 2)
                        current.Opacity = 1.0f - ParseFloat(p[1]);
                    else if (p[0] == "Ke" && p.Length >= 4)
                        current.Emissive = new Vector3(ParseFloat(p[1]), ParseFloat(p[2]), ParseFloat(p[3]));
                    else if (p[0] == "Ks" && p.Length >= 4)
                    {
                        float spec = Math.Max(ParseFloat(p[1]), Math.Max(ParseFloat(p[2]), ParseFloat(p[3])));
                        if (spec > 0.1f) current.Metallic = Math.Min(1.0f, spec);
                    }
                    else if (p[0] == "Ns" && p.Length >= 2)
                        current.Roughness = 1.0f - Math.Min(1.0f, ParseFloat(p[1]) / 1000f);
                    else if (l.StartsWith("map_Kd ", StringComparison.OrdinalIgnoreCase))
                        current.AlbedoTexturePath = ParseTexturePath(l, "map_Kd", materialDirectory);
                    else if (l.StartsWith("map_bump ", StringComparison.OrdinalIgnoreCase))
                        current.NormalTexturePath = ParseTexturePath(l, "map_bump", materialDirectory);
                    else if (l.StartsWith("bump ", StringComparison.OrdinalIgnoreCase))
                        current.NormalTexturePath = ParseTexturePath(l, "bump", materialDirectory);
                    else if (l.StartsWith("norm ", StringComparison.OrdinalIgnoreCase))
                        current.NormalTexturePath = ParseTexturePath(l, "norm", materialDirectory);
                    else if (l.StartsWith("map_Pr ", StringComparison.OrdinalIgnoreCase))
                        current.RoughnessTexturePath = ParseTexturePath(l, "map_Pr", materialDirectory);
                }
            }
        }
        catch { }
    }

    private static string? ParseTexturePath(string line, string keyword, string baseDirectory)
    {
        string remainder = line[keyword.Length..].Trim();
        if (string.IsNullOrWhiteSpace(remainder))
            return null;

        string? directPath = ResolveTextureCandidate(baseDirectory, remainder);
        if (directPath != null)
            return directPath;

        var tokens = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < tokens.Length; index++)
        {
            string candidate = string.Join(' ', tokens.Skip(index));
            string? resolved = ResolveTextureCandidate(baseDirectory, candidate);
            if (resolved != null)
                return resolved;
        }

        return null;
    }

    private static string? ResolveTextureCandidate(string baseDirectory, string candidate)
    {
        string normalized = candidate.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith("-", StringComparison.Ordinal))
            return null;

        string fullPath = Path.IsPathRooted(normalized)
            ? normalized
            : Path.Combine(baseDirectory, normalized);

        try
        {
            fullPath = Path.GetFullPath(fullPath);
        }
        catch
        {
            return null;
        }

        return File.Exists(fullPath) ? fullPath : null;
    }

    private static float ParseFloat(string s)
    {
        return float.Parse(s, CultureInfo.InvariantCulture);
    }

    private static string BuildMeshName(string groupName, string materialName)
    {
        var cleanGroup = string.IsNullOrWhiteSpace(groupName) ? "Object" : groupName.Trim();
        var cleanMaterial = string.IsNullOrWhiteSpace(materialName) ? "default" : materialName.Trim();

        if (cleanGroup.Equals(cleanMaterial, StringComparison.OrdinalIgnoreCase))
            return cleanGroup;

        return $"{cleanGroup} [{cleanMaterial}]";
    }
}
