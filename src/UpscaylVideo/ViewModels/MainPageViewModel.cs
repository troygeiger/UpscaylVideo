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
using UpscaylVideo.Services;

namespace UpscaylVideo.ViewModels;

public partial class MainPageViewModel : PageBase
{
    private bool isFirstLoad = true;
    [ObservableProperty] private IEnumerable<AIModelOption> _modelOptions = [];
    [ObservableProperty] private UpscaleJob _job = new();
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RunCommand))] private bool _readyToRun;
    [ObservableProperty] private string _gpuNumberList = string.Join(',', AppConfiguration.Instance.GpuNumbers);
    private ToolStripButtonDefinition _startButton;
    private ToolStripButtonDefinition _cancelButton;

    public MainPageViewModel()
    {
        _startButton = new ToolStripButtonDefinition(ToolStripButtonLocations.Right, MaterialIconKind.PlayArrow, "Start", RunCommand)
        {
            ShowText = true
        };
        _cancelButton = new ToolStripButtonDefinition(ToolStripButtonLocations.Right, MaterialIconKind.Cancel, "Cancel", CancelJobCommand)
        {
            ShowText = true,
            Visible = UpscaylVideo.Services.JobQueueService.Instance.IsProcessing
        };

        base.ToolStripButtonDefinitions =
        [
            new(ToolStripButtonLocations.Left, MaterialIconKind.Note, "New Job", NewJobCommand),
            _startButton,
            _cancelButton,
            new ToolStripButtonDefinition(ToolStripButtonLocations.Right, MaterialIconKind.Gear, "Settings", SettingsCommand),
            new ToolStripButtonDefinition(ToolStripButtonLocations.Right, MaterialIconKind.ListStatus, "Queue", OpenQueueCommand)
        ];

        // Subscribe to JobQueueService.IsProcessing to update Cancel button visibility using WhenPropertyChanged
        UpscaylVideo.Services.JobQueueService.Instance.WhenPropertyChanged(x => x.IsProcessing, false)
            .Subscribe(x => _cancelButton.Visible = x.Value);

        

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

        UpscaylVideo.Services.JobQueueService.Instance.JobQueue.CollectionChanged += (s, e) =>
        {
            var queueCount = UpscaylVideo.Services.JobQueueService.Instance.JobQueue.Count;
            _startButton.Text = queueCount > 0 ? "Add to Queue" : "Start";
        };
    }

    [RelayCommand]
    private void CancelJob()
    {
        UpscaylVideo.Services.JobQueueService.Instance.CancelCurrentJob();
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
        UpscaylVideo.Services.PageManager.Instance.SetPage(typeof(ConfigPageViewModel));
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
                Patterns = [
                    "*.mp4", "*.mkv", "*.m4v", "*.avi", "*.wmv", "*.webm", "*.mov",
                    "*.flv", "*.mpg", "*.mpeg", "*.ts", "*.3gp", "*.3g2", "*.vob",
                    "*.ogv", "*.mts", "*.m2ts", "*.divx", "*.asf", "*.rm", "*.rmvb",
                    "*.f4v", "*.dat", "*.mxf"
                ],
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
    
    [RelayCommand]
    private async Task BrowseOutputPath()
    {
        if (!File.Exists(Job.VideoPath))
            return;
        var videoExtension = Path.GetExtension(Job.VideoPath);
        
        var provider = App.Window!.StorageProvider;
        var result = await provider.SaveFilePickerAsync(new()
        {
            Title = "Select output file",
            SuggestedFileName = Job.OutputFilePath ?? UpscaleJob.GenerateDefaultOutputPath(Job.VideoPath),
            FileTypeChoices = [new(videoExtension)
            {
                Patterns = [$"*{videoExtension}"]
            }],
        });
        if (result is null)
            return;
        Job.OutputFilePath = result.Path.LocalPath;
    }
        
    
    [RelayCommand(CanExecute = nameof(ReadyToRun))]
    private void Run()
    {
        var config = AppConfiguration.Instance;
        config.LastScale = Job.SelectedScale;
        config.LastUpscaleFrameChunkSize = Job.UpscaleFrameChunkSize;
        config.LastModelUsed = Job.SelectedModel?.Name;
        config.GpuNumbers = Job.GpuNumber;
        config.Save();

        if (string.IsNullOrWhiteSpace(Job.OutputFilePath))
        {
            Job.OutputFilePath = UpscaleJob.GenerateDefaultOutputPath(Job.VideoPath);
        }

        // Enqueue the job instead of navigating
        UpscaylVideo.Services.JobQueueService.Instance.EnqueueJob(Job);
        // Optionally reset the job form for new input
        Job = new();
        UpdateSelectedModel();
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

    [RelayCommand]
    private void OpenQueue()
    {
        UpscaylVideo.Services.PageManager.Instance.SetPage(typeof(QueuePageViewModel));
    }
}