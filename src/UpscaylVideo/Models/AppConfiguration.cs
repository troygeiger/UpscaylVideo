using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UpscaylVideo.Models;

public partial class AppConfiguration : ObservableObject
{
    private string? _upscaylPath;

    public string? UpscaylPath
    {
        get => _upscaylPath;
        set => SetProperty(ref _upscaylPath, value);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfiguration))]
public partial class AppConfigurationJsonContext : JsonSerializerContext
{
}