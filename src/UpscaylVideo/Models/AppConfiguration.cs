using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using UpscaylVideo.FFMpegWrap;
using UpscaylVideo.Helpers;

namespace UpscaylVideo.Models;

public partial class AppConfiguration : ObservableObject
{
    private static AppConfiguration? _instance;

    public static AppConfiguration Instance
    {
        get
        {
            if (_instance is null)
                _instance = ConfigurationHelper.LoadConfig(AppConfigurationJsonContext.Default.AppConfiguration);
            return _instance;
        }
    }
    
    private string? _upscaylPath;

    public string? UpscaylPath
    {
        get => _upscaylPath;
        set => SetProperty(ref _upscaylPath, value);
    }

    public string? FFmpegBinariesPath
    {
        get => FFMpegOptions.Global.FFMpegFolder;
        set
        {
            FFMpegOptions.Global.FFMpegFolder = value ?? string.Empty;
            OnPropertyChanged(nameof(FFmpegBinariesPath));  
        } 
    }

    public int LastScale { get; set; } = 4;

    public int LastUpscaleFrameChunkSize { get; set; } = 1000;

    private string? _tempWorkingFolder;
    public string? TempWorkingFolder
    {
        get => _tempWorkingFolder;
        set => SetProperty(ref _tempWorkingFolder, value);
    }
    
    public void Save()
    {
        ConfigurationHelper.SaveConfig(this, AppConfigurationJsonContext.Default.AppConfiguration);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfiguration))]
public partial class AppConfigurationJsonContext : JsonSerializerContext
{
}