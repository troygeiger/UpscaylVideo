using System.Text.RegularExpressions;

namespace UpscaylVideo.Helpers;

public static partial class RegexHelper
{
    [GeneratedRegex(@"\d+:\d+", RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    public static partial Regex AspectRatioRegex { get; } 
    
    [GeneratedRegex(@"^(\d+\.\d+)%?$")]
    public static partial Regex UpscaylPercent { get; }
}