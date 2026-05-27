namespace FileConversionApi.Utilities;

/// <summary>
/// Generates 20-character operation IDs for tracking and path scoping.
/// Format: yyMMddHHmmssffff + 4 hex chars of Random.Shared entropy
/// (e.g., "260523123045123a7c4f"). UTC so IDs surface in client-visible
/// X-Operation-Id headers without timezone ambiguity. The hex suffix
/// closes the collision window left open by the 0.1ms tick resolution
/// when two requests reach the controller in the same tick (the default
/// semaphore allows 2 concurrent conversions, so collisions are realistic).
/// Compact enough to stay well inside Windows MAX_PATH.
/// </summary>
public static class UniqueIdGenerator
{
    private const string DateTimeFormat = "yyMMddHHmmssffff";

    public static string GenerateId()
    {
        var suffix = Random.Shared.Next(0x10000).ToString("x4");
        return DateTime.UtcNow.ToString(DateTimeFormat) + suffix;
    }
}
