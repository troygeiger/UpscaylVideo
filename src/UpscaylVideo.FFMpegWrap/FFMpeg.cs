using System.Diagnostics;
using System.Globalization;
using CliWrap;
using CliWrap.Buffered;
using UpscaylVideo.FFMpegWrap.Models.Probe;

namespace UpscaylVideo.FFMpegWrap;

public static class FFMpeg
{
    /*public static Task<bool> ExtractFrames(
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
    }*/

    /*public static async Task<bool> CreateVideoFromFrames(
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

        var cmd = FFMpegHelper.GetFFMpeg(options)
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
    }*/

    /*public static async Task<bool> ConcatinateFiles(
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

        var cmd = FFMpegHelper.GetFFMpeg(options)
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
    }*/

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

    /*public static async Task<bool> MergeFiles(
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
    }*/
    
    public static async Task<bool> MergeFiles(
        string videoFile,
        string audioFile,
        string metadataFile,
        string outputFilePath,
        FFMpegOptions? options = null,
        CancellationToken cancellationToken = default,
        Action<string>? progressAction = null)
    {
        var cmd = FFMpegHelper.GetFFMpeg(options ?? FFMpegOptions.Global)
            .WithArguments([
                "-y",
                "-i", videoFile,
                "-i", audioFile,
                "-i", metadataFile,
                "-map", "0:v",
                "-map", "1:a",
                "-c", "copy",
                "-progress", "pipe:1",
                outputFilePath
            ])
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => progressAction?.Invoke(line)));
        var result = await cmd.ExecuteBufferedAsync(cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    /// <summary>
    /// Merge the newly created video file with all non-video streams from the source media (audio, subtitles, attachments),
    /// while also copying chapters and global metadata from the source. This avoids temporary audio/metadata extraction.
    /// NOTE: Container compatibility applies. For example, MP4 does not support image-based subtitles (VobSub/PGS).
    /// Prefer MKV to preserve DVD/BD subtitles.
    /// </summary>
    public static async Task<bool> MergeVideoWithSourceStreams(
        string newVideoFile,
        string sourceMediaFile,
        string outputFilePath,
        IEnumerable<int>? selectedSubtitleIndices = null,
        bool includeAudio = true,
        bool includeAttachments = true,
        FFMpegOptions? options = null,
        CancellationToken cancellationToken = default,
        Action<string>? progressAction = null)
    {
        // Input 0: the upscaled video
        // Input 1: the original source with audio/subs/etc
        // Map: take video from 0, everything from 1 except video
        var args = new List<string>
        {
            "-y",
            // 0 = new video with upscaled frames
            "-i", newVideoFile,
            // 1 = original source (audio, subs, chapters, attachments)
            "-i", sourceMediaFile,
            // Take video only from input 0
            "-map", "0:v",
        };

        if (includeAudio)
        {
            args.AddRange(["-map", "1:a?"]);
        }

        if (selectedSubtitleIndices != null)
        {
            foreach (var idx in selectedSubtitleIndices)
            {
                args.AddRange(["-map", $"1:{idx}"]);
            }
        }
        else
        {
            // default: include all subtitle streams if any
            args.AddRange(["-map", "1:s?"]);
        }

        if (includeAttachments)
        {
            // Optionally take all attachments from input 1 (e.g., fonts in MKV)
            args.AddRange(["-map", "1:t?"]);
        }

        args.AddRange(new[]
        {
            // Copy chapters and global metadata from source
            "-map_chapters", "1",
            "-map_metadata", "1",
            // Stream copy for all, no re-encode
            "-c", "copy",
            "-progress", "pipe:1",
            outputFilePath
        });

        var cmd = FFMpegHelper.GetFFMpeg(options ?? FFMpegOptions.Global)
            .WithArguments(args)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => progressAction?.Invoke(line)));

        var result = await cmd.ExecuteBufferedAsync(cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    /*public static (Process ffProcess, Stream stdOutStream) StartPngPipe(string inputFilePath, double framerate, FFMpegOptions? options = null)
    {
        ProcessStartInfo ffStart = new(FFMpegHelper.GetFFMpegBinaryPath(options), [
            "-i",
            inputFilePath,
            "-r", framerate.ToString(CultureInfo.InvariantCulture),
            "-vf",
            "scale='max(iw,iw*sar)':'max(ih,ih/sar)'",
            "-c:v",
            "png", "-f", "image2pipe",
            "-",
        ])
        {
            RedirectStandardOutput = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = false,
        };
        var process = Process.Start(ffStart);
        if (process == null)
            throw new Exception("Unable to start FFMpeg");
        return (process, new BufferedStream(process.StandardOutput.BaseStream));
    }*/

    public static (Process ffProcess, Stream stdOutStream) StartImagePipe(string inputFilePath, double framerate, string? imageFormat, FFMpegOptions? options = null, bool cropToWidescreen = false, float cropVerticalOffset = 0.5f)
    {
        options ??= FFMpegOptions.Global;
        if (options.JpegQuality < 1 || options.JpegQuality > 31)
            options.JpegQuality = 2; // reset to default if out of range
        
        var fmt = (imageFormat ?? "png").ToLowerInvariant();
        var decoder = fmt switch
        {
            "png" => "png",
            "jpg" => "mjpeg",
            "jpeg" => "mjpeg",
            _ => "png"
        };
        
        // Build video filter chain
        var filters = new List<string>();
        
        // Crop filter for 4:3 to 16:9 conversion
        if (cropToWidescreen)
        {
            // crop=in_w:in_w*9/16:x:y
            // x=0 (keep full width), y=(in_h-in_w*9/16)*offset
            var yOffset = $"(ih-iw*9/16)*{cropVerticalOffset.ToString(CultureInfo.InvariantCulture)}";
            filters.Add($"crop=iw:iw*9/16:0:{yOffset}");
        }
        
        // Scale filter (always applied to handle SAR)
        filters.Add("scale='max(iw,iw*sar)':'max(ih,ih/sar)'");
        
        List<string> args = new()
        {
            "-i",
            inputFilePath,
            "-r", framerate.ToString(CultureInfo.InvariantCulture),
            "-vf",
            string.Join(',', filters),
        };
        if (decoder == "mjpeg")
        {
            args.AddRange([
                "-q:v", options.JpegQuality.ToString(),
            ]);
        }
        args.AddRange([
            "-c:v",
            decoder, "-f", "image2pipe",
            "-",
        ]);
        
        ProcessStartInfo ffStart = new(FFMpegHelper.GetFFMpegBinaryPath(options), args)
        {
            RedirectStandardOutput = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = false,
        };
        var process = Process.Start(ffStart);
        if (process == null)
            throw new Exception("Unable to start FFMpeg");
        return (process, new BufferedStream(process.StandardOutput.BaseStream));
    }

    /*public static (Process ffProcess, Stream stdInStream) StartPngFramesToVideoPipe(
        string outputFilePath,
        double framerate,
        FFMpegOptions? options = null,
        double? frameInterpolationFps = null)
    {
        var strFramerate = framerate.ToString(CultureInfo.InvariantCulture);
        List<string> args = new List<string>()
        {
            "-y",
            "-framerate",
            strFramerate,
            "-f", "image2pipe",
            "-c:v", "png",
            "-i", "-",
        };
        List<string> formats = new() { "yuv420p" };

        if (frameInterpolationFps.HasValue)
        {
            formats.Add($"minterpolate='fps={frameInterpolationFps.Value.ToString(CultureInfo.InvariantCulture)}'");
        }
        else
        {
            args.AddRange([
                "-r",
                strFramerate,
            ]);
        }

        args.AddRange([
            "-vf", $"format={string.Join(',', formats)}",
            outputFilePath
        ]);


        ProcessStartInfo ffStart = new(FFMpegHelper.GetFFMpegBinaryPath(options), args)
        {
            RedirectStandardInput = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = false,
        };
        var process = Process.Start(ffStart);
        if (process == null)
            throw new Exception("Unable to start FFMpeg");
        return (process, process.StandardInput.BaseStream);
    }*/

    public static (Process ffProcess, Stream stdInStream) StartFramesToVideoPipe(
        string outputFilePath,
        double framerate,
        string imageFormat,
        FFMpegOptions? options = null,
        double? frameInterpolationFps = null)
    {
        var strFramerate = framerate.ToString(CultureInfo.InvariantCulture);
        var fmt = (imageFormat).ToLowerInvariant();
        // Map image format to appropriate decoder name
        var decoder = fmt switch
        {
            "png" => "png",
            "jpg" => "mjpeg",
            "jpeg" => "mjpeg",
            _ => "png"
        };
        List<string> args = new List<string>()
        {
            "-y",
            "-framerate",
            strFramerate,
            "-f", "image2pipe",
            "-c:v", decoder,
            "-i", "-",
        };
        List<string> formats = new() { "yuv420p" };

        if (frameInterpolationFps.HasValue)
        {
            formats.Add($"minterpolate='fps={frameInterpolationFps.Value.ToString(CultureInfo.InvariantCulture)}'");
        }
        else
        {
            args.AddRange([
                "-r",
                strFramerate,
            ]);
        }

        args.AddRange([
            "-vf", $"format={string.Join(',', formats)},pad=ceil(iw/2)*2:ceil(ih/2)*2",
            outputFilePath
        ]);


        ProcessStartInfo ffStart = new(FFMpegHelper.GetFFMpegBinaryPath(options), args)
        {
            RedirectStandardInput = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = false,
        };
        var process = Process.Start(ffStart);
        if (process == null)
            throw new Exception("Unable to start FFMpeg");
        return (process, process.StandardInput.BaseStream);
    }

    /*public static async Task<bool> CopyWithAspectRatio(
        string inputFilePath,
        string outputFilePath,
        string aspectRatioValue,
        FFMpegOptions? options = null,
        CancellationToken cancellationToken = default,
        Action<string>? progressAction = null)
    {
        var cmd = FFMpegHelper.GetFFMpeg(options ?? FFMpegOptions.Global)
            .WithArguments([
                "-i",
                inputFilePath,
                "-aspect",
                aspectRatioValue,
                "-progress",
                "pipe:1",
                outputFilePath,
            ])
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => progressAction?.Invoke(line)));
        var result = await cmd.ExecuteBufferedAsync(cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }*/
}