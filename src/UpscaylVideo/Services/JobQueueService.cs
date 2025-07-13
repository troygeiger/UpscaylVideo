using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UpscaylVideo.Helpers;
using UpscaylVideo.FFMpegWrap;
using CommunityToolkit.Mvvm.ComponentModel;
using UpscaylVideo.Models;

namespace UpscaylVideo.Services;

public partial class JobQueueService : ObservableObject
{
    private const string TimespanFormat = @"d\.hh\:mm\:ss";
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
    [ObservableProperty] private string? _dspElapsedTime;
    [ObservableProperty] private string? _dspEta;
    [ObservableProperty] private string _statusMessage = string.Empty;
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

    public async Task CancelCurrentJobAsync()
    {
        _cancellationTokenSource?.Cancel();
        if (_processingTask != null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException) { /* Swallow cancellation */ }
        }

        _processingTask = null;
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
                if (job.Status != UpscaleJobStatus.Queued)
                {
                    index++;
                    continue;
                }
                CurrentJob = job;
                job.Status = UpscaleJobStatus.Running;
                await RunJobAsync(job, _cancellationTokenSource.Token);
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    job.Status = UpscaleJobStatus.Queued;
                    // Do not remove current job, just stop processing
                    break;
                }
                else
                {
                    job.Status = UpscaleJobStatus.Completed;
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
            CurrentJob = null;
        }
    }

    private async Task RunJobAsync(UpscaleJob job, CancellationToken cancellationToken)
    {
        OverallProgress = 0;
        var elapsedStopwatch = new System.Diagnostics.Stopwatch();
        var upscaleRuntimeStopwatch = new System.Diagnostics.Stopwatch();
        System.Diagnostics.Process? inputProcess = null;
        System.IO.Stream? pngStream = null;
        var jobCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task? progressUpdateTask = null;
        try
        {
            if (!System.IO.File.Exists(job.VideoPath) || job.VideoStream is null || job.WorkingFolder is null)
            {
                StatusMessage = "Input video or working folder missing.";
                return;
            }
            if (!System.IO.Directory.Exists(AppConfiguration.Instance.UpscaylPath))
            {
                StatusMessage = "Upscayl path not found.";
                return;
            }

            string upscaylBin = System.IO.Path.Combine(AppConfiguration.Instance.UpscaylPath, "resources", "bin",
                OperatingSystem.IsWindows() ? "upscayl-bin.exe" : "upscayl-bin");
            if (!System.IO.File.Exists(upscaylBin))
            {
                StatusMessage = "Upscayl binary not found.";
                return;
            }

            string modelsPath = System.IO.Path.Combine(AppConfiguration.Instance.UpscaylPath, "resources", "models");
            if (!System.IO.Directory.Exists(modelsPath))
            {
                StatusMessage = "Upscayl models folder not found.";
                return;
            }

            var srcVideoFolder = System.IO.Path.GetDirectoryName(job.VideoPath);
            if (srcVideoFolder == null)
            {
                StatusMessage = "Source video folder not found.";
                return;
            }

            elapsedStopwatch.Start();
            
            progressUpdateTask = Task.Run(() => UpdateProgress(elapsedStopwatch, jobCancellation.Token));
            var duration = job.VideoDetails.GetDuration();
            TotalFrames = (long)Math.Floor(duration.TotalSeconds * job.VideoStream.CalcAvgFrameRate);

            await Task.Run(() => System.IO.Directory.CreateDirectory(job.WorkingFolder), jobCancellation.Token);
            var framesFolder = System.IO.Path.Combine(job.WorkingFolder, "Frames");
            var upscaleOutput = System.IO.Path.Combine(job.WorkingFolder, "Upscale");
            var extension = System.IO.Path.GetExtension(job.VideoPath);
            System.IO.Directory.CreateDirectory(framesFolder);
            System.IO.Directory.CreateDirectory(upscaleOutput);
            if (string.IsNullOrWhiteSpace(job.OutputFilePath))
            {
                StatusMessage = "Output file path is empty.";
                return;
            }
            string final = job.OutputFilePath;
            var audioFile = System.IO.Path.Combine(job.WorkingFolder, $"Audio{extension}");
            var metadataFile = System.IO.Path.Combine(job.WorkingFolder, $"Metadata.ffmeta");
            // Extract audio
            StatusMessage = "Extracting audio...";
            await FFMpeg.CopyStreams(job.VideoPath, audioFile, job.VideoDetails.Streams.Where(d => d.CodecType != "video" && d.CodecName != "dvd_subtitle" && d.CodecName != "bin_data"), cancellationToken: jobCancellation.Token);
            // Extract chapter metadata
            StatusMessage = "Extracting chapter metadata...";
            await FFMpeg.ExtractFFMetadata(job.VideoPath, metadataFile, cancellationToken: jobCancellation.Token);
            string upscaledVideoPath = System.IO.Path.Combine(job.WorkingFolder, $"{System.IO.Path.GetFileNameWithoutExtension(job.VideoPath)}-video{extension}");

            (inputProcess, pngStream) = FFMpeg.StartPngPipe(job.VideoPath, job.VideoStream.CalcAvgFrameRate);
            using var pngVideo = new PngVideoHelper(upscaledVideoPath, job.VideoStream.CalcAvgFrameRate, jobCancellation.Token, job.SelectedInterpolatedFps.FrameRate);
            await pngVideo.StartAsync();
            upscaleRuntimeStopwatch.Restart();
            long outFrameNumber = 0;
            while (!cancellationToken.IsCancellationRequested && pngVideo.IsRunning)
            {
                StatusMessage = "Extracting frames...";
                await Task.Run(() => ClearFolders(framesFolder));
                string upscaleChunkFolder = System.IO.Path.Combine(upscaleOutput, Guid.NewGuid().ToString());
                System.IO.Directory.CreateDirectory(upscaleChunkFolder);
                var shouldResume = false; 
                var hasNewFrames = false;
                for (int i = 0; i < job.UpscaleFrameChunkSize; i++)
                {
                    using var frameStream = await pngStream.ReadNextPngAsync();
                    hasNewFrames |= frameStream.Length > 0;
                    if (frameStream.Length == 0)
                        break;
                    outFrameNumber++;
                    await using var frameFileStream = System.IO.File.Create(System.IO.Path.Combine(framesFolder, $"{outFrameNumber:00000000}.png"));
                    await frameStream.CopyToAsync(frameFileStream, jobCancellation.Token);
                }
                if (!hasNewFrames)
                    break;
                do
                {
                    shouldResume = false;
                    StatusMessage = "Upscaling frames...";
                    if (await RunUpscayl(job, upscaylBin, modelsPath, framesFolder, upscaleChunkFolder, job.GpuNumber, jobCancellation.Token) == false)
                    {
                        StatusMessage = "Upscaling cancelled or failed.";
                        return;
                    }
                } while (shouldResume && !cancellationToken.IsCancellationRequested);
                cancellationToken.ThrowIfCancellationRequested();
                pngVideo.EnqueueFramePath(upscaleChunkFolder);
            }
            upscaleRuntimeStopwatch.Stop();
            await pngVideo.CompleteAsync();
            if (cancellationToken.IsCancellationRequested)
            {
                StatusMessage = "Job cancelled.";
                return;
            }
            StatusMessage = "Merging video and audio...";
            await FFMpeg.MergeFiles(upscaledVideoPath, audioFile, metadataFile, final, cancellationToken: jobCancellation.Token);

            StatusMessage = "Job completed.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Job cancelled.";
            // Silently catch cancellation
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            // Silently catch all other exceptions
        }
        finally
        {
            await jobCancellation.CancelAsync();
            if (progressUpdateTask != null && !progressUpdateTask.IsCompleted)
            {
                await progressUpdateTask.ConfigureAwait(false);
            }
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
                try { inputProcess.Dispose(); } catch { /* ignore dispose exceptions */ }
            }
            pngStream?.Dispose();

            if (job.DeleteWorkingFolderWhenCompleted)
            {
                try
                {
                    StatusMessage = "Cleaning up...";
                    System.IO.Directory.Delete(job.WorkingFolder!, true);
                }
                catch (Exception cleanupEx)
                {
                    StatusMessage = $"Cleanup failed: {cleanupEx.Message}";
                }
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

    private async Task<bool> RunUpscayl(UpscaleJob job, string upscaylBinPath, string modelsPath, string framesFolder, string upscaledFolder, int[]? gpuNumbers,
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
                            DspEta = Eta?.ToString(TimespanFormat);
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

    private async Task UpdateProgress(System.Diagnostics.Stopwatch elapsedStopwatch, CancellationToken token)
    {
        while (!token.IsCancellationRequested && IsProcessing)
        {
            ElapsedTime = elapsedStopwatch.Elapsed;
            DspElapsedTime = ElapsedTime.ToString(TimespanFormat);
            
            var frameCount = CompletedFrames;
            ElapsedTime = elapsedStopwatch.Elapsed;
            var progress = (int)((decimal)frameCount / TotalFrames * 100);
            OverallProgress = progress > 100 ? 100 : progress;

            if (AvgFrameRate.HasValue)
            {
                var remaining = TotalFrames - frameCount;
                Eta = (remaining * AvgFrameRate.Value);
                DspEta = Eta.Value.ToString(TimespanFormat);
            }
            
            await TaskHelpers.Wait(1000, token).ConfigureAwait(false);
        }
    }
}
