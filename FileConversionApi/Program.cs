using FileConversionApi;
using FileConversionApi.Models;
using FileConversionApi.Services;
using FileConversionApi.Services.Interfaces;
using FileConversionApi.Middleware;
using Serilog;
using FileConversionApi.Controllers;
using AspNetCoreRateLimit;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel request size limits from configuration
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    var maxRequestSize = builder.Configuration.GetValue<long>("FileHandling:MaxFileSize", 52428800);
    serverOptions.Limits.MaxRequestBodySize = maxRequestSize;
});

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

// Register configuration
builder.Services.Configure<FileHandlingConfig>(builder.Configuration.GetSection("FileHandling"));
builder.Services.Configure<SecurityConfig>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<SecurityHeadersConfig>(builder.Configuration.GetSection("SecurityHeaders"));
builder.Services.Configure<ConcurrencyConfig>(builder.Configuration.GetSection("Concurrency"));
builder.Services.Configure<LibreOfficeConfig>(builder.Configuration.GetSection("LibreOffice"));

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "File Conversion API",
        Version = "0.3.0",
        Description = "REST API for converting files between various formats including Office documents, images, and PDFs. Supports DOC, DOCX, XLSX, PPTX, PDF, images, and more.",
        Contact = new OpenApiContact
        {
            Name = "File Conversion Service"
        }
    });

    // Add API Key authentication to Swagger UI
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-API-Key",
        Description = "API Key authentication. Enter your API key in the field below."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Add health checks
builder.Services.AddHealthChecks();

// Add rate limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

// Add CORS - Configurable for intranet environments
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>();

        if (allowedOrigins != null && allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .WithMethods(Constants.HttpMethods.Get, Constants.HttpMethods.Post)
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            // Development fallback - Allow any origin
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
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
builder.Services.AddSingleton<IDocumentService, DocumentService>();
builder.Services.AddSingleton<IPdfService, PdfService>();
builder.Services.AddSingleton<IInputValidator, InputValidator>();
builder.Services.AddSingleton<ISemaphoreService, SemaphoreService>();

var app = builder.Build();

// Support IIS sub-application deployments (e.g., /FileConversionApi)
// This ensures correct path resolution when deployed to virtual directories
var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrEmpty(pathBase))
{
    app.UsePathBase(pathBase);
}

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    // Use relative path to support IIS sub-application deployments
    options.SwaggerEndpoint("../swagger/v1/swagger.json", "File Conversion API v0.3");
    options.RoutePrefix = Constants.ApiPaths.ApiDocs;
    options.DocumentTitle = "File Conversion API Documentation";
});

app.UseCors();
app.UseIpRateLimiting();
app.UseApiKeyAuthentication();
app.UseSecurityMiddleware();
app.UseExceptionHandling();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
