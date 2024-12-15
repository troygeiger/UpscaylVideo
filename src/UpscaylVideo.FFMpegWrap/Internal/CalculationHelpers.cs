namespace UpscaylVideo.FFMpegWrap.Internal;

internal static class CalculationHelpers
{
    public static double CalcStringDivideExpression(ReadOnlySpan<char> expression)
    {
        var segments = expression.IndexOf('/');
        if (segments <= 0)
            return double.TryParse(expression, out var result) ? result : 0;
        
        var left = double.Parse(expression.Slice(0, segments));
        var right = double.Parse(expression.Slice(segments + 1));
        return left != 0 && right != 0 ? left / right : 0;
    }
    
    public static int? TryStringToInt(ReadOnlySpan<char> value) => int.TryParse(value, out int result) ? result : null;
}