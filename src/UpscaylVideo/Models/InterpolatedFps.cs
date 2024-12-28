namespace UpscaylVideo.Models;

public record InterpolatedFps(double? FrameRate, string DisplayName)
{
    public InterpolatedFps(double? frameRate) : this(frameRate, frameRate?.ToString() ?? string.Empty)
    { }
}