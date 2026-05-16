using Avalonia.Controls;
using Avalonia.Input;
using OpenRender.ViewModels;

namespace OpenRender.Views;

/// <summary>
/// Main application window code-behind.
/// Creates ViewModel in code to avoid XAML parser blocking the UI thread.
/// Handles keyboard shortcuts.
/// </summary>
public partial class MainWindow : Window
{
    private bool _startupHandled;

    public MainWindow()
    {
        InitializeComponent();

        // Create ViewModel in code-behind instead of XAML
        // This ensures the XAML parser finishes first, then we set DataContext
        DataContext = new MainViewModel();

        // Register keyboard shortcuts
        KeyDown += OnKeyDown;
        Opened += OnOpened;
    }

    /// <summary>
    /// Handles global keyboard shortcuts for the application.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.O:
                    vm.ImportFileCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.N:
                    vm.NewSceneCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.S:
                    vm.ExportRenderCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
        else if (e.Key == Key.F5)
        {
            vm.RenderCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (_startupHandled || DataContext is not MainViewModel vm)
            return;

        _startupHandled = true;
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
}
