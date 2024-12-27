using CliWrap;

namespace UpscaylVideo.FFMpegWrap;

public static class FFMpegHelper
{
    public static Command GetFFMpeg(FFMpegOptions options) => Cli.Wrap(options.GetFFMpegBinaryPath())
        .WithValidation(CommandResultValidation.None);

    public static string GetFFMpegBinaryPath(FFMpegOptions? options)
    {
        if (options == null)
            options = FFMpegOptions.Global;
        return options.GetFFMpegBinaryPath();
    }
    
    public static Command GetFFProbe(FFMpegOptions options) => Cli.Wrap(options.GetFFProbeBinaryPath())
        .WithValidation(CommandResultValidation.None);

    public static string FFMpegExecutable { get; } = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

    public static string FFProbeExecutable { get; } = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
}