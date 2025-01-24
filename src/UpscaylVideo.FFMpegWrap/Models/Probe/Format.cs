using System.Text.Json.Serialization;
using UpscaylVideo.FFMpegWrap.Models.Converters;

namespace UpscaylVideo.FFMpegWrap.Models.Probe;

public class Format
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; }

    [JsonPropertyName("nb_streams")]
    public int NbStreams { get; set; }

    [JsonPropertyName("nb_programs")]
    public int NbPrograms { get; set; }

    [JsonPropertyName("nb_stream_groups")]
    public int NbStreamGroups { get; set; }

    [JsonPropertyName("format_name")]
    public string FormatName { get; set; }

    [JsonPropertyName("format_long_name")]
    public string FormatLongName { get; set; }

    [JsonPropertyName("start_time")]
    [JsonConverter(typeof(StringToDoubleConverter))]
    public double StartTimeDouble { get; set; }
    
    public TimeSpan StartTime => TimeSpan.FromSeconds(StartTimeDouble);

    [JsonPropertyName("duration")]
    [JsonConverter(typeof(StringToDoubleConverter))]
    public double DurationDouble { get; set; }
    
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationDouble);

    [JsonPropertyName("size")]
    [JsonConverter(typeof(Converters.StringToIntConverter))]
    public int Size { get; set; }

    [JsonPropertyName("bit_rate")]
    [JsonConverter(typeof(Converters.StringToIntConverter))]
    public int BitRate { get; set; }

    [JsonPropertyName("probe_score")]
    public int ProbeScore { get; set; }

    [JsonPropertyName("tags")]
    public Tags? Tags { get; set; }
}