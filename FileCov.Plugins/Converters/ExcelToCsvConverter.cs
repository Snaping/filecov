using System.ComponentModel.Composition;
using System.Text;
using ClosedXML.Excel;
using FileCov.Contracts;

namespace FileCov.Plugins.Converters;

[Export(typeof(IConverter))]
public class ExcelToCsvConverter : IConverter
{
    public string Name => "Excel 转 CSV";
    public string Description => "将 Excel 文件 (.xlsx) 转换为 CSV 格式";
    public IReadOnlyList<string> SupportedInputExtensions => new List<string> { ".xlsx", ".xls" }.AsReadOnly();
    public string OutputExtension => ".csv";

    public async Task<ConversionResult> ConvertAsync(string inputPath, ConversionParameters parameters, CancellationToken cancellationToken, IProgress<double>? progress = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var outputDir = parameters.OutputDirectory ?? Path.GetDirectoryName(inputPath)!;
            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputPath) + OutputExtension);

            progress?.Report(0.1);

            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(inputPath);
                var worksheets = workbook.Worksheets.ToList();

                if (worksheets.Count == 0)
                {
                    throw new InvalidDataException("Excel 文件中没有工作表");
                }

                var firstOutputPath = "";

                for (int sheetIndex = 0; sheetIndex < worksheets.Count; sheetIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var worksheet = worksheets[sheetIndex];
                    var safeSheetName = GetSafeFileName(worksheet.Name);

                    string sheetOutputPath;
                    if (worksheets.Count == 1)
                    {
                        sheetOutputPath = outputPath;
                    }
                    else
                    {
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(outputPath);
                        var ext = Path.GetExtension(outputPath);
                        var dir = Path.GetDirectoryName(outputPath)!;
                        sheetOutputPath = Path.Combine(dir, $"{fileNameWithoutExt}_{safeSheetName}{ext}");
                    }

                    if (string.IsNullOrEmpty(firstOutputPath))
                    {
                        firstOutputPath = sheetOutputPath;
                    }

                    var lastRowUsed = worksheet.LastRowUsed();
                    var lastColumnUsed = worksheet.LastColumnUsed();
                    var totalRows = lastRowUsed?.RowNumber() ?? 0;
                    var totalColumns = lastColumnUsed?.ColumnNumber() ?? 0;

                    using var writer = new StreamWriter(sheetOutputPath, false, new UTF8Encoding(true));

                    for (int row = 1; row <= totalRows; row++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var fields = new List<string>();
                        for (int col = 1; col <= totalColumns; col++)
                        {
                            var cell = worksheet.Cell(row, col);
                            var value = cell.Value.ToString();
                            fields.Add(EscapeCsvField(value));
                        }

                        writer.WriteLine(string.Join(",", fields));

                        var overallProgress = (double)sheetIndex / worksheets.Count
                                              + ((double)row / Math.Max(totalRows, 1)) / worksheets.Count;
                        progress?.Report(0.1 + 0.9 * overallProgress);
                    }
                }

                outputPath = firstOutputPath;
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

    private static string GetSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(result) ? "Sheet" : result.Trim();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }
}
