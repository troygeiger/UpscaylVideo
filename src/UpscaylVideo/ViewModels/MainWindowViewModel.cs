using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using UpscaylVideo.Services;

namespace UpscaylVideo.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";
    
    public UpscaylVideo.Services.PageManager PageManager { get; } = UpscaylVideo.Services.PageManager.Instance;
    public JobProcessingService JobQueueService { get; } = JobProcessingService.Instance;

    public string Version { get; }

    public MainWindowViewModel()
    {
        TG.Common.AssemblyInfo.ReferenceAssembly = typeof(MainWindowViewModel).Assembly;
        Version = TG.Common.AssemblyInfo.InformationVersion;
    }

    [RelayCommand]
    private async Task Test()
    {
        var videoPath = "/home/troy/Videos/Always/Always (1989).mp4";
        await UpscaylVideo.FFMpegWrap.FFProbe.AnalyseAsync(videoPath);
    }
}