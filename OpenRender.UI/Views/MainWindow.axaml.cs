using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenRender.Controls;
using OpenRender.ViewModels;

namespace OpenRender.Views;

/// <summary>
/// Code-behind for the main application window.
/// Keeps the window behavior lightweight while the visual Lumion-style layout
/// stays in MainWindow.axaml and the functional state stays in MainViewModel.cs.
/// </summary>
public partial class MainWindow : Window
{
    private bool _startupHandled;
    private bool _isFullScreenViewport;
    private WindowState _previousWindowState = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();

        // Create the ViewModel here so XAML loads fast and does not block
        // while the rendering/editor services initialize.
        DataContext = new MainViewModel();

        KeyDown += OnKeyDown;
        Opened += OnOpened;
        PointerPressed += OnWindowPointerPressed;
    }

    /// <summary>
    /// Global keyboard shortcuts inspired by real-time visualization editors.
    /// These shortcuts are intentionally handled here because they are window-level
    /// interactions, not scene data logic.
    /// </summary>
    private void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.O:
                    vm.ImportFileCommand.Execute(null);
                    e.Handled = true;
                    return;

                case Key.N:
                    vm.NewSceneCommand.Execute(null);
                    e.Handled = true;
                    return;

                case Key.S:
                    vm.ExportRenderCommand.Execute(null);
                    e.Handled = true;
                    return;

                case Key.R:
                    vm.ReloadCurrentModelCommand.Execute(null);
                    e.Handled = true;
                    return;

                case Key.F:
                    vm.FrameAllCommand.Execute(null);
                    e.Handled = true;
                    return;

                case Key.D1:
                case Key.NumPad1:
                    vm.SetViewCommand.Execute("Front");
                    e.Handled = true;
                    return;

                case Key.D3:
                case Key.NumPad3:
                    vm.SetViewCommand.Execute("Right");
                    e.Handled = true;
                    return;

                case Key.D7:
                case Key.NumPad7:
                    vm.SetViewCommand.Execute("Top");
                    e.Handled = true;
                    return;
            }
        }

        switch (e.Key)
        {
            case Key.F5:
                vm.RenderCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.F11:
                ToggleViewportFullScreen();
                e.Handled = true;
                break;

            case Key.Escape:
                if (_isFullScreenViewport)
                {
                    ExitViewportFullScreen();
                    e.Handled = true;
                }
                break;

            case Key.OemPlus:
            case Key.Add:
                vm.ZoomInCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.OemMinus:
            case Key.Subtract:
                vm.ZoomOutCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Home:
                vm.ResetCameraCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (_startupHandled || DataContext is not MainViewModel vm)
            return;

        _startupHandled = true;
        FocusViewport();

        var options = LaunchContext.Options;

        if (string.IsNullOrWhiteSpace(options.StartupFilePath))
            return;

        await vm.LoadStartupFileAsync(
            options.StartupFilePath,
            options.RunSmokeTest,
            options.CapturePath);

        if (options.RunSmokeTest && options.ExitAfterSmokeTest)
            Close();
    }

    private void FocusViewport()
    {
        Dispatcher.UIThread.Post(() =>
        {
            this.FindControl<StrideViewportControl>("ViewportHost")?.Focus();
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Lets the user return focus to the 3D viewport by clicking anywhere that is
    /// not an interactive control. This makes navigation feel closer to Lumion:
    /// click the scene, then move immediately.
    /// </summary>
    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Avalonia.Controls.Control control && IsInteractiveControl(control))
        {
            return;
        }

        FocusViewport();
    }

    private static bool IsInteractiveControl(Avalonia.Controls.Control control)
    {
        return control.GetSelfAndVisualAncestors().OfType<Avalonia.Controls.Button>().Any()
               || control.GetSelfAndVisualAncestors().OfType<Avalonia.Controls.TextBox>().Any()
               || control.GetSelfAndVisualAncestors().OfType<Avalonia.Controls.Slider>().Any()
               || control.GetSelfAndVisualAncestors().OfType<Avalonia.Controls.ComboBox>().Any()
               || control.GetSelfAndVisualAncestors().OfType<Avalonia.Controls.ListBox>().Any()
               || control.GetSelfAndVisualAncestors().OfType<Avalonia.Controls.MenuItem>().Any()
               || control.GetSelfAndVisualAncestors().OfType<Avalonia.Controls.ScrollViewer>().Any();
    }

    private void HeaderBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.ClickCount == 2)
        {
            ToggleMaximizeWindow_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (e.Source is Avalonia.Controls.Control control &&
            control.GetSelfAndVisualAncestors().OfType<Avalonia.Controls.Button>().Any())
        {
            return;
        }

        BeginMoveDrag(e);
    }

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void ToggleMaximizeWindow_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseWindow_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Optional handler for a fullscreen/viewport button in MainWindow.axaml.
    /// If the XAML contains a button wired to this method, it will behave like
    /// Lumion's immersive viewport mode.
    /// </summary>
    private void ToggleViewportFullScreen_Click(object? sender, RoutedEventArgs e)
    {
        ToggleViewportFullScreen();
    }

    private void ToggleViewportFullScreen()
    {
        if (_isFullScreenViewport)
        {
            ExitViewportFullScreen();
            return;
        }

        _previousWindowState = WindowState;
        WindowState = WindowState.FullScreen;
        _isFullScreenViewport = true;
        FocusViewport();

        if (DataContext is MainViewModel vm)
            vm.StatusText = "Modo viewport inmersivo activo. Presiona Esc o F11 para salir.";
    }

    private void ExitViewportFullScreen()
    {
        WindowState = _previousWindowState == WindowState.FullScreen
            ? WindowState.Maximized
            : _previousWindowState;

        _isFullScreenViewport = false;
        FocusViewport();

        if (DataContext is MainViewModel vm)
            vm.StatusText = "Modo viewport inmersivo desactivado.";
    }

    /// <summary>
    /// Optional handler for an import button placed in the Lumion-style left bar.
    /// Keeping these wrappers here makes the XAML cleaner when buttons need
    /// click handlers instead of ICommand bindings.
    /// </summary>
    private void ImportModel_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ImportFileCommand.Execute(null);
    }

    private void NewScene_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NewSceneCommand.Execute(null);
    }

    private void ExportRender_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ExportRenderCommand.Execute(null);
    }

    private void RenderPreview_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.RenderCommand.Execute(null);
    }

    private void FrameAll_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.FrameAllCommand.Execute(null);
    }

    private void ResetCamera_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ResetCameraCommand.Execute(null);
    }
}