namespace FileConversionApi.Models;

/// <summary>
/// Result of an input-validation check: a pass/fail bit plus the collected human-readable errors
/// (used by ConversionController to project per-field 400 responses).
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}
