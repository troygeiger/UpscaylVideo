using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private string? _ffmpegBinariesPath;

    public string? UpscaylPath
    {
        get => _upscaylPath;
        set => SetProperty(ref _upscaylPath, value);
    }

    public string? FFmpegBinariesPath
    {
        get => _ffmpegBinariesPath;
        set => SetProperty(ref _ffmpegBinariesPath, value);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfiguration))]
public partial class AppConfigurationJsonContext : JsonSerializerContext
{
}