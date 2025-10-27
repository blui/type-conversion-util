namespace FileConversionApi.Services.Interfaces;

/// <summary>
/// Interface for resolving LibreOffice executable paths
/// </summary>
public interface ILibreOfficePathResolver
{
    /// <summary>
    /// Get the path to the LibreOffice executable
    /// </summary>
    Task<string> GetExecutablePathAsync();
}
