using System.ComponentModel.Composition;

namespace FileCov.Contracts;

[InheritedExport(typeof(IConverter))]
public interface IConverter
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<string> SupportedInputExtensions { get; }
    string OutputExtension { get; }
    Task<ConversionResult> ConvertAsync(string inputPath, ConversionParameters parameters, CancellationToken cancellationToken, IProgress<double>? progress = null);
}
