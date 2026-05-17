using System;
using Silk.NET.Vulkan;

namespace OpenRender.Rendering;

public unsafe partial class VulkanRenderer
{
    private void CreateSwapchainResources(uint preferredWidth, uint preferredHeight)
    {
        CreateSwapchain(preferredWidth, preferredHeight);
        CreateImageViews();

        if (_advancedPipelineEnabled)
        {
            CreateGBufferResources();
            CreateDepthResources();
        }
    }

    private void CreateRenderTargets()
    {
        CreateRenderPass();
        CreateFramebuffers();
    }

    private void CreateSwapchain(uint preferredWidth, uint preferredHeight)
    {
        _context.Vk.TryGetDeviceExtension(_context.Instance, _context.Device, out _swapchainExt);
        _surfaceExt!.GetPhysicalDeviceSurfaceCapabilities(_context.PhysicalDevice, _surface, out var capabilities);

        uint formatCount = 0;
        _surfaceExt.GetPhysicalDeviceSurfaceFormats(_context.PhysicalDevice, _surface, ref formatCount, null);
        if (formatCount == 0)
            throw new InvalidOperationException("Vulkan surface returned no supported formats.");

        var formats = stackalloc SurfaceFormatKHR[(int)formatCount];
        _surfaceExt.GetPhysicalDeviceSurfaceFormats(_context.PhysicalDevice, _surface, ref formatCount, formats);
        _swapchainFormat = formats[0].Format;
        _swapchainExtent = ResolveSwapchainExtent(capabilities, preferredWidth, preferredHeight);

        uint minImageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount > 0 && minImageCount > capabilities.MaxImageCount)
            minImageCount = capabilities.MaxImageCount;

        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = minImageCount,
            ImageFormat = _swapchainFormat,
            ImageColorSpace = formats[0].ColorSpace,
            ImageExtent = _swapchainExtent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = PresentModeKHR.FifoKhr,
            Clipped = true
        };

        _swapchainExt!.CreateSwapchain(_context.Device, &createInfo, null, out _swapchain);
        _swapchainExt.GetSwapchainImages(_context.Device, _swapchain, ref formatCount, null);
        _swapchainImages = new Image[formatCount];

        fixed (Image* swapchainImages = _swapchainImages)
            _swapchainExt.GetSwapchainImages(_context.Device, _swapchain, ref formatCount, swapchainImages);
    }

    private static Extent2D ResolveSwapchainExtent(SurfaceCapabilitiesKHR capabilities, uint preferredWidth, uint preferredHeight)
    {
        if (capabilities.CurrentExtent.Width > 0 &&
            capabilities.CurrentExtent.Height > 0 &&
            capabilities.CurrentExtent.Width != uint.MaxValue &&
            capabilities.CurrentExtent.Height != uint.MaxValue)
        {
            return capabilities.CurrentExtent;
        }

        return new Extent2D
        {
            Width = ClampExtent(preferredWidth, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width, 1280),
            Height = ClampExtent(preferredHeight, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height, 720)
        };
    }

    private static uint ClampExtent(uint preferred, uint min, uint max, uint fallback)
    {
        uint value = preferred > 0 ? preferred : fallback;
        uint minValue = min > 0 ? min : 1u;
        uint maxValue = max >= minValue ? max : minValue;
        return Math.Clamp(value, minValue, maxValue);
    }

    private void CreateImageViews()
    {
        _swapchainImageViews = new ImageView[_swapchainImages.Length];
        for (int index = 0; index < _swapchainImages.Length; index++)
        {
            var createInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[index],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainFormat,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    LevelCount = 1,
                    LayerCount = 1
                }
            };

            _context.Vk.CreateImageView(_context.Device, &createInfo, null, out _swapchainImageViews[index]);
        }
    }

    private void CreateGBufferResources()
    {
        _gPosition = new VulkanImage(_context, _swapchainExtent.Width, _swapchainExtent.Height, Format.R16G16B16A16Sfloat, ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit, MemoryPropertyFlags.DeviceLocalBit, ImageAspectFlags.ColorBit);
        _gNormal = new VulkanImage(_context, _swapchainExtent.Width, _swapchainExtent.Height, Format.R16G16B16A16Sfloat, ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit, MemoryPropertyFlags.DeviceLocalBit, ImageAspectFlags.ColorBit);
        _ssaoImage = new VulkanImage(_context, _swapchainExtent.Width, _swapchainExtent.Height, Format.R8Unorm, ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit, MemoryPropertyFlags.DeviceLocalBit, ImageAspectFlags.ColorBit);
        _bloomImage = new VulkanImage(_context, _swapchainExtent.Width, _swapchainExtent.Height, Format.R16G16B16A16Sfloat, ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit, MemoryPropertyFlags.DeviceLocalBit, ImageAspectFlags.ColorBit);
        _ssaoResources ??= new SSAOResources(_context);
        _fullscreenQuad ??= new FullscreenQuad(_context);
        _defaultSampler ??= new VulkanSampler(_context);
    }

    private void CreateDepthResources()
    {
        _depthFormat = Format.D32Sfloat;
        _depthImage = new VulkanImage(_context, _swapchainExtent.Width, _swapchainExtent.Height, _depthFormat, ImageUsageFlags.DepthStencilAttachmentBit, MemoryPropertyFlags.DeviceLocalBit, ImageAspectFlags.DepthBit);
    }

    private void CreateRenderPass()
    {
        if (!_advancedPipelineEnabled)
        {
            CreateMinimalRenderPass();
            return;
        }

        var colorAttachment = new AttachmentDescription { Format = _swapchainFormat, Samples = SampleCountFlags.Count1Bit, LoadOp = AttachmentLoadOp.Clear, StoreOp = AttachmentStoreOp.Store, FinalLayout = ImageLayout.PresentSrcKhr };
        var positionAttachment = new AttachmentDescription { Format = Format.R16G16B16A16Sfloat, Samples = SampleCountFlags.Count1Bit, LoadOp = AttachmentLoadOp.Clear, StoreOp = AttachmentStoreOp.Store, FinalLayout = ImageLayout.ShaderReadOnlyOptimal };
        var normalAttachment = new AttachmentDescription { Format = Format.R16G16B16A16Sfloat, Samples = SampleCountFlags.Count1Bit, LoadOp = AttachmentLoadOp.Clear, StoreOp = AttachmentStoreOp.Store, FinalLayout = ImageLayout.ShaderReadOnlyOptimal };
        var depthAttachment = new AttachmentDescription { Format = _depthFormat, Samples = SampleCountFlags.Count1Bit, LoadOp = AttachmentLoadOp.Clear, StoreOp = AttachmentStoreOp.DontCare, FinalLayout = ImageLayout.DepthStencilAttachmentOptimal };
        var colorReferences = stackalloc AttachmentReference[3];
        colorReferences[0] = new AttachmentReference { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };
        colorReferences[1] = new AttachmentReference { Attachment = 1, Layout = ImageLayout.ColorAttachmentOptimal };
        colorReferences[2] = new AttachmentReference { Attachment = 2, Layout = ImageLayout.ColorAttachmentOptimal };
        var depthReference = new AttachmentReference { Attachment = 3, Layout = ImageLayout.DepthStencilAttachmentOptimal };
        var subpass = new SubpassDescription { PipelineBindPoint = PipelineBindPoint.Graphics, ColorAttachmentCount = 3, PColorAttachments = colorReferences, PDepthStencilAttachment = &depthReference };
        var attachments = stackalloc[] { colorAttachment, positionAttachment, normalAttachment, depthAttachment };
        var createInfo = new RenderPassCreateInfo { SType = StructureType.RenderPassCreateInfo, AttachmentCount = 4, PAttachments = attachments, SubpassCount = 1, PSubpasses = &subpass };
        _context.Vk.CreateRenderPass(_context.Device, &createInfo, null, out _renderPass);
    }

    private void CreateMinimalRenderPass()
    {
        var colorAttachment = new AttachmentDescription
        {
            Format = _swapchainFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };
        var colorReference = new AttachmentReference { Attachment = 0, Layout = ImageLayout.ColorAttachmentOptimal };
        var subpass = new SubpassDescription { PipelineBindPoint = PipelineBindPoint.Graphics, ColorAttachmentCount = 1, PColorAttachments = &colorReference };
        var createInfo = new RenderPassCreateInfo { SType = StructureType.RenderPassCreateInfo, AttachmentCount = 1, PAttachments = &colorAttachment, SubpassCount = 1, PSubpasses = &subpass };
        Check(_context.Vk.CreateRenderPass(_context.Device, &createInfo, null, out _renderPass), "create minimal render pass");
    }

    private void CreateFramebuffers()
    {
        _swapchainFramebuffers = new Framebuffer[_swapchainImages.Length];

        for (int index = 0; index < _swapchainImages.Length; index++)
        {
            if (!_advancedPipelineEnabled)
            {
                var attachment = _swapchainImageViews[index];
                var createInfo = new FramebufferCreateInfo
                {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = _renderPass,
                    AttachmentCount = 1,
                    PAttachments = &attachment,
                    Width = _swapchainExtent.Width,
                    Height = _swapchainExtent.Height,
                    Layers = 1
                };
                Check(_context.Vk.CreateFramebuffer(_context.Device, &createInfo, null, out _swapchainFramebuffers[index]), "create minimal framebuffer");
                continue;
            }

            var attachments = stackalloc[] { _swapchainImageViews[index], _gPosition!.View, _gNormal!.View, _depthImage!.View };
            var advancedInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _renderPass,
                AttachmentCount = 4,
                PAttachments = attachments,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                Layers = 1
            };
            Check(_context.Vk.CreateFramebuffer(_context.Device, &advancedInfo, null, out _swapchainFramebuffers[index]), "create advanced framebuffer");
        }
    }

    private void CleanupSwapchain()
    {
        foreach (var framebuffer in _swapchainFramebuffers)
            _context.Vk.DestroyFramebuffer(_context.Device, framebuffer, null);
        foreach (var imageView in _swapchainImageViews)
            _context.Vk.DestroyImageView(_context.Device, imageView, null);

        _depthImage?.Dispose();
        _gPosition?.Dispose();
        _gNormal?.Dispose();
        _ssaoImage?.Dispose();
        _bloomImage?.Dispose();
        _swapchainExt?.DestroySwapchain(_context.Device, _swapchain, null);
    }
}
