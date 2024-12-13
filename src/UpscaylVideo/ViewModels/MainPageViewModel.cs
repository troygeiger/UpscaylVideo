using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using Material.Icons;
using UpscaylVideo.FFMpegWrap;
using UpscaylVideo.Models;

namespace UpscaylVideo.ViewModels;

public partial class MainPageViewModel : PageBase
{
    private bool isFirstLoad = true;
    [ObservableProperty] private IEnumerable<AIModelOption> _modelOptions = [];
    [ObservableProperty] private UpscaleJob _job = new();
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RunCommand))] private bool _readyToRun;

    public MainPageViewModel()
    {
        base.ToolStripButtonDefinitions =
        [
            new(ToolStripButtonLocations.Left, MaterialIconKind.Note, "New Job", NewJobCommand),
            new(ToolStripButtonLocations.Right, MaterialIconKind.PlayArrow, "Start", RunCommand)
            {
                ShowText = true
            },
            new ToolStripButtonDefinition(ToolStripButtonLocations.Right, MaterialIconKind.Gear, "Settings", SettingsCommand)
        ];

        this.WhenPropertyChanged(p => p.Job.IsLoaded, false)
            .Subscribe(j => CheckReadyToRun());
        this.WhenPropertyChanged(p => p.Job.SelectedModel, false)
            .Subscribe(p => CheckReadyToRun());
        AppConfiguration.Instance.WhenPropertyChanged(p => p.UpscaylPath)
            .Subscribe(p => LoadModelOptions(p.Value));
    }

    public override void OnAppearing()
    {
        if (!isFirstLoad)
        {
            return;
        }

        isFirstLoad = false;
        var config = AppConfiguration.Instance;
        TryFindFFmpegBinariesPath();
        if ((string.IsNullOrWhiteSpace(config.FFmpegBinariesPath) && !TryFindFFmpegBinariesPath()) || string.IsNullOrWhiteSpace(config.UpscaylPath))
        {
            Settings();
        }
    }

    private void CheckReadyToRun()
    {
        ReadyToRun = Job.IsLoaded && Job.SelectedModel is not null;
    }

    private bool TryFindFFmpegBinariesPath()
    {
        var envPath = Environment.GetEnvironmentVariable("PATH")?.Split(OperatingSystem.IsWindows() ? ';' : ':');
        if (envPath == null)
            return false;

        foreach (var path in envPath)
        {
            var testPath = Path.Combine(path, FFMpegHelper.FFMpegExecutable);
            if (File.Exists(testPath))
            {
                AppConfiguration.Instance.FFmpegBinariesPath = path;
                return true;
            }
        }

        return false;
    }

    

    [RelayCommand]
    private void Settings()
    {
        PageManager.Instance.SetPage(typeof(ConfigPageViewModel));
    }

    [RelayCommand]
    private async Task BrowseVideos()
    {
        var provider = App.Window!.StorageProvider;
        var result = await provider.OpenFilePickerAsync(new()
        {
            Title = "Select video file",
            AllowMultiple = false,
            FileTypeFilter = [new("Videos")
            {
                MimeTypes = [
                    "video/mp4",
                    "videa/mvk",
                    "video/m4v",
                    "video/x-msvideo",
                    "video/mpeg",
                    "video/ogg",
                    "video/webm",
                ],
            },
        ]});
        var selected = result.FirstOrDefault();
        if (selected is null)
            return;
        Job.VideoPath = selected.Path.LocalPath;
    }

    [RelayCommand]
    private async Task BrowseWorkingPath()
    {
        var provider = App.Window!.StorageProvider;
        var result = await provider.OpenFolderPickerAsync(new()
        {
            Title = "Select output path",
        });
        var selected = result.FirstOrDefault();
        if (selected is null)
            return;
        Job.WorkingFolder = selected.Path.LocalPath;
        CheckReadyToRun();
    }
        
    
    [RelayCommand(CanExecute = nameof(ReadyToRun))]
    private async Task Run()
    {
        var job = new JobPageViewModel(new());
        PageManager.Instance.SetPage(job);
        await job.RunAsync();
        PageManager.Instance.SetPage(this);
    }

    [RelayCommand]
    private void NewJob()
    {
        Job = new();
    }
    
    private void LoadModelOptions(string? upscaylPath)
    {
        Job.SelectedModel = null;
        ModelOptions = [];
        if (string.IsNullOrWhiteSpace(upscaylPath))
            return;
        if (!Directory.Exists(upscaylPath))
            return;
        upscaylPath = Path.Combine(upscaylPath, "resources", "models");
        if (!Directory.Exists(upscaylPath))
            return;
        
        var bins = Directory.GetFiles(upscaylPath, "*.bin");

        ModelOptions = bins.Select(f => Path.GetFileNameWithoutExtension(f) ?? string.Empty)
            .Select(f => new AIModelOption(f, f.ToUpperInvariant()))
            .ToArray();
    }
}