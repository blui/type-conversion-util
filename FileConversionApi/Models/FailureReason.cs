namespace FileConversionApi.Models;

/// <summary>
/// Categorizes a conversion failure so callers can map it to an HTTP status code
/// without parsing the free-text <see cref="ConversionResult.Error"/> message.
/// </summary>
public enum FailureReason
{
    /// <summary>
    /// No failure, or a failure that carries no specific category. This is the default and
    /// maps to a generic server error.
    /// </summary>
    None = 0,

    /// <summary>
    /// The conversion exceeded its configured timeout and was aborted.
    /// </summary>
    Timeout,

    /// <summary>
    /// The conversion engine reported a non-timeout failure.
    /// </summary>
    EngineError
}
