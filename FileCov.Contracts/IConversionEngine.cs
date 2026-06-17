namespace FileCov.Contracts;

public interface IConversionEngine
{
    Task<ConversionResult> SubmitTaskAsync(ConversionTask task);
    void PauseAll();
    void ResumeAll();
    void CancelAll();
    int MaxConcurrency { get; set; }
    event EventHandler<ConversionTaskEventArgs>? TaskStatusChanged;
}
