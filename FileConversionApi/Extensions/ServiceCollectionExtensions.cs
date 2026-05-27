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
        services.AddSingleton<IDocumentService, DocumentService>();

        // LibreOffice integration services
        services.AddSingleton<ILibreOfficeService, LibreOfficeService>();
        services.AddSingleton<ILibreOfficeProcessManager, LibreOfficeProcessManager>();
        services.AddSingleton<ILibreOfficePathResolver, LibreOfficePathResolver>();

        // Bundled Node PDF->HTML engine (hop 2 of the docx/doc->PDF->HTML pipeline)
        services.AddSingleton<INodeEnginePathResolver, NodeEnginePathResolver>();
        services.AddSingleton<INodeEngineProcessManager, NodeEngineProcessManager>();

        // Two-hop docx/doc->HTML pipeline (composes LibreOffice hop 1 + Node hop 2)
        services.AddSingleton<IDocxToHtmlPipeline, DocxToHtmlPipeline>();

        // Specialized conversion services
        services.AddSingleton<IPdfService, PdfService>();
        services.AddSingleton<ISpreadsheetService, SpreadsheetService>();

        // Infrastructure services
        services.AddSingleton<IInputValidator, InputValidator>();
        services.AddSingleton<ISemaphoreService, SemaphoreService>();

        return services;
    }
}
