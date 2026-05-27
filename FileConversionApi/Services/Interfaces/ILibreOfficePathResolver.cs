namespace FileConversionApi.Services.Interfaces;

/// <summary>
/// Resolves the absolute path to soffice.exe for the running service: honors the configured
/// override when set, otherwise falls back to the bundled binary under
/// <c>AppContext.BaseDirectory/LibreOffice/program/</c>.
/// </summary>
public interface ILibreOfficePathResolver
{
    /// <summary>
    /// Returns the resolved soffice.exe path. Does not verify the file exists; that check is
    /// the process manager's job before the spawn.
    /// </summary>
    Task<string> GetExecutablePathAsync();
}
