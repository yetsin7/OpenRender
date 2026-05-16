using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using OpenRender.Core.Scene;
using OpenRender.Rendering.Import;

namespace OpenRender.Rendering;

/// <summary>
/// Manages the Silk.NET OpenGL window for 3D viewport rendering.
/// Handles input (orbit, zoom, pan) and the render loop.
/// This can be launched standalone for testing the 3D engine.
/// </summary>
public class ViewportWindow : IDisposable
{
    private IWindow? _window;
    private GL? _gl;
    private SceneRenderer? _renderer;
    private Scene3D _scene;
    private IInputContext? _input;

    // Mouse state
    private bool _isOrbiting;
    private Vector2 _lastMousePos;
    private bool _disposed;

    public ViewportWindow(Scene3D? scene = null)
    {
        _scene = scene ?? DemoScene.Create();
    }

    /// <summary>
    /// Creates and runs the OpenGL viewport window.
    /// This is blocking and runs until the window is closed.
    /// </summary>
    public void Run()
    {
        var options = WindowOptions.Default;
        options.Title = "Open Render — 3D Viewport";
        options.Size = new Vector2D<int>(1280, 720);
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
        options.VSync = true;

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Resize += OnResize;
        _window.Closing += OnClosing;

        _window.Run();
    }

    private void OnLoad()
    {
        _gl = _window!.CreateOpenGL();
        _renderer = new SceneRenderer(_gl);
        _renderer.Initialize();
        _renderer.BackgroundColor = _scene.BackgroundColor;

        // Set up input
        _input = _window!.CreateInput();
        if (_input != null)
        {
            foreach (var mouse in _input.Mice)
            {
                mouse.MouseDown += OnMouseDown;
                mouse.MouseUp += OnMouseUp;
                mouse.MouseMove += OnMouseMove;
                mouse.Scroll += OnScroll;
            }
        }
    }

    private void OnRender(double deltaTime)
    {
        if (_gl == null || _renderer == null) return;

        _renderer.Render(_scene, _window!.Size.X, _window.Size.Y);
    }

    private void OnResize(Vector2D<int> size)
    {
        _gl?.Viewport(size);
        _scene.Camera.AspectRatio = (float)size.X / size.Y;
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        var pos = mouse.Position;
        _lastMousePos = new Vector2(pos.X, pos.Y);

        if (button == MouseButton.Left)
            _isOrbiting = true;
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
            _isOrbiting = false;
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        var currentPos = new Vector2(position.X, position.Y);
        var delta = currentPos - _lastMousePos;
        _lastMousePos = currentPos;

        if (_isOrbiting)
        {
            _scene.Camera.Rotate(delta.X, delta.Y);
        }
    }

    private void OnScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        _scene.Camera.Zoom(-scrollWheel.Y * 0.8f);
    }

    private void OnClosing()
    {
        _renderer?.Dispose();
        _input?.Dispose();
        _gl?.Dispose();
    }

    /// <summary>
    /// Loads an OBJ file into the viewport.
    /// </summary>
    public async Task<bool> LoadObjAsync(string filePath)
    {
        var importer = new ObjImporter();
        if (!importer.CanImport(filePath))
            return false;

        var result = await importer.ImportAsync(filePath);
        if (result.Success && result.Scene != null)
        {
            _scene = result.Scene;
            _renderer?.ClearMeshCache();
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _window?.Close();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
