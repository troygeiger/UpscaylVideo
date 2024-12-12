using System.Text.Json;
using System.Text.Json.Serialization;

namespace UpscaylVideo.FFMpegWrap.Models.Converters;

internal class StringToIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return int.TryParse(reader.GetString(), out int result) ? result : default(int);
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}