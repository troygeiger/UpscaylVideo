using System.Text.Json.Serialization;

namespace UpscaylVideo.FFMpegWrap.Models.Probe;

public class Disposition
{
    [JsonPropertyName("default")]
    public int Default { get; set; }

    [JsonPropertyName("dub")]
    public int Dub { get; set; }

    [JsonPropertyName("original")]
    public int Original { get; set; }

    [JsonPropertyName("comment")]
    public int Comment { get; set; }

    [JsonPropertyName("lyrics")]
    public int Lyrics { get; set; }

    [JsonPropertyName("karaoke")]
    public int Karaoke { get; set; }

    [JsonPropertyName("forced")]
    public int Forced { get; set; }

    [JsonPropertyName("hearing_impaired")]
    public int HearingImpaired { get; set; }

    [JsonPropertyName("visual_impaired")]
    public int VisualImpaired { get; set; }

    [JsonPropertyName("clean_effects")]
    public int CleanEffects { get; set; }

    [JsonPropertyName("attached_pic")]
    public int AttachedPic { get; set; }

    [JsonPropertyName("timed_thumbnails")]
    public int TimedThumbnails { get; set; }

    [JsonPropertyName("non_diegetic")]
    public int NonDiegetic { get; set; }

    [JsonPropertyName("captions")]
    public int Captions { get; set; }

    [JsonPropertyName("descriptions")]
    public int Descriptions { get; set; }

    [JsonPropertyName("metadata")]
    public int Metadata { get; set; }

    [JsonPropertyName("dependent")]
    public int Dependent { get; set; }

    [JsonPropertyName("still_image")]
    public int StillImage { get; set; }
}