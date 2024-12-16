using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using CliWrap;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using UpscaylVideo.FFMpegWrap;
using UpscaylVideo.FFMpegWrap.Models.Probe;
using UpscaylVideo.Helpers;
using UpscaylVideo.Models;
using Path = System.IO.Path;

namespace UpscaylVideo.ViewModels;

public partial class JobPageViewModel : PageBase, IDisposable
{
    private const string TimespanFormat = @"d\.hh\:mm\:ss";
    private readonly CancellationTokenSource _tokenSource = new();
    private CancellationTokenSource _pauseTokenSource;
    private readonly Stopwatch _elapsedStopwatch = new();
    private readonly Stopwatch _upscaleTimeStopwatch = new();

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private int _progressOverall;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private BindingList<string> _log = new();
    [ObservableProperty] private int _totalFrames;
    [ObservableProperty] private int _completedFrames;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(DspElapsedTime))]
    private TimeSpan _elapsedTime = TimeSpan.Zero;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(DspEta))]
    private TimeSpan _eta = TimeSpan.Zero;

    [ObservableProperty] private DateTime _expectedCompletionTime;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PauseButtonText)), NotifyPropertyChangedFor(nameof(PauseButtonIcon))] 
    private bool _isPaused;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    private bool _canPause;

    /// <inheritdoc/>
    public JobPageViewModel(UpscaleJob job)
    {
        Job = job;
        base.ToolStripButtonDefinitions =
        [
            new(ToolStripButtonLocations.Left, MaterialIconKind.Stop, "Cancel", CancelCommand)
            {
                ShowText = true
            }
        ];
        _pauseTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_tokenSource.Token);
    }

    public string DspElapsedTime => ElapsedTime.ToString(TimespanFormat);

    public string DspEta => Eta.ToString(TimespanFormat);
    
    public string PauseButtonText => IsPaused ? "Resume" : "Pause";

    public MaterialIconKind PauseButtonIcon => IsPaused ? MaterialIconKind.Resume : MaterialIconKind.Pause;
    
    public UpscaleJob Job { get; }

    public async Task RunAsync()
    {
        if (!File.Exists(Job.VideoPath) || Job.VideoStream is null || Job.WorkingFolder is null)
            return;
        if (!Directory.Exists(AppConfiguration.Instance.UpscaylPath))
            return;

        string upscaylBin = Path.Combine(AppConfiguration.Instance.UpscaylPath, "resources", "bin",
            OperatingSystem.IsWindows() ? "upscayl-bin.exe" : "upscayl-bin");

        if (!File.Exists(upscaylBin))
            return;

        string modelsPath = Path.Combine(AppConfiguration.Instance.UpscaylPath, "resources", "models");
        if (!Directory.Exists(modelsPath))
            return;

        var srcVideoFolder = Path.GetDirectoryName(Job.VideoPath);
        if (srcVideoFolder == null)
            return; // TODO: Add some kind of messaging indicating why it exited early.

        IsRunning = true;
        Status = "Starting...";
        _elapsedStopwatch.Start();
        var stream = Job.VideoStream;
        TotalFrames = Job.VideoStream.CalcNbFrames;

        Task progressUpdateTask = Task.Run(() => UpdateProgress(_tokenSource.Token));
        try
        {
            Directory.CreateDirectory(Job.WorkingFolder);

            var framesFolder = Path.Combine(Job.WorkingFolder, "Frames");
            var upscaleWorking = Path.Combine(Job.WorkingFolder, "Upscale");
            var upscaledFramesFolder = Path.Combine(Job.WorkingFolder, "Frames_Upscaled");

            var extension = Path.GetExtension(Job.VideoPath);

            Directory.CreateDirectory(framesFolder);
            Directory.CreateDirectory(upscaleWorking);
            Directory.CreateDirectory(upscaledFramesFolder);

            Stopwatch processingStopwatch = new();

            processingStopwatch.Restart();

            var audioFile = Path.Combine(Job.WorkingFolder, $"Audio.{extension}");
            var metadataFile = Path.Combine(Job.WorkingFolder, $"Metadata.ffmeta");
            Status = "Extracting audio...";
            if (await FFMpeg.CopyStreams(Job.VideoPath,
                    audioFile,
                    Job.VideoDetails.Streams.Where(d => d.CodecType != "video"),
                    cancellationToken: _tokenSource.Token) == false)
            {
                return;
            }

            Status = "Extracting chapter metadata...";
            if (await FFMpeg.ExtractFFMetadata(Job.VideoPath,
                    metadataFile, cancellationToken: _tokenSource.Token) == false)
            {
                return;
            }

            Status = "Clearing frames...";
            await ClearFoldersAsync(framesFolder);
            await ClearFoldersAsync(upscaleWorking);
            await ClearFoldersAsync(upscaledFramesFolder);


            Status = "Extracting frames...";
            if (await FFMpeg.ExtractFrames(Job.VideoPath,
                        framesFolder,
                        stream.CalcRFrameRate,
                        _tokenSource.Token,
                        progressAction: ProcessFFMpegProgress) == false)
            {
                return;
            }

            var frameFiles = await Task.Run(() => Directory.GetFiles(framesFolder, "*.png"));
            Array.Sort(frameFiles);
            Progress = 0;
            TotalFrames = frameFiles.Length;

            Status = "Upscaling frames...";


            int chunkSkip = 0;
            int chunkSize = Job.UpscaleFrameChunkSize == 0 ? TotalFrames : Job.UpscaleFrameChunkSize;
            var frameChunk = frameFiles.Skip(chunkSkip).Take(chunkSize);
            _upscaleTimeStopwatch.Start();
            bool shouldResume = false;

            CanPause = true;
            while (frameChunk.Any() && _tokenSource.Token.IsCancellationRequested == false)
            {
                foreach (var src in frameChunk)
                {
                    var dest = Path.Combine(upscaleWorking, Path.GetFileName(src));
                    File.Move(src, dest);
                }

                do
                {
                    if (await RunUpscayl(upscaylBin, modelsPath, upscaleWorking, upscaledFramesFolder, _pauseTokenSource.Token) == false)
                    {
                        return;
                    }

                    if (IsPaused && _tokenSource.Token.IsCancellationRequested == false)
                    {
                        Status = "Paused";
                        ClearCompletedUpscaled(upscaleWorking, upscaledFramesFolder);
                        await WaitUntilUnpaused(_tokenSource.Token);
                        _pauseTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_tokenSource.Token);
                        shouldResume = true;
                        Status = "Upscaling frames...";
                    }
                    
                } while (shouldResume && _tokenSource.Token.IsCancellationRequested == false);


                await ClearFoldersAsync(upscaleWorking);

                chunkSkip += chunkSize;
                frameChunk = frameFiles.Skip(chunkSkip).Take(chunkSize);
            }
            CanPause = false;

            _upscaleTimeStopwatch.Stop();
            if (_tokenSource.IsCancellationRequested)
                return;
            
            CompletedFrames = 0;
            frameFiles = await Task.Run(() => Directory.GetFiles(upscaledFramesFolder, "*.png"));
            Array.Sort(frameFiles);

            string upscaledVideoPath = Path.Combine(Job.WorkingFolder, $"{Path.GetFileNameWithoutExtension(Job.VideoPath)}-video.{extension}");
            Progress = 0;
            Status = $"Generating video file...";
            if (await FFMpeg.CreateVideoFromFrames(frameFiles, upscaledVideoPath, stream.CalcRFrameRate,
                    cancellationToken: _tokenSource.Token, progressAction: ProcessFFMpegProgress) == false)
            {
                return;
            }

            Status = "Generating final video file...";
            string final = Path.Combine(srcVideoFolder, $"{Path.GetFileNameWithoutExtension(Job.VideoPath)} - upscaled.{extension}");

            await FFMpeg.MergeFiles([upscaledVideoPath, audioFile, metadataFile], final, cancellationToken: _tokenSource.Token);


            processingStopwatch.Stop();

            CompletedFrames += frameFiles.Length;

            Directory.Delete(Job.WorkingFolder, true);
        }
        catch (OperationCanceledException)
        {
            // Silently catch cancellation
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }
        finally
        {
            IsRunning = false;
            _tokenSource.Cancel();
            _elapsedStopwatch.Stop();
        }
    }

    private void ClearFolders(string folder)
    {
        foreach (var file in Directory.GetFiles(folder))
        {
            File.Delete(file);
        }
    }

    private void ClearCompletedUpscaled(string workingFolder, string upscaledVideoPath)
    {
        foreach (string workingFile in Directory.GetFiles(workingFolder))
        {
            if (File.Exists(Path.Combine(upscaledVideoPath, Path.GetFileName(workingFile))))
                File.Delete(workingFile);
        }
    }

    private Task ClearFoldersAsync(string folder) => Task.Run(() => ClearFolders(folder));

    private async Task UpdateProgress(CancellationToken token)
    {
        if (TotalFrames == 0)
            return;
        var fpsStopwatch = new Stopwatch();
        int previousFrameCount = 0;
        TimeSpan timePerFrame = TimeSpan.Zero;
        do
        {
            var frameCount = CompletedFrames;
            ElapsedTime = _elapsedStopwatch.Elapsed;
            var progress = (int)((decimal)frameCount / TotalFrames * 100);
            ProgressOverall = progress > 100 ? 100 : progress;

            if (_upscaleTimeStopwatch.IsRunning && frameCount > 0)
            {
                var diff = frameCount - previousFrameCount;
                if (diff > 0)
                {
                    timePerFrame = fpsStopwatch.Elapsed / diff;
                }

                var remaining = TotalFrames - frameCount;
                Eta = TotalFrames * timePerFrame - _upscaleTimeStopwatch.Elapsed;
            }

            if (_upscaleTimeStopwatch.IsRunning)
            {
                previousFrameCount = frameCount;
                fpsStopwatch.Restart();
            }

            /*
            if (Eta > TimeSpan.Zero)
            {
                ExpectedCompletionTime = DateTime.Now.Add(Eta);
            }*/

            await TaskHelpers.Wait(1000, token).ConfigureAwait(false);
        } while (!token.IsCancellationRequested && IsRunning);
    }

    private async Task WaitUntilUnpaused(CancellationToken token)
    {
        while (IsPaused && !token.IsCancellationRequested)
        {
            await TaskHelpers.Wait(1000, token).ConfigureAwait(false);
        }
    }

    private void ProcessFFMpegProgress(string line)
    {
        var span = line.AsSpan();
        var equalPos = span.IndexOf('=');
        if (equalPos <= 0)
            return;
        var key = span.Slice(0, equalPos);
        var value = span.Slice(equalPos + 1);

        switch (key)
        {
            case "frame":
                var framesCount = double.Parse(value);
                Progress = framesCount > 0 && TotalFrames > 0 ? (int)(framesCount / TotalFrames * 100) % 101 : 0;
                break;
            default:
                break;
        }
    }

    private async Task<bool> RunUpscayl(string upscaylBinPath, string modelsPath, string framesFolder, string upscaledFolder,
        CancellationToken cancellationToken)
    {
        try
        {
            var cmd = Cli.Wrap(upscaylBinPath)
                .WithArguments([
                    "-i",
                    framesFolder,
                    "-o",
                    upscaledFolder,
                    "-s",
                    Job.SelectedScale.ToString(),
                    "-m",
                    modelsPath,
                    "-n",
                    Job.SelectedModel?.Name ?? string.Empty,
                ])
                .WithValidation(CommandResultValidation.None)
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                {
                    if (line.EndsWith("Successfully!"))
                    {
                        CompletedFrames++;
                    }

                    var match = GlobalRegex.UpscaylPercent().Match(line);
                    if (!match.Success)
                    {
                        Console.WriteLine(line);
                        return;
                    }

                    if (float.TryParse(match.Groups[1].Value, out var value))
                        Progress = (int)Math.Round(value);
                }));
            var result = await cmd.ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
            return result.ExitCode == 0;
        }
        catch (OperationCanceledException c)
        {
            return IsPaused;
        }
        catch (Exception e)
        {
            throw;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _tokenSource.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        IsPaused = !IsPaused;
        if (IsPaused)
        {
            _pauseTokenSource.Cancel();
        }
    }

    public void Dispose()
    {
    }
}