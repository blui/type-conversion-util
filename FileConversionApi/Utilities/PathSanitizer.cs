namespace FileConversionApi.Utilities;

/// <summary>
/// Sanitizes file paths for secure logging.
/// Prevents information disclosure of internal server directory structure.
/// </summary>
public static class PathSanitizer
{
    /// <summary>
    /// Extracts only the filename from a full path for safe logging.
    /// Use this for Information, Warning, and Error log levels.
    /// Full paths should only be logged at Debug level.
    /// </summary>
    /// <param name="path">Full file path</param>
    /// <returns>Filename only, or empty string if path is null/empty</returns>
    public static string GetSafeFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return Path.GetFileName(path);
    }

    /// <summary>
    /// Extracts directory name (last segment only) from a full path for safe logging.
    /// </summary>
    /// <param name="path">Full directory path</param>
    /// <returns>Directory name only, or empty string if path is null/empty</returns>
    public static string GetSafeDirectoryName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }
}
