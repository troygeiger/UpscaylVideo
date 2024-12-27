using System.Text.Json.Serialization;

namespace UpscaylVideo.FFMpegWrap.Models.Probe;

public class FFProbeResult
{
    [JsonPropertyName("streams")]
    public IEnumerable<FFProbeStream> Streams { get; set; } = [];

    [JsonPropertyName("format")] 
    public Format Format { get; set; } = new Format();

    public TimeSpan GetDuration()
    {
        var videoStream = Streams.FirstOrDefault(s => s.CodecType == "video");
        TimeSpan result = videoStream?.Duration ?? TimeSpan.Zero;
        return result == TimeSpan.Zero ? Format.Duration : result;
    }
}