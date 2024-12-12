using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using UpscaylVideo.FFMpegWrap;
using UpscaylVideo.FFMpegWrap.Models.Probe;


namespace UpscaylVideo.Models;

public partial class UpscaleJob : ObservableObject
{
    [ObservableProperty] private string? _videoPath;
    [ObservableProperty] private FFProbeResult _videoDetails = new();
    [ObservableProperty] private FFProbeStream? _videoStream;
    

    public UpscaleJob()
    {
        this.WhenPropertyChanged(p => p.VideoPath, false)
            .Subscribe(async void (p) => await LoadVideoDetails());

    }

    private async Task LoadVideoDetails()
    {
        if (string.IsNullOrWhiteSpace(VideoPath))
            return;
        if (!File.Exists(VideoPath))
            return;
        try
        {
            VideoDetails = await FFProbe.AnalyseAsync(VideoPath);

            VideoStream = VideoDetails.Streams.FirstOrDefault(s => s.CodecType == "video");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }
    }
    
}