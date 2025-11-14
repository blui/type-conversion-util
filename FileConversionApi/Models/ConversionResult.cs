namespace FileConversionApi.Services;

public class ConversionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OutputPath { get; set; }
    public long? ProcessingTimeMs { get; set; }
    public string? ConversionMethod { get; set; }
    public string? AdditionalInfo { get; set; }
}
