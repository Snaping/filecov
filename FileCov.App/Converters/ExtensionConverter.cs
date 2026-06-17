using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace FileCov.App.Converters;

public class ExtensionConverter : IValueConverter
{
    public static ExtensionConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path)
        {
            var ext = Path.GetExtension(path);
            return string.IsNullOrEmpty(ext) ? "" : ext.TrimStart('.').ToUpperInvariant();
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
