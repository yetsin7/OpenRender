using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using OpenRender.Core.Rendering;
using OpenRender.Core.Scene;
using OpenRender.Rendering;
using OpenRender.ViewModels;

namespace OpenRender.Controls;

/// <summary>
/// OpenGL viewport control with professional Lumion-style 3D navigation.
/// 
/// Controls:
///   Right-Click + Drag  → Free look (FPS camera rotation)
///   Left-Click + Drag   → Orbit around focal point
///   Middle-Click + Drag → Pan (slide camera)
///   Scroll Wheel        → Zoom in/out
///   Double Middle-Click → Frame All (center model)
///   W/A/S/D             → Fly forward/left/back/right
///   Space               → Fly up
///   X                   → Fly down
///   Shift (held)        → Sprint (3x speed)
/// </summary>
public class ViewportControl : OpenGlControlBase
{
    private GL? _gl;
    private SceneRenderer? _renderer;
    private readonly Stopwatch _frameTimer = new();
    private CaptureRequest? _pendingCapture;

    public ViewportControl()
    {
        Focusable = true;
        ViewportCaptureService.Register(this);
    }

    // ── Scene Binding ──

    public static readonly DirectProperty<ViewportControl, Scene3D?> SceneProperty =
        AvaloniaProperty.RegisterDirect<ViewportControl, Scene3D?>(
            nameof(Scene),
            o => o.Scene,
            (o, v) => o.Scene = v);

    private Scene3D? _scene;
    public Scene3D? Scene
    {
        get => _scene;
        set
        {
            var old = _scene;
            SetAndRaise(SceneProperty, ref _scene, value);
            if (old != value)
            {
                // Clear mesh cache when scene changes to avoid stale GPU data
                _renderer?.ClearMeshCache();
                _sceneJustChanged = true;
            }
        }
    }

    // ── Input State ──

    private bool _isLooking;    // Right-click look
    private bool _isOrbiting;   // Left-click orbit
    private bool _isPanning;    // Middle-click pan
    private bool _isDraggingViewCube;
    private bool _viewCubeDragged;
    private double _totalDragDistance;
    private string? _pressedViewCubeFace;
    private Avalonia.Point _lastMousePos;
    private readonly HashSet<Key> _pressedKeys = new();
    private long _lastMiddleClickTime;
    private bool _sceneJustChanged;

    // ── OpenGL Lifecycle ──

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        _frameTimer.Start();
        
        try 
        {
            _gl = GL.GetApi(gl.GetProcAddress);
            _renderer = new SceneRenderer(_gl);
            _renderer.Initialize();
        }
        catch (Exception ex)
        {
            MainViewModel.ReportGlError($"OpenGL Init Error: {ex.Message}");
            Console.WriteLine($"OpenGL Init Error: {ex.Message}");
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _renderer?.Dispose();
        _gl?.Dispose();
        base.OnOpenGlDeinit(gl);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        float deltaTime = (float)_frameTimer.Elapsed.TotalSeconds;
        _frameTimer.Restart();

        // Clamp delta to prevent huge jumps after tab-away
        deltaTime = Math.Min(deltaTime, 0.1f);

        ProcessKeyboardMovement(deltaTime);

        if (_renderer != null && _scene != null)
        {
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
            double renderScaling = topLevel?.RenderScaling ?? 1.0;
            int pixelWidth = (int)(Bounds.Width * renderScaling);
            int pixelHeight = (int)(Bounds.Height * renderScaling);

            bool restoreGrid = false;
            bool restoreViewCube = false;
            bool previousShowGrid = false;
            bool previousShowViewCube = false;

            if (_pendingCapture?.CleanViewport == true)
            {
                previousShowGrid = _renderer.ShowGrid;
                previousShowViewCube = _renderer.ShowViewCube;
                _renderer.ShowGrid = false;
                _renderer.ShowViewCube = false;
                restoreGrid = true;
                restoreViewCube = true;
            }

            _renderer.Render(_scene, pixelWidth, pixelHeight);

            if (restoreGrid)
                _renderer.ShowGrid = previousShowGrid;

            if (restoreViewCube)
                _renderer.ShowViewCube = previousShowViewCube;

            if (_sceneJustChanged)
            {
                _sceneJustChanged = false;
                MainViewModel.ReportViewportReady();
            }

            if (_pendingCapture != null && _gl != null)
            {
                var capture = _pendingCapture;
                _pendingCapture = null;

                try
                {
                    ViewportFrameExporter.SaveFramebuffer(
                        _gl,
                        pixelWidth,
                        pixelHeight,
                        capture.OutputPath,
                        capture.Width,
                        capture.Height,
                        capture.Format,
                        capture.JpegQuality);

                    capture.Completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    capture.Completion.TrySetException(ex);
                }
            }
        }

        RequestNextFrameRendering();
    }

    public Task CaptureFrameAsync(string outputPath, int width, int height, OutputFormat format, int jpegQuality = 95, bool cleanViewport = true)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Dispatcher.UIThread.Post(() =>
        {
            if (_gl == null || _renderer == null || _scene == null)
            {
                completion.TrySetException(new InvalidOperationException("The viewport is not initialized."));
                return;
            }

            if (_pendingCapture != null)
            {
                completion.TrySetException(new InvalidOperationException("A viewport export is already in progress."));
                return;
            }

            _pendingCapture = new CaptureRequest(outputPath, width, height, format, jpegQuality, cleanViewport, completion);
            RequestNextFrameRendering();
        });

