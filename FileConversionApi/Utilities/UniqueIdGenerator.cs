namespace FileConversionApi.Utilities;

/// <summary>
/// Generates unique identifiers for operations, profiles, and temporary resources.
/// Uses DateTime-based format optimized for Windows path length constraints.
/// </summary>
/// <remarks>
/// Format: yyMMddHHmmssffff (16 characters)
/// Example: "2510270058301234" represents October 27, 2025 at 00:58:30.1234
///
/// Design Rationale:
/// - Replaces GUID (36 chars) with timestamp (16 chars) = 20 character savings per ID
/// - Reduces total path length by ~40 characters per conversion operation
/// - Critical for avoiding Windows MAX_PATH (260 character) limitations
/// - Human-readable format aids debugging and log analysis
/// - Chronologically sortable for operational monitoring
///
/// Collision Risk:
/// - Time resolution: 0.1 millisecond (0.0001 second)
/// - Collision occurs only if multiple operations start in same 0.1ms window
/// - With MaxConcurrentConversions: 2, collision probability is negligible
/// - If collision occurs: Second operation would overwrite first's temp directories
/// - Mitigation: Keep concurrent operations low, or add counter suffix if needed
///
/// Implementation Notes:
/// - Centralized generation prevents duplicate ID format logic across the codebase
/// - Provides a single location for format changes if requirements evolve
/// - Future enhancements like collision detection can be added here
/// </remarks>
public static class UniqueIdGenerator
{
    /// <summary>
    /// DateTime format string for generating unique identifiers.
    /// Format: yyMMddHHmmssffff produces 16-character timestamp.
    /// </summary>
    private const string DateTimeFormat = "yyMMddHHmmssffff";

    /// <summary>
    /// Generates a timestamp-based unique identifier using local server time.
    /// </summary>
    /// <returns>
    /// 16-character string representing current timestamp.
    /// Format: yyMMddHHmmssffff (e.g., "2510270058301234" = Oct 27, 2025, 00:58:30.1234)
    /// </returns>
    /// <remarks>
    /// Uses DateTime.Now (local server time) for consistency with application logs
    /// and to match the timezone context where the application is deployed.
    ///
    /// The identifier is suitable for:
    /// - Conversion operation tracking
    /// - LibreOffice profile directory naming
    /// - Temporary file identification
    /// - Error reference numbers
    ///
    /// Thread Safety: DateTime.Now is thread-safe. Multiple concurrent calls
    /// may return identical values if called within the same 0.1ms window.
    /// </remarks>
    public static string GenerateId()
    {
        return DateTime.Now.ToString(DateTimeFormat);
    }
}
