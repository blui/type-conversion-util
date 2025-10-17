using FileConversionApi.Models;
using FileConversionApi.Services;
using FileConversionApi.Services.Interfaces;
using FileConversionApi.Middleware;
using Serilog;
using FileConversionApi.Controllers;
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

// Register configuration classes
builder.Services.Configure<ApplicationConfig>(builder.Configuration.GetSection("Application"));
builder.Services.Configure<FileHandlingConfig>(builder.Configuration.GetSection("FileHandling"));
builder.Services.Configure<RateLimitingConfig>(builder.Configuration.GetSection("RateLimiting"));
builder.Services.Configure<CorsConfig>(builder.Configuration.GetSection("Cors"));
builder.Services.Configure<SecurityHeadersConfig>(builder.Configuration.GetSection("SecurityHeaders"));
builder.Services.Configure<CustomLoggingConfig>(builder.Configuration.GetSection("CustomLogging"));
builder.Services.Configure<ConcurrencyConfig>(builder.Configuration.GetSection("Concurrency"));
builder.Services.Configure<TimeoutConfig>(builder.Configuration.GetSection("Timeouts"));
builder.Services.Configure<NetworkConfig>(builder.Configuration.GetSection("Network"));
builder.Services.Configure<HealthCheckConfig>(builder.Configuration.GetSection("HealthChecks"));
builder.Services.Configure<LibreOfficeConfig>(builder.Configuration.GetSection("LibreOffice"));
builder.Services.Configure<SSLConfig>(builder.Configuration.GetSection("SSL"));
builder.Services.Configure<SecurityConfig>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<PreprocessingConfig>(builder.Configuration.GetSection("Preprocessing"));

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add health checks
builder.Services.AddHealthChecks();

// Add rate limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register custom services
builder.Services.AddSingleton<IConversionEngine, ConversionEngine>();
builder.Services.AddSingleton<ILibreOfficeService, LibreOfficeService>();
builder.Services.AddSingleton<ILibreOfficeProcessManager, LibreOfficeProcessManager>();
builder.Services.AddSingleton<ILibreOfficePathResolver, LibreOfficePathResolver>();
builder.Services.AddSingleton<ISpreadsheetService, SpreadsheetService>();
builder.Services.AddSingleton<IDocxPreProcessor, DocxPreProcessor>();
builder.Services.AddSingleton<IPreprocessingService, PreprocessingService>();
builder.Services.AddSingleton<IConfigValidator, ConfigValidator>();
builder.Services.AddSingleton<IXmlProcessingService, XmlProcessingService>();
builder.Services.AddSingleton<IConversionValidator, ConversionValidator>();
builder.Services.AddSingleton<IDocumentService, DocumentService>();
builder.Services.AddSingleton<IPdfService, PdfService>();
builder.Services.AddSingleton<IImageService, ImageService>();
builder.Services.AddSingleton<IInputValidator, InputValidator>();
builder.Services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
builder.Services.AddSingleton<ITelemetryService, TelemetryService>();
builder.Services.AddSingleton<ISemaphoreService, FileConversionApi.Services.SemaphoreService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseIpRateLimiting();
app.UseSecurityMiddleware();
app.UseExceptionHandling();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
