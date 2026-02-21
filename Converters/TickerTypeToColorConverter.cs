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
                    TickerMessageType.Success => new SolidColorBrush(Color.FromRgb(67, 160, 71)),   // Verde #43A047
                    TickerMessageType.Error => new SolidColorBrush(Color.FromRgb(229, 57, 53)),     // Vermelho #E53935
                    TickerMessageType.Warning => new SolidColorBrush(Color.FromRgb(251, 140, 0)),    // Laranja #FB8C00
                    TickerMessageType.Info => new SolidColorBrush(Color.FromRgb(25, 118, 210)),     // Azul #1976D2
                    _ => new SolidColorBrush(Color.FromRgb(25, 118, 210))
                };
            }

            return new SolidColorBrush(Color.FromRgb(25, 118, 210));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
