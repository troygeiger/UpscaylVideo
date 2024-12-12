using System.Text.Json.Serialization;

namespace UpscaylVideo.FFMpegWrap.Models.Probe;

public class Tags
{
    [JsonPropertyName("creation_time")]
    public DateTime CreationTime { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; }

    [JsonPropertyName("handler_name")]
    public string HandlerName { get; set; }

    [JsonPropertyName("vendor_id")]
    public string VendorId { get; set; }

    [JsonPropertyName("major_brand")]
    public string MajorBrand { get; set; }

    [JsonPropertyName("minor_version")]
    public string MinorVersion { get; set; }

    [JsonPropertyName("compatible_brands")]
    public string CompatibleBrands { get; set; }

    [JsonPropertyName("encoder")]
    public string Encoder { get; set; }
}