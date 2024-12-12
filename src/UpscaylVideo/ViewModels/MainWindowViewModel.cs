using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using UpscaylVideo.Models;

namespace UpscaylVideo.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";
    
    public PageManager PageManager { get; } = PageManager.Instance;

    public MainWindowViewModel()
    {
    }

    [RelayCommand]
    private async Task Test()
    {
        var videoPath = "/home/troy/Videos/Always/Always (1989).mp4";
        await UpscaylVideo.FFMpegWrap.FFProbe.AnalyseAsync(videoPath);
    }
}