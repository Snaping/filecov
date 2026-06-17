namespace FileCov.Contracts;

public class ConversionParameters
{
    public string PageSize { get; set; } = "A4";
    public int ImageQuality { get; set; } = 85;
    public string? OutputDirectory { get; set; }
    public Dictionary<string, string> AdditionalOptions { get; set; } = new();
}
