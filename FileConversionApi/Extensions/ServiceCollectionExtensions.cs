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
        services.AddSingleton<DocumentService>();

        // LibreOffice integration services. The process manager is exposed only through its
        // interface so the integration tests can swap in a FakeLibreOfficeProcessManager via
        // services.Replace(ServiceDescriptor.Singleton<ILibreOfficeProcessManager>(...)).
        services.AddSingleton<ILibreOfficeProcessManager, LibreOfficeProcessManager>();
        services.AddSingleton<LibreOfficePathResolver>();

        // Bundled Node PDF->HTML engine (hop 2 of the docx/doc->PDF->HTML pipeline). Same
        // interface-as-test-seam pattern as the LibreOffice process manager above.
        services.AddSingleton<NodeEnginePathResolver>();
        services.AddSingleton<INodeEngineProcessManager, NodeEngineProcessManager>();

        // Two-hop docx/doc->HTML pipeline (composes LibreOffice hop 1 + Node hop 2)
        services.AddSingleton<DocxToHtmlPipeline>();

        // Specialized conversion services
        services.AddSingleton<PdfService>();
        services.AddSingleton<SpreadsheetService>();

        // Infrastructure services
        services.AddSingleton<InputValidator>();
        services.AddSingleton<SemaphoreService>();

        return services;
    }
}
