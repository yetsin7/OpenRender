using System;
using System.Numerics;
using Silk.NET.Vulkan;

namespace OpenRender.Rendering;

public class GpuMesh : IDisposable
{
    private readonly VulkanContext _context;
    private readonly VulkanBuffer _vertexBuffer;
    private readonly VulkanBuffer _indexBuffer;
    private readonly uint _indexCount;

    private VulkanBuffer? _instanceBuffer;
    private uint _instanceCount;

    public VulkanBuffer VertexBuffer => _vertexBuffer;
    public VulkanBuffer IndexBuffer => _indexBuffer;
    public uint IndexCount => _indexCount;

    public VulkanBuffer? InstanceBuffer => _instanceBuffer;
    public uint InstanceCount => _instanceCount;

    public GpuMesh(VulkanContext context, Vertex[] vertices, uint[] indices)
    {
        _context = context;
        _indexCount = (uint)indices.Length;
        _instanceCount = 1;

        _vertexBuffer = new VulkanBuffer(
            context,
            (ulong)(vertices.Length * System.Runtime.InteropServices.Marshal.SizeOf<Vertex>()),
            BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
        );
        _vertexBuffer.UpdateData(vertices);

        _indexBuffer = new VulkanBuffer(
            context,
            (ulong)(indices.Length * sizeof(uint)),
            BufferUsageFlags.IndexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
        );
        _indexBuffer.UpdateData(indices);
        CreateInstanceBuffer(new[] { Matrix4x4.Identity });
    }

    public void SetupInstancing(Matrix4x4[] instances)
    {
        CreateInstanceBuffer(instances.Length == 0 ? new[] { Matrix4x4.Identity } : instances);
    }

    private void CreateInstanceBuffer(Matrix4x4[] instances)
    {
        _instanceCount = (uint)instances.Length;

        _instanceBuffer?.Dispose();
        _instanceBuffer = new VulkanBuffer(
            _context,
            (ulong)(instances.Length * System.Runtime.InteropServices.Marshal.SizeOf<Matrix4x4>()),
            BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
        );
        _instanceBuffer.UpdateData(instances);
    }

    public void Dispose()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _instanceBuffer?.Dispose();
    }
}
