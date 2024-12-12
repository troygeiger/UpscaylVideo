using CommunityToolkit.Mvvm.ComponentModel;

namespace UpscaylVideo.Models;

public partial class AppConfiguration : ObservableObject
{
    [ObservableProperty] private string? _upscaylPath;
}