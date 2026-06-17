using System.ComponentModel.Composition;
using FileCov.Contracts;
using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using IOPath = System.IO.Path;

namespace FileCov.Plugins.Converters;

[Export(typeof(IConverter))]
public class ImagesToPdfConverter : IConverter
{
    public string Name => "图片合并为 PDF";
    public string Description => "将多张图片合并为一个 PDF 文件";
    public IReadOnlyList<string> SupportedInputExtensions => new List<string> { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif" }.AsReadOnly();
    public string OutputExtension => ".pdf";

    public async Task<ConversionResult> ConvertAsync(string inputPath, ConversionParameters parameters, CancellationToken cancellationToken, IProgress<double>? progress = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            progress?.Report(0.1);

            var outputDir = parameters.OutputDirectory ?? IOPath.GetDirectoryName(inputPath)!;
            System.IO.Directory.CreateDirectory(outputDir);
            var outputPath = IOPath.Combine(outputDir, IOPath.GetFileNameWithoutExtension(inputPath) + OutputExtension);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(0.2);

            await Task.Run(() =>
            {
                var pageSize = GetPageSize(parameters.PageSize);
                var imageData = ImageDataFactory.Create(inputPath);

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(0.4);

                using var writer = new PdfWriter(outputPath);
                using var pdf = new PdfDocument(writer);
                pdf.SetDefaultPageSize(pageSize);

                var margin = 36f;
                using var document = new Document(pdf);
                document.SetMargins(margin, margin, margin, margin);

                var availableWidth = pageSize.GetWidth() - 2 * margin;
                var availableHeight = pageSize.GetHeight() - 2 * margin;

                var image = new Image(imageData);

                var imageWidth = imageData.GetWidth();
                var imageHeight = imageData.GetHeight();

                var scaleX = availableWidth / imageWidth;
                var scaleY = availableHeight / imageHeight;
                var scale = Math.Min(scaleX, scaleY);

                if (scale < 1)
                {
                    image.Scale(scale, scale);
                }

                image.SetHorizontalAlignment(HorizontalAlignment.CENTER);

                if (parameters.ImageQuality > 0 && parameters.ImageQuality < 100)
                {
                    var compressionLevel = 1.0 - (parameters.ImageQuality / 100.0);
                    pdf.GetWriter().SetCompressionLevel((int)(compressionLevel * 9));
                }

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(0.7);

                document.Add(image);
                document.Close();

                progress?.Report(1.0);
            }, cancellationToken);

            stopwatch.Stop();

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                Duration = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            return new ConversionResult
            {
                Success = false,
                ErrorMessage = "转换已取消",
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new ConversionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    private static PageSize GetPageSize(string? pageSize)
    {
        return pageSize?.ToUpperInvariant() switch
        {
            "LETTER" => PageSize.LETTER,
            "LEGAL" => PageSize.LEGAL,
            _ => PageSize.A4
        };
    }
}
