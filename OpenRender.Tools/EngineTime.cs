using System.Diagnostics;

namespace OpenRender.Tools;

/// <summary>
/// High-precision time and frame management for the engine.
/// </summary>
public class EngineTime
{
    private readonly Stopwatch _stopwatch = new();
    private double _lastTime;
    
    public float DeltaTime { get; private set; }
    public float TotalTime { get; private set; }
    public long FrameCount { get; private set; }

    public void Start()
    {
        _stopwatch.Start();
        _lastTime = 0;
    }

    public void Update()
    {
        double currentTime = _stopwatch.Elapsed.TotalSeconds;
        DeltaTime = (float)(currentTime - _lastTime);
        TotalTime = (float)currentTime;
        _lastTime = currentTime;
        FrameCount++;
    }
}
