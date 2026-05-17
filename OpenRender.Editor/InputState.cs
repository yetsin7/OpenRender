using System.Numerics;
using System.Collections.Generic;

namespace OpenRender.Editor;

/// <summary>
/// Captures the current state of user input to be processed by engine systems.
/// </summary>
public class InputState
{
    private readonly HashSet<string> _pressedKeys = new();
    
    public Vector2 MousePosition { get; set; }
    public Vector2 MouseDelta { get; set; }
    public float MouseWheelDelta { get; set; }
    
    public bool IsLeftMouseDown { get; set; }
    public bool IsRightMouseDown { get; set; }
    public bool IsMiddleMouseDown { get; set; }
    
    public bool IsShiftDown { get; set; }
    public bool IsControlDown { get; set; }

    public void SetKeyDown(string key) => _pressedKeys.Add(key.ToUpperInvariant());
    public void SetKeyUp(string key) => _pressedKeys.Remove(key.ToUpperInvariant());
    public bool IsKeyDown(string key) => _pressedKeys.Contains(key.ToUpperInvariant());

    public void ClearDelta()
    {
        MouseDelta = Vector2.Zero;
        MouseWheelDelta = 0;
    }
}
