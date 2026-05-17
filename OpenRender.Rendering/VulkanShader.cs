using System;
using Silk.NET.Vulkan;
using Silk.NET.Shaderc;
using Silk.NET.Core.Native;

namespace OpenRender.Rendering;

public unsafe class VulkanShader : IDisposable
{
    private readonly VulkanContext _context;
    private readonly ShaderModule _module;

    public ShaderModule Module => _module;

    public VulkanShader(VulkanContext context, string source, ShaderKind kind, string entryPoint = "main")
    {
        _context = context;

        var shaderc = Shaderc.GetApi();
        var compiler = shaderc.CompilerInitialize();
        var options = shaderc.CompileOptionsInitialize();
        
        shaderc.CompileOptionsSetSourceLanguage(options, SourceLanguage.Hlsl);
        shaderc.CompileOptionsSetOptimizationLevel(options, OptimizationLevel.Performance);

        var result = shaderc.CompileIntoSpv(compiler, source, (nuint)source.Length, kind, "shader.hlsl", entryPoint, options);

        if (shaderc.ResultGetCompilationStatus(result) != CompilationStatus.Success)
        {
            var error = shaderc.ResultGetErrorMessageS(result);
            throw new Exception($"Shader compilation failed: {error}");
        }

        var length = (nuint)shaderc.ResultGetLength(result);
        var bytes = shaderc.ResultGetBytes(result);

        var createInfo = new ShaderModuleCreateInfo
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = length,
            PCode = (uint*)bytes
        };

        if (_context.Vk.CreateShaderModule(_context.Device, &createInfo, null, out _module) != Result.Success)
        {
            throw new Exception("Failed to create shader module.");
        }

        shaderc.ResultRelease(result);
        shaderc.CompileOptionsRelease(options);
        shaderc.CompilerRelease(compiler);
        shaderc.Dispose();
    }

    public void Dispose()
    {
        _context.Vk.DestroyShaderModule(_context.Device, _module, null);
    }
}
