using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace UpscaylVideo.Helpers;

/// <summary>
/// Multi-value converter that returns true only if all bound boolean inputs are true.
/// Any non-boolean or null value is treated as false.
/// </summary>
public class BooleanAndMultiConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Count == 0)
            return false;

        foreach (var v in values)
        {
            if (v is bool b)
            {
                if (!b)
                    return false;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    public object?[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
