using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using CliWrap;
using CliWrap.Buffered;
using UpscaylVideo.FFMpegWrap.Models;
using UpscaylVideo.FFMpegWrap.Models.Probe;

namespace UpscaylVideo.FFMpegWrap;

public static class FFMpeg
{
    
    
    public static Task<bool> ExtractFrames(
        string inputFilePath,
        string outputFilePath,
        double framerate,
        CancellationToken cancellationToken = default,
        FFMpegOptions? options = null,
        Action<string>? progressAction = null)
        => ExtractFrames(inputFilePath, outputFilePath, framerate, null, null, cancellationToken, options, progressAction);

    public static async Task<bool> ExtractFrames(
        string inputFilePath,
        string outputPath,
        double? framerate = null,
        TimeSpan? start = null,
        TimeSpan? end = null,
        CancellationToken cancellationToken = default,
        FFMpegOptions? options = null,
        Action<string>? progressAction = null)
    {
        if (!Path.HasExtension(outputPath))
        {
            outputPath = Path.Combine(outputPath, "%08d.png");
        }

        List<string> args = new(["-y"]);
        if (start.HasValue)
        {
            args.AddRange(["-ss", start.Value.ToString()]);
        }
        if (end.HasValue)
        {
            args.AddRange(["-ss", end.Value.ToString()]);
        }
        args.AddRange(["-i", inputFilePath]);
        if (framerate.HasValue)
        {
            args.AddRange(["-r", framerate.Value.ToString(CultureInfo.InvariantCulture)]);
        }
        args.AddRange([
            "-progress",
            "pipe:1",
            outputPath
        ]);
        var cmd = FFMpegHelper.GetFFMpeg(options ?? FFMpegOptions.Global)
            .WithArguments(args)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => { progressAction?.Invoke(line); }));
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
            ]).WithStandardOutputPipe(PipeTarget.ToDelegate(line => { progressAction?.Invoke(line); }));
        var result = await cmd.ExecuteBufferedAsync(cancellationToken).ConfigureAwait(false);
        File.Delete(tmpList);
        return result.ExitCode == 0;
    }

    public static async Task<bool> ConcatinateFiles(
        IEnumerable<string> filePaths,
        string outputFilePath,
        FFMpegOptions? options = null,
        CancellationToken cancellationToken = default,
        Action<string>? progressAction = null
    )

    {
        if (options is null)
            options = FFMpegOptions.Global;

        var tmpList = Path.Combine(options.TempFolder, $"{Guid.NewGuid()}.txt");
        await File.WriteAllLinesAsync(tmpList, filePaths.Select(p => $"file '{p}'"), cancellationToken).ConfigureAwait(false);

        var cmd = FFMpegHelper.GetFFMpeg(options ?? FFMpegOptions.Global)
            .WithArguments([
                "-y",
                "-f",
                "concat",
                "-safe",
                "0",
                "-i",
                tmpList,
                "-c",
                "copy",
                "-progress",
                "pipe:1",
                outputFilePath
            ]).WithStandardOutputPipe(PipeTarget.ToDelegate(line => { progressAction?.Invoke(line); }));
        var result = await cmd.ExecuteBufferedAsync(cancellationToken).ConfigureAwait(false);
        File.Delete(tmpList);
        return result.ExitCode == 0;
    }

    public static async Task<bool> CopyStreams(
        string inputFilePath,
        string outputFilePath,
        IEnumerable<FFProbeStream> streams,
        FFMpegOptions? options = null,
        CancellationToken cancellationToken = default,
        Action<string>? progressAction = null)
    {
        List<string> args = new List<string>();
        args.AddRange(["-y", "-i", inputFilePath]);
        foreach (var stream in streams)
        {
            args.AddRange([
                "-map", $"0:{stream.Index}"
            ]);
        }
        args.AddRange([
            "-c", "copy",
            "-progress",
            "pipe:1",
            outputFilePath,
        ]);
        
        var cmd = FFMpegHelper.GetFFMpeg(options ?? FFMpegOptions.Global)
            .WithArguments(args)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => progressAction?.Invoke(line)));
        var result = await cmd.ExecuteBufferedAsync(cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    public static async Task<bool> ExtractFFMetadata(
        string inputFilePath,
        string outputFilePath,
        FFMpegOptions? options = null,
        CancellationToken cancellationToken = default,
        Action<string>? progressAction = null)
    {
        var cmd = FFMpegHelper.GetFFMpeg(options ?? FFMpegOptions.Global)
            .WithArguments([
                "-i",
                inputFilePath,
                "-f",
                "ffmetadata",
                "-progress",
                "pipe:1",
                outputFilePath,
            ])
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => progressAction?.Invoke(line)));
        var result = await cmd.ExecuteBufferedAsync(cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    public static async Task<bool> MergeFiles(
        IEnumerable<string> inputFilePaths,
        string outputFilePath,
        FFMpegOptions? options = null,
        CancellationToken cancellationToken = default,
        Action<string>? progressAction = null)
    {
        List<string> args = new List<string>();
        args.Add("-y");
        foreach (var inputFilePath in inputFilePaths)
        {
            args.AddRange(["-i", inputFilePath,]);
        }
        args.AddRange([
            "-c", "copy",
            "-progress", "pipe:1",
            outputFilePath,
        ]);
        
        var cmd = FFMpegHelper.GetFFMpeg(options ?? FFMpegOptions.Global)
            .WithArguments(args)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => progressAction?.Invoke(line)));
        var result = await cmd.ExecuteBufferedAsync(cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    public static (Process ffProcess, Stream stdOutStream)  StartPngPipe(string inputFilePath, double framerate, FFMpegOptions? options = null)
    {
        ProcessStartInfo ffStart = new(FFMpegHelper.GetFFMpegBinaryPath(options), [
            "-i",
            inputFilePath,
            "-r", framerate.ToString(CultureInfo.InvariantCulture),
            "-c:v",
            "png", "-f", "image2pipe",
            "-",
        ])
        {
            RedirectStandardOutput = true
        };
        //ffstate.RedirectStandardError = true;
        var process = Process.Start(ffStart);
        if (process == null)
            throw new Exception("Unable to start FFMpeg");
        return (process, new BufferedStream(process.StandardOutput.BaseStream));
    }

    public static (Process ffProcess, Stream stdInStream) StartPngFramesToVideoPipe(string outputFilePath, double framerate, FFMpegOptions? options = null)
    {
        var strFramerate = framerate.ToString(CultureInfo.InvariantCulture);
        ProcessStartInfo ffStart = new(FFMpegHelper.GetFFMpegBinaryPath(options),[
            "-y",
            "-framerate",
            strFramerate,
            "-f", "image2pipe",
            "-c:v", "png",
            "-i", "-",
            "-r",
            strFramerate,
            "-vf", "format=yuv420p",
            outputFilePath,
        ])
        {
            RedirectStandardInput = true
        };
        var process = Process.Start(ffStart);
        if (process == null)
            throw new Exception("Unable to start FFMpeg");
        return (process, process.StandardInput.BaseStream);
    }
}