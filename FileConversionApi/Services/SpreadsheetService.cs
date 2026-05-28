using Microsoft.Extensions.Logging;
using NPOI.XSSF.UserModel;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Diagnostics;
using FileConversionApi.Models;
using FileConversionApi.Utilities;

namespace FileConversionApi.Services;

/// <summary>
/// NPOI + CsvHelper-backed spreadsheet conversions. Single-sheet xlsx emits one .csv at the
/// requested output path; multi-sheet xlsx fans out to one .csv per sheet using the
/// (sanitized) sheet name as a suffix on the same base path.
/// </summary>
public class SpreadsheetService
{
    /// <summary>
    /// Power-of-Ten rule 2 ceiling on the duplicate-sheet-name suffix loop in
    /// <see cref="ConvertMultipleSheetsToCsvAsync"/>. A legitimate xlsx workbook never approaches
    /// this; the bound exists so an adversarial or corrupted file cannot drive the loop without
    /// limit. Sized well above any plausible workbook and well below <c>int.MaxValue</c>.
    /// </summary>
    private const int MaxDuplicateSuffix = 1024;

    private readonly ILogger<SpreadsheetService> _logger;

    public SpreadsheetService(ILogger<SpreadsheetService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Converts an XLSX workbook to CSV. A single-sheet workbook emits one CSV at
    /// <paramref name="outputPath"/>; a multi-sheet workbook fans out to one CSV per sheet.
    /// </summary>
    public async Task<ConversionResult> XlsxToCsvAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inputFileName = PathSanitizer.GetSafeFileName(inputPath);
            _logger.LogInformation("Converting XLSX to CSV - File: {InputFile}", inputFileName);
            _logger.LogDebug("Full input path for debugging: {InputPath}", inputPath);

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
                var result = await ConvertSheetToCsvAsync(sheet, outputPath, cancellationToken);
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                return result;
            }
            else
            {
                // Multiple sheets - create separate CSV files
                return await ConvertMultipleSheetsToCsvAsync(workbook, outputPath, stopwatch, cancellationToken);
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

    /// <summary>
    /// Reads a header-less, invariant-culture CSV into a single-sheet XLSX workbook written at
    /// <paramref name="outputPath"/>.
    /// </summary>
    public async Task<ConversionResult> CsvToXlsxAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inputFileName = PathSanitizer.GetSafeFileName(inputPath);
            _logger.LogInformation("Converting CSV to XLSX - File: {InputFile}", inputFileName);
            _logger.LogDebug("Full input path for debugging: {InputPath}", inputPath);

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
                    cancellationToken.ThrowIfCancellationRequested();
                    var record = new List<string>();
                    for (int i = 0; csv.TryGetField<string>(i, out var field); i++)
                    {
                        record.Add(field ?? string.Empty);
                    }
                    records.Add(record);
                }
            }

            var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("Sheet1") as XSSFSheet
                ?? throw new InvalidOperationException("Failed to create sheet in workbook");

            for (int i = 0; i < records.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
    /// Extracts the sheet's row/cell grid via <see cref="ExtractWorksheetData"/> and writes a
    /// single CSV at <paramref name="outputPath"/>.
    /// </summary>
    private async Task<ConversionResult> ConvertSheetToCsvAsync(XSSFSheet sheet, string outputPath, CancellationToken cancellationToken)
    {
        var data = ExtractWorksheetData(sheet);
        await WriteCsvDataAsync(data, outputPath, cancellationToken);

        return new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ConversionMethod = "NPOI+CsvHelper"
        };
    }

    /// <summary>
    /// Fans the workbook's sheets out to per-sheet CSVs at
    /// <c>{outputDir}/{outputStem}_{sheetName}.csv</c>. Duplicate sheet names get a numeric
    /// suffix so no fan-out collides. The result's OutputPath is the comma-joined list of
    /// emitted paths.
    /// </summary>
    private async Task<ConversionResult> ConvertMultipleSheetsToCsvAsync(XSSFWorkbook workbook, string outputPath, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath) ?? Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var basePath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(outputPath));
        var usedSheetNames = new HashSet<string>();
        var results = new List<string>();

        for (int i = 0; i < workbook.NumberOfSheets; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sheet = workbook.GetSheetAt(i) as XSSFSheet;
            if (sheet == null) continue;

            var baseSheetName = SanitizeSheetName(sheet.SheetName);
            var sheetName = baseSheetName;
            var counter = 1;

            while (usedSheetNames.Contains(sheetName))
            {
                if (counter > MaxDuplicateSuffix)
                {
                    throw new InvalidOperationException(
                        $"Too many duplicate sheet names: {baseSheetName}");
                }

                sheetName = $"{baseSheetName}_{counter}";
                counter++;
            }
            usedSheetNames.Add(sheetName);

            var sheetPath = $"{basePath}_{sheetName}.csv";
            var data = ExtractWorksheetData(sheet);
            await WriteCsvDataAsync(data, sheetPath, cancellationToken);
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
    /// Reads the sheet's used range (FirstRowNum..LastRowNum, FirstCellNum..LastCellNum) into
    /// a row-major list-of-lists. Rows whose cells are all empty/whitespace are dropped so
    /// trailing blank rows do not bloat the CSV.
    /// </summary>
    private List<List<string>> ExtractWorksheetData(XSSFSheet sheet)
    {
        var data = new List<List<string>>();

        for (int i = sheet.FirstRowNum; i <= sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row == null) continue;

            var rowData = new List<string>();
            var firstCellNum = Math.Max((int)row.FirstCellNum, 0);
            var lastCellNum = row.LastCellNum;

            if (lastCellNum > 0)
            {
                for (int j = firstCellNum; j < lastCellNum; j++)
                {
                    var cell = row.GetCell(j);
                    var cellValue = cell?.ToString() ?? "";
                    rowData.Add(cellValue);
                }
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
    /// Writes the row-major grid through CsvHelper with RFC-4180-style double-quote escaping
    /// and a comma delimiter. Cancellation is honored between rows.
    /// </summary>
    private async Task WriteCsvDataAsync(List<List<string>> data, string outputPath, CancellationToken cancellationToken)
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
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var cell in row)
            {
                csv.WriteField(cell);
            }
            await csv.NextRecordAsync();
        }
    }

    /// <summary>
    /// Replaces every <see cref="Path.GetInvalidFileNameChars"/> character with '_' so the
    /// sheet name is safe to use as a path suffix in the multi-sheet fan-out.
    /// </summary>
    private static string SanitizeSheetName(string sheetName)
    {
        return string.Join("_", sheetName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    }
}
