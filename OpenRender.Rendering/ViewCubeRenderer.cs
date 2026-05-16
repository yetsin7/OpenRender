using System;
using System.Numerics;
using Silk.NET.OpenGL;
using OpenRender.Core.Scene;
using OpenRender.Rendering.Shaders;

namespace OpenRender.Rendering;

/// <summary>
/// Renders an orientation cube overlay inspired by Autodesk Revit's ViewCube.
/// The cube is only visible while the camera is in perspective/3D mode.
/// </summary>
public class ViewCubeRenderer : IDisposable
{
    private const int CubeSizePx = 132;
    private const int CubePaddingPx = 18;

    private readonly GL _gl;
    private ShaderProgram? _cubeShader;
    private ShaderProgram? _lineShader;
    private GpuMesh? _cubeMesh;
    private GpuMesh? _ringMesh;
    private bool _disposed;

    public ViewCubeRenderer(GL gl)
    {
        _gl = gl;
    }

    public void Initialize()
    {
        const string cubeVertexSource = @"#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 vNormal;
out vec3 vWorldPos;

void main()
{
    vec4 worldPos = uModel * vec4(aPos, 1.0);
    vWorldPos = worldPos.xyz;
    vNormal = mat3(uModel) * aNormal;
    gl_Position = uProjection * uView * worldPos;
}";

        const string cubeFragmentSource = @"#version 330 core
in vec3 vNormal;
in vec3 vWorldPos;

out vec4 FragColor;

void main()
{
    vec3 n = normalize(vNormal);

    vec3 baseColor = vec3(0.84, 0.86, 0.89);
    vec3 topColor = vec3(0.78, 0.86, 0.80);
    vec3 sideColor = vec3(0.71, 0.74, 0.79);
    vec3 frontColor = vec3(0.88, 0.89, 0.92);

    vec3 faceColor = baseColor;
    if (n.y > 0.7)
        faceColor = topColor;
    else if (abs(n.x) > 0.7)
        faceColor = sideColor;
    else if (n.z > 0.7)
        faceColor = frontColor;

    vec3 lightDir = normalize(vec3(-0.45, 0.85, 0.55));
    float diffuse = max(dot(n, lightDir), 0.0);
    float lighting = 0.56 + diffuse * 0.44;

    vec3 absPos = abs(vWorldPos);
    float edge = max(max(absPos.x, absPos.y), absPos.z);
    float edgeDarken = smoothstep(0.42, 0.50, edge) * 0.18;

    vec3 color = faceColor * lighting - vec3(edgeDarken);
    FragColor = vec4(color, 1.0);
}";

        const string lineVertexSource = @"#version 330 core
layout (location = 0) in vec3 aPos;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

void main()
{
    gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0);
}";

        const string lineFragmentSource = @"#version 330 core
uniform vec3 uColor;
out vec4 FragColor;

void main()
{
    FragColor = vec4(uColor, 1.0);
}";

