using System.Globalization;
using CliWrap;
using CliWrap.Buffered;
using UpscaylVideo.FFMpegWrap.Models;

namespace UpscaylVideo.FFMpegWrap;

public static class FFMpeg
{
    public static async Task<bool> ExtractFrames(
        string videoPath, 
        string outputPath, 
        TimeSpan start, 
        TimeSpan end, 
        double? framerate = null, 
        FFMpegOptions? options = null, 
        CancellationToken cancellationToken = default,
        Action<string>? progressAction = null)
    {
        if (!Path.HasExtension(outputPath))
        {
            outputPath = Path.Combine(outputPath, "%08d.png");
        }
        
        var cmd = FFMpegHelper.GetFFMpeg(options ?? FFMpegOptions.Global)
            .WithArguments([
                "-ss",
                start.ToString(),
                "-to",
                end.ToString(),
                "-i",
                videoPath,
                framerate.HasValue ? "-r" : string.Empty,
                framerate.HasValue ? framerate.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                "-progress",
                "pipe:1",
                outputPath
            ]).WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
            {
               progressAction?.Invoke(line); 
            }))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
            {
                
            }));
        var result = await cmd.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    public static async Task<bool> CreateVideoFromFrames(
        IEnumerable<string> framePaths,
        string outputFilePath,
        double frameRate,
        FFMpegOptions? options = null,
        CancellationToken cancellationToken = default,
        Action<string>? progressAction = null
    )

    {
        if (options is null)
            options = FFMpegOptions.Global;

        var tmpList = Path.Combine(options.TempFolder, $"{Guid.NewGuid()}.txt");
        await File.WriteAllLinesAsync(tmpList, framePaths.Select(p => $"file '{p}'"), cancellationToken).ConfigureAwait(false);
        
        var cmd = FFMpegHelper.GetFFMpeg(options ?? FFMpegOptions.Global)
            .WithArguments([
                "-y",
                "-r",
                frameRate.ToString(CultureInfo.InvariantCulture),
                "-f",
                "concat",
                "-safe",
                "0",
                "-i",
                tmpList,
                 "-c:v",
                "libx264",
                "-vf",
                $"fps={frameRate.ToString(CultureInfo.InvariantCulture)},format=yuv420p",
                "-progress",
                "pipe:1",
                outputFilePath
            ]).WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
            {
                progressAction?.Invoke(line);
            }))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
            {
                
            }));
        var result = await cmd.ExecuteBufferedAsync(cancellationToken).ConfigureAwait(false);
        File.Delete(tmpList);
        return result.ExitCode == 0;
    }
}