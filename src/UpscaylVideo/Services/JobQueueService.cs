using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UpscaylVideo.Helpers;
using UpscaylVideo.FFMpegWrap;
using CommunityToolkit.Mvvm.ComponentModel;
using UpscaylVideo.Models;
using UpscaylVideo.ViewModels;

namespace UpscaylVideo.Services;

public partial class JobQueueService : ObservableObject
{
    public static JobQueueService Instance { get; } = new();

    public ObservableCollection<UpscaleJob> JobQueue { get; } = new();
    [ObservableProperty] private bool _showProgressPanel;
    [ObservableProperty] private double _overallProgress;
    [ObservableProperty] private UpscaleJob? _currentJob;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private int _completedFrames;
    [ObservableProperty] private long _totalFrames;
    [ObservableProperty] private TimeSpan? _avgFrameRate;
    [ObservableProperty] private TimeSpan? _eta;
    [ObservableProperty] private TimeSpan _elapsedTime = TimeSpan.Zero;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _processingTask;

    public void EnqueueJob(UpscaleJob job)
    {
        JobQueue.Add(job);
        ShowProgressPanel = true;
        // Start processing if not already running
        if (!IsProcessing)
            _processingTask = Task.Run(ProcessQueueIfNeeded);
    }

    /// <summary>
    /// Public method to start processing the queue if not already running and jobs exist.
    /// </summary>
    public void StartQueueIfStopped()
    {
        if (!IsProcessing && JobQueue.Count > 0)
        {
            _ = ProcessQueueIfNeeded();
        }
    }

    public void RemoveJob(UpscaleJob job)
    {
        // Prevent removing the currently running job
        if (job == CurrentJob)
            return;
        JobQueue.Remove(job);
    }

    public void ClearQueue()
    {
        // Only clear jobs that are not currently running
        for (int i = JobQueue.Count - 1; i >= 0; i--)
        {
            if (JobQueue[i] != CurrentJob)
                JobQueue.RemoveAt(i);
        }
        if (JobQueue.Count == 1 && JobQueue[0] == CurrentJob)
        {
            // Only current job remains
            ShowProgressPanel = true;
        }
        else if (JobQueue.Count == 0)
        {
            ShowProgressPanel = false;
            OverallProgress = 0;
        }
    }

    public void CancelCurrentJob()
    {
        _cancellationTokenSource?.Cancel();
    }

