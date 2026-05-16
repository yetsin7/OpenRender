namespace OpenRender.Core.Rendering;

/// <summary>
/// Configuration settings for the render output.
/// Controls resolution, quality, and post-processing.
/// </summary>
public class RenderSettings
{
    /// <summary>
    /// Output image width in pixels.
    /// </summary>
    public int Width { get; set; } = 1920;

    /// <summary>
    /// Output image height in pixels.
    /// </summary>
    public int Height { get; set; } = 1080;

    /// <summary>
    /// Anti-aliasing sample count (1 = no AA, 2/4/8 = MSAA).
    /// </summary>
    public int SampleCount { get; set; } = 4;

    /// <summary>
    /// Render quality preset.
    /// </summary>
    public RenderQuality Quality { get; set; } = RenderQuality.High;

    /// <summary>
    /// Whether to enable tone mapping.
    /// </summary>
    public bool ToneMapping { get; set; } = true;

    /// <summary>
    /// Exposure value for tone mapping.
    /// </summary>
    public float Exposure { get; set; } = 1.0f;

    /// <summary>
    /// Gamma correction value.
    /// </summary>
    public float Gamma { get; set; } = 2.2f;

    /// <summary>
    /// Whether to enable ambient occlusion.
    /// </summary>
    public bool AmbientOcclusion { get; set; } = true;

    /// <summary>
    /// Whether to enable shadow rendering.
    /// </summary>
    public bool Shadows { get; set; } = true;

    /// <summary>
    /// Shadow map resolution.
    /// </summary>
    public int ShadowMapResolution { get; set; } = 2048;

    /// <summary>
    /// Output file format for rendered images.
    /// </summary>
    public OutputFormat Format { get; set; } = OutputFormat.Png;

    /// <summary>
    /// JPEG quality (1-100), only used when Format is Jpeg.
    /// </summary>
    public int JpegQuality { get; set; } = 95;
}

/// <summary>
/// Render quality presets.
/// </summary>
public enum RenderQuality
{
    /// <summary>Draft quality for quick previews.</summary>
    Draft,
    /// <summary>Medium quality balanced performance.</summary>
    Medium,
    /// <summary>High quality for final renders.</summary>
    High,
    /// <summary>Ultra quality maximum fidelity.</summary>
    Ultra
}

/// <summary>
/// Output image format.
/// </summary>
public enum OutputFormat
{
    Png,
    Jpeg,
    Bmp,
    Tiff
}
