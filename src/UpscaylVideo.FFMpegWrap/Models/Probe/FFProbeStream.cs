using System.Text.Json.Serialization;
using UpscaylVideo.FFMpegWrap.Models.Converters;

namespace UpscaylVideo.FFMpegWrap.Models.Probe;

public class FFProbeStream
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("codec_name")]
    public required string CodecName { get; set; }

    [JsonPropertyName("codec_long_name")]
    public string? CodecLongName { get; set; }

    [JsonPropertyName("profile")]
    public string? Profile { get; set; }

    [JsonPropertyName("codec_type")]
    public required string CodecType { get; set; }

    [JsonPropertyName("codec_tag_string")]
    public string CodecTagString { get; set; }

    [JsonPropertyName("codec_tag")]
    public string CodecTag { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("coded_width")]
    public int CodedWidth { get; set; }

    [JsonPropertyName("coded_height")]
    public int CodedHeight { get; set; }

    [JsonPropertyName("closed_captions")]
    public int ClosedCaptions { get; set; }

    [JsonPropertyName("film_grain")]
    public int FilmGrain { get; set; }

    [JsonPropertyName("has_b_frames")]
    public int HasBFrames { get; set; }

    [JsonPropertyName("sample_aspect_ratio")]
    public string SampleAspectRatio { get; set; }

    [JsonPropertyName("display_aspect_ratio")]
    public string DisplayAspectRatio { get; set; }

    [JsonPropertyName("pix_fmt")]
    public string PixFmt { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("color_range")]
    public string ColorRange { get; set; }

    [JsonPropertyName("color_space")]
    public string ColorSpace { get; set; }

    [JsonPropertyName("color_transfer")]
    public string ColorTransfer { get; set; }

    [JsonPropertyName("color_primaries")]
    public string ColorPrimaries { get; set; }

    [JsonPropertyName("chroma_location")]
    public string ChromaLocation { get; set; }

    [JsonPropertyName("field_order")]
    public string FieldOrder { get; set; }

    [JsonPropertyName("refs")]
    public int Refs { get; set; }

    [JsonPropertyName("is_avc")]
    public string IsAvc { get; set; }

    [JsonPropertyName("nal_length_size")]
    public string NalLengthSize { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("r_frame_rate")]
    [JsonConverter(typeof(DivisionConverter))]
    public double RFrameRate { get; set; }

    [JsonPropertyName("avg_frame_rate")]
    [JsonConverter(typeof(DivisionConverter))]
    public double AvgFrameRate { get; set; }

    [JsonPropertyName("time_base")]
    [JsonConverter(typeof(DivisionConverter))]
    public double TimeBase { get; set; }

    [JsonPropertyName("start_pts")]
    public int StartPts { get; set; }

    [JsonPropertyName("start_time")]
    public TimeSpan StartTime { get; set; }

    [JsonPropertyName("duration_ts")]
    public int DurationTs { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }

    [JsonPropertyName("bit_rate")]
    [JsonConverter(typeof(StringToIntConverter))]
    public int BitRate { get; set; }

    [JsonPropertyName("bits_per_raw_sample")]
    [JsonConverter(typeof(StringToIntConverter))]
    public int BitsPerRawSample { get; set; }

    [JsonPropertyName("nb_frames")]
    [JsonConverter(typeof(StringToIntConverter))]
    public int NbFrames { get; set; }

    [JsonPropertyName("extradata_size")]
    public int ExtradataSize { get; set; }

    [JsonPropertyName("disposition")]
    public Disposition Disposition { get; set; }

    [JsonPropertyName("tags")]
    public Tags Tags { get; set; }

    [JsonPropertyName("sample_fmt")]
    public string SampleFmt { get; set; }

    [JsonPropertyName("sample_rate")]
    public string SampleRate { get; set; }

    [JsonPropertyName("channels")]
    public int? Channels { get; set; }

    [JsonPropertyName("channel_layout")]
    public string ChannelLayout { get; set; }

    [JsonPropertyName("bits_per_sample")]
    public int? BitsPerSample { get; set; }

    [JsonPropertyName("initial_padding")]
    public int? InitialPadding { get; set; }

    [JsonPropertyName("side_data_list")]
    public List<SideDataList> SideDataList { get; set; }
}