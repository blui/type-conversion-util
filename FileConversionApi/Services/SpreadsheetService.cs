using Microsoft.Extensions.Logging;
using NPOI.XSSF.UserModel;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Diagnostics;

namespace FileConversionApi.Services;

/// <summary>
/// Spreadsheet processing service implementation
/// Handles advanced XLSX/CSV conversions with multi-sheet support
/// </summary>
public class SpreadsheetService : ISpreadsheetService
{
    private readonly ILogger<SpreadsheetService> _logger;

    public SpreadsheetService(ILogger<SpreadsheetService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> XlsxToCsvAsync(string inputPath, string outputPath)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting XLSX to CSV: {InputPath}", inputPath);

            using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            var workbook = new XSSFWorkbook(fs);

            if (workbook.NumberOfSheets == 0)
            {
                return new ConversionResult
                {
                    Success = false,
                    Error = "No worksheets found in the Excel file"
                };
            }

            if (workbook.NumberOfSheets == 1)
            {
                // Single sheet - convert to single CSV
                var sheet = workbook.GetSheetAt(0) as XSSFSheet 
                    ?? throw new InvalidOperationException("Failed to get sheet from workbook");
                var result = await ConvertSheetToCsvAsync(sheet, outputPath);
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                return result;
            }
            else
            {
                // Multiple sheets - create separate CSV files
                return await ConvertMultipleSheetsToCsvAsync(workbook, outputPath, stopwatch);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "XLSX to CSV conversion failed");
            return new ConversionResult
            {
                Success = false,
                Error = $"XLSX to CSV conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> CsvToXlsxAsync(string inputPath, string outputPath)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting CSV to XLSX: {InputPath}", inputPath);

            var records = new List<List<string>>();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null // Skip bad data instead of throwing
            };

            using (var reader = new StreamReader(inputPath))
            using (var csv = new CsvReader(reader, config))
            {
                while (await csv.ReadAsync())
                {
                    var record = new List<string>();
                    for (int i = 0; csv.TryGetField<string>(i, out var field); i++)
                    {
                        record.Add(field ?? string.Empty);
                    }
                    records.Add(record);
                }
            }

            // Create XLSX workbook
            var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("Sheet1") as XSSFSheet 
                ?? throw new InvalidOperationException("Failed to create sheet in workbook");

            for (int i = 0; i < records.Count; i++)
            {
                var row = sheet.CreateRow(i);
                var record = records[i];

                for (int j = 0; j < record.Count; j++)
                {
                    var cell = row.CreateCell(j);
                    cell.SetCellValue(record[j]);
                }
            }

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            workbook.Write(fs);

            stopwatch.Stop();

            _logger.LogInformation("CSV to XLSX conversion completed successfully in {Time}ms. Rows: {RowCount}",
                stopwatch.ElapsedMilliseconds, records.Count);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "NPOI+CsvHelper"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "CSV to XLSX conversion failed");
            return new ConversionResult
            {
                Success = false,
                Error = $"CSV to XLSX conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Convert single sheet to CSV
    /// </summary>
    private async Task<ConversionResult> ConvertSheetToCsvAsync(XSSFSheet sheet, string outputPath)
    {
        var data = ExtractWorksheetData(sheet);
        await WriteCsvDataAsync(data, outputPath);

        return new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ConversionMethod = "NPOI+CsvHelper"
        };
    }

    /// <summary>
    /// Convert multiple sheets to separate CSV files
    /// </summary>
    private async Task<ConversionResult> ConvertMultipleSheetsToCsvAsync(XSSFWorkbook workbook, string outputPath, Stopwatch stopwatch)
    {
        var basePath = Path.Combine(Path.GetDirectoryName(outputPath) ?? "", Path.GetFileNameWithoutExtension(outputPath));
        var usedSheetNames = new HashSet<string>();
        var results = new List<string>();

        for (int i = 0; i < workbook.NumberOfSheets; i++)
        {
            var sheet = workbook.GetSheetAt(i) as XSSFSheet;
            if (sheet == null) continue;

            var baseSheetName = SanitizeSheetName(sheet.SheetName);
            var sheetName = baseSheetName;
            var counter = 1;

            while (usedSheetNames.Contains(sheetName))
            {
                sheetName = $"{baseSheetName}_{counter}";
                counter++;
            }
            usedSheetNames.Add(sheetName);

            var sheetPath = $"{basePath}_{sheetName}.csv";
            var data = ExtractWorksheetData(sheet);
            await WriteCsvDataAsync(data, sheetPath);
            results.Add(sheetPath);
        }

        stopwatch.Stop();

        _logger.LogInformation("Multi-sheet XLSX to CSV conversion completed in {Time}ms. Sheets: {SheetCount}",
            stopwatch.ElapsedMilliseconds, results.Count);

        return new ConversionResult
        {
            Success = true,
            OutputPath = string.Join(", ", results),
            ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
            ConversionMethod = "NPOI+CsvHelper"
        };
    }

    /// <summary>
    /// Extract data from worksheet
    /// </summary>
    private List<List<string>> ExtractWorksheetData(XSSFSheet sheet)
    {
        var data = new List<List<string>>();

        for (int i = sheet.FirstRowNum; i <= sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row == null) continue;

            var rowData = new List<string>();
            for (int j = row.FirstCellNum; j < row.LastCellNum; j++)
            {
                var cell = row.GetCell(j);
                var cellValue = cell?.ToString() ?? "";
                rowData.Add(cellValue);
            }

            // Only add non-empty rows
            if (rowData.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            {
                data.Add(rowData);
            }
        }

        return data;
    }

    /// <summary>
    /// Write CSV data with proper quoting
    /// </summary>
    private async Task WriteCsvDataAsync(List<List<string>> data, string outputPath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            Quote = '"',
            Delimiter = ",",
            Escape = '"'
        };

        await using var writer = new StreamWriter(outputPath);
        using var csv = new CsvWriter(writer, config);

        foreach (var row in data)
        {
            foreach (var cell in row)
            {
                csv.WriteField(cell);
            }
            csv.NextRecord();
        }
    }

    /// <summary>
    /// Sanitize sheet name for filename use
    /// </summary>
    private static string SanitizeSheetName(string sheetName)
    {
        return string.Join("_", sheetName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    }
}

/// <summary>
/// Spreadsheet service interface
/// </summary>
public interface ISpreadsheetService
{
    Task<ConversionResult> XlsxToCsvAsync(string inputPath, string outputPath);
    Task<ConversionResult> CsvToXlsxAsync(string inputPath, string outputPath);
}
