using Silk.NET.Vulkan;
using System.Numerics;

namespace OpenRender.Rendering.RenderGraph;

public class PbrPassNode : RenderPassNode
{
    private readonly Matrix4x4 _viewProjection;
    private readonly Vector3 _cameraPos;
    private readonly float _time;

    public PbrPassNode(Matrix4x4 vp, Vector3 cam, float t) : base("PBR Main Pass")
    {
        _viewProjection = vp;
        _cameraPos = cam;
        _time = t;
    }

    public override void Execute(CommandBuffer cmd, VulkanRenderer renderer)
    {
        // This is a simplified execution. In a full system, 
        // the node would use the renderer's public methods to bind pipelines and draw.
        renderer.ExecuteMainPass(cmd, _viewProjection, _cameraPos, _time);
    }
}
