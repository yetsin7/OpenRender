using System;
using Silk.NET.Vulkan;

namespace OpenRender.Rendering;

public unsafe class VulkanSampler : IDisposable
{
    private readonly VulkanContext _context;
    private Sampler _sampler;

    public Sampler Sampler => _sampler;

    public VulkanSampler(VulkanContext context)
    {
        _context = context;

        var samplerInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            AnisotropyEnable = true,
            MaxAnisotropy = 16.0f,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            MipmapMode = SamplerMipmapMode.Linear
        };

        if (_context.Vk.CreateSampler(_context.Device, &samplerInfo, null, out _sampler) != Result.Success)
            throw new Exception("Failed to create sampler.");
    }

    public void Dispose()
    {
        _context.Vk.DestroySampler(_context.Device, _sampler, null);
    }
}
