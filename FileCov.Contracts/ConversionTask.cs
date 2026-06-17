using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileCov.Contracts;

public class ConversionTask : INotifyPropertyChanged
{
    private ConversionStatus _status;
    private double _progress;

    public Guid Id { get; } = Guid.NewGuid();
    public string InputPath { get; set; } = string.Empty;
    public string TargetFormat { get; set; } = string.Empty;
    public ConversionParameters Parameters { get; set; } = new();

    public ConversionStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public double Progress
    {
        get => _progress;
        set
        {
            if (_progress != value)
            {
                _progress = value;
                OnPropertyChanged();
            }
        }
    }

    public string? ErrorMessage { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ConversionResult? Result { get; set; }
    public CancellationTokenSource CancellationTokenSource { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
