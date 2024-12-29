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
using Avalonia.Interactivity;
using CliWrap;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using DynamicData.Binding;
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
    private readonly Stopwatch _upscaleFrameStopwatch = new();
    private readonly Stopwatch _upscaleRuntimeStopwatch = new();
    private AverageProvider<long> _averageProvider = new();
    

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private int _progressOverall;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private BindingList<string> _log = new();
    [ObservableProperty] private long _totalFrames;
    [ObservableProperty] private int _completedFrames;
    [ObservableProperty] private TimeSpan? _avgFrameRate;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DspElapsedTime))] private TimeSpan _elapsedTime = TimeSpan.Zero;
    [ObservableProperty] private string _dialogMessage;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DspEta))]
    private TimeSpan? _eta;

    [ObservableProperty] private bool _dialogShown;

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
        this.WhenPropertyChanged(p => p.DialogShown, false)
            .Subscribe(p =>
            {
                if (p.Value == false)
                {
                    GoToMain();
                }
            });
    }

    public string DspElapsedTime => ElapsedTime.ToString(TimespanFormat);

    public string DspEta => Eta.HasValue ? Eta.Value.ToString(TimespanFormat) : "Calculating";
    
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
        TimeSpan duration = Job.VideoDetails.GetDuration();
        TotalFrames = (long)Math.Floor(duration.TotalSeconds * Job.VideoStream.CalcRFrameRate);
        var reportedNumberFrames = Job.VideoStream.CalcNbFrames;
        Stream? pngStream = null;
        //Stream? outVideoStream = null;
        Process? inputProcess = null;
        //Process? outputProcess = null;
        

        Task progressUpdateTask = Task.Run(() => UpdateProgress(_tokenSource.Token));
        try
        {
            Directory.CreateDirectory(Job.WorkingFolder);

            var framesFolder = Path.Combine(Job.WorkingFolder, "Frames");
            var upscaleOutput = Path.Combine(Job.WorkingFolder, "Upscale");
            

            var extension = Path.GetExtension(Job.VideoPath);

            Directory.CreateDirectory(framesFolder);
            Directory.CreateDirectory(upscaleOutput);

           

            var audioFile = Path.Combine(Job.WorkingFolder, $"Audio.{extension}");
            var metadataFile = Path.Combine(Job.WorkingFolder, $"Metadata.ffmeta");
            Status = "Extracting audio...";
            if (await FFMpeg.CopyStreams(Job.VideoPath,
                    audioFile,
                    Job.VideoDetails.Streams.Where(d => d.CodecType != "video" && d.CodecName != "dvd_subtitle" && d.CodecName != "bin_data"),
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

            string upscaledVideoPath = Path.Combine(Job.WorkingFolder, $"{Path.GetFileNameWithoutExtension(Job.VideoPath)}-video{extension}");
            (inputProcess, pngStream) = FFMpeg.StartPngPipe(Job.VideoPath, Job.VideoStream.CalcRFrameRate);
            using var pngVideo = new PngVideoHelper(upscaledVideoPath, Job.VideoStream.CalcRFrameRate, _tokenSource.Token, Job.SelectedInterpolatedFps.FrameRate);
            
            pngVideo.Start();

            CanPause = true;
            _upscaleRuntimeStopwatch.Restart();
            long outFrameNumber = 0;
            while (_tokenSource.Token.IsCancellationRequested == false && pngVideo.IsRunning)
            {
                Status = "Clearing frames...";
                await ClearFoldersAsync(framesFolder);
                string upscaleChunkFolder = Path.Combine(upscaleOutput, Guid.NewGuid().ToString());
                Directory.CreateDirectory(upscaleChunkFolder);
                var shouldResume = false; var hasNewFrames = false;
                for (int i = 0; i < Job.UpscaleFrameChunkSize; i++)
                {
                    using var frameStream = await pngStream.ReadNextPngAsync();
                    hasNewFrames |= frameStream.Length > 0;
                    if (frameStream.Length == 0)
                        break;
                    outFrameNumber++;
                    await using var frameFileStream = File.Create(Path.Combine(framesFolder, $"{outFrameNumber:00000000}.png"));
                    await frameStream.CopyToAsync(frameFileStream, _tokenSource.Token);
                    
                }
                if (!hasNewFrames)
                    break;

                do
                {
                    Status = "Upscaling frames...";
                    if (await RunUpscayl(upscaylBin, modelsPath, framesFolder, upscaleChunkFolder, Job.GpuNumber, _pauseTokenSource.Token) == false)
                    {
                        return;
                    }

                    if (IsPaused && _tokenSource.Token.IsCancellationRequested == false)
                    {
                        Status = "Paused";
                        ClearCompletedUpscaled(framesFolder, upscaleOutput);
                        await WaitUntilUnpaused(_tokenSource.Token);
                        _pauseTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_tokenSource.Token);
                        shouldResume = true;
                        
                    }
                    
                } while (shouldResume && _tokenSource.Token.IsCancellationRequested == false);

                _tokenSource.Token.ThrowIfCancellationRequested();
                
                pngVideo.EnqueueFramePath(upscaleChunkFolder);
              
            }
            CanPause = false;
            _upscaleRuntimeStopwatch.Stop();

            Status = "Finishing video generation...";
            await pngVideo.CompleteAsync();
            
            if (_tokenSource.IsCancellationRequested)
                return;
            
            
            Status = "Generating final video file...";
            string final = Path.Combine(srcVideoFolder, $"{Path.GetFileNameWithoutExtension(Job.VideoPath)} - upscaled{extension}");

            await FFMpeg.MergeFiles([upscaledVideoPath, audioFile, metadataFile], final, cancellationToken: _tokenSource.Token);

            Directory.Delete(Job.WorkingFolder, true);
            DialogMessage = "Upscale Completed!";
        }
        catch (OperationCanceledException)
        {
            // Silently catch cancellation
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            DialogMessage = "An error occured while processing!";
        }
        finally
        {
            bool wasCancelled = _tokenSource.IsCancellationRequested;
            IsRunning = false;
            _tokenSource.Cancel();
            _elapsedStopwatch.Stop();
            _upscaleRuntimeStopwatch.Reset();
            pngStream?.Dispose();
            
            if (string.IsNullOrEmpty(DialogMessage))
                DialogMessage = "Processing did not complete successfully!";

            if (inputProcess != null && !inputProcess.HasExited)
            {
                inputProcess.Kill();
            }
            _averageProvider.Reset();

            DialogShown = wasCancelled == false;

            if (wasCancelled)
            {
                GoToMain();
            }
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
        var timeSinceLastAverage = new Stopwatch();
        do
        {
            var frameCount = CompletedFrames;
            ElapsedTime = _elapsedStopwatch.Elapsed;
            var progress = (int)((decimal)frameCount / TotalFrames * 100);
            ProgressOverall = progress > 100 ? 100 : progress;

            if (_upscaleFrameStopwatch.IsRunning && _averageProvider.AverageReady && frameCount > 0)
            {
                AvgFrameRate = TimeSpan.FromTicks(_averageProvider.GetAverage(true));
                timeSinceLastAverage.Restart();
            }

            if (_upscaleFrameStopwatch.IsRunning && AvgFrameRate.HasValue)
            {
                var remaining = TotalFrames - frameCount;
                Eta = (remaining * AvgFrameRate.Value) - timeSinceLastAverage.Elapsed;
            }

            /*
            if (Eta > TimeSpan.Zero)
            {
                ExpectedCompletionTime = DateTime.Now.Add(Eta);
            }*/

            await TaskHelpers.Wait(1000, token).ConfigureAwait(false);
        } while (!token.IsCancellationRequested && IsRunning);
        timeSinceLastAverage.Stop();
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

    private async Task<bool> RunUpscayl(string upscaylBinPath, string modelsPath, string framesFolder, string upscaledFolder, int[] gpuNumbers,
        CancellationToken cancellationToken)
    {
        _upscaleFrameStopwatch.Restart();
        try
        {
            var args = new List<string>([
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
            ]);

            if (gpuNumbers.Any())
            {
                args.AddRange(["-g", string.Join(',', gpuNumbers)]);
            }
            
            
            var cmd = Cli.Wrap(upscaylBinPath)
                .WithArguments(args)
                .WithValidation(CommandResultValidation.None)
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                {
                    if (line.EndsWith("Successfully!"))
                    {
                        CompletedFrames++;
                        _averageProvider.Push(_upscaleFrameStopwatch.Elapsed.Ticks);
                        _upscaleFrameStopwatch.Restart();
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
        finally
        {
            _upscaleFrameStopwatch.Stop();
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

    [RelayCommand]
    private void GoToMain()
    {
        PageManager.Instance.SetPage(typeof(MainPageViewModel));
    }

    public void Dispose()
    {
    }
}