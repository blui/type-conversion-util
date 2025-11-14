namespace FileConversionApi.Utilities;

/// <summary>
/// Generates unique 16-character timestamp IDs for operation tracking.
/// Format: yyMMddHHmmssffff (e.g., "2510270058301234" = Oct 27, 2025, 00:58:30.1234)
/// Uses timestamp instead of GUID to save ~20 characters per path for Windows MAX_PATH constraints.
/// Collision risk negligible with low concurrency (0.1ms resolution).
/// </summary>
public static class UniqueIdGenerator
{
    private const string DateTimeFormat = "yyMMddHHmmssffff";

    public static string GenerateId()
    {
        return DateTime.Now.ToString(DateTimeFormat);
    }
}
