namespace OpenRender.Rendering;

public enum OutputFormat
{
    Png,
    Jpeg,
    Bmp,
    Tiff
}

public enum RenderQuality
{
    Draft,
    Low,
    Medium,
    High,
    Ultra
}

public class RenderSettings
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public OutputFormat Format { get; set; } = OutputFormat.Png;
    public int JpegQuality { get; set; } = 90;
    public RenderQuality Quality { get; set; } = RenderQuality.High;
    public int SampleCount { get; set; } = 1;
    public float Exposure { get; set; } = 1.0f;
    public float Gamma { get; set; } = 2.2f;
}
