using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace OpenRender.Rendering;

public unsafe partial class VulkanRenderer : IDisposable
{
    private readonly VulkanContext _context;
    private KhrSurface? _surfaceExt;
    private KhrWin32Surface? _win32SurfaceExt;
    private SurfaceKHR _surface;
    private KhrSwapchain? _swapchainExt;
    private SwapchainKHR _swapchain;
    private Image[] _swapchainImages = Array.Empty<Image>();
    private ImageView[] _swapchainImageViews = Array.Empty<ImageView>();
    private Format _swapchainFormat;
    private Extent2D _swapchainExtent;
    private VulkanImage? _depthImage;
    private Format _depthFormat;
    private VulkanImage? _gPosition;
    private VulkanImage? _gNormal;
    private VulkanImage? _ssaoImage;
    private VulkanImage? _bloomImage;
    private SSAOResources? _ssaoResources;
    private FullscreenQuad? _fullscreenQuad;
    private VulkanSampler? _defaultSampler;
    private RenderPass _renderPass;
    private DescriptorSetLayout _descriptorSetLayout;
    private DescriptorSetLayout _ssaoDescriptorSetLayout;
    private DescriptorSetLayout _compositeDescriptorSetLayout;
    private PipelineLayout _pipelineLayout;
    private PipelineLayout _ssaoPipelineLayout;
    private PipelineLayout _compositePipelineLayout;
    private Pipeline _graphicsPipeline;
    private Pipeline _ssaoPipeline;
    private Pipeline _compositePipeline;
    private Framebuffer[] _swapchainFramebuffers = Array.Empty<Framebuffer>();
    private DescriptorSet[] _descriptorSets = Array.Empty<DescriptorSet>();
    private DescriptorSet[] _ssaoDescriptorSets = Array.Empty<DescriptorSet>();
    private DescriptorSet[] _compositeDescriptorSets = Array.Empty<DescriptorSet>();
    private CommandPool _commandPool;
    private CommandBuffer _commandBuffer;
    private Silk.NET.Vulkan.Semaphore _imageAvailableSemaphore;
    private Silk.NET.Vulkan.Semaphore _renderFinishedSemaphore;
    private Fence _inFlightFence;
    private DescriptorPool _descriptorPool;
    private VulkanBuffer[] _uniformBuffers = Array.Empty<VulkanBuffer>();
    private VulkanBuffer[] _ssaoParamsBuffers = Array.Empty<VulkanBuffer>();
    private readonly bool _advancedPipelineEnabled = IsAdvancedPipelineEnabled();
    private readonly bool _frameSubmitEnabled = IsFrameSubmitEnabled();
    private readonly List<GpuMesh> _meshes = new();

    public VulkanRenderer(VulkanContext context)
    {
        _context = context;
    }

    public bool AdvancedPipelineEnabled => _advancedPipelineEnabled;
    public bool FrameSubmitEnabled => _frameSubmitEnabled;

    public void InitializeSurface(IntPtr hwnd)
    {
        _context.Vk.TryGetInstanceExtension(_context.Instance, out _surfaceExt);
        _context.Vk.TryGetInstanceExtension(_context.Instance, out _win32SurfaceExt);

        var createInfo = new Win32SurfaceCreateInfoKHR
        {
            SType = StructureType.Win32SurfaceCreateInfoKhr,
            Hwnd = hwnd,
            Hinstance = GetModuleHandle(null)
        };

        _win32SurfaceExt!.CreateWin32Surface(_context.Instance, &createInfo, null, out _surface);
        CreateSwapchainResources(1280, 720);
        CreateRenderTargets();
        CreatePipelineResources();
        CreateSyncObjects();
        CreateCommandPool();
    }

    public void Resize(uint width, uint height)
    {
        if (width == 0 || height == 0)
            return;

        if (_swapchainExtent.Width == width && _swapchainExtent.Height == height)
            return;

        _context.Vk.DeviceWaitIdle(_context.Device);
        CleanupSwapchain();
        CreateSwapchainResources(width, height);
        CreateRenderTargets();
    }

    public GpuMesh AddMesh(Vertex[] vertices, uint[] indices)
    {
        var mesh = new GpuMesh(_context, vertices, indices);
        _meshes.Add(mesh);
        return mesh;
    }

    public void ExecuteMainPass(CommandBuffer commandBuffer, Matrix4x4 viewProjection, Vector3 cameraPosition, float time)
    {
        _context.Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _graphicsPipeline);
        var vertexBuffers = stackalloc Silk.NET.Vulkan.Buffer[2];
        var offsets = stackalloc ulong[2];
        offsets[0] = 0;
        offsets[1] = 0;

