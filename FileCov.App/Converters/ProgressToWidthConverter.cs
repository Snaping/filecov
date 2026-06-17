using System.Globalization;
using System.Windows.Data;

namespace FileCov.App.Converters;

public class ProgressToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var progress = value is double d ? d : 0.0;
        var totalWidth = parameter is double tw ? tw : 100.0;
        return progress / 100.0 * totalWidth;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
