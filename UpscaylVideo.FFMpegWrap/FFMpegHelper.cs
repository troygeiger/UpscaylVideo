using CliWrap;

namespace UpscaylVideo.FFMpegWrap;

public static class FFMpegHelper
{
    public static Command GetFFMpeg(FFMpegOptions options) => Cli.Wrap(options.GetFFMpegBinaryPath())
        .WithValidation(CommandResultValidation.None);

    public static Command GetFFProbe(FFMpegOptions options) => Cli.Wrap(options.GetFFProbeBinaryPath())
        .WithValidation(CommandResultValidation.None);
}