    private async Task ProcessQueueIfNeeded()
    {
        if (IsProcessing || JobQueue.Count == 0)
            return;
        IsProcessing = true;
        _cancellationTokenSource = new CancellationTokenSource();
        try
        {
            int index = 0;
            while (index < JobQueue.Count)
            {
                var job = JobQueue[index];
                CurrentJob = job;
                await RunJobAsync(job, _cancellationTokenSource.Token);
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    // Do not remove current job, just stop processing
                    break;
                }
                // Only remove if not cancelled
                index++;
            }
        }
        finally
        {
            IsProcessing = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            if (JobQueue.Count == 0 || (JobQueue.Count == 1 && JobQueue[0] == CurrentJob))
                ShowProgressPanel = false;
            OverallProgress = 0;
        }
    }

    private async Task RunJobAsync(UpscaleJob job, CancellationToken cancellationToken)
    {
        // Migrated execution logic from JobPageViewModel
        OverallProgress = 0;
        long totalFrames = 0;
        int completedFrames = 0;
        var averageProvider = new AverageProvider<long>();
        var elapsedStopwatch = new System.Diagnostics.Stopwatch();
        var upscaleFrameStopwatch = new System.Diagnostics.Stopwatch();
        var upscaleRuntimeStopwatch = new System.Diagnostics.Stopwatch();
        string status = string.Empty;
        System.Diagnostics.Process? inputProcess = null;
        try
        {
            if (!System.IO.File.Exists(job.VideoPath) || job.VideoStream is null || job.WorkingFolder is null)
                return;
            if (!System.IO.Directory.Exists(AppConfiguration.Instance.UpscaylPath))
                return;

            string upscaylBin = System.IO.Path.Combine(AppConfiguration.Instance.UpscaylPath, "resources", "bin",
                OperatingSystem.IsWindows() ? "upscayl-bin.exe" : "upscayl-bin");
            if (!System.IO.File.Exists(upscaylBin))
                return;

            string modelsPath = System.IO.Path.Combine(AppConfiguration.Instance.UpscaylPath, "resources", "models");
            if (!System.IO.Directory.Exists(modelsPath))
                return;

            var srcVideoFolder = System.IO.Path.GetDirectoryName(job.VideoPath);
            if (srcVideoFolder == null)
                return;

            elapsedStopwatch.Start();
            Task? progressUpdateTask = null;
            progressUpdateTask = Task.Run(() => UpdateProgress(cancellationToken, elapsedStopwatch), cancellationToken);
            var stream = job.VideoStream;
            var duration = job.VideoDetails.GetDuration();
            TotalFrames = (long)Math.Floor(duration.TotalSeconds * job.VideoStream.CalcAvgFrameRate);
            var reportedNumberFrames = job.VideoStream.CalcNbFrames;
            System.IO.Stream? pngStream = null;

            await Task.Run(() => System.IO.Directory.CreateDirectory(job.WorkingFolder), cancellationToken);
            var framesFolder = System.IO.Path.Combine(job.WorkingFolder, "Frames");
            var upscaleOutput = System.IO.Path.Combine(job.WorkingFolder, "Upscale");
            var extension = System.IO.Path.GetExtension(job.VideoPath);
            System.IO.Directory.CreateDirectory(framesFolder);
            System.IO.Directory.CreateDirectory(upscaleOutput);
            if (string.IsNullOrWhiteSpace(job.OutputFilePath))
                return;
            string final = job.OutputFilePath;
            var audioFile = System.IO.Path.Combine(job.WorkingFolder, $"Audio{extension}");
            var metadataFile = System.IO.Path.Combine(job.WorkingFolder, $"Metadata.ffmeta");
            // Extract audio
            await FFMpeg.CopyStreams(job.VideoPath, audioFile, job.VideoDetails.Streams.Where(d => d.CodecType != "video" && d.CodecName != "dvd_subtitle" && d.CodecName != "bin_data"), cancellationToken: cancellationToken);
            // Extract chapter metadata
            await FFMpeg.ExtractFFMetadata(job.VideoPath, metadataFile, cancellationToken: cancellationToken);
            string upscaledVideoPath = System.IO.Path.Combine(job.WorkingFolder, $"{System.IO.Path.GetFileNameWithoutExtension(job.VideoPath)}-video{extension}");
            (inputProcess, pngStream) = FFMpeg.StartPngPipe(job.VideoPath, job.VideoStream.CalcAvgFrameRate);
            using var pngVideo = new PngVideoHelper(upscaledVideoPath, job.VideoStream.CalcAvgFrameRate, cancellationToken, job.SelectedInterpolatedFps.FrameRate);
            await pngVideo.StartAsync();
            upscaleRuntimeStopwatch.Restart();
            long outFrameNumber = 0;
            while (!cancellationToken.IsCancellationRequested && pngVideo.IsRunning)
            {
                await Task.Run(() => ClearFolders(framesFolder));
                string upscaleChunkFolder = System.IO.Path.Combine(upscaleOutput, Guid.NewGuid().ToString());
                System.IO.Directory.CreateDirectory(upscaleChunkFolder);
                var shouldResume = false; var hasNewFrames = false;
                for (int i = 0; i < job.UpscaleFrameChunkSize; i++)
                {
                    using var frameStream = await pngStream.ReadNextPngAsync();
                    hasNewFrames |= frameStream.Length > 0;
                    if (frameStream.Length == 0)
                        break;
                    outFrameNumber++;
                    await using var frameFileStream = System.IO.File.Create(System.IO.Path.Combine(framesFolder, $"{outFrameNumber:00000000}.png"));
                    await frameStream.CopyToAsync(frameFileStream, cancellationToken);
                }
                if (!hasNewFrames)
                    break;
                do
                {
                    shouldResume = false;
                    if (await RunUpscayl(job, upscaylBin, modelsPath, framesFolder, upscaleChunkFolder, job.GpuNumber, cancellationToken) == false)
                    {
                        return;
                    }
                } while (shouldResume && !cancellationToken.IsCancellationRequested);
                cancellationToken.ThrowIfCancellationRequested();
                pngVideo.EnqueueFramePath(upscaleChunkFolder);
            }
            upscaleRuntimeStopwatch.Stop();
            await pngVideo.CompleteAsync();
            if (cancellationToken.IsCancellationRequested)
                return;
            await FFMpeg.MergeFiles(upscaledVideoPath, audioFile, metadataFile, final, cancellationToken: cancellationToken);
            if (job.DeleteWorkingFolderWhenCompleted)
            {
                System.IO.Directory.Delete(job.WorkingFolder, true);
            }
        }
        catch (OperationCanceledException)
        {
            // Silently catch cancellation
        }
        catch (Exception)
        {
            // Silently catch all other exceptions
        }
        finally
        {
            elapsedStopwatch.Stop();
            // Ensure ffmpeg process is killed and disposed if still running
            if (inputProcess != null)
            {
                try
                {
                    if (!inputProcess.HasExited)
                        inputProcess.Kill(true);
                }
                catch { /* ignore exceptions on kill */ }
                try { inputProcess.Dispose(); } catch { }
            }
        }
    }

    private void ClearFolders(string folder)
    {
        foreach (var file in System.IO.Directory.GetFiles(folder))
        {
            System.IO.File.Delete(file);
        }
    }

    private async Task<bool> RunUpscayl(UpscaleJob job, string upscaylBinPath, string modelsPath, string framesFolder, string upscaledFolder, int[] gpuNumbers,
        CancellationToken cancellationToken)
    {
        var upscaleFrameStopwatch = new System.Diagnostics.Stopwatch();
        upscaleFrameStopwatch.Restart();
        try
        {
            var args = new System.Collections.Generic.List<string>([
                "-i",
                framesFolder,
                "-o",
                upscaledFolder,
                "-s",
                job.SelectedScale.ToString(),
                "-m",
                modelsPath,
                "-n",
                job.SelectedModel?.Name ?? string.Empty,
            ]);
            if (gpuNumbers != null && gpuNumbers.Length > 0)
            {
                args.AddRange(["-g", string.Join(',', gpuNumbers)]);
            }
            var averageProvider = new AverageProvider<long>();
            // Use the observable property directly for completed frames
            var cmd = CliWrap.Cli.Wrap(upscaylBinPath)
                .WithArguments(args)
                .WithValidation(CliWrap.CommandResultValidation.None)
                .WithStandardErrorPipe(CliWrap.PipeTarget.ToDelegate(line =>
                {
                    if (line.EndsWith("Successfully!"))
                    {
                        CompletedFrames++;
                        averageProvider.Push(upscaleFrameStopwatch.Elapsed.Ticks);
                        upscaleFrameStopwatch.Restart();
                        // Update average frame rate and ETA
                        if (averageProvider.AverageReady && TotalFrames > 0)
                        {
                            AvgFrameRate = TimeSpan.FromTicks(averageProvider.GetAverage(true));
                            var remaining = TotalFrames - CompletedFrames;
                            Eta = AvgFrameRate.HasValue ? (remaining * AvgFrameRate.Value) : null;
                        }
                    }
                    var match = RegexHelper.UpscaylPercent.Match(line);
                    if (match.Success && float.TryParse(match.Groups[1].Value, out var value))
                    {
                        Progress = (int)Math.Round(value);
                    }
                }));
            var result = await cmd.ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
            return result.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            upscaleFrameStopwatch.Stop();
        }
    }

    private async Task UpdateProgress(CancellationToken token, System.Diagnostics.Stopwatch stopwatch)
    {
        while (!token.IsCancellationRequested && IsProcessing)
        {
            ElapsedTime = stopwatch.Elapsed;
            // Optionally update ETA here if needed
            await Task.Delay(500, token).ContinueWith(_ => { });
        }
    }
}
