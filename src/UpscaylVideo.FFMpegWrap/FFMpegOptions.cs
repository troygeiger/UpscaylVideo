using System.Runtime.InteropServices;

namespace UpscaylVideo.FFMpegWrap;

public class FFMpegOptions
{
    private static readonly Lazy<FFMpegOptions> _globalInstance = new(() => new FFMpegOptions());

    public static FFMpegOptions Global => _globalInstance.Value;
    
    public string FFMpegFolder { get; set; } = string.Empty;

    public string GetFFMpegBinaryPath()
    {
        var bin = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        return string.IsNullOrEmpty(FFMpegFolder) ? bin : Path.Combine(FFMpegFolder, bin);
    }

    public string GetFFProbeBinaryPath()
    {
        var bin = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
        return string.IsNullOrEmpty(FFMpegFolder) ? bin : Path.Combine(FFMpegFolder, bin);
    }
}