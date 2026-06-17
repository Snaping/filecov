using System.ComponentModel.Composition;
using System.IO.Packaging;
using System.Text;
using System.Xml;
using FileCov.Contracts;
using IOPath = System.IO.Path;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Geom;

namespace FileCov.Plugins.Converters;

[Export(typeof(IConverter))]
public class WordToPdfConverter : IConverter
{
    public string Name => "Word 转 PDF";
    public string Description => "将 Word 文档 (.docx) 转换为 PDF 格式";
    public IReadOnlyList<string> SupportedInputExtensions => new List<string> { ".docx", ".doc" }.AsReadOnly();
    public string OutputExtension => ".pdf";

    public async Task<ConversionResult> ConvertAsync(string inputPath, ConversionParameters parameters, CancellationToken cancellationToken, IProgress<double>? progress = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var extension = IOPath.GetExtension(inputPath).ToLowerInvariant();

            if (extension == ".doc")
            {
                return new ConversionResult
                {
                    Success = false,
                    ErrorMessage = "不支持旧版 .doc 格式，请先将文件另存为 .docx 格式",
                    Duration = stopwatch.Elapsed
                };
            }

            progress?.Report(0.1);

            var text = await Task.Run(() => ExtractTextFromDocx(inputPath), cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(0.4);

            var outputDir = parameters.OutputDirectory ?? IOPath.GetDirectoryName(inputPath)!;
            System.IO.Directory.CreateDirectory(outputDir);
            var outputPath = IOPath.Combine(outputDir, IOPath.GetFileNameWithoutExtension(inputPath) + OutputExtension);

            await Task.Run(() =>
            {
                var pageSize = GetPageSize(parameters.PageSize);
                using var writer = new PdfWriter(outputPath);
                using var pdf = new PdfDocument(writer);
                pdf.SetDefaultPageSize(pageSize);
                using var document = new Document(pdf);

                var paragraphs = text.Split('\n');
                for (int i = 0; i < paragraphs.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!string.IsNullOrEmpty(paragraphs[i]))
                    {
                        document.Add(new Paragraph(paragraphs[i]));
                    }
                    else
                    {
                        document.Add(new Paragraph());
                    }

                    progress?.Report(0.4 + 0.6 * ((double)(i + 1) / paragraphs.Length));
                }

                document.Close();
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

    private static string ExtractTextFromDocx(string filePath)
    {
        var textBuilder = new StringBuilder();

        using var package = Package.Open(filePath, FileMode.Open, FileAccess.Read);
        var documentUri = new Uri("/word/document.xml", UriKind.Relative);
        var documentPart = package.GetPart(documentUri);

        using var stream = documentPart.GetStream();
        using var reader = XmlReader.Create(stream);

        var isInText = false;

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t" && reader.NamespaceURI == "http://schemas.openxmlformats.org/wordprocessingml/2006/main")
            {
                isInText = true;
            }
            else if (reader.NodeType == XmlNodeType.Text && isInText)
            {
                textBuilder.Append(reader.Value);
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "p" && reader.NamespaceURI == "http://schemas.openxmlformats.org/wordprocessingml/2006/main")
            {
                textBuilder.AppendLine();
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "t" && reader.NamespaceURI == "http://schemas.openxmlformats.org/wordprocessingml/2006/main")
            {
                isInText = false;
            }
        }

        return textBuilder.ToString();
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
