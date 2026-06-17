using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FileCov.Contracts;

namespace FileCov.App.Converters;

public class StatusToBrushConverter : IValueConverter
{
    private static readonly Brush WaitingBrush = new SolidColorBrush(Colors.Gray);
    private static readonly Brush ProcessingBrush = new SolidColorBrush(Color.FromRgb(66, 133, 244));
    private static readonly Brush CompletedBrush = new SolidColorBrush(Color.FromRgb(52, 168, 83));
    private static readonly Brush FailedBrush = new SolidColorBrush(Color.FromRgb(234, 67, 53));
    private static readonly Brush CancelledBrush = new SolidColorBrush(Color.FromRgb(255, 152, 0));
    private static readonly Brush PausedBrush = new SolidColorBrush(Color.FromRgb(255, 235, 59));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConversionStatus status)
        {
            return status switch
            {
                ConversionStatus.Waiting => WaitingBrush,
                ConversionStatus.Processing => ProcessingBrush,
                ConversionStatus.Completed => CompletedBrush,
                ConversionStatus.Failed => FailedBrush,
                ConversionStatus.Cancelled => CancelledBrush,
                ConversionStatus.Paused => PausedBrush,
                _ => Brushes.White
            };
        }
        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
