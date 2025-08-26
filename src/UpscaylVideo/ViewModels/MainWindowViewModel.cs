using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using UpscaylVideo.Services;
using System.Diagnostics;

namespace UpscaylVideo.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public UpscaylVideo.Services.PageManager PageManager { get; } = UpscaylVideo.Services.PageManager.Instance;
    public JobProcessingService JobProcessingService { get; } = JobProcessingService.Instance;
    public UpdateService UpdateService { get; } = UpdateService.Instance;

    public string Version { get; }

    public MainWindowViewModel()
    {
        TG.Common.AssemblyInfo.ReferenceAssembly = typeof(MainWindowViewModel).Assembly;
        Version = TG.Common.AssemblyInfo.InformationVersion;
    }

    [RelayCommand]
    private void OpenLatestRelease()
    {
        var url = UpdateService.LatestReleaseUrl;
        if (string.IsNullOrWhiteSpace(url))
            return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // ignore
        }
    }
}