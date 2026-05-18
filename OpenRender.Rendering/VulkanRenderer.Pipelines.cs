using System;
using System.IO;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Shaderc;
using Silk.NET.Vulkan;

namespace OpenRender.Rendering;

public unsafe partial class VulkanRenderer
{
    private void CreatePipelineResources()
    {
        CreateDescriptorSetLayouts();
        CreateGraphicsPipeline();
        CreateUniformBuffers();
        CreateDescriptorPool();
        CreateDescriptorSets();

        if (!_advancedPipelineEnabled)
            return;

        CreateSSAOPipeline();
        CreateCompositePipeline();
    }

    private void CreateDescriptorSetLayouts()
    {
        var uniformBinding = new DescriptorSetLayoutBinding { Binding = 0, DescriptorType = DescriptorType.UniformBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit };
        var uniformInfo = new DescriptorSetLayoutCreateInfo { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 1, PBindings = &uniformBinding };
        _context.Vk.CreateDescriptorSetLayout(_context.Device, &uniformInfo, null, out _descriptorSetLayout);

        if (!_advancedPipelineEnabled)
            return;

        var ssaoBindings = stackalloc DescriptorSetLayoutBinding[5];
        ssaoBindings[0] = new DescriptorSetLayoutBinding { Binding = 0, DescriptorType = DescriptorType.UniformBuffer, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        ssaoBindings[1] = new DescriptorSetLayoutBinding { Binding = 1, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        ssaoBindings[2] = new DescriptorSetLayoutBinding { Binding = 2, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        ssaoBindings[3] = new DescriptorSetLayoutBinding { Binding = 3, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        ssaoBindings[4] = new DescriptorSetLayoutBinding { Binding = 4, DescriptorType = DescriptorType.Sampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        var ssaoInfo = new DescriptorSetLayoutCreateInfo { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 5, PBindings = ssaoBindings };
        _context.Vk.CreateDescriptorSetLayout(_context.Device, &ssaoInfo, null, out _ssaoDescriptorSetLayout);

        var compositeBindings = stackalloc DescriptorSetLayoutBinding[4];
        compositeBindings[0] = new DescriptorSetLayoutBinding { Binding = 0, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        compositeBindings[1] = new DescriptorSetLayoutBinding { Binding = 1, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        compositeBindings[2] = new DescriptorSetLayoutBinding { Binding = 2, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        compositeBindings[3] = new DescriptorSetLayoutBinding { Binding = 3, DescriptorType = DescriptorType.Sampler, DescriptorCount = 1, StageFlags = ShaderStageFlags.FragmentBit };
        var compositeInfo = new DescriptorSetLayoutCreateInfo { SType = StructureType.DescriptorSetLayoutCreateInfo, BindingCount = 4, PBindings = compositeBindings };
        _context.Vk.CreateDescriptorSetLayout(_context.Device, &compositeInfo, null, out _compositeDescriptorSetLayout);
    }

    private void CreateGraphicsPipeline()
    {
        string shaderPath = ResolveShaderPath(_advancedPipelineEnabled ? "Pbr.hlsl" : "PbrStable.hlsl");
        var shaderSource = File.ReadAllText(shaderPath);
        using var vertexShader = new VulkanShader(_context, shaderSource, ShaderKind.VertexShader, "VSMain");
        using var fragmentShader = new VulkanShader(_context, shaderSource, ShaderKind.FragmentShader, "PSMain");

        var stages = stackalloc PipelineShaderStageCreateInfo[2];
        stages[0] = CreateShaderStage(ShaderStageFlags.VertexBit, vertexShader.Module, "VSMain");
        stages[1] = CreateShaderStage(ShaderStageFlags.FragmentBit, fragmentShader.Module, "PSMain");

        var bindings = Vertex.GetBindingDescriptions();
        var attributes = Vertex.GetAttributeDescriptions();
        fixed (VertexInputBindingDescription* bindingPointer = bindings)
        fixed (VertexInputAttributeDescription* attributePointer = attributes)
        {
            var vertexInput = new PipelineVertexInputStateCreateInfo { SType = StructureType.PipelineVertexInputStateCreateInfo, VertexBindingDescriptionCount = (uint)bindings.Length, PVertexBindingDescriptions = bindingPointer, VertexAttributeDescriptionCount = (uint)attributes.Length, PVertexAttributeDescriptions = attributePointer };
            CreatePipeline(stages, 2, vertexInput, _descriptorSetLayout, out _pipelineLayout, out _graphicsPipeline, _advancedPipelineEnabled ? 3u : 1u, true);
        }

        FreeShaderStageNames(stages, 2);
    }

    private void CreateSSAOPipeline()
    {
        string shaderPath = ResolveShaderPath("SSAO.hlsl");
        var shaderSource = File.ReadAllText(shaderPath);
        using var vertexShader = new VulkanShader(_context, shaderSource, ShaderKind.VertexShader, "VSMain");
        using var fragmentShader = new VulkanShader(_context, shaderSource, ShaderKind.FragmentShader, "PSMain");

        var stages = stackalloc PipelineShaderStageCreateInfo[2];
        stages[0] = CreateShaderStage(ShaderStageFlags.VertexBit, vertexShader.Module, "VSMain");
        stages[1] = CreateShaderStage(ShaderStageFlags.FragmentBit, fragmentShader.Module, "PSMain");

        var vertexInput = new PipelineVertexInputStateCreateInfo { SType = StructureType.PipelineVertexInputStateCreateInfo };
        CreatePipeline(stages, 2, vertexInput, _ssaoDescriptorSetLayout, out _ssaoPipelineLayout, out _ssaoPipeline, 1, false);
        FreeShaderStageNames(stages, 2);
    }

    private void CreateCompositePipeline()
    {
        string shaderPath = ResolveShaderPath("Composite.hlsl");
        var shaderSource = File.ReadAllText(shaderPath);
        using var vertexShader = new VulkanShader(_context, shaderSource, ShaderKind.VertexShader, "VSMain");
        using var fragmentShader = new VulkanShader(_context, shaderSource, ShaderKind.FragmentShader, "PSMain");

        var stages = stackalloc PipelineShaderStageCreateInfo[2];
        stages[0] = CreateShaderStage(ShaderStageFlags.VertexBit, vertexShader.Module, "VSMain");
        stages[1] = CreateShaderStage(ShaderStageFlags.FragmentBit, fragmentShader.Module, "PSMain");

        var vertexInput = new PipelineVertexInputStateCreateInfo { SType = StructureType.PipelineVertexInputStateCreateInfo };
        CreatePipeline(stages, 2, vertexInput, _compositeDescriptorSetLayout, out _compositePipelineLayout, out _compositePipeline, 1, false);
        FreeShaderStageNames(stages, 2);
    }

    private void CreatePipeline(PipelineShaderStageCreateInfo* stages, uint stageCount, PipelineVertexInputStateCreateInfo vertexInput, DescriptorSetLayout descriptorSetLayout, out PipelineLayout pipelineLayout, out Pipeline pipeline, uint colorAttachmentCount, bool enableDepth)
    {
        var inputAssembly = new PipelineInputAssemblyStateCreateInfo { SType = StructureType.PipelineInputAssemblyStateCreateInfo, Topology = PrimitiveTopology.TriangleList };
        var viewport = new Viewport { Width = _swapchainExtent.Width, Height = _swapchainExtent.Height, MaxDepth = 1 };
        var scissor = new Rect2D { Extent = _swapchainExtent };
        var viewportState = new PipelineViewportStateCreateInfo { SType = StructureType.PipelineViewportStateCreateInfo, ViewportCount = 1, PViewports = &viewport, ScissorCount = 1, PScissors = &scissor };
        var rasterization = new PipelineRasterizationStateCreateInfo { SType = StructureType.PipelineRasterizationStateCreateInfo, CullMode = enableDepth ? CullModeFlags.BackBit : CullModeFlags.None, FrontFace = FrontFace.CounterClockwise, LineWidth = 1 };
        var multisample = new PipelineMultisampleStateCreateInfo { SType = StructureType.PipelineMultisampleStateCreateInfo, RasterizationSamples = SampleCountFlags.Count1Bit };
        var colorAttachments = stackalloc PipelineColorBlendAttachmentState[(int)colorAttachmentCount];
        for (int index = 0; index < colorAttachmentCount; index++)
            colorAttachments[index] = new PipelineColorBlendAttachmentState { ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit };
        var colorBlend = new PipelineColorBlendStateCreateInfo { SType = StructureType.PipelineColorBlendStateCreateInfo, AttachmentCount = colorAttachmentCount, PAttachments = colorAttachments };
        var depthStencil = new PipelineDepthStencilStateCreateInfo { SType = StructureType.PipelineDepthStencilStateCreateInfo, DepthTestEnable = enableDepth, DepthWriteEnable = enableDepth, DepthCompareOp = CompareOp.Less };

        var layoutPointer = stackalloc DescriptorSetLayout[1];
        layoutPointer[0] = descriptorSetLayout;
        var layoutInfo = new PipelineLayoutCreateInfo { SType = StructureType.PipelineLayoutCreateInfo, SetLayoutCount = 1, PSetLayouts = layoutPointer };
        _context.Vk.CreatePipelineLayout(_context.Device, &layoutInfo, null, out pipelineLayout);

        var createInfo = new GraphicsPipelineCreateInfo
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = stageCount,
            PStages = stages,
            PVertexInputState = &vertexInput,
            PInputAssemblyState = &inputAssembly,
            PViewportState = &viewportState,
            PRasterizationState = &rasterization,
            PMultisampleState = &multisample,
            PColorBlendState = &colorBlend,
            PDepthStencilState = enableDepth ? &depthStencil : null,
            Layout = pipelineLayout,
            RenderPass = _renderPass,
            Subpass = 0
        };
        _context.Vk.CreateGraphicsPipelines(_context.Device, default, 1, &createInfo, null, out pipeline);
    }

    private static PipelineShaderStageCreateInfo CreateShaderStage(ShaderStageFlags stage, ShaderModule module, string entryPoint)
    {
        return new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = stage,
            Module = module,
            PName = (byte*)SilkMarshal.StringToPtr(entryPoint)
        };
    }

    private static void FreeShaderStageNames(PipelineShaderStageCreateInfo* stages, int count)
    {
        for (int index = 0; index < count; index++)
            SilkMarshal.Free((nint)stages[index].PName);
    }

    private static string ResolveShaderPath(string fileName)
    {
        string outputPath = Path.Combine(AppContext.BaseDirectory, "Shaders", fileName);
        if (File.Exists(outputPath))
            return outputPath;

        return Path.Combine(Directory.GetCurrentDirectory(), "OpenRender.Rendering", "Shaders", fileName);
    }
}
