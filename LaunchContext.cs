namespace OpenRender;

public sealed class LaunchOptions
{
    public string? StartupFilePath { get; init; }
    public bool RunSmokeTest { get; init; }
    public bool ExitAfterSmokeTest { get; init; }
    public string? CapturePath { get; init; }
}

public static class LaunchContext
{
    public static LaunchOptions Options { get; private set; } = new();

    public static void Initialize(string[] args)
    {
        string? filePath = null;
        bool smokeTest = false;
        bool exitAfterSmoke = false;
        string? capturePath = null;

        foreach (var arg in args)
        {
            if (arg.StartsWith("--capture=", StringComparison.OrdinalIgnoreCase))
            {
                capturePath = arg["--capture=".Length..].Trim('"');
                continue;
            }

            if (string.Equals(arg, "--smoke-test", StringComparison.OrdinalIgnoreCase))
            {
                smokeTest = true;
                continue;
            }

            if (string.Equals(arg, "--exit-after-smoke", StringComparison.OrdinalIgnoreCase))
            {
                exitAfterSmoke = true;
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.OrdinalIgnoreCase) && filePath == null)
                filePath = arg.Trim('"');
        }

        Options = new LaunchOptions
        {
            StartupFilePath = filePath,
            RunSmokeTest = smokeTest,
            ExitAfterSmokeTest = exitAfterSmoke,
            CapturePath = capturePath
        };
    }
}
