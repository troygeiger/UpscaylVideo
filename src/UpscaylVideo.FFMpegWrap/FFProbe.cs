using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using UpscaylVideo.FFMpegWrap.Models.Probe;

namespace UpscaylVideo.FFMpegWrap;

public static class FFProbe
{
    public static async Task<(bool success, CommandResult cmdResult, FFProbeResult? result)> AnalyseAsync(string mediaPath,
        FFMpegOptions? options = null)
    {
        var cmd = FFMpegHelper.GetFFProbe(options ?? FFMpegOptions.Global)
            .WithArguments([
                "-print_format",
                "json",
                "-show_format",
                //"-sexagesimal",
                "-show_streams",
                mediaPath
            ]);
        var result = await cmd.ExecuteBufferedAsync().ConfigureAwait(false);
        if (result.ExitCode != 0)
            return (false, result, null);
        return (
            success: result.ExitCode == 0, 
            cmdResult: result,
            result: JsonSerializer.Deserialize<FFProbeResult>(result.StandardOutput.AsSpan())
            );
    }
}