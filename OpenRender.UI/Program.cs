using Avalonia;

namespace OpenRender;

/// <summary>
/// Application entry point.
/// Configures and launches the Avalonia application.
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry point for the Open Render application.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        LaunchContext.Initialize(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Builds the Avalonia application with fluent theme and Inter font.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
