using System;
using System.Numerics;
using Silk.NET.Vulkan;

namespace OpenRender.Rendering;

/// <summary>
/// Helper to render a full-screen triangle for post-processing effects.
/// This is more efficient than a quad as it avoids the diagonal split.
/// </summary>
public class FullscreenQuad : IDisposable
{
    private readonly VulkanBuffer _vertexBuffer;

    public VulkanBuffer VertexBuffer => _vertexBuffer;

    public FullscreenQuad(VulkanContext context)
    {
        // Coordinates for a single triangle that covers the whole screen [-1, 1]
        // (x, y, u, v)
        float[] vertices = {
            -1.0f, -1.0f, 0.0f, 0.0f,
             3.0f, -1.0f, 2.0f, 0.0f,
            -1.0f,  3.0f, 0.0f, 2.0f
        };

        _vertexBuffer = new VulkanBuffer(
            context,
            (ulong)(vertices.Length * sizeof(float)),
            BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
        );
        _vertexBuffer.UpdateData(vertices);
    }

    public void BindAndDraw(CommandBuffer cmd, Vk vk)
    {
        var buffer = _vertexBuffer.Buffer;
        ulong offset = 0;
        vk.CmdBindVertexBuffers(cmd, 0, 1, ref buffer, ref offset);
        vk.CmdDraw(cmd, 3, 1, 0, 0);
    }

    public void Dispose()
    {
        _vertexBuffer.Dispose();
    }
}
