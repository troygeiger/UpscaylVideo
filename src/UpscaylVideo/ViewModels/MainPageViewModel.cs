using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using Material.Icons;
using Material.Icons.Avalonia;
using UpscaylVideo.FFMpegWrap;
using UpscaylVideo.Helpers;
using UpscaylVideo.Models;
using UpscaylVideo.Services;
using UpscaylVideo; // for Localization

namespace UpscaylVideo.ViewModels;

public partial class MainPageViewModel : PageBase
{
    private static readonly string[] _supportedVideoExtensions =
    [
        "*.mp4", "*.mkv", "*.m4v", "*.avi", "*.wmv", "*.webm", "*.mov",
        "*.flv", "*.mpg", "*.mpeg", "*.ts", "*.3gp", "*.3g2", "*.vob",
        "*.ogv", "*.mts", "*.m2ts", "*.divx", "*.asf", "*.rm", "*.rmvb",
        "*.f4v", "*.dat", "*.mxf"
    ];
    private bool isFirstLoad = true;
    [ObservableProperty] private IEnumerable<AIModelOption> _modelOptions = [];
    [ObservableProperty] private UpscaleJob _job = new();
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RunCommand))] private bool _readyToRun;
    [ObservableProperty] private string _gpuNumberList = string.Join(',', AppConfiguration.Instance.GpuNumbers);

    // New: options for output image formats used by Upscayl-bin (-f)
    public IEnumerable<string> ImageFormats { get; } = ["jpeg", "png"];

    public MainPageViewModel()
    {
        // Initialize job defaults from configuration
        var lastFmt = AppConfiguration.Instance.LastImageFormat?.ToLowerInvariant();
        if (lastFmt == "webp") lastFmt = "png"; // sanitize removed format
        Job.OutputImageFormat = string.IsNullOrWhiteSpace(lastFmt) ? "png" : lastFmt;
        Job.TileSize = AppConfiguration.Instance.LastTileSize;

        var checkUpdateText = UpscaylVideo.Localization.ResourceManager.GetString("MainPageView_CheckUpdates") ?? "Check for Updates";

        // Build toolstrip controls
        // SplitButton: primary New Job, dropdown has Queue Multiple
        var newJobSplit = CreateSplitButton(
            MaterialIconKind.Note,
            Localization.MainPageView_NewJob,
            NewJobCommand,
            [
                CreateMenuItem(Localization.MainPageView_QueueMultiple, MaterialIconKind.FileMultiple, AddBatchCommand),
            ], showText:false); 
        

        var startBtn = CreateToolButton(MaterialIconKind.PlayArrow, Localization.MainPageView_Start, RunCommand, out var startText, toolTip: Localization.MainPageView_Start, showText: true);
        var cancelButton = CreateToolButton(MaterialIconKind.Cancel, Localization.MainPageView_Cancel, CancelJobCommand, toolTip: Localization.MainPageView_Cancel, showText: true);
        //var settingsBtn = CreateToolButton(MaterialIconKind.Gear, "Settings", SettingsCommand, toolTip: "Settings", showText: false);
        var queueBtn = CreateToolButton(MaterialIconKind.ListStatus, Localization.MainPageView_Queue, OpenQueueCommand, toolTip: Localization.MainPageView_Queue, showText: false);
        //var updateBtn = CreateToolButton(MaterialIconKind.Update, checkUpdateText, CheckUpdatesCommand, toolTip: checkUpdateText, showText: false);
        var settingsBtn = CreateSplitButton(
            MaterialIconKind.Gear,
            Localization.MainPageView_Settings,
            SettingsCommand,
            [
                CreateMenuItem(checkUpdateText, MaterialIconKind.Update, CheckUpdatesCommand),
            ], showText:false
        );
        cancelButton.IsVisible = UpscaylVideo.Services.JobProcessingService.Instance.IsProcessing;

        LeftToolStripControls =
        [
            newJobSplit
        ];
        RightToolStripControls =
        [
            startBtn,
            cancelButton,
            queueBtn,
            settingsBtn,
        ];
        cancelButton.Bind(Visual.IsVisibleProperty, new Binding(nameof(JobProcessingService.IsProcessing)){ Source = UpscaylVideo.Services.JobProcessingService.Instance });

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
                    throw new ArgumentException(Localization.MainPageView_InvalidNumberString);
                Job.GpuNumber = results.ToArray();
                
            });

        UpscaylVideo.Services.JobProcessingService.Instance.JobQueue.CollectionChanged += (s, e) =>
        {
            var queueCount = UpscaylVideo.Services.JobProcessingService.Instance.JobQueue.Count;
            if (startText != null)
                startText.Text = queueCount > 0 ? Localization.MainPageView_AddToQueue : Localization.MainPageView_Start;
        };
    }

    [RelayCommand]
    private async Task CancelJob()
    {
        await Services.JobProcessingService.Instance.CancelCurrentJobAsync();
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

        // Fire-and-forget update check in background
        _ = UpscaylVideo.Services.UpdateService.Instance.CheckForUpdatesAsync(true);
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
        var lastStorage = await AppConfiguration.Instance.LastBrowsedVideoPath.TryGetStorageFolderAsync(provider);
        var result = await provider.OpenFilePickerAsync(new()
        {
            Title = Localization.MainPageView_SelectVideoFile,
            AllowMultiple = false,
            SuggestedStartLocation = lastStorage, 
            FileTypeFilter = [ new(Localization.Common_Videos)
            {
                Patterns = _supportedVideoExtensions,
            }, 
                FilePickerFileTypes.All,
            ]});
        var selected = result.FirstOrDefault();
        if (selected is null)
            return;
        Job.VideoPath = selected.Path.LocalPath;
        AppConfiguration.Instance.LastBrowsedVideoPath = await selected.GetParentAsync().GetUriAsync();
        AppConfiguration.Instance.Save();
    }

    [RelayCommand]
    private async Task AddBatch()
    {
        var provider = App.Window!.StorageProvider;
        var lastStorage = await AppConfiguration.Instance.LastBrowsedVideoPath.TryGetStorageFolderAsync(provider);
        var result = await provider.OpenFilePickerAsync(new()
        {
            Title = Localization.MainPageView_SelectVideoFiles,
            AllowMultiple = true,
            SuggestedStartLocation = lastStorage, 
            FileTypeFilter = [ new(Localization.Common_Videos)
                {
                    Patterns = _supportedVideoExtensions,
                }, 
                FilePickerFileTypes.All,
            ]});

        foreach (var selected in result)
        {
            var batchJob = new UpscaleJob();
            batchJob.UpscaleFrameChunkSize = Job.UpscaleFrameChunkSize;
            batchJob.SelectedScale = Job.SelectedScale;
            batchJob.SelectedInterpolatedFps = Job.SelectedInterpolatedFps;
            batchJob.SelectedModel = Job.SelectedModel;
            batchJob.GpuNumber = Job.GpuNumber;
            // New: copy format and tile size options
            batchJob.OutputImageFormat = Job.OutputImageFormat;
            batchJob.TileSize = Job.TileSize;
            batchJob.VideoPath = selected.Path.LocalPath;
            await batchJob.WaitForLoadAsync();
            JobProcessingService.Instance.EnqueueJob(batchJob);
        }
        
        var first = result.FirstOrDefault();
        if (first is null)
            return;
        
        AppConfiguration.Instance.LastBrowsedVideoPath = await first.GetParentAsync().GetUriAsync();
        AppConfiguration.Instance.Save();
    }

    [RelayCommand]
    private async Task BrowseWorkingPath()
    {
        var provider = App.Window!.StorageProvider;
        var lastStorage = await AppConfiguration.Instance.LastBrowsedWorkingFolder.TryGetStorageFolderAsync(provider);
        var result = await provider.OpenFolderPickerAsync(new()
        {
            Title = Localization.MainPageView_SelectOutputPath,
            SuggestedStartLocation = lastStorage,
        });
        var selected = result.FirstOrDefault();
        if (selected is null)
            return;
        Job.WorkingFolder = selected.Path.LocalPath;
        AppConfiguration.Instance.LastBrowsedWorkingFolder = await selected.GetParentAsync().GetUriAsync();
        AppConfiguration.Instance.Save();
        CheckReadyToRun();
    }
    
    [RelayCommand]
    private async Task BrowseOutputPath()
    {
        if (!File.Exists(Job.VideoPath))
            return;
        var videoExtension = Path.GetExtension(Job.VideoPath);
        
        var provider = App.Window!.StorageProvider;
        var lastStorage = await AppConfiguration.Instance.LastBrowsedOutputPath.TryGetStorageFolderAsync(provider);
        var result = await provider.SaveFilePickerAsync(new()
        {
            Title = Localization.MainPageView_SelectOutputFile,
            SuggestedFileName = Job.OutputFilePath ?? UpscaleJob.GenerateDefaultOutputPath(Job.VideoPath),
            FileTypeChoices = [new(videoExtension)
            {
                Patterns = [$"*{videoExtension}"]
            }],
            SuggestedStartLocation = lastStorage,
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
        // Persist new options
        config.LastImageFormat = string.IsNullOrWhiteSpace(Job.OutputImageFormat) ? "png" : Job.OutputImageFormat!;
        config.LastTileSize = Job.TileSize;
        config.Save();

        if (string.IsNullOrWhiteSpace(Job.OutputFilePath))
        {
            Job.OutputFilePath = UpscaleJob.GenerateDefaultOutputPath(Job.VideoPath);
        }

        // Enqueue the job instead of navigating
        UpscaylVideo.Services.JobProcessingService.Instance.EnqueueJob(Job);
        // Optionally reset the job form for new input
        NewJob();
    }

    [RelayCommand]
    private void NewJob()
    {
        Job = new();
        // Initialize defaults on new job
        var lastFmt = AppConfiguration.Instance.LastImageFormat?.ToLowerInvariant();
        if (lastFmt == "webp") lastFmt = "png";
        Job.OutputImageFormat = string.IsNullOrWhiteSpace(lastFmt) ? "png" : lastFmt;
        Job.TileSize = AppConfiguration.Instance.LastTileSize;
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

    [RelayCommand]
    private async Task CheckUpdates()
    {
        await UpscaylVideo.Services.UpdateService.Instance.CheckForUpdatesAsync(true);
    }
}
