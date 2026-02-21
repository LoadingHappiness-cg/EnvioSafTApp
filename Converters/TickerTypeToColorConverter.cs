using EnvioSafTApp.Services;
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace EnvioSafTApp.Converters
{
    public class TickerTypeToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TickerMessageType type)
            {
                return type switch
                {
                    TickerMessageType.Success => new SolidColorBrush(Color.FromRgb(46, 157, 102)),
                    TickerMessageType.Error => new SolidColorBrush(Color.FromRgb(205, 63, 63)),
                    TickerMessageType.Warning => new SolidColorBrush(Color.FromRgb(204, 138, 0)),
                    TickerMessageType.Info => new SolidColorBrush(Color.FromRgb(43, 108, 176)),
                    _ => new SolidColorBrush(Color.FromRgb(43, 108, 176))
                };
            }

            return new SolidColorBrush(Color.FromRgb(43, 108, 176));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
