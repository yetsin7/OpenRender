using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace OpenRender.Rendering;

public unsafe partial class VulkanRenderer
{
    private void CreateUniformBuffers()
    {
        _uniformBuffers = new VulkanBuffer[_swapchainImages.Length];
        _ssaoParamsBuffers = new VulkanBuffer[_swapchainImages.Length];

        for (int index = 0; index < _swapchainImages.Length; index++)
        {
            _uniformBuffers[index] = new VulkanBuffer(_context, (ulong)Marshal.SizeOf<SceneBuffer>(), BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            _ssaoParamsBuffers[index] = new VulkanBuffer(_context, 4096, BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        }
    }

    private void CreateDescriptorPool()
    {
        var poolSizes = stackalloc DescriptorPoolSize[3];
        poolSizes[0] = new DescriptorPoolSize { Type = DescriptorType.UniformBuffer, DescriptorCount = (uint)_swapchainImages.Length * 10 };
        poolSizes[1] = new DescriptorPoolSize { Type = DescriptorType.CombinedImageSampler, DescriptorCount = (uint)_swapchainImages.Length * 20 };
        poolSizes[2] = new DescriptorPoolSize { Type = DescriptorType.Sampler, DescriptorCount = (uint)_swapchainImages.Length * 5 };
        var createInfo = new DescriptorPoolCreateInfo { SType = StructureType.DescriptorPoolCreateInfo, PoolSizeCount = 3, PPoolSizes = poolSizes, MaxSets = (uint)_swapchainImages.Length * 10 };
        _context.Vk.CreateDescriptorPool(_context.Device, &createInfo, null, out _descriptorPool);
    }

    private void CreateDescriptorSets()
    {
        AllocateSceneDescriptorSets();
        AllocateSsaoDescriptorSets();
        AllocateCompositeDescriptorSets();
    }

    private void AllocateSceneDescriptorSets()
    {
        var layouts = new DescriptorSetLayout[_swapchainImages.Length];
        Array.Fill(layouts, _descriptorSetLayout);
        fixed (DescriptorSetLayout* layoutPointer = layouts)
        {
            var allocateInfo = new DescriptorSetAllocateInfo { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _descriptorPool, DescriptorSetCount = (uint)_swapchainImages.Length, PSetLayouts = layoutPointer };
            _descriptorSets = new DescriptorSet[_swapchainImages.Length];
            fixed (DescriptorSet* descriptorPointer = _descriptorSets)
                _context.Vk.AllocateDescriptorSets(_context.Device, &allocateInfo, descriptorPointer);
        }

        for (int index = 0; index < _swapchainImages.Length; index++)
        {
            var bufferInfo = new DescriptorBufferInfo { Buffer = _uniformBuffers[index].Buffer, Range = (ulong)Marshal.SizeOf<SceneBuffer>() };
            var write = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _descriptorSets[index], DescriptorType = DescriptorType.UniformBuffer, DescriptorCount = 1, PBufferInfo = &bufferInfo };
            _context.Vk.UpdateDescriptorSets(_context.Device, 1, &write, 0, null);
        }
    }

    private void AllocateSsaoDescriptorSets()
    {
        var layouts = new DescriptorSetLayout[_swapchainImages.Length];
        Array.Fill(layouts, _ssaoDescriptorSetLayout);
        fixed (DescriptorSetLayout* layoutPointer = layouts)
        {
            var allocateInfo = new DescriptorSetAllocateInfo { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _descriptorPool, DescriptorSetCount = (uint)_swapchainImages.Length, PSetLayouts = layoutPointer };
            _ssaoDescriptorSets = new DescriptorSet[_swapchainImages.Length];
            fixed (DescriptorSet* descriptorPointer = _ssaoDescriptorSets)
                _context.Vk.AllocateDescriptorSets(_context.Device, &allocateInfo, descriptorPointer);
        }

        for (int index = 0; index < _swapchainImages.Length; index++)
        {
            var uniformInfo = new DescriptorBufferInfo { Buffer = _ssaoParamsBuffers[index].Buffer, Range = 4096 };
            var positionInfo = new DescriptorImageInfo { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = _gPosition!.View, Sampler = _defaultSampler!.Sampler };
            var normalInfo = new DescriptorImageInfo { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = _gNormal!.View, Sampler = _defaultSampler!.Sampler };
            var noiseInfo = new DescriptorImageInfo { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = _ssaoResources!.NoiseTexture.View, Sampler = _defaultSampler!.Sampler };
            var samplerInfo = new DescriptorImageInfo { Sampler = _defaultSampler!.Sampler };
            var writes = new WriteDescriptorSet[5];
            writes[0] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _ssaoDescriptorSets[index], DstBinding = 0, DescriptorType = DescriptorType.UniformBuffer, DescriptorCount = 1, PBufferInfo = &uniformInfo };
            writes[1] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _ssaoDescriptorSets[index], DstBinding = 1, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, PImageInfo = &positionInfo };
            writes[2] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _ssaoDescriptorSets[index], DstBinding = 2, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, PImageInfo = &normalInfo };
            writes[3] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _ssaoDescriptorSets[index], DstBinding = 3, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, PImageInfo = &noiseInfo };
            writes[4] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _ssaoDescriptorSets[index], DstBinding = 4, DescriptorType = DescriptorType.Sampler, DescriptorCount = 1, PImageInfo = &samplerInfo };
            fixed (WriteDescriptorSet* writesPointer = writes)
                _context.Vk.UpdateDescriptorSets(_context.Device, 5, writesPointer, 0, null);
        }
    }

    private void AllocateCompositeDescriptorSets()
    {
        var layouts = new DescriptorSetLayout[_swapchainImages.Length];
        Array.Fill(layouts, _compositeDescriptorSetLayout);
        fixed (DescriptorSetLayout* layoutPointer = layouts)
        {
            var allocateInfo = new DescriptorSetAllocateInfo { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _descriptorPool, DescriptorSetCount = (uint)_swapchainImages.Length, PSetLayouts = layoutPointer };
            _compositeDescriptorSets = new DescriptorSet[_swapchainImages.Length];
            fixed (DescriptorSet* descriptorPointer = _compositeDescriptorSets)
                _context.Vk.AllocateDescriptorSets(_context.Device, &allocateInfo, descriptorPointer);
        }

        for (int index = 0; index < _swapchainImages.Length; index++)
        {
            var swapchainInfo = new DescriptorImageInfo { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = _swapchainImageViews[index], Sampler = _defaultSampler!.Sampler };
            var ssaoInfo = new DescriptorImageInfo { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = _ssaoImage!.View, Sampler = _defaultSampler!.Sampler };
            var bloomInfo = new DescriptorImageInfo { ImageLayout = ImageLayout.ShaderReadOnlyOptimal, ImageView = _bloomImage!.View, Sampler = _defaultSampler!.Sampler };
            var samplerInfo = new DescriptorImageInfo { Sampler = _defaultSampler!.Sampler };
            var writes = new WriteDescriptorSet[4];
            writes[0] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _compositeDescriptorSets[index], DstBinding = 0, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, PImageInfo = &swapchainInfo };
            writes[1] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _compositeDescriptorSets[index], DstBinding = 1, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, PImageInfo = &ssaoInfo };
            writes[2] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _compositeDescriptorSets[index], DstBinding = 2, DescriptorType = DescriptorType.CombinedImageSampler, DescriptorCount = 1, PImageInfo = &bloomInfo };
            writes[3] = new WriteDescriptorSet { SType = StructureType.WriteDescriptorSet, DstSet = _compositeDescriptorSets[index], DstBinding = 3, DescriptorType = DescriptorType.Sampler, DescriptorCount = 1, PImageInfo = &samplerInfo };
            fixed (WriteDescriptorSet* writesPointer = writes)
                _context.Vk.UpdateDescriptorSets(_context.Device, 4, writesPointer, 0, null);
        }
    }

    private void CreateSyncObjects()
    {
        var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo, Flags = FenceCreateFlags.SignaledBit };
        _context.Vk.CreateSemaphore(_context.Device, &semaphoreInfo, null, out _imageAvailableSemaphore);
        _context.Vk.CreateSemaphore(_context.Device, &semaphoreInfo, null, out _renderFinishedSemaphore);
        _context.Vk.CreateFence(_context.Device, &fenceInfo, null, out _inFlightFence);
    }

    private void CreateCommandPool()
    {
        var poolInfo = new CommandPoolCreateInfo { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = _context.GraphicsQueueFamilyIndex, Flags = CommandPoolCreateFlags.ResetCommandBufferBit };
        _context.Vk.CreateCommandPool(_context.Device, &poolInfo, null, out _commandPool);
        var allocateInfo = new CommandBufferAllocateInfo { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _commandPool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
        _context.Vk.AllocateCommandBuffers(_context.Device, &allocateInfo, out _commandBuffer);
    }

    public void Render(Matrix4x4 viewProjection, Vector3 cameraPosition, float time, Matrix4x4 projection)
    {
        if (!_frameSubmitEnabled)
            return;

        if (!_advancedPipelineEnabled)
        {
            RenderMinimalFrame(time);
            return;
        }

        if (_swapchainExt == null || _swapchainFramebuffers.Length == 0)
            return;

        _context.Vk.WaitForFences(_context.Device, 1, ref _inFlightFence, true, ulong.MaxValue);
        _context.Vk.ResetFences(_context.Device, 1, ref _inFlightFence);

        uint imageIndex = 0;
        _swapchainExt.AcquireNextImage(_context.Device, _swapchain, ulong.MaxValue, _imageAvailableSemaphore, default, ref imageIndex);
        _uniformBuffers[imageIndex].UpdateData(new[] { new SceneBuffer { ViewProjection = viewProjection, CameraPos = cameraPosition, Time = time, LightDir = new Vector3(1, -1, -1), LightColor = new Vector3(1, 0.95f, 0.8f), LightIntensity = 1.5f } });
        _context.Vk.ResetCommandBuffer(_commandBuffer, 0);
        var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        _context.Vk.BeginCommandBuffer(_commandBuffer, &beginInfo);

        var renderPassInfo = CreateAdvancedRenderPassInfo(imageIndex);
        _context.Vk.CmdBeginRenderPass(_commandBuffer, &renderPassInfo, SubpassContents.Inline);
        _context.Vk.CmdBindDescriptorSets(_commandBuffer, PipelineBindPoint.Graphics, _pipelineLayout, 0, 1, ref _descriptorSets[imageIndex], 0, null);
        ExecuteMainPass(_commandBuffer, viewProjection, cameraPosition, time);
        ExecuteSSAOPass(_commandBuffer, imageIndex, projection);
        _context.Vk.CmdEndRenderPass(_commandBuffer);
        _context.Vk.EndCommandBuffer(_commandBuffer);
        SubmitAdvancedFrame(imageIndex);
    }

    private RenderPassBeginInfo CreateAdvancedRenderPassInfo(uint imageIndex)
    {
        var clearValues = stackalloc ClearValue[4];
        clearValues[0].Color = new ClearColorValue(0.05f, 0.1f, 0.2f, 1);
        clearValues[1].Color = new ClearColorValue(0, 0, 0, 1);
        clearValues[2].Color = new ClearColorValue(0, 0, 0, 1);
        clearValues[3].DepthStencil = new ClearDepthStencilValue(1, 0);
        return new RenderPassBeginInfo
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _swapchainFramebuffers[imageIndex],
            RenderArea = new Rect2D { Extent = _swapchainExtent },
            ClearValueCount = 4,
            PClearValues = clearValues
        };
    }

    private void SubmitAdvancedFrame(uint imageIndex)
    {
        var waitSemaphores = stackalloc[] { _imageAvailableSemaphore };
        var signalSemaphores = stackalloc[] { _renderFinishedSemaphore };
        var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };

        fixed (CommandBuffer* commandBufferPointer = &_commandBuffer)
        {
            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = waitSemaphores,
                PWaitDstStageMask = waitStages,
                CommandBufferCount = 1,
                PCommandBuffers = commandBufferPointer,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = signalSemaphores
            };
            _context.Vk.QueueSubmit(_context.GraphicsQueue, 1, &submitInfo, _inFlightFence);
        }

        fixed (SwapchainKHR* swapchainPointer = &_swapchain)
        {
            var presentInfo = new PresentInfoKHR
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = signalSemaphores,
                SwapchainCount = 1,
                PSwapchains = swapchainPointer,
                PImageIndices = &imageIndex
            };
            _swapchainExt!.QueuePresent(_context.GraphicsQueue, &presentInfo);
        }
    }

    private void RenderMinimalFrame(float time)
    {
        if (_swapchainExt == null || _swapchainFramebuffers.Length == 0 || _renderPass.Handle == 0 || _commandBuffer.Handle == 0)
            return;

        Check(_context.Vk.WaitForFences(_context.Device, 1, ref _inFlightFence, true, ulong.MaxValue), "wait frame fence");
        Check(_context.Vk.ResetFences(_context.Device, 1, ref _inFlightFence), "reset frame fence");

        uint imageIndex = 0;
        var acquireResult = _swapchainExt.AcquireNextImage(_context.Device, _swapchain, ulong.MaxValue, _imageAvailableSemaphore, default, ref imageIndex);
        if (acquireResult is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr)
            return;
        Check(acquireResult, "acquire swapchain image");

        if (imageIndex >= _swapchainFramebuffers.Length)
            return;

        Check(_context.Vk.ResetCommandBuffer(_commandBuffer, 0), "reset command buffer");
        var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        Check(_context.Vk.BeginCommandBuffer(_commandBuffer, &beginInfo), "begin command buffer");

        float pulse = 0.5f + MathF.Sin(time * 0.6f) * 0.5f;
        var clearColor = new ClearValue { Color = new ClearColorValue(0.035f + pulse * 0.015f, 0.075f + pulse * 0.020f, 0.105f + pulse * 0.025f, 1.0f) };
        var renderPassInfo = new RenderPassBeginInfo { SType = StructureType.RenderPassBeginInfo, RenderPass = _renderPass, Framebuffer = _swapchainFramebuffers[imageIndex], RenderArea = new Rect2D { Extent = _swapchainExtent }, ClearValueCount = 1, PClearValues = &clearColor };
        _context.Vk.CmdBeginRenderPass(_commandBuffer, &renderPassInfo, SubpassContents.Inline);
        _context.Vk.CmdEndRenderPass(_commandBuffer);
        Check(_context.Vk.EndCommandBuffer(_commandBuffer), "end command buffer");

        var waitSemaphores = stackalloc[] { _imageAvailableSemaphore };
        var signalSemaphores = stackalloc[] { _renderFinishedSemaphore };
        var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
        fixed (CommandBuffer* commandBufferPointer = &_commandBuffer)
        {
            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = waitSemaphores,
                PWaitDstStageMask = waitStages,
                CommandBufferCount = 1,
                PCommandBuffers = commandBufferPointer,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = signalSemaphores
            };
            Check(_context.Vk.QueueSubmit(_context.GraphicsQueue, 1, &submitInfo, _inFlightFence), "submit minimal frame");
        }

        fixed (SwapchainKHR* swapchainPointer = &_swapchain)
        {
            var presentInfo = new PresentInfoKHR
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = signalSemaphores,
                SwapchainCount = 1,
                PSwapchains = swapchainPointer,
                PImageIndices = &imageIndex
            };

            var presentResult = _swapchainExt.QueuePresent(_context.GraphicsQueue, &presentInfo);
            if (presentResult is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr)
                return;
            Check(presentResult, "present minimal frame");
        }
    }
}
