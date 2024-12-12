using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using UpscaylVideo.Models;

namespace UpscaylVideo.ViewModels;

public partial class MainPageViewModel : PageBase
{
    public MainPageViewModel()
    {
        base.ToolStripButtonDefinitions =
        [
            new(ToolStripButtonLocations.Left, MaterialIconKind.PlayArrow, "Start", RunCommand)
            {
                ShowText = true
            },
            new ToolStripButtonDefinition(ToolStripButtonLocations.Right, MaterialIconKind.Gear, "Settings", SettingsCommand)
        ];
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