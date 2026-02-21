using System;
using System.Globalization;
using Avalonia.Data.Converters;
using EnvioSafTApp.Services;

namespace EnvioSafTApp.Converters
{
    public class TickerTypeToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not TickerMessageType type)
            {
                return "i";
            }

            return type switch
            {
                TickerMessageType.Success => "✓",
                TickerMessageType.Error => "!",
                TickerMessageType.Warning => "⚠",
                TickerMessageType.Info => "i",
                _ => "i"
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
