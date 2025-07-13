using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// <summary>
    /// The full output file path for the upscaled video (set when enqueued)
    /// </summary>
    [ObservableProperty]
    private string? _outputFilePath;
    private string? _previousVideoPath;
    [ObservableProperty] private string? _videoPath;
    [ObservableProperty] private FFProbeResult _videoDetails = new();

    [ObservableProperty, NotifyPropertyChangedFor(nameof(OriginalDimension)), NotifyPropertyChangedFor(nameof(ScaledDimensions))]
    private FFProbeStream? _videoStream;

    [ObservableProperty] private BindingList<string> _messages = new();
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private int _upscaleFrameChunkSize = AppConfiguration.Instance.LastUpscaleFrameChunkSize;
    [ObservableProperty] private string? _workingFolder;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ScaledDimensions))]
    private int _selectedScale = AppConfiguration.Instance.LastScale;
    [ObservableProperty] private InterpolatedFps _selectedInterpolatedFps;
    [ObservableProperty] private AIModelOption? _selectedModel;
    [ObservableProperty] private int[] _gpuNumber = AppConfiguration.Instance.GpuNumbers;
    [ObservableProperty] private bool _deleteWorkingFolderWhenCompleted = true;
    [ObservableProperty] private string _status = Localization.Status_Queued;

    public UpscaleJob()
    {
        this.WhenPropertyChanged(p => p.VideoPath, false)
            .Subscribe(async void (p) =>
            {
                await LoadVideoDetails();
                if (_previousVideoPath != p.Value && File.Exists(p.Value) && VideoStream is not null)
                {
                    var fileName = Path.GetFileNameWithoutExtension(p.Value);
                    var folder = Directory.Exists(AppConfiguration.Instance.TempWorkingFolder)
                        ? AppConfiguration.Instance.TempWorkingFolder
                        : Path.GetDirectoryName(p.Value);
                    WorkingFolder = folder is null ? WorkingFolder : Path.Combine(folder, $"{fileName}_Working", string.Empty);
                    
                    OutputFilePath = GenerateDefaultOutputPath(p.Value);
                }

                _previousVideoPath = p.Value;
            });

        this.WhenPropertyChanged(p => p.VideoStream)
            .Subscribe(stream => { IsLoaded = _videoStream is not null; });
        
        InterpolatedFpsOptions =
        [
            new(null, "No Interpolation (Same as source)"),
            new(5),
            new(10),
            new(12),
            new(15),
            new(20),
            new(23.976, "23.976 (NTSC Film)"),
            new(24),
            new(25, "25 (PAL Film/Video)"),
            new(29.97, "29.97 (NTSC Video)"),
            new(30),
            new(48),
            new(50),
            new (59.94),
            new(60),
            new(72),
            new(75),
            new (90),
            new(100),
            new (120)
        ];
        SelectedInterpolatedFps = InterpolatedFpsOptions.First(i => !i.FrameRate.HasValue);
    }

    public string OriginalDimension =>
        VideoStream is null ? string.Empty : $"{VideoStream.Width} x {VideoStream.Height}";

    public string ScaledDimensions =>
        VideoStream is null ? string.Empty : $"{(VideoStream.Width * SelectedScale)} x {(VideoStream.Height * SelectedScale)}";

    public IEnumerable<InterpolatedFps> InterpolatedFpsOptions { get; }
    
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

    public static string? GenerateDefaultOutputPath(string? videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath))
            return null;
        var config = AppConfiguration.Instance;
        string outputFolder = !string.IsNullOrWhiteSpace(config.OutputPath) 
            ? config.OutputPath : Path.GetDirectoryName(videoPath) ?? Environment.CurrentDirectory;
        
        string originalFile = Path.GetFileNameWithoutExtension(videoPath);
        string originalExtension = Path.GetExtension(videoPath);
        var templateModel = new {
            OriginalFile = originalFile,
            OriginalExtension = originalExtension
        };
        string templateString = string.IsNullOrWhiteSpace(config.OutputFileNameTemplate)
            ? "{{OriginalFile}}-upscaled{{OriginalExtension}}"
            : config.OutputFileNameTemplate;
        var template = HandlebarsDotNet.Handlebars.Compile(templateString);
        string outputFileName = template(templateModel);
        
        return Path.Combine(outputFolder, outputFileName);
    }
}