        foreach (var mesh in _meshes)
        {
            vertexBuffers[0] = mesh.VertexBuffer.Buffer;
            vertexBuffers[1] = mesh.InstanceBuffer!.Buffer;
            _context.Vk.CmdBindVertexBuffers(commandBuffer, 0, 2, vertexBuffers, offsets);
            _context.Vk.CmdBindIndexBuffer(commandBuffer, mesh.IndexBuffer.Buffer, 0, IndexType.Uint32);
            _context.Vk.CmdDrawIndexed(commandBuffer, mesh.IndexCount, mesh.InstanceCount, 0, 0, 0);
        }
    }

    public void ExecuteSSAOPass(CommandBuffer commandBuffer, uint imageIndex, Matrix4x4 projection)
    {
        _context.Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _ssaoPipeline);
        _context.Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, _ssaoPipelineLayout, 0, 1, ref _ssaoDescriptorSets[imageIndex], 0, null);
        _fullscreenQuad!.BindAndDraw(commandBuffer, _context.Vk);
    }

    public void ExecuteCompositePass(CommandBuffer commandBuffer, uint imageIndex)
    {
        _context.Vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _compositePipeline);
        _context.Vk.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, _compositePipelineLayout, 0, 1, ref _compositeDescriptorSets[imageIndex], 0, null);
        _fullscreenQuad!.BindAndDraw(commandBuffer, _context.Vk);
    }

    private static bool IsAdvancedPipelineEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("OPENRENDER_ENABLE_VULKAN_ADVANCED_PIPELINE"),
            "1",
            StringComparison.Ordinal);
    }

    private static bool IsFrameSubmitEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("OPENRENDER_ENABLE_VULKAN_FRAME_SUBMIT"),
            "1",
            StringComparison.Ordinal);
    }

    private static void Check(Result result, string operation)
    {
        if (result != Result.Success)
            throw new InvalidOperationException($"Vulkan failed to {operation}. Result: {result}");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lp);

    public void Dispose()
    {
        _context.Vk.DeviceWaitIdle(_context.Device);
        ClearSceneMeshes();
        CleanupSwapchain();
        DisposeBufferArray(_uniformBuffers);
        DisposeBufferArray(_ssaoParamsBuffers);
        _uniformBuffers = Array.Empty<VulkanBuffer>();
        _ssaoParamsBuffers = Array.Empty<VulkanBuffer>();
        DisposeCoreResources();
    }

    private static void DisposeBufferArray(IEnumerable<VulkanBuffer> buffers)
    {
        foreach (var buffer in buffers)
            buffer.Dispose();
    }

    private void DisposeCoreResources()
    {
        if (_descriptorPool.Handle != 0)
            _context.Vk.DestroyDescriptorPool(_context.Device, _descriptorPool, null);
        if (_graphicsPipeline.Handle != 0)
            _context.Vk.DestroyPipeline(_context.Device, _graphicsPipeline, null);
        if (_pipelineLayout.Handle != 0)
            _context.Vk.DestroyPipelineLayout(_context.Device, _pipelineLayout, null);
        if (_ssaoPipeline.Handle != 0)
            _context.Vk.DestroyPipeline(_context.Device, _ssaoPipeline, null);
        if (_ssaoPipelineLayout.Handle != 0)
            _context.Vk.DestroyPipelineLayout(_context.Device, _ssaoPipelineLayout, null);
        if (_compositePipeline.Handle != 0)
            _context.Vk.DestroyPipeline(_context.Device, _compositePipeline, null);
        if (_compositePipelineLayout.Handle != 0)
            _context.Vk.DestroyPipelineLayout(_context.Device, _compositePipelineLayout, null);
        if (_descriptorSetLayout.Handle != 0)
            _context.Vk.DestroyDescriptorSetLayout(_context.Device, _descriptorSetLayout, null);
        if (_ssaoDescriptorSetLayout.Handle != 0)
            _context.Vk.DestroyDescriptorSetLayout(_context.Device, _ssaoDescriptorSetLayout, null);
        if (_compositeDescriptorSetLayout.Handle != 0)
            _context.Vk.DestroyDescriptorSetLayout(_context.Device, _compositeDescriptorSetLayout, null);
        if (_renderPass.Handle != 0)
            _context.Vk.DestroyRenderPass(_context.Device, _renderPass, null);
        if (_renderFinishedSemaphore.Handle != 0)
            _context.Vk.DestroySemaphore(_context.Device, _renderFinishedSemaphore, null);
        if (_imageAvailableSemaphore.Handle != 0)
            _context.Vk.DestroySemaphore(_context.Device, _imageAvailableSemaphore, null);
        if (_inFlightFence.Handle != 0)
            _context.Vk.DestroyFence(_context.Device, _inFlightFence, null);
        if (_commandPool.Handle != 0)
            _context.Vk.DestroyCommandPool(_context.Device, _commandPool, null);
        if (_surface.Handle != 0)
            _surfaceExt?.DestroySurface(_context.Instance, _surface, null);

        _ssaoResources?.Dispose();
        _fullscreenQuad?.Dispose();
        _defaultSampler?.Dispose();
    }
}
