namespace FileCov.Contracts;

public class ConversionLogEntry
{
    public DateTime Timestamp { get; set; }
    public Guid TaskId { get; set; }
    public string InputPath { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public ConversionStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
}
