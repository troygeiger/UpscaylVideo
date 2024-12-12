using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UpscaylVideo.FFMpegWrap.Models.Converters;

internal class DivisionConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString().AsSpan();
        
        var segments = value.IndexOf('/');
        if (segments <= 0)
            return double.TryParse(value, out var result) ? result : 0;
        
        var left = double.Parse(value.Slice(0, segments));
        var right = double.Parse(value.Slice(segments + 1));
        return left != 0 && right != 0 ? left / right : 0;
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}