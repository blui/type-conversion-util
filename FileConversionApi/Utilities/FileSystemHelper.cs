namespace FileConversionApi.Utilities;

/// <summary>
/// Provides file system operations for managing temporary files and directories.
/// </summary>
public static class FileSystemHelper
{
    /// <summary>
    /// Ensures that the directory for the specified path exists.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    /// <param name="filePath">File path whose directory should be ensured</param>
    public static void EnsureDirectoryExists(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or whitespace", nameof(filePath));
        }

        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
