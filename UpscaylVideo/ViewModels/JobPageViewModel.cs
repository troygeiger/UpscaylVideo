using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using UpscaylVideo.FFMpegWrap.Models.Probe;

namespace UpscaylVideo.ViewModels;

public partial class JobPageViewModel : PageBase
{
    private readonly FFProbeResult _probeResult;
    CancellationTokenSource _tokenSource = new();

    /// <inheritdoc/>
    public JobPageViewModel(FFProbeResult probeResult)
    {
        _probeResult = probeResult;
    }

    public async Task RunAsync()
    {
        await Task.Delay(5000);
    }

    [RelayCommand]
    private void Cancel()
    {
        _tokenSource.Cancel();
    }
    
}