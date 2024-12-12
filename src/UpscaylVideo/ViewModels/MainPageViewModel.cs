using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using UpscaylVideo.FFMpegWrap;
using UpscaylVideo.Models;

namespace UpscaylVideo.ViewModels;

public partial class MainPageViewModel : PageBase
{
    private bool isFirstLoad = true;
    [ObservableProperty] private UpscaleJob _job = new();

    public MainPageViewModel()
    {
        base.ToolStripButtonDefinitions =
        [
            new(ToolStripButtonLocations.Right, MaterialIconKind.PlayArrow, "Start", RunCommand)
            {
                ShowText = true
            },
            new ToolStripButtonDefinition(ToolStripButtonLocations.Right, MaterialIconKind.Gear, "Settings", SettingsCommand)
        ];
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

    [ObservableProperty] private bool _readyToRun = true;

    [RelayCommand]
    private void Settings()
    {
        PageManager.Instance.SetPage(typeof(ConfigPageViewModel));
    }

    [RelayCommand(CanExecute = nameof(ReadyToRun))]
    private async Task Run()
    {
        var job = new JobPageViewModel(new());
        PageManager.Instance.SetPage(job);
        await job.RunAsync();
        PageManager.Instance.SetPage(this);
    }
}