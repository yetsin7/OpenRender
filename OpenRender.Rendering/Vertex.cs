using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace OpenRender.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TexCoord;
    public Vector3 Tangent;

    public static VertexInputBindingDescription GetBindingDescription()
    {
        return new VertexInputBindingDescription
        {
            Binding = 0,
            Stride = (uint)Marshal.SizeOf<Vertex>(),
            InputRate = VertexInputRate.Vertex
        };
    }

    public static VertexInputAttributeDescription[] GetAttributeDescriptions()
    {
        return new[]
        {
            // Vertex Data (Binding 0)
            new VertexInputAttributeDescription { Binding = 0, Location = 0, Format = Format.R32G32B32Sfloat, Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Position)) },
            new VertexInputAttributeDescription { Binding = 0, Location = 1, Format = Format.R32G32B32Sfloat, Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Normal)) },
            new VertexInputAttributeDescription { Binding = 0, Location = 2, Format = Format.R32G32Sfloat, Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(TexCoord)) },
            new VertexInputAttributeDescription { Binding = 0, Location = 3, Format = Format.R32G32B32Sfloat, Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Tangent)) },
            
            // Instance Data (Binding 1)
            new VertexInputAttributeDescription { Binding = 1, Location = 4, Format = Format.R32G32B32A32Sfloat, Offset = 0 },
            new VertexInputAttributeDescription { Binding = 1, Location = 5, Format = Format.R32G32B32A32Sfloat, Offset = 16 },
            new VertexInputAttributeDescription { Binding = 1, Location = 6, Format = Format.R32G32B32A32Sfloat, Offset = 32 },
            new VertexInputAttributeDescription { Binding = 1, Location = 7, Format = Format.R32G32B32A32Sfloat, Offset = 48 }
        };
    }

    public static VertexInputBindingDescription[] GetBindingDescriptions()
    {
        return new[]
        {
            new VertexInputBindingDescription { Binding = 0, Stride = (uint)Marshal.SizeOf<Vertex>(), InputRate = VertexInputRate.Vertex },
            new VertexInputBindingDescription { Binding = 1, Stride = (uint)Marshal.SizeOf<Matrix4x4>(), InputRate = VertexInputRate.Instance }
        };
    }
}
