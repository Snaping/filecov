using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FileCov.Contracts;

namespace FileCov.App.Converters;

public class StatusToVisibilityConverter : IValueConverter
{
    public static StatusToVisibilityConverter PauseVisible { get; } = new() { _targetStatus = ConversionStatus.Waiting };
    public static StatusToVisibilityConverter ResumeVisible { get; } = new() { _targetStatus = ConversionStatus.Paused };
    public static StatusToVisibilityConverter CancelVisible { get; } = new() { _targetMode = "Cancel" };

    private ConversionStatus? _targetStatus;
    private string? _targetMode;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConversionStatus status)
        {
            if (_targetMode == "Cancel")
            {
                return status == ConversionStatus.Waiting || status == ConversionStatus.Processing || status == ConversionStatus.Paused
                    ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_targetStatus.HasValue)
            {
                return status == _targetStatus.Value ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