        return completion.Task;
    }

    // ── Fly Mode: WASD + Space + X ──

    private void ProcessKeyboardMovement(float deltaTime)
    {
        if (_scene == null) return;

        var camera = _scene.Camera;
        var moveDir = Vector3.Zero;

        if (_pressedKeys.Contains(Key.W)) moveDir += camera.Forward;
        if (_pressedKeys.Contains(Key.S)) moveDir -= camera.Forward;
        if (_pressedKeys.Contains(Key.A)) moveDir -= camera.Right;
        if (_pressedKeys.Contains(Key.D)) moveDir += camera.Right;
        if (_pressedKeys.Contains(Key.Space) || _pressedKeys.Contains(Key.E)) moveDir += Vector3.UnitY;
        if (_pressedKeys.Contains(Key.X) || _pressedKeys.Contains(Key.Q)) moveDir -= Vector3.UnitY;

        if (moveDir.LengthSquared() > 0)
            moveDir = Vector3.Normalize(moveDir);

        // Sprint with Shift
        float speedMul = (_pressedKeys.Contains(Key.LeftShift) || _pressedKeys.Contains(Key.RightShift))
            ? 3.0f : 1.0f;

        camera.Update(moveDir, deltaTime, speedMul);
    }

    // ── Focus Management ──

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        this.Focus();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pressedKeys.Clear();
        _isLooking = false;
        _isOrbiting = false;
        _isPanning = false;
        _isDraggingViewCube = false;
        _viewCubeDragged = false;
        _pressedViewCubeFace = null;
    }

    // ── Keyboard ──

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _pressedKeys.Add(e.Key);
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _pressedKeys.Remove(e.Key);
        e.Handled = true;
    }

    // ── Mouse Button Handling ──

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        _lastMousePos = point.Position;

        if (point.Properties.IsRightButtonPressed)
        {
            _isLooking = true;
        }
        else if (point.Properties.IsLeftButtonPressed)
        {
            string? face = HitTestViewCube(point.Position);
            if (face != null)
            {
                _pressedViewCubeFace = face;
                _isDraggingViewCube = true;
                _viewCubeDragged = false;
                _totalDragDistance = 0;
                e.Handled = true;
                return;
            }

            _isOrbiting = true;
        }
        else if (point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;

            // Double middle-click detection → Frame All
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now - _lastMiddleClickTime < 400)
            {
                FrameAllObjects();
                _isPanning = false; // Cancel pan on double-click
            }
            _lastMiddleClickTime = now;
        }

        this.Focus();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

        if (kind == PointerUpdateKind.RightButtonReleased)
            _isLooking = false;
        else if (kind == PointerUpdateKind.LeftButtonReleased)
        {
            if (_isDraggingViewCube)
            {
                _isDraggingViewCube = false;
                if (!_viewCubeDragged && _pressedViewCubeFace != null)
                {
                    var vm = DataContext as MainViewModel;
                    if (vm != null && vm.SetViewCommand.CanExecute(_pressedViewCubeFace))
                    {
                        vm.SetViewCommand.Execute(_pressedViewCubeFace);
                    }
                }
                _pressedViewCubeFace = null;
            }
            else
            {
                _isOrbiting = false;
            }
        }
        else if (kind == PointerUpdateKind.MiddleButtonReleased)
            _isPanning = false;

        e.Handled = true;
    }

    // ── Mouse Movement ──

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var currentPos = e.GetPosition(this);
        float dx = (float)(currentPos.X - _lastMousePos.X);
        float dy = (float)(currentPos.Y - _lastMousePos.Y);
        _lastMousePos = currentPos;

        if (_scene == null) return;

        if (_isDraggingViewCube)
        {
            _totalDragDistance += Math.Abs(dx) + Math.Abs(dy);
            if (_totalDragDistance > 5)
                _viewCubeDragged = true;
            _scene.Camera.Orbit(dx, dy);
        }
        else if (_isLooking)
        {
            _scene.Camera.LookAround(dx, dy);
        }
        else if (_isOrbiting)
        {
            _scene.Camera.Orbit(dx, dy);
        }
        else if (_isPanning)
        {
            _scene.Camera.Pan(dx, dy);
        }
    }

    // ── Scroll Wheel Zoom ──

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_scene != null)
        {
            _scene.Camera.Zoom((float)e.Delta.Y * 2.0f);
        }
        e.Handled = true;
    }

    // ── Frame All Helper ──

    private void FrameAllObjects()
    {
        if (_scene == null) return;

        var vm = DataContext as MainViewModel;
        if (vm != null && vm.FrameAllCommand.CanExecute(null))
        {
            vm.FrameAllCommand.Execute(null);
        }
    }

    // ── ViewCube Hit Test ──

    private string? HitTestViewCube(Avalonia.Point screenPos)
    {
        if (_scene == null || _renderer?.ViewCube == null) return null;

        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
        double renderScaling = topLevel?.RenderScaling ?? 1.0;

        return _renderer.ViewCube.HitTest(
            screenPos.X,
            screenPos.Y,
            _scene.Camera,
            Bounds.Width,
            Bounds.Height,
            renderScaling);
    }

    private sealed record CaptureRequest(
        string OutputPath,
        int Width,
        int Height,
        OutputFormat Format,
        int JpegQuality,
        bool CleanViewport,
        TaskCompletionSource Completion);
}
