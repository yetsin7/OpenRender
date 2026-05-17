using System;
using System.Collections.Generic;
using Silk.NET.Vulkan;

namespace OpenRender.Rendering.RenderGraph;

public enum RenderResourceType
{
    Texture,
    Buffer
}

public class RenderResource
{
    public string Name { get; }
    public RenderResourceType Type { get; }
    public Format Format { get; set; }
    
    // Internal Vulkan objects would be managed by the graph executor
    public RenderResource(string name, RenderResourceType type)
    {
        Name = name;
        Type = type;
    }
}

public abstract class RenderPassNode
{
    public string Name { get; }
    public List<RenderResource> Inputs { get; } = new();
    public List<RenderResource> Outputs { get; } = new();

    protected RenderPassNode(string name)
    {
        Name = name;
    }

    public abstract void Execute(CommandBuffer cmd, VulkanRenderer renderer);
}

public class RenderGraph
{
    private readonly List<RenderPassNode> _nodes = new();
    private readonly Dictionary<string, RenderResource> _resources = new();

    public void AddNode(RenderPassNode node)
    {
        _nodes.Add(node);
    }

    public RenderResource GetOrCreateResource(string name, RenderResourceType type)
    {
        if (!_resources.TryGetValue(name, out var resource))
        {
            resource = new RenderResource(name, type);
            _resources[name] = resource;
        }
        return resource;
    }

    public void Compile()
    {
        // TODO: Logic to sort nodes based on dependencies and manage resource transitions
    }

    public void Execute(CommandBuffer cmd, VulkanRenderer renderer)
    {
        foreach (var node in _nodes)
        {
            node.Execute(cmd, renderer);
        }
    }
}
