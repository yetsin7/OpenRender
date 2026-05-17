using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenRender.Materials;
using OpenRender.Scene;
using OpenRender.Rendering;
using OpenRender.Assets;
using OpenRender.Services;

namespace OpenRender.ViewModels;

public partial class MainViewModel : ObservableObject
{    private static Scene3D CreateWorkspaceScene()
    {
        return new Scene3D
        {
            Name = "Estudio vacío",
            AmbientIntensity = 0.18f,
            BackgroundColor = new Vector3(0.52f, 0.68f, 0.85f),
            Exposure = 1.02f,
            Gamma = 2.18f,
            Contrast = 1.01f,
            WhiteBalance = 0.02f
        };
    }

    private static Scene3D CreateDefaultScene()
    {
        var scene = new Scene3D();
        scene.Name = "Villa Demo";
        return scene;
    }

    private static string GetExtensionForFormat(OutputFormat format) => format switch
    {
        OutputFormat.Jpeg => ".jpg",
        OutputFormat.Bmp => ".bmp",
        OutputFormat.Tiff => ".tiff",
        _ => ".png"
    };

    private static OutputFormat ResolveFormatFromPath(string filePath, OutputFormat fallback)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => OutputFormat.Jpeg,
            ".bmp" => OutputFormat.Bmp,
            ".tif" or ".tiff" => OutputFormat.Tiff,
            ".png" => OutputFormat.Png,
            _ => fallback
        };
    }

    private static string EnsureOutputExtension(string filePath, OutputFormat format)
    {
        if (!string.IsNullOrWhiteSpace(Path.GetExtension(filePath)))
            return filePath;
        return filePath + GetExtensionForFormat(format);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(name) ? "render" : name;
    }

    private static int GetCategoryOrder(string? category) => category?.ToLowerInvariant() switch
    {
        "walls" => 0,
        "accent" => 1,
        "ceiling" => 2,
        "stone" => 3,
        "masonry" => 4,
        "concrete" => 5,
        "wood" => 6,
        "ceramic" => 7,
        "metal" => 8,
        "glass" => 9,
        "roof" => 10,
        "landscape" => 11,
        "textile" => 12,
        "synthetic" => 13,
        "concept" => 14,
        _ => 99
    };

    private static string NormalizeSurfaceHint(string value)
    {
        return value.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ").Replace("\\", " ");
    }

    private static bool ContainsAnyHint(string text, params string[] tokens) => tokens.Any(text.Contains);

    private static string TrimNodeName(string name)
    {
        const int maxLength = 34;
        return name.Length <= maxLength ? name : name[..maxLength];
    }

    private static string WithSuffix(string filePath, string suffix)
    {
        string directory = Path.GetDirectoryName(filePath) ?? ".";
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);
        return Path.Combine(directory, $"{fileName}{suffix}{extension}");
    }

    private static bool IsExperimentalVulkanRequested()
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

    private static bool NearlyEqual(float left, float right) => MathF.Abs(left - right) <= 0.0005f;

    private static bool NearlyEqual(Vector3 left, Vector3 right)
    {
        return NearlyEqual(left.X, right.X) && NearlyEqual(left.Y, right.Y) && NearlyEqual(left.Z, right.Z);
    }

    private static Window? GetMainWindow() =>
        Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
