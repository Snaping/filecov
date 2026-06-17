namespace FileCov.Contracts;

public class ConversionTaskEventArgs : EventArgs
{
    public ConversionTask Task { get; }

    public ConversionTaskEventArgs(ConversionTask task)
    {
        Task = task;
    }
}
