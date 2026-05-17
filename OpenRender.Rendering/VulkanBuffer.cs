using System;
using Silk.NET.Vulkan;
using Silk.NET.Core.Native;

namespace OpenRender.Rendering;

public unsafe class VulkanBuffer : IDisposable
{
    private readonly VulkanContext _context;
    private Silk.NET.Vulkan.Buffer _buffer;
    private DeviceMemory _memory;
    private ulong _size;

    public Silk.NET.Vulkan.Buffer Buffer => _buffer;
    public DeviceMemory Memory => _memory;
    public ulong Size => _size;

    public VulkanBuffer(VulkanContext context, ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties)
    {
        _context = context;
        _size = size;

        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        if (_context.Vk.CreateBuffer(_context.Device, &bufferInfo, null, out _buffer) != Result.Success)
        {
            throw new Exception("Failed to create buffer.");
        }

        _context.Vk.GetBufferMemoryRequirements(_context.Device, _buffer, out var memRequirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties)
        };

        if (_context.Vk.AllocateMemory(_context.Device, &allocInfo, null, out _memory) != Result.Success)
        {
            throw new Exception("Failed to allocate buffer memory.");
        }

        _context.Vk.BindBufferMemory(_context.Device, _buffer, _memory, 0);
    }

    public void UpdateData<T>(T[] data) where T : unmanaged
    {
        void* pData;
        _context.Vk.MapMemory(_context.Device, _memory, 0, _size, 0, &pData);
        data.AsSpan().CopyTo(new Span<T>(pData, data.Length));
        _context.Vk.UnmapMemory(_context.Device, _memory);
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _context.Vk.GetPhysicalDeviceMemoryProperties(_context.PhysicalDevice, out var memProperties);

        for (int i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                return (uint)i;
            }
        }

        throw new Exception("Failed to find suitable memory type.");
    }

    public void Dispose()
    {
        _context.Vk.DestroyBuffer(_context.Device, _buffer, null);
        _context.Vk.FreeMemory(_context.Device, _memory, null);
    }
}
