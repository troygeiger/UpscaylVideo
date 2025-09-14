using CommunityToolkit.Mvvm.ComponentModel;

namespace UpscaylVideo.Models;

public partial class SubtitleTrackOption : ObservableObject
{
    public SubtitleTrackOption(int streamIndex, string? language, string codec, bool forced, bool isImageBased)
    {
        StreamIndex = streamIndex;
        Language = language;
        Codec = codec;
        Forced = forced;
        _selected = true;
        IsImageBased = isImageBased;
    }

    public int StreamIndex { get; }
    public string? Language { get; }
    public string Codec { get; }
    public bool Forced { get; }
    public bool IsImageBased { get; }

    [ObservableProperty]
    private bool _selected;
}
