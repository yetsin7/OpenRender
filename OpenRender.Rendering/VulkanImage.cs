using System;
using Silk.NET.Vulkan;

namespace OpenRender.Rendering;

public unsafe class VulkanImage : IDisposable
{
    private readonly VulkanContext _context;
    private Image _image;
    private DeviceMemory _memory;
    private ImageView _view;
    private uint _width;
    private uint _height;

    public Image Image => _image;
    public DeviceMemory Memory => _memory;
    public ImageView View => _view;

    public VulkanImage(VulkanContext context, uint width, uint height, Format format, ImageUsageFlags usage, MemoryPropertyFlags properties, ImageAspectFlags aspectFlags)
    {
        _context = context;
        _width = width;
        _height = height;

        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D { Width = width, Height = height, Depth = 1 },
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
            Samples = SampleCountFlags.Count1Bit
        };

        if (_context.Vk.CreateImage(_context.Device, &imageInfo, null, out _image) != Result.Success)
            throw new Exception("Failed to create image.");

        _context.Vk.GetImageMemoryRequirements(_context.Device, _image, out var memRequirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties)
        };

        if (_context.Vk.AllocateMemory(_context.Device, &allocInfo, null, out _memory) != Result.Success)
            throw new Exception("Failed to allocate image memory.");

        _context.Vk.BindImageMemory(_context.Device, _image, _memory, 0);

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = aspectFlags,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        if (_context.Vk.CreateImageView(_context.Device, &viewInfo, null, out _view) != Result.Success)
            throw new Exception("Failed to create image view.");
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _context.Vk.GetPhysicalDeviceMemoryProperties(_context.PhysicalDevice, out var memProperties);
        for (int i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
                return (uint)i;
        }
        throw new Exception("Failed to find suitable memory type.");
    }

    public void Dispose()
    {
        _context.Vk.DestroyImageView(_context.Device, _view, null);
        _context.Vk.DestroyImage(_context.Device, _image, null);
        _context.Vk.FreeMemory(_context.Device, _memory, null);
    }
}
