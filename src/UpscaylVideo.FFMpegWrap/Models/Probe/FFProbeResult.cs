using System.Text.Json.Serialization;

namespace UpscaylVideo.FFMpegWrap.Models.Probe;

public class FFProbeResult
{
    [JsonPropertyName("streams")]
    public IEnumerable<FFProbeStream> Streams { get; set; } = [];

    [JsonPropertyName("format")] 
    public Format Format { get; set; } = new Format();
}