using FileConversionApi.Services;
using FileConversionApi.Services.Interfaces;

namespace FileConversionApi.Extensions;

/// <summary>
/// Service collection extension methods for dependency injection configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application services for document conversion.
    /// </summary>
    public static IServiceCollection AddConversionServices(this IServiceCollection services)
    {
        // Core conversion services
        services.AddSingleton<IConversionEngine, ConversionEngine>();
        services.AddSingleton<IDocumentService, DocumentService>();

        // LibreOffice integration services
        services.AddSingleton<ILibreOfficeService, LibreOfficeService>();
        services.AddSingleton<ILibreOfficeProcessManager, LibreOfficeProcessManager>();
        services.AddSingleton<ILibreOfficePathResolver, LibreOfficePathResolver>();

        // Specialized conversion services
        services.AddSingleton<IPdfService, PdfService>();
        services.AddSingleton<ISpreadsheetService, SpreadsheetService>();

        // Infrastructure services
        services.AddSingleton<IInputValidator, InputValidator>();
        services.AddSingleton<ISemaphoreService, SemaphoreService>();
        services.AddSingleton<IConfigValidator, ConfigValidator>();

        return services;
    }
}
