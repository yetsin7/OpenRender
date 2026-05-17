using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenRender.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct SceneBuffer
{
    public Matrix4x4 ViewProjection;
    public Vector3 CameraPos;
    public float Time;
    public Vector3 LightDir;
    private float _pad1;
    public Vector3 LightColor;
    public float LightIntensity;
}

[StructLayout(LayoutKind.Sequential)]
public struct ModelBuffer
{
    public Matrix4x4 ModelMatrix;
}

[StructLayout(LayoutKind.Sequential)]
public struct SSAOParams
{
    public Matrix4x4 Projection;
    public float Radius;
    public float Bias;
    public Vector2 NoiseScale;
    // Note: Samples are managed via raw data upload for simplicity with Silk.NET UpdateData
}
