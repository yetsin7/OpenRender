using System.ComponentModel;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OpenRender.Editor;
using OpenRender.Scene;
using OpenRender.Services;
using OpenRender.Tools;
using OpenRender.ViewModels;

namespace OpenRender.Controls;

/// <summary>
/// Viewport seguro para navegar modelos importados mientras el backend
/// nativo sigue en estabilización.
/// </summary>
public sealed class SoftwareViewportControl : Control
{
    private readonly DispatcherTimer _timer;
    private readonly EngineTime _time = new();
    private readonly InputState _input = new();
    private readonly CameraController _cameraController = new();
    private readonly ViewportPreviewRenderer _previewRenderer = new();
    private readonly SelectionSystem _selectionSystem = new();

    private MainViewModel? _viewModel;
    private WriteableBitmap? _bitmap;
    private Scene3D? _scene;
    private Point _lastPointerPosition;
    private CameraSnapshot _lastCameraSnapshot;
    private SceneNode? _lastSelectedNode;
    private MaterialSnapshot _lastMaterialSnapshot;
    private bool _isDirty = true;

    public SoftwareViewportControl()
    {
        Focusable = true;
        ClipToBounds = true;
        _time.Start();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, OnTick);
        _timer.Start();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as MainViewModel;
        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        _scene = null;
        _isDirty = true;
        base.OnDataContextChanged(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0D1014")), Bounds);

        if (_bitmap != null)
        {
            context.DrawImage(_bitmap, new Rect(0, 0, _bitmap.Size.Width, _bitmap.Size.Height), Bounds);
            return;
        }

        var pen = new Pen(new SolidColorBrush(Color.Parse("#27313A")));
        context.DrawRectangle(pen, Bounds.Deflate(1));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsLeftButtonPressed)
        {
            _input.IsLeftMouseDown = true;
            SelectNodeFromViewport(point.Position);
        }

        if (point.Properties.IsRightButtonPressed)
            _input.IsRightMouseDown = true;

        if (point.Properties.IsMiddleButtonPressed)
            _input.IsMiddleMouseDown = true;

        _lastPointerPosition = point.Position;
        e.Pointer.Capture(this);
        Focus();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        var delta = point.Position - _lastPointerPosition;

        _input.MouseDelta = new Vector2((float)delta.X, (float)delta.Y);
        _input.MousePosition = new Vector2((float)point.Position.X, (float)point.Position.Y);
        _lastPointerPosition = point.Position;

        if (_input.IsRightMouseDown || _input.IsMiddleMouseDown)
            _isDirty = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetCurrentPoint(this);

        if (!point.Properties.IsLeftButtonPressed)
            _input.IsLeftMouseDown = false;
        if (!point.Properties.IsRightButtonPressed)
            _input.IsRightMouseDown = false;
        if (!point.Properties.IsMiddleButtonPressed)
            _input.IsMiddleMouseDown = false;

        _input.MouseDelta = Vector2.Zero;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _input.MouseWheelDelta = (float)e.Delta.Y;
        _isDirty = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _input.SetKeyDown(e.Key.ToString());
        _input.IsShiftDown = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _input.IsControlDown = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        _isDirty = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _input.SetKeyUp(e.Key.ToString());
        _input.IsShiftDown = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _input.IsControlDown = e.KeyModifiers.HasFlag(KeyModifiers.Control);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_viewModel?.Scene == null || Bounds.Width < 40 || Bounds.Height < 40)
            return;

        if (!ReferenceEquals(_scene, _viewModel.Scene))
        {
            _scene = _viewModel.Scene;
            _previewRenderer.SetScene(_scene);
            _isDirty = true;
        }

        _time.Update();
        var before = CameraSnapshot.Create(_scene.Camera);
        _cameraController.Update(_scene.Camera, _input, _time);
        var after = CameraSnapshot.Create(_scene.Camera);

        if (!before.Equals(after))
        {
            _viewModel.SyncViewportCameraState();
            _isDirty = true;
        }

        var selectedNode = _viewModel.SelectedSceneNode?.Node;
        if (!ReferenceEquals(_lastSelectedNode, selectedNode))
        {
            _lastSelectedNode = selectedNode;
            _isDirty = true;
        }

        var materialSnapshot = MaterialSnapshot.Create(_viewModel.SelectedMaterial);
        if (!_lastMaterialSnapshot.Equals(materialSnapshot))
        {
            _lastMaterialSnapshot = materialSnapshot;
            _viewModel.SyncViewportMaterialState();
            _isDirty = true;
        }

        if (_isDirty || !_lastCameraSnapshot.Equals(after))
        {
            RenderViewport(selectedNode);
            _lastCameraSnapshot = after;
            _isDirty = false;
        }

        _input.ClearDelta();
    }

    private void RenderViewport(SceneNode? selectedNode)
    {
        if (_scene == null)
            return;

        _bitmap = _previewRenderer.Render(
            _scene,
            _scene.Camera,
            new PixelSize((int)Math.Max(320, Bounds.Width), (int)Math.Max(220, Bounds.Height)),
            selectedNode);

        InvalidateVisual();
    }

    private void SelectNodeFromViewport(Point position)
    {
        if (_scene == null || _viewModel == null || Bounds.Width < 2 || Bounds.Height < 2)
            return;

        float aspect = (float)(Bounds.Width / Bounds.Height);
        float fovRad = MathF.PI * _scene.Camera.FieldOfView / 180f;
        float nx = (float)(2.0 * position.X / Bounds.Width - 1.0);
        float ny = (float)(1.0 - 2.0 * position.Y / Bounds.Height);

        var view = Matrix4x4.CreateLookAt(_scene.Camera.Position, _scene.Camera.Target, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(fovRad, aspect, _scene.Camera.NearPlane, _scene.Camera.FarPlane);
        Matrix4x4.Invert(view * projection, out var inverseViewProjection);

        Vector4 nearPoint = Vector4.Transform(new Vector4(nx, ny, 0.0f, 1.0f), inverseViewProjection);
        Vector4 farPoint = Vector4.Transform(new Vector4(nx, ny, 1.0f, 1.0f), inverseViewProjection);
        nearPoint /= nearPoint.W;
        farPoint /= farPoint.W;

        var ray = new Ray(
            new Vector3(nearPoint.X, nearPoint.Y, nearPoint.Z),
            Vector3.Normalize(new Vector3(farPoint.X - nearPoint.X, farPoint.Y - nearPoint.Y, farPoint.Z - nearPoint.Z)));

        var hitNode = _selectionSystem.Pick(ray, _scene);
        _viewModel.SelectViewportHit(hitNode?.Id.ToString());
        _isDirty = true;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.Scene) or nameof(MainViewModel.SelectedSceneNode))
            _isDirty = true;
    }

    private readonly record struct CameraSnapshot(Vector3 Position, Vector3 Target, float OrbitDistance, float FieldOfView)
    {
        public static CameraSnapshot Create(CameraComponent camera) => new(camera.Position, camera.Target, camera.OrbitDistance, camera.FieldOfView);
    }

    private readonly record struct MaterialSnapshot(Vector3 Albedo, float Roughness, float Metallic, float Opacity, float NormalStrength)
    {
        public static MaterialSnapshot Create(OpenRender.Materials.PbrMaterial? material) =>
            material == null
                ? default
                : new(material.Albedo, material.Roughness, material.Metallic, material.Opacity, material.NormalStrength);
    }
}
