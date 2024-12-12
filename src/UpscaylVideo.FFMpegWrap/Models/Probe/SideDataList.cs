using System.Text.Json.Serialization;

namespace UpscaylVideo.FFMpegWrap.Models.Probe;

    // Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);

    public class SideDataList
    {
        [JsonPropertyName("side_data_type")]
        public string SideDataType { get; set; } = string.Empty;

        [JsonPropertyName("service_type")]
        public int ServiceType { get; set; }
    }