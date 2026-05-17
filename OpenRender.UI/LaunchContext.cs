namespace OpenRender;

public sealed class LaunchOptions
{
    public string? StartupFilePath { get; init; }
    public bool RunSmokeTest { get; init; }
    public bool ExitAfterSmokeTest { get; init; }
    public string? CapturePath { get; init; }
    public string? StartSection { get; init; }
    public string? StartTool { get; init; }
}

public static class LaunchContext
{
    public static LaunchOptions Options { get; private set; } = new();

    public static void Initialize(string[] args)
    {
        bool smokeTest = false;
        bool exitAfterSmoke = false;
        string? capturePath = null;
        string? startSection = null;
        string? startTool = null;
        var pathParts = new List<string>();

        foreach (var arg in args)
        {
            if (arg.StartsWith("--capture=", StringComparison.OrdinalIgnoreCase))
            {
                capturePath = arg["--capture=".Length..].Trim('"');
                continue;
            }

            if (arg.StartsWith("--start-section=", StringComparison.OrdinalIgnoreCase))
            {
                startSection = arg["--start-section=".Length..].Trim('"');
                continue;
            }

            if (arg.StartsWith("--start-tool=", StringComparison.OrdinalIgnoreCase))
            {
                startTool = arg["--start-tool=".Length..].Trim('"');
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

            if (!arg.StartsWith("--", StringComparison.OrdinalIgnoreCase))
                pathParts.Add(arg.Trim('"'));
        }

        Options = new LaunchOptions
        {
            StartupFilePath = ResolveStartupPath(pathParts),
            RunSmokeTest = smokeTest,
            ExitAfterSmokeTest = exitAfterSmoke,
            CapturePath = capturePath,
            StartSection = startSection,
            StartTool = startTool
        };
    }

    private static string? ResolveStartupPath(IReadOnlyList<string> pathParts)
    {
        if (pathParts.Count == 0)
            return null;

        if (pathParts.Count == 1)
            return pathParts[0];

        var joined = string.Join(" ", pathParts);
        return File.Exists(joined) ? joined : pathParts[0];
    }
}
