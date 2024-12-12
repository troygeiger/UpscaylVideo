using System.Text.Json;
using CliWrap.Buffered;
using UpscaylVideo.FFMpegWrap.Models.Probe;

namespace UpscaylVideo.FFMpegWrap;

public static class FFProbe
{
    public static async Task<FFProbeResult> AnalyseAsync(string mediaPath, FFMpegOptions? options = null)
    {
        var cmd = FFMpegHelper.GetFFProbe(options ?? FFMpegOptions.Global)
            .WithArguments([
                "-print_format",
                "json",
                "-show_format",
                "-sexagesimal",
                "-show_streams",
                mediaPath
            ]);
        var result = await cmd.ExecuteBufferedAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<FFProbeResult>(result.StandardOutput.AsSpan()) ?? new();
    }
}