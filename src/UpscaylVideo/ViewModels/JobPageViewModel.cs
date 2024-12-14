using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
    private const string upscaledSuccessMsg = "?? Upscayled Successfully!";
    CancellationTokenSource _tokenSource = new();
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private int _progressOverall;
    [ObservableProperty] private float _progress;
    [ObservableProperty] private BindingList<string> _log = new();
    [ObservableProperty] private int _totalFrames;
    [ObservableProperty] private int _completedFrames;

    /// <inheritdoc/>
    public JobPageViewModel(UpscaleJob job)
    {
        Job = job;
        base.ToolStripButtonDefinitions =
        [
            new(ToolStripButtonLocations.Left, MaterialIconKind.Stop, "Cancel", CancelCommand)
            {
                ShowText = true
            },
        ];
    }


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

        IsRunning = true;
        Status = "Starting...";

        var stream = Job.VideoStream;
        TotalFrames = stream.NbFrames;
        Task progressUpdateTask = Task.Run(() => UpdateProgress(_tokenSource.Token));
        try
        {
            Directory.CreateDirectory(Job.WorkingFolder);

            var framesFolder = Path.Combine(Job.WorkingFolder, "Frames");
            var upscaledFramesFolder = Path.Combine(Job.WorkingFolder, "Frames_Upscaled");
            var clipsFolder = Path.Combine(Job.WorkingFolder, "Clips");

            Directory.CreateDirectory(framesFolder);
            Directory.CreateDirectory(upscaledFramesFolder);
            Directory.CreateDirectory(clipsFolder);
            await Task.Run(() => ClearFolders(clipsFolder));
            TimeSpan durationRemaining = stream.Duration;
            TimeSpan clipLength = Job.ClipSeconds <= 0 ? durationRemaining : TimeSpan.FromSeconds(Job.ClipSeconds);
            TimeSpan clipStart = TimeSpan.Zero;
            TimeSpan clipEnd = TimeSpan.Zero;
            int clipNumber = 1;
            var frameFilesx = await Task.Run(() => Directory.GetFiles(upscaledFramesFolder, "*.png"));

            while (durationRemaining > TimeSpan.Zero && !_tokenSource.IsCancellationRequested)
            {
                Status = "Clearing frames...";
                await Task.Run(() => ClearFolders(framesFolder)).ConfigureAwait(false);
                await Task.Run(() => ClearFolders(upscaledFramesFolder)).ConfigureAwait(false);
                clipEnd = clipStart + clipLength;

                Status = "Extracting next batch of frames...";
                if (await FFMpeg.ExtractFrames(Job.VideoPath, framesFolder, clipStart, clipEnd, stream.RFrameRate, null, _tokenSource.Token)
                        .ConfigureAwait(false) == false)
                {
                    return;
                }

                Status = "Upscaling frames...";
                if (await RunUpscayl(upscaylBin, modelsPath, framesFolder, upscaledFramesFolder, _tokenSource.Token).ConfigureAwait(false) == false)
                {
                    return;
                }

                var frameFiles = await Task.Run(() => Directory.GetFiles(upscaledFramesFolder, "*.png"));

                if (await FFMpeg.CreateVideoFromFrames(frameFiles, Path.Combine(clipsFolder, $"{clipNumber:00000000}.mkv"), stream.RFrameRate,
                        cancellationToken: _tokenSource.Token).ConfigureAwait(false) == false)
                {
                    return;
                }

                CompletedFrames += frameFiles.Length;

                durationRemaining -= clipLength;
                clipStart += clipLength;
                clipNumber++;
            }

            await Task.Run(() => ClearFolders(framesFolder)).ConfigureAwait(false);
            await Task.Run(() => ClearFolders(upscaledFramesFolder)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }
        finally
        {
            IsRunning = false;
            _tokenSource.Cancel();
        }
    }

    private void ClearFolders(string folder)
    {
        foreach (var file in Directory.GetFiles(folder))
        {
            File.Delete(file);
        }
    }

    private async Task UpdateProgress(CancellationToken token)
    {
        if (TotalFrames == 0)
            return;
        do
        {
            var progress = (int)((decimal)CompletedFrames / TotalFrames * 100);
            ProgressOverall = progress > 100 ? 100 : progress;

            await TaskHelpers.Wait(1000, token).ConfigureAwait(false);
        } while (!token.IsCancellationRequested && IsRunning);
    }

    private async Task<bool> RunUpscayl(string upscaylBinPath, string modelsPath, string framesFolder, string upscaledFolder,
        CancellationToken cancellationToken)
    {
        var cmd = CliWrap.Cli.Wrap(upscaylBinPath)
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
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
            {
                var match = GlobalRegex.UpscaylPercent().Match(line);
                if (!match.Success)
                    return;
                if (float.TryParse(match.Groups[1].Value, out var value))
                    Progress = value;
            }));
        var result = await cmd.ExecuteAsync();
        return result.ExitCode == 0;
    }

    [RelayCommand]
    private void Cancel()
    {
        _tokenSource.Cancel();
    }

    public void Dispose()
    {
    }
}