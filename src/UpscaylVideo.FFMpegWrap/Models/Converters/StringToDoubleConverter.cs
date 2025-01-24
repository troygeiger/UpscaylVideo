using System.Text.Json;
using System.Text.Json.Serialization;

namespace UpscaylVideo.FFMpegWrap.Models.Converters;

internal class StringToDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return double.TryParse(reader.GetString(), out var result) ? result : 0;
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}