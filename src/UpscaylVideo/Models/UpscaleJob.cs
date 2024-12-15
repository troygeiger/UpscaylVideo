using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using ReactiveUI;
using UpscaylVideo.FFMpegWrap;
using UpscaylVideo.FFMpegWrap.Models.Probe;


namespace UpscaylVideo.Models;

public partial class UpscaleJob : ObservableObject
{
    [ObservableProperty] private string? _videoPath;
    [ObservableProperty] private FFProbeResult _videoDetails = new();
    
    [ObservableProperty, NotifyPropertyChangedFor(nameof(OriginalDimension)), NotifyPropertyChangedFor(nameof(ScaledDimensions))] 
    private FFProbeStream? _videoStream;
    [ObservableProperty] private BindingList<string> _messages = new();
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private int _clipSeconds = AppConfiguration.Instance.LastClipSeconds;
    [ObservableProperty] private int _upscaleFrameChunkSize = 1000;
    [ObservableProperty] private string? _workingFolder;
    
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ScaledDimensions))] 
    private int _selectedScale = AppConfiguration.Instance.LastScale;
    
    [ObservableProperty] private AIModelOption? _selectedModel;

    public UpscaleJob()
    {
        this.WhenPropertyChanged(p => p.VideoPath, false)
            .Subscribe(async void (p) =>
            {
                await LoadVideoDetails();
                if (string.IsNullOrWhiteSpace(WorkingFolder) && File.Exists(p.Value) && VideoStream is not null)
                {
                    var fileName = Path.GetFileNameWithoutExtension(p.Value);
                    var folder = Path.GetDirectoryName(p.Value);
                    WorkingFolder = folder is null ? WorkingFolder : Path.Combine(folder, $"{fileName}_Working", string.Empty);
                }
            });

        this.WhenPropertyChanged(p => p.VideoStream)
            .Subscribe(stream => { IsLoaded = _videoStream is not null; });
        
    }

    public string OriginalDimension =>
        VideoStream is null ? string.Empty : $"{VideoStream.Width} x {VideoStream.Height}";

    public string ScaledDimensions =>
        VideoStream is null ? string.Empty : $"{(VideoStream.Width * SelectedScale)} x {(VideoStream.Height * SelectedScale)}";

    private async Task LoadVideoDetails()
    {
        VideoStream = null;
        VideoDetails = new();
        if (string.IsNullOrWhiteSpace(VideoPath))
            return;
        if (!File.Exists(VideoPath))
            return;
        try
        {
            var detail = await FFProbe.AnalyseAsync(VideoPath);

            if (!detail.success || detail.result == null)
                return;

            VideoDetails = detail.result;

            VideoStream = VideoDetails.Streams.FirstOrDefault(s => s.CodecType == "video");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }
    }
}