        _cubeShader = new ShaderProgram(_gl, cubeVertexSource, cubeFragmentSource);
        _lineShader = new ShaderProgram(_gl, lineVertexSource, lineFragmentSource);
        _cubeMesh = new GpuMesh(_gl, Primitives.PrimitiveGenerator.CreateCube(1.0f));
        _ringMesh = new GpuMesh(_gl, CreateCompassRingMesh());
    }

    public void Render(Camera mainCamera, int viewportWidth, int viewportHeight)
    {
        if (_cubeShader == null || _lineShader == null || _cubeMesh == null || _ringMesh == null)
            return;

        if (!mainCamera.IsPerspective || viewportWidth <= 0 || viewportHeight <= 0)
            return;

        int x = Math.Max(0, viewportWidth - CubeSizePx - CubePaddingPx);
        int y = Math.Max(0, viewportHeight - CubeSizePx - CubePaddingPx);

        _gl.Viewport(x, y, CubeSizePx, CubeSizePx);
        _gl.Clear(ClearBufferMask.DepthBufferBit);

        var overlayProjection = Matrix4x4.CreateOrthographic(4.0f, 4.0f, -5f, 5f);
        var overlayView = Matrix4x4.Identity;

        _gl.Disable(EnableCap.DepthTest);
        _lineShader.Use();
        _lineShader.SetMat4("uView", overlayView);
        _lineShader.SetMat4("uProjection", overlayProjection);
        _lineShader.SetMat4("uModel", Matrix4x4.Identity);
        _lineShader.SetVec3("uColor", 0.73f, 0.74f, 0.76f);
        _ringMesh.Draw(PrimitiveType.Lines);

        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);

        _cubeShader.Use();

        float yawRad = DegreesToRadians(mainCamera.Yaw + 90f);
        float pitchRad = DegreesToRadians(mainCamera.Pitch);

        var model =
            Matrix4x4.CreateScale(0.92f) *
            Matrix4x4.CreateRotationY(-yawRad) *
            Matrix4x4.CreateRotationX(-pitchRad) *
            Matrix4x4.CreateTranslation(0f, 0.18f, 0f);

        var view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 4.2f), Vector3.Zero, Vector3.UnitY);
        var projection = Matrix4x4.CreateOrthographic(3.6f, 3.6f, 0.1f, 20f);

        _cubeShader.SetMat4("uModel", model);
        _cubeShader.SetMat4("uView", view);
        _cubeShader.SetMat4("uProjection", projection);
        _cubeMesh.Draw();

        _gl.Viewport(0, 0, (uint)viewportWidth, (uint)viewportHeight);
    }

    public string? HitTest(
        double mouseX,
        double mouseY,
        Camera mainCamera,
        double controlWidth,
        double controlHeight,
        double dpiScale)
    {
        if (!mainCamera.IsPerspective)
            return null;

        if (controlWidth <= 0 || controlHeight <= 0 || dpiScale <= 0)
            return null;

        double sizeUi = CubeSizePx / dpiScale;
        double paddingUi = CubePaddingPx / dpiScale;
        double cubeX = controlWidth - sizeUi - paddingUi;
        double cubeY = paddingUi;

        if (mouseX < cubeX || mouseX > cubeX + sizeUi ||
            mouseY < cubeY || mouseY > cubeY + sizeUi)
        {
            return null;
        }

        float ndcX = (float)(((mouseX - cubeX) / sizeUi) * 2.0 - 1.0);
        float ndcY = (float)(1.0 - ((mouseY - cubeY) / sizeUi) * 2.0);

        float yawRad = DegreesToRadians(mainCamera.Yaw + 90f);
        float pitchRad = DegreesToRadians(mainCamera.Pitch);

        var model =
            Matrix4x4.CreateScale(0.92f) *
            Matrix4x4.CreateRotationY(-yawRad) *
            Matrix4x4.CreateRotationX(-pitchRad) *
            Matrix4x4.CreateTranslation(0f, 0.18f, 0f);

        var view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 4.2f), Vector3.Zero, Vector3.UnitY);
        var projection = Matrix4x4.CreateOrthographic(3.6f, 3.6f, 0.1f, 20f);

        var mvp = model * view * projection;
        if (!Matrix4x4.Invert(mvp, out var inverseMvp))
            return null;

        var nearPoint = Vector4.Transform(new Vector4(ndcX, ndcY, -1f, 1f), inverseMvp);
        var farPoint = Vector4.Transform(new Vector4(ndcX, ndcY, 1f, 1f), inverseMvp);

        if (MathF.Abs(nearPoint.W) < 0.00001f || MathF.Abs(farPoint.W) < 0.00001f)
            return null;

        nearPoint /= nearPoint.W;
        farPoint /= farPoint.W;

        var rayOrigin = new Vector3(nearPoint.X, nearPoint.Y, nearPoint.Z);
        var rayDirection = Vector3.Normalize(new Vector3(
            farPoint.X - nearPoint.X,
            farPoint.Y - nearPoint.Y,
            farPoint.Z - nearPoint.Z));

        if (!IntersectUnitCube(rayOrigin, rayDirection, out float distance))
            return null;

        var hitPoint = rayOrigin + rayDirection * distance;
        return GetDominantFace(hitPoint);
    }

    private static MeshData CreateCompassRingMesh()
    {
        const int segments = 40;
        const float radiusX = 1.40f;
        const float radiusY = 0.48f;
        const float dashRatio = 0.55f;
        const float centerY = -0.88f;

        var vertices = new List<float>();
        var indices = new List<uint>();
        uint currentIndex = 0;

        for (int i = 0; i < segments; i++)
        {
            float startAngle = MathF.Tau * i / segments;
            float endAngle = MathF.Tau * (i + dashRatio) / segments;

            Vector3 a = new(radiusX * MathF.Cos(startAngle), centerY + radiusY * MathF.Sin(startAngle), 0f);
            Vector3 b = new(radiusX * MathF.Cos(endAngle), centerY + radiusY * MathF.Sin(endAngle), 0f);

            vertices.AddRange(new[] { a.X, a.Y, a.Z, b.X, b.Y, b.Z });
            indices.Add(currentIndex++);
            indices.Add(currentIndex++);
        }

        AddArrow(vertices, indices, ref currentIndex, new Vector3(0f, centerY - radiusY - 0.10f, 0f), new Vector3(0f, -1f, 0f));
        AddArrow(vertices, indices, ref currentIndex, new Vector3(radiusX + 0.10f, centerY, 0f), new Vector3(1f, 0f, 0f));
        AddArrow(vertices, indices, ref currentIndex, new Vector3(-radiusX - 0.10f, centerY, 0f), new Vector3(-1f, 0f, 0f));

        return new MeshData
        {
            Name = "ViewCubeCompass",
            Vertices = vertices.ToArray(),
            Indices = indices.ToArray()
        };
    }

    private static void AddArrow(List<float> vertices, List<uint> indices, ref uint currentIndex, Vector3 tip, Vector3 direction)
    {
        Vector3 dir = Vector3.Normalize(direction);
        Vector3 side = new(-dir.Y, dir.X, 0f);
        float arrowLength = 0.16f;
        float arrowWidth = 0.10f;

        Vector3 baseCenter = tip - dir * arrowLength;
        Vector3 left = baseCenter + side * arrowWidth;
        Vector3 right = baseCenter - side * arrowWidth;

        vertices.AddRange(new[] { tip.X, tip.Y, tip.Z, left.X, left.Y, left.Z });
        indices.Add(currentIndex++);
        indices.Add(currentIndex++);

        vertices.AddRange(new[] { tip.X, tip.Y, tip.Z, right.X, right.Y, right.Z });
        indices.Add(currentIndex++);
        indices.Add(currentIndex++);
    }

    private static bool IntersectUnitCube(Vector3 rayOrigin, Vector3 rayDirection, out float distance)
    {
        distance = 0f;

        Vector3 min = new(-0.47f, -0.29f, -0.47f);
        Vector3 max = new(0.47f, 0.65f, 0.47f);

        float tMin = 0f;
        float tMax = float.PositiveInfinity;

        if (!UpdateSlab(rayOrigin.X, rayDirection.X, min.X, max.X, ref tMin, ref tMax)) return false;
        if (!UpdateSlab(rayOrigin.Y, rayDirection.Y, min.Y, max.Y, ref tMin, ref tMax)) return false;
        if (!UpdateSlab(rayOrigin.Z, rayDirection.Z, min.Z, max.Z, ref tMin, ref tMax)) return false;

        distance = tMin >= 0f ? tMin : tMax;
        return distance >= 0f && !float.IsInfinity(distance) && !float.IsNaN(distance);
    }

    private static bool UpdateSlab(
        float origin,
        float direction,
        float min,
        float max,
        ref float tMin,
        ref float tMax)
    {
        if (MathF.Abs(direction) < 0.00001f)
            return origin >= min && origin <= max;

        float t1 = (min - origin) / direction;
        float t2 = (max - origin) / direction;

        if (t1 > t2)
            (t1, t2) = (t2, t1);

        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);

        return tMin <= tMax;
    }

    private static string GetDominantFace(Vector3 hitPoint)
    {
        float ax = MathF.Abs(hitPoint.X);
        float ay = MathF.Abs(hitPoint.Y);
        float az = MathF.Abs(hitPoint.Z);

        if (az >= ax && az >= ay)
            return hitPoint.Z >= 0f ? "Front" : "Back";

        if (ax >= ay && ax >= az)
            return hitPoint.X >= 0f ? "Right" : "Left";

        return hitPoint.Y >= 0f ? "Top" : "Bottom";
    }

    private static float DegreesToRadians(float degrees)
    {
        return MathF.PI / 180f * degrees;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cubeMesh?.Dispose();
        _ringMesh?.Dispose();
        _cubeShader?.Dispose();
        _lineShader?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
