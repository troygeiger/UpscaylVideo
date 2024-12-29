using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
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
    [ObservableProperty] private string _gpuNumberList = string.Join(',', AppConfiguration.Instance.GpuNumbers);
    

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

        this.WhenPropertyChanged(p => GpuNumberList, false)
            .Subscribe(p =>
            {
                if (string.IsNullOrWhiteSpace(p.Value))
                {
                    Job.GpuNumber = [];
                    return;
                }
                bool anyInvalid = false;
                var numberStrings = p.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                List<int> results = new(numberStrings.Length);
                foreach (var numStr in numberStrings)
                {
                    if (int.TryParse(numStr, out var value))
                    {
                        results.Add(value);
                        continue;
                    }
                    anyInvalid = true;
                }
                if (anyInvalid)
                    throw new ArgumentException("Invalid number string");
                Job.GpuNumber = results.ToArray();
                
            });
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
        if ((string.IsNullOrWhiteSpace(config.FFmpegBinariesPath) && !TryFindFFmpegBinariesPath()))
        {
            Settings();
            return;
        }

        if (!Directory.Exists(config.FFmpegBinariesPath))
        {
            Settings();
            return;
        }

        if (string.IsNullOrWhiteSpace(config.UpscaylPath) || (!string.IsNullOrEmpty(config.UpscaylPath) && !Directory.Exists(config.UpscaylPath)))
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
            FileTypeFilter = [ new("Videos")
            {
                Patterns = ["*.mp4", "*.mkv", "*.m4v", "*.avi", "*.wmv", "*.webm"],
            }, 
                FilePickerFileTypes.All,
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
        var config = AppConfiguration.Instance;
        config.LastScale = Job.SelectedScale;
        config.LastUpscaleFrameChunkSize = Job.UpscaleFrameChunkSize;
        config.LastModelUsed = Job.SelectedModel?.Name;
        config.GpuNumbers = Job.GpuNumber;
        config.Save();
        
        var jobViewModel = new JobPageViewModel(Job);
        PageManager.Instance.SetPage(jobViewModel);
        await jobViewModel.RunAsync();
    }

    [RelayCommand]
    private void NewJob()
    {
        Job = new();
        UpdateSelectedModel();
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
        
        UpdateSelectedModel();
    }

    private void UpdateSelectedModel()
    {
        if (string.IsNullOrEmpty(AppConfiguration.Instance.LastModelUsed))
            return;
        if (Job.SelectedModel is not null)
            return;
        Job.SelectedModel = ModelOptions.FirstOrDefault(m => m.Name == AppConfiguration.Instance.LastModelUsed);
    }
}