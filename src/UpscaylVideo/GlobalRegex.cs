using System.Text.RegularExpressions;

namespace UpscaylVideo;

public static partial class GlobalRegex
{
    [GeneratedRegex(@"^(\d+\.\d+)%?$")]
    public static partial Regex UpscaylPercent();
}