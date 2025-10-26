using System.IO;

namespace FileConversionApi.Utilities;

/// <summary>
/// File system utility methods
/// </summary>
public static class FileSystemHelper
{
    /// <summary>
    /// Ensures that the directory for the specified path exists
    /// Creates the directory if it doesn't exist
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

    /// <summary>
    /// Safely deletes a file if it exists
    /// </summary>
    /// <param name="filePath">Path to file to delete</param>
    /// <returns>True if file was deleted, false if it didn't exist or deletion failed</returns>
    public static bool SafeDeleteFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets a safe temporary file path with the specified extension
    /// </summary>
    /// <param name="extension">File extension (with or without dot)</param>
    /// <returns>Full path to temporary file</returns>
    public static string GetTempFilePath(string extension)
    {
        var ext = extension.StartsWith(".") ? extension : $".{extension}";
        return Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{ext}");
    }
}
