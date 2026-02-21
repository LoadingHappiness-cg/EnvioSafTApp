using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace EnvioSafTApp.Converters
{
    public class SaftSeverityToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string severity && severity.Equals("Aviso", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Color.FromRgb(245, 124, 0));
            }

            return new SolidColorBrush(Color.FromRgb(198, 40, 40));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
