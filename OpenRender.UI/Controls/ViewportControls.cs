using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using OpenRender.Rendering;
using OpenRender.Editor;
using OpenRender.Scene;
using OpenRender.Tools;
using OpenRender.ViewModels;

namespace OpenRender.Controls;

public class StrideViewportControl : NativeControlHost
{
}

public class VulkanViewportControl : NativeControlHost
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    private const uint WS_CHILD = 0x40000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_CLIPSIBLINGS = 0x04000000;
    private const uint WS_CLIPCHILDREN = 0x02000000;
    private const uint SS_BLACKRECT = 0x00000004;

    private IntPtr _hWnd;
    private VulkanContext? _context;
    private VulkanRenderer? _renderer;
    private Thread? _renderThread;
    private bool _isRunning;
    private bool _hasViewportFailure;

    private readonly InputState _input = new();
    private readonly CameraController _cameraController = new();
    private readonly EngineTime _time = new();
    private Point _lastMousePos;
    public Scene3D Scene { get; } = new();

    public VulkanViewportControl()
    {
        Focusable = true;
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (!IsExperimentalRenderLoopEnabled())
        {
            MainViewModel.ReportViewportSafeMode("Viewport en modo seguro. El loop Vulkan experimental queda apagado por defecto en Debug mientras estabilizamos el pipeline.");
            return base.CreateNativeControlCore(parent);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            int initialWidth = Math.Max(16, (int)Bounds.Width);
            int initialHeight = Math.Max(16, (int)Bounds.Height);
            _hWnd = CreateWindowEx(
                0, "STATIC", "", WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN | SS_BLACKRECT,
                0, 0, initialWidth, initialHeight,
                parent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (_hWnd != IntPtr.Zero)
            {
                try
                {
                    InitializeVulkan(_hWnd);
                    MainViewModel.ReportViewportReady(
                        _renderer?.FrameSubmitEnabled != true
                            ? "Vulkan inicializo contexto, surface y swapchain. Frame submit queda pausado hasta OPENRENDER_ENABLE_VULKAN_FRAME_SUBMIT=1 para evitar el crash nativo en CmdBeginRenderPass."
                            : _renderer?.AdvancedPipelineEnabled == true
                            ? "Vulkan avanzado encendido: GBuffer/SSAO y frame submit completo bajo prueba."
                            : "Vulkan inicializo estable en modo present minimo. GBuffer/SSAO queda detras de OPENRENDER_ENABLE_VULKAN_ADVANCED_PIPELINE=1.");
                }
                catch (Exception ex)
                {
                    FailViewport("Vulkan no inició", ex);
                    if (_hWnd != IntPtr.Zero)
                    {
                        DestroyWindow(_hWnd);
                        _hWnd = IntPtr.Zero;
                    }

                    return base.CreateNativeControlCore(parent);
                }

                return new PlatformHandle(_hWnd, "HWND");
            }
        }

        MainViewModel.ReportGlError("Viewport nativo no disponible en esta plataforma.");
        return base.CreateNativeControlCore(parent);
    }

    private void InitializeVulkan(IntPtr hwnd)
    {
        _context = new VulkanContext();
        _renderer = new VulkanRenderer(_context);
        _renderer.InitializeSurface(hwnd);

        _time.Start();
        _isRunning = true;
        _renderThread = new Thread(RenderLoop) { IsBackground = true, Name = "VulkanRenderThread" };
        _renderThread.Start();
    }

    private void RenderLoop()
    {
        try
        {
            if (_renderer?.AdvancedPipelineEnabled == true)
            {
                var testMesh = _renderer.AddMesh(new[]
                {
                    new Vertex { Position = new Vector3(0.0f, 0.0f, 0.0f), Normal = Vector3.UnitY, TexCoord = new Vector2(0.5f, 0.0f) },
                    new Vertex { Position = new Vector3(0.2f, 1.0f, 0.0f), Normal = Vector3.UnitY, TexCoord = new Vector2(1.0f, 1.0f) },
                    new Vertex { Position = new Vector3(-0.2f, 1.0f, 0.0f), Normal = Vector3.UnitY, TexCoord = new Vector2(0.0f, 1.0f) }
                }, new uint[] { 0, 1, 2 });

                var instances = new Matrix4x4[1000];
                int i = 0;
                for (int x = -10; x < 10; x++)
                {
                    for (int z = -25; z < 25; z++)
                    {
                        if (i >= 1000) break;
                        instances[i++] = Matrix4x4.CreateTranslation(new Vector3(x * 1.5f, 0, z * 1.5f));
                    }
                }
                testMesh.SetupInstancing(instances);
            }

            while (_isRunning)
            {
                _time.Update();
                _cameraController.Update(Scene.Camera, _input, _time);

                float aspect = (float)(Bounds.Width / Bounds.Height);
                if (aspect < 0.1f) aspect = 1.0f;

                Matrix4x4 view = Matrix4x4.CreateLookAt(Scene.Camera.Position, Scene.Camera.Target, Vector3.UnitY);
                Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathHelper.ToRadians(Scene.Camera.FieldOfView), aspect, Scene.Camera.NearPlane, Scene.Camera.FarPlane);
                proj.M22 *= -1;

                Matrix4x4 viewProjection = view * proj;
                _input.ClearDelta();
                _renderer?.Render(viewProjection, Scene.Camera.Position, _time.TotalTime, proj);
                Thread.Sleep(1);
            }
        }
        catch (Exception ex)
        {
            FailViewport("El render Vulkan falló durante la ejecución", ex);
        }
    }

    private readonly SelectionSystem _selectionSystem = new();
    private readonly GizmoSystem _gizmoSystem = new();

    private static bool IsExperimentalRenderLoopEnabled()
    {
#if DEBUG
        return string.Equals(
            Environment.GetEnvironmentVariable("OPENRENDER_ENABLE_EXPERIMENTAL_VULKAN_LOOP"),
            "1",
            StringComparison.Ordinal);
#else
        return true;
#endif
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        
        if (point.Properties.IsLeftButtonPressed)
        {
            _input.IsLeftMouseDown = true;
            PerformPicking(point.Position);
        }
        
        if (point.Properties.IsRightButtonPressed) _input.IsRightMouseDown = true;
        if (point.Properties.IsMiddleButtonPressed) _input.IsMiddleMouseDown = true;
        
        e.Pointer.Capture(this);
        _lastMousePos = point.Position;
        Focus();
    }

    private void PerformPicking(Point mousePos)
    {
        float aspect = (float)(Bounds.Width / Bounds.Height);
        float fovRad = MathHelper.ToRadians(Scene.Camera.FieldOfView);

        float nx = (float)(2.0 * mousePos.X / Bounds.Width - 1.0);
        float ny = (float)(1.0 - 2.0 * mousePos.Y / Bounds.Height);

        Matrix4x4 view = Matrix4x4.CreateLookAt(Scene.Camera.Position, Scene.Camera.Target, Vector3.UnitY);
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(fovRad, aspect, Scene.Camera.NearPlane, Scene.Camera.FarPlane);
        Matrix4x4.Invert(view * proj, out var invVP);

        Vector4 nearPoint = Vector4.Transform(new Vector4(nx, ny, 0.0f, 1.0f), invVP);
        Vector4 farPoint = Vector4.Transform(new Vector4(nx, ny, 1.0f, 1.0f), invVP);

        nearPoint /= nearPoint.W;
        farPoint /= farPoint.W;

        var ray = new Ray(new Vector3(nearPoint.X, nearPoint.Y, nearPoint.Z),
                          new Vector3(farPoint.X - nearPoint.X, farPoint.Y - nearPoint.Y, farPoint.Z - nearPoint.Z));

        var hitNode = _selectionSystem.Pick(ray, Scene);
        _selectionSystem.Select(hitNode);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        
        var delta = point.Position - _lastMousePos;
        _input.MouseDelta = new Vector2((float)delta.X, (float)delta.Y);
        _input.MousePosition = new Vector2((float)point.Position.X, (float)point.Position.Y);
        
        _lastMousePos = point.Position;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsRightButtonPressed) _input.IsRightMouseDown = false;
        if (!point.Properties.IsMiddleButtonPressed) _input.IsMiddleMouseDown = false;
        if (!point.Properties.IsLeftButtonPressed) _input.IsLeftMouseDown = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _input.MouseWheelDelta = (float)e.Delta.Y;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _input.SetKeyDown(e.Key.ToString());
        _input.IsShiftDown = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _input.IsControlDown = e.KeyModifiers.HasFlag(KeyModifiers.Control);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _input.SetKeyUp(e.Key.ToString());
        _input.IsShiftDown = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _input.IsControlDown = e.KeyModifiers.HasFlag(KeyModifiers.Control);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (_renderer != null && !_hasViewportFailure && (size.Width > 1 && size.Height > 1))
        {
            try
            {
                if (_hWnd != IntPtr.Zero)
                    MoveWindow(_hWnd, 0, 0, (int)size.Width, (int)size.Height, true);
                _renderer.Resize((uint)size.Width, (uint)size.Height);
            }
            catch (Exception ex)
            {
                FailViewport("No pude redimensionar el viewport Vulkan", ex);
            }
        }
        return size;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        ShutdownViewport();

        if (_hWnd != IntPtr.Zero)
        {
            DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        base.DestroyNativeControlCore(control);
    }

    private void FailViewport(string stage, Exception ex)
    {
        if (_hasViewportFailure)
            return;

        _hasViewportFailure = true;
        ShutdownViewport();
        MainViewModel.ReportViewportSafeMode($"Viewport en modo seguro. {stage}: {ex.Message}");
    }

    private void ShutdownViewport()
    {
        _isRunning = false;

        if (_renderThread != null && _renderThread.IsAlive && _renderThread != Thread.CurrentThread)
            _renderThread.Join();

        _renderThread = null;
        _renderer?.Dispose();
        _renderer = null;
        _context?.Dispose();
        _context = null;
    }